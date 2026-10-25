# 03 · Process and window model

How a session is launched, found, identified, watched, killed and re-adopted. All of it uses documented Windows APIs and Electron's documented `--user-data-dir` switch, nothing else.

## Finding Claude.exe

Resolved at **every** launch, never stored as a path with a version in it. Order:

1. `--claude-path` command-line argument (for testing).
2. `settings.json › claudePath` if the user chose a file in the "Couldn't find Claude Desktop" dialog. Stored as the *folder* the user picked; the newest `Claude.exe` inside it (depth 2) is used, so a Squirrel `app-<version>` change is survived.
3. Store build: enumerate `HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages`, keys starting `Claude_`, read `PackageRootFolder`, pick the highest version, exe at `<root>\app\Claude.exe`. This is the fast path the C++ switcher uses; `Get-AppxPackage` takes seconds.
4. Installer build: Uninstall keys under HKCU and HKLM (both views) with `DisplayName` like `Claude*`, then `%LOCALAPPDATA%\AnthropicClaude`, `%LOCALAPPDATA%\Programs\Claude`, `%ProgramFiles%\Claude`, `%ProgramFiles(x86)%\Claude`; newest `Claude.exe` within depth 2.
5. Nothing: the "Couldn't find Claude Desktop" dialog.

The result (`kind`, `exe`, `version`, `aumid`, `defaultDataDir`) is cached in memory for the process lifetime and re-resolved if the exe disappears (Claude updated) or a launch fails.

## Launching

| Profile | Store build | Installer build |
| --- | --- | --- |
| Original account | `explorer.exe shell:AppsFolder\<PackageFamilyName>!Claude` so the instance keeps package identity (claude:// links, auto-update, notifications) | `Claude.exe` with no arguments |
| Any other profile | `Claude.exe --user-data-dir="<profile folder>"` | same |

`ShellExecuteEx` with the exe's folder as working directory, no job object, no inherited handles, so the session outlives the host. The profile folder is created first if the profile is new (the only time the app creates a folder in a profile path). A sign-in URL, when sign-in routing is on, is passed as a second argument exactly as the PowerShell switcher does.

A launch yields no usable process id for the Store path (explorer hands off) and for an already-running profile (Electron's single-instance lock hands off). So **the launcher never trusts its own pid**; it marks the session `Starting` and waits for discovery to find the process by command line within a timeout (default 20 s, then "Starting is taking longer than usual" with Cancel).

## Discovery: identifying running instances

Every 2 s, and immediately after any launch or window event:

1. Toolhelp snapshot of processes named `Claude.exe` (case-insensitive).
2. Drop processes whose image path is not under a known Claude install root (this excludes the Claude Code CLI, also named `claude.exe`). "Known root" matches the install *family*, not the exact version folder, so an instance still running the previous version after an update is kept.
3. Drop Chromium helpers: command line contains `--type=`.
4. Read the command line with `NtQueryInformationProcess(ProcessCommandLineInformation)` (falls back to WMI `Win32_Process.CommandLine` if that is denied). Parse `--user-data-dir=` with or without quotes, including a quoted whole argument. No switch means the original account.
5. Map: normalised (lower-case, trailing slash trimmed, `LocalCache` redirection resolved) profile folder → root pid.

The result is diffed against the previous map: new pid → session is `Running` (or adopted if nobody launched it), vanished pid → see exits below.

## Finding the main window

For a root pid, `EnumWindows` filtered by: owner pid matches (`GetWindowThreadProcessId`), class `Chrome_WidgetWin_1`, `WS_VISIBLE`, or hidden by us (tracked by handle), no owner (`GetWindow(GW_OWNER) == 0`), not `WS_EX_TOOLWINDOW`, and non-empty title. Electron creates one such window per BrowserWindow; Claude has one main window, so the first match is it. Popups, tooltips and the tray-hidden state are excluded by the owner and visibility rules. A WinEventHook on `EVENT_OBJECT_SHOW` for the Claude pids keeps the host from waiting for the next tick.

The window class name is a Chromium implementation detail; it is one of two constants in the codebase that could change with an Electron update (the other is the `--type=` helper marker). Both live in `ClaudeFacts.cs` with a comment and a test that fails loudly if a running Claude has no window matching the filter.

## Watching for exits

For each root pid the host opens a handle with `SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION` and registers a wait callback. On signal, `GetExitCodeProcess` gives the exit code and the time is recorded. The session's next state depends on who asked (state-machine doc):

| What happened | Evidence | Resulting state |
| --- | --- | --- |
| Killed from the app | the Session has a pending `KillRequested` | Killed |
| Closed by the user inside Claude | no pending request, exit code 0, and a `WM_CLOSE`-like clean shutdown (window destroyed before process exit) | Not running ("closed by you at HH:MM") |
| Crashed | no pending request, any non-zero exit code, or exit code 0 with the window still existing at exit | Crashed, exit code recorded |

Note: current Claude builds hide to the tray on Quit instead of exiting. That is not an exit at all; the window just becomes invisible. Discovery still sees the pid; the host shows it as Running and offers "Bring window forward", which relaunches the profile so Electron's single-instance hand-off shows the window (the same trick both switchers use).

## Hung detection

Once a second, `IsHungAppWindow(mainHwnd)`. True for 5 s → `Not responding` with Wait and Restart. False again → back to the previous state. The host never sends synchronous messages (`SendMessage`) to Claude windows; everything is `SendMessageTimeout` with 200 ms or `PostMessage`, so a hung Claude cannot hang the host.

## Busy heuristic

Reported, never acted on automatically. Busy when, over the last 10 s, the process tree's CPU average is above 15 % or any child process was created. It gates only one thing: whether Kill and Restart ask first.

## Memory and CPU

Every N s (default 3): build the process tree under the root pid (Toolhelp, parent pid plus creation-time check so recycled pids are not adopted), sum `PROCESS_MEMORY_COUNTERS_EX.PrivateUsage` from `K32GetProcessMemoryInfo`, and compute CPU from `GetProcessTimes` deltas divided by wall time and core count. Cost: a few hundred microseconds for a 40-process tree; measured in the spike.

## Killing

`ProcessKiller.Kill(session, graceful: true)`:

1. `PostMessage(mainHwnd, WM_CLOSE)` and wait up to 3 s for the window to go away *and* the root process to exit.
2. Because Claude's Quit hides rather than exits, step 1 usually leaves the process alive. Then: snapshot the tree, `NtSuspendProcess` every process (so hundreds of MCP helper processes cannot spawn replacements or flood Explorer, the C++ switcher's lesson), lower their priority, `TerminateProcess` in batches of 16 with a 10 s deadline, root last.
3. Record `KillRequested` before step 1 so the exit is classified as Killed, not Crashed.

Restart is Kill followed by Launch of the same profile; Restart all does them in parallel, one confirmation listing the busy ones.

## Re-adoption on host start

On start the host runs discovery before showing the shell. Every running profile it recognises is attached to its Session as `Running` without any launch, its window is hosted into the saved slot, and the log notes "adopted pid N for <profile>". Unknown `--user-data-dir` folders (not in `profiles.json`) are added to the catalogue as "found running" with the folder name as label, so a profile created by the old switcher after our first run still appears.

If the host exits, it first clears the owner links and restores the window styles of every hosted window (`WindowHost.ReleaseAll`), then exits. On an unclean host exit the styles stay stripped until the next host start re-adopts them or the user restarts Claude; the spike measures how a style-stripped Claude window behaves on its own (test row 16).

## Windows APIs used by this model

`CreateToolhelp32Snapshot`, `Process32First/Next`, `OpenProcess`, `QueryFullProcessImageName`, `NtQueryInformationProcess`, `GetExitCodeProcess`, `RegisterWaitForSingleObject`, `K32GetProcessMemoryInfo`, `GetProcessTimes`, `NtSuspendProcess`, `TerminateProcess`, `EnumWindows`, `GetWindowThreadProcessId`, `GetClassName`, `GetWindow`, `GetWindowLongPtr`, `IsHungAppWindow`, `PostMessage`, `SendMessageTimeout`, `SetWinEventHook`, `ShellExecuteEx`, registry read of the two key families above. The full list with purposes is in the trust document.
