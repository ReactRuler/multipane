# What this app touches

Everything Multipane reads or writes outside its own process, and every family of Windows API it calls. This page is kept in step with `src/Multipane.Native/NativeMethods.txt`; a pull request that changes one must change the other.

## Folders and files

| Path | Read | Write | Why |
| --- | --- | --- | --- |
| `%LOCALAPPDATA%\Multipane\settings.json`, `profiles.json`, `layout.json` | yes | yes | The app's own settings, profile names and colours, and window layout. Human-readable JSON. |
| `%LOCALAPPDATA%\Multipane\logs\` | yes | yes | Local log, rotated, never uploaded. Contains no window titles, chat names or user paths. |
| `%LOCALAPPDATA%\ClaudeProfiles\settings.json` | yes, once at first run and when you ask to re-import | never | Names and colours chosen in the Claude Profile Switcher, so nothing has to be typed again. |
| `%LOCALAPPDATA%\ClaudeProfile-*\` and `%LOCALAPPDATA%\ClaudeProfiles\*\` | directory listing (names and whether a folder exists) | create an empty folder on Add account; delete a folder on confirmed Delete (to the Recycle Bin) | Profile folders. Their contents are not read. |
| `<profile>\config.json`, `<profile>\claude-code-sessions\` | existence check only | never | To show "folder not found" and to know whether Transfer chats has somewhere to copy to. |
| `<profile>\claude-code-sessions\<account>\<org>\local_*.json` | only during Transfer chats, the files you tick | only during Transfer chats, into the target profile; existing files are never overwritten | Copies a chat's index entry so the other profile's Claude Code tab lists it. The transcript (in `%USERPROFILE%\.claude`) is not touched. |
| `%APPDATA%\Claude`, `%LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Claude` | existence only | never | Finding the original account's data folder to know which running instance is the original. |
| Desktop and `%APPDATA%\Microsoft\Windows\Start Menu\Programs\` | no | `.lnk` files you ask for in "Add shortcuts"; removed on Delete or Rename | Shortcuts that open a profile through Multipane. |
| `%LOCALAPPDATA%\Multipane\icons\` | yes | yes | Rendered per-profile `.ico` files for those shortcuts. |
| Claude's install folder (`WindowsApps\Claude_*` or `%LOCALAPPDATA%\AnthropicClaude`) | file existence (`Claude.exe`) | never | Finding the executable to launch. |

Nothing else. In portable mode the first two rows move to `data\` beside the exe.

## Registry

| Key | Read | Write | Why |
| --- | --- | --- | --- |
| `HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages\Claude_*` | yes | never | Locate the Store build of Claude without the slow PowerShell cmdlet. |
| `HKCU` and `HKLM` `...\Uninstall\*` with DisplayName `Claude*` | yes | never | Locate the installer build. |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `Multipane` | yes | only when you toggle "Start with Windows" | Start with Windows, per user, no service. |
| `HKCU\Software\Classes\claude` and `HKCU\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\claude` | yes | only while "Send sign-in links to the account that asked" is on; removed when turned off | Routing `claude://` sign-in links to the profile that is signing in. Off by default. |

## Processes

| Action | API family | Why |
| --- | --- | --- |
| List processes, read image path and command line | Toolhelp snapshot, `QueryFullProcessImageName`, `NtQueryInformationProcess` (command line), WMI `Win32_Process` as fallback | To know which Claude instance belongs to which profile (`--user-data-dir`). Only processes named `Claude.exe` under a Claude install folder are inspected. |
| Start a process | `ShellExecuteEx` | Start Claude for a profile, or `explorer.exe shell:AppsFolder\…` for the Store build's original account. Never anything else; the code that does it is `ProcessLauncher.cs`. |
| Wait for exit, read exit code | `OpenProcess(SYNCHRONIZE)`, `RegisterWaitForSingleObject`, `GetExitCodeProcess` | Crash detection. |
| Memory and CPU | `K32GetProcessMemoryInfo`, `GetProcessTimes` | The readouts and warnings. |
| End a process tree | `PostMessage(WM_CLOSE)`, `NtSuspendProcess`, `TerminateProcess` | Kill and Restart, only for the session you chose, only on its own process tree. The code is `ProcessKiller.cs`. |

## Windows

| Action | API family | Why |
| --- | --- | --- |
| Find Claude's main window | `EnumWindows`, `GetWindowThreadProcessId`, `GetClassName`, `GetWindow` | To host it. |
| Host it | `SetWindowLongPtr` (owner and style bits), `SetWindowPos` / `DeferWindowPos`, `ShowWindow` (hide and show), `SetForegroundWindow` | Placing the window over its pane, hiding it when its pane is hidden, focusing it. Reversed on exit (`WindowHost.ReleaseAll`). |
| Watch for new windows | `SetWinEventHook` (out-of-context) | Notice a new Claude window quickly. Out-of-context means no code is loaded into Claude. |
| Detect hangs | `IsHungAppWindow` | "Not responding". |
| Global shortcuts | `RegisterHotKey` | Ctrl+Alt+C and Ctrl+Alt+1…9, configurable. |
| Own window look | `DwmSetWindowAttribute` (Mica, dark title bar, corner preference) | Theming of Multipane's own windows. |
| Tray | `Shell_NotifyIcon` | Tray icon, menu and flyout. |
| Shortcuts | `IShellLink`, `IPropertyStore` | Writing `.lnk` files. |

## What the app does not use

No `WriteProcessMemory`, `CreateRemoteThread`, `SetWindowsHookEx` (in-process hooks), no debugger APIs, no `AttachThreadInput`, no clipboard reads, no keyboard hooks, no screen capture, no file reads inside profile folders beyond the rows above, no network APIs of any kind.
