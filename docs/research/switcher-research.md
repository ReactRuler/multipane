# Claude profile switchers: feature and mechanics research

Sources (cloned read-only, nothing executed):

- **PS** = `PriyanshuGeTRekT/Claude-Code-Desktop-Switcher`, HEAD `517369823b` (2026-10-18), single file `ClaudeSwitcher.ps1` (2655 lines), MIT. Line numbers below refer to that file unless another is named.
- **CPP** = `Piwqust/claude-switcher`, HEAD `180472ef79` (2026-10-23), version 1.0.2 (+ one unreleased fix), native C++/Win32/Direct2D, MIT. Files under `src/`.

CPP is a declared rewrite of PS 2.0 and keeps the same `settings.json` keys, label/colour scheme and `.ico` folder so an install carries over. One important divergence is flagged in section J.

---

## A. User-facing features

### A1. PS main window (`ClaudeSwitcher.ps1` 1945-2140)

WinForms, light only, title **"Claude Profile Switcher"**, 760x520 (min 700x440), centred on screen, background RGB(250,249,245), accent RGB(203,123,93), "running" green RGB(46,125,74), Segoe UI 9.5. Heading **"Claude accounts"**, subtitle *"Each profile is a separate login. Several can run at the same time. Right-click a profile for more."*

ListView (Details, single select, 20 px badge icons) columns:

| Column | Width | Content |
| --- | --- | --- |
| Profile | 210 | label + badge icon |
| Status | 150 | **"Running"** (green) / **"Not running"** (muted) |
| Last used | 170 | **"now"** if running, else `d MMM yyyy, HH:mm`, else **"never"** |
| (blank) | 150 | **"your original"** for Default, **"new - F2 to rename"** for a just-added still-unnamed account, **"folder: <id>"** when label != folder id |

Empty-state hint over the list (only Default exists): *"Only your original account is here so far. Click Add account to sign in to another one in its own window."*

Status line (bottom left, ASCII only on purpose, l.2139): **"N profile(s), M running   |   Claude <version or 'Classic'>"**. Also used for transient messages: *"Starting 'X'..."*, *"Switched to 'X'."*, *"Renamed 'A' to 'B'."*, *"Added 'Claude - X' to the desktop and Start menu."*, *"Deleted 'X'."*, *"Sign in to 'Account 2' in the new Claude window. Press F2 here to give it a better name."*

Checkbox bottom right: **"Keep running in the tray"** (default on, setting `KeepInTray`).

Buttons (l.2063-2068): **Launch** (primary; becomes **"Switch to"** when the selected profile is running), **Add account**, **Rename**, **Add shortcuts**, **Transfer chats**, **Delete** (disabled for Default). Tooltips: "Opens Claude at a fresh sign-in screen for another account (Ctrl+N)", "Desktop and Start menu shortcuts that open this account directly", "Copy Claude Code chats from this account to another one".

Right-click menu (l.2479-2492): **Launch**/**Switch to** (bold, "Enter"), **Rename...** (F2), **Add shortcuts**, **Transfer Code chats...**, **Open folder**, separator, **Delete** (Del, disabled for Default).

Keyboard: Enter launch, F2 rename, Del delete, F5 force refresh, Ctrl+N add account, double-click launch. List auto-refreshes every 5 s while visible (l.2591).

### A2. PS dialogs

- **Rename** (l.2277): title "Rename profile", text *"New name for '<name>'. Its sign-in and data stay as they are."*, OK button **"Rename"**, Cancel. Errors in a "Could not rename" warning box.
- **Delete** (l.2316): YesNo warning *"Delete the '<name>' profile and sign that account out on this computer?\n\nIts folder, saved login and shortcuts are removed. Your other profiles are untouched."*
- **Open folder** when missing: "Nothing there yet" / *"This profile has no folder yet. It is created the first time you launch it."*
- **Transfer Claude Code chats** (l.2328-2466): 700x500; header "Chats in '<source>'", **Select all** checkbox, list columns Chat 340 / Folder 150 / Updated 140 with checkboxes, **"Copy to"** dropdown of other profiles, note *"The chat stays in this account too. Finish a chat in one account before continuing it in the other, since both continue the same history."*, buttons **Copy chats** / Cancel. Pre-checks: "'X' has no Claude Code chats to transfer." / "Create a second profile first, then transfer chats to it." / "'X' has not used Claude Code yet, so there is nowhere to put the chats. Open it, sign in, visit the Code tab once, then try again." / if target open: "'X' is open. It may only show the copied chats after you quit and reopen it. Copy anyway?". Result: "Copied N chat(s) to 'X'. M were already there." then "Open 'X' now?" if target was closed.
- **Sign-in routing** prompts (l.1753-1770), shown once on first Add account: *"Send browser sign-ins to the account that asked for them? ..."* (YesNo) and on the Store build a follow-up *"One more step, which only you can do ... set CLAUDE to 'Claude Profile Router'"* which opens `ms-settings:defaultapps?registeredAppUser=ClaudeProfileRouter`.
- Fatal errors: trap (l.92-108) logs to `%LOCALAPPDATA%\ClaudeProfiles\switcher-error.log` and shows a MessageBox "Claude Profile Switcher".

### A3. PS tray (l.2528-2588)

NotifyIcon tooltip "Claude Profile Switcher"; left-click shows the window. Right-click menu is rebuilt on open: one item per profile with its badge image (bold + right-aligned text **"open"** when running), separator, **Add account...**, **Open switcher**, **Start with Windows** (checked = Startup-folder shortcut exists), **Send sign-ins to the right account** (checked = routing on), separator, **Exit**. Closing the window with "Keep running in the tray" on hides it and shows one balloon ever: "Still here" / *"The switcher is in the notification area. Right-click it to switch accounts or exit."* (setting `TrayHintShown`).

Command line: `-Launch <name|id>`, `-List`, `-Shortcut <name> [-To <dir>]`, `-Install`, `-ClaudePath <exe>`, `-Tray`, `-AddAccount`, `-RouteSignIns`, `-Status`, `-Revert`, `-HandleLink <url>` (l.71-84).

### A4. CPP picker window (`picker.cpp`)

Direct2D card grid, Mica backdrop, dark/light from the system, Windows 11 styling. Title **"Choose an account"** (window caption **"Claude Accounts"** / «Аккаунты Claude»), gear icon top right. Width 612 DIP, 4 columns of 132x152 cards (204 tall when any account has current usage), centred on the monitor under the pointer, 40 px above centre (l.315-320).

Each card: number badge 1-9 top-left (becomes a tick circle on hover/select), **⋯** button top-right, 56 px avatar (Claude logo for Main, coloured gradient circle with initials otherwise, green dot bottom-right when open), name, status line, and when current: separator, **"5 h"** and **"7 d"** usage bars with percentages, **"Updated <age>"**. Status strings (`app.cpp` 7-12): **"Starting…"**, **"Closing…"**, **"Open"**, else last-used text from `Util::When`: **"Not opened yet"**, **"Today, 10:09 AM"**, **"Yesterday, …"**, **"October 14"**, **"October 14, 2025"**.

Last tile is **Add** / "New sign-in" / "Ctrl+N" (dashed border). Footer: toggle **"Show when Windows starts"** and either hint *"Press a number to open, Ctrl+click to pick several"* or, with a selection, **"Clear"** and primary **"Open “Work”"** / **"Open N accounts"**.

Banners above the grid: countdown **"Opening “Work” in 5 s"** / *"Press any key to choose yourself"* / button **"Open now"** with a shrinking accent bar; new-account **"Sign in in the new Claude window"** / *"The name “Account 2” is selected: type your own, like “Work”"* / button **"Done"**. Rename is in-place: a native edit box over the card name (F2).

Card ⋯ / right-click menu (`app.cpp` 371-411, native menu, dark-aware): **Open**/**Switch to window** (`\tN`), **Restart** (Ctrl+R, greyed if closed), **Close Claude**, **Open when Windows starts** (checkable), sep, **Rename** (F2), **Desktop shortcut**, **Move Code chats…**, **Show profile folder**, sep, **Delete…** (Del, greyed for Main).

Keys (l.256-277): 1-9 / numpad open, Ctrl+click or Space multi-select then Enter, Ctrl+N add, Ctrl+, settings, Esc clear selection or close, F2 rename, Ctrl+R restart, Del delete, Shift+F10 or Apps menu, Tab/arrows move focus. Global: **Ctrl+Alt+C** picker (configurable, `MOD_NOREPEAT`), **Ctrl+Alt+1…9** open account N (`app.cpp` 58-78).

### A5. CPP tray (`app.cpp` 97-177, `flyout.cpp`)

Tooltip "Claude Accounts". **Left-click**: acrylic flyout (352 wide) with title, open-picker and settings icon buttons, one 52 px row per account (32 px avatar, name, status, usage summary **"·  5 h 40% · 7 d 12% · at 07:42"**, ⟳ restart and ✕ close icon buttons for open accounts, digit badge), then **Add account**, **Quit switcher**. Digits and Esc work in it. **Right-click**: native menu — accounts (`Ctrl+Alt+N` accelerator text, checked when open), sep, **Restart Claude ▸** and **Close Claude ▸** submenus (one entry per open account plus **"All open"** when >1, greyed when none), sep, **Show accounts**, **Add account…**, **Settings**, sep, **Quit switcher**. Balloon notifications via `NIF_INFO` (e.g. "Shortcut created" / "“Claude - Work” is on the desktop", "Opening “Work” and “Personal”", launch errors). Jump list on the pinned taskbar button: category **Accounts** + task **Add account** (`shell.cpp` 146-172).

### A6. CPP Settings window (`settings.cpp`, 600x720, "Settings — Claude Accounts")

| Section | Row | Control / default |
| --- | --- | --- |
| Startup | **Start with Windows** — "The switcher starts when you sign in and waits in the tray" | toggle; HKCU Run value |
| | **Ask at sign-in** — "Asks which account to open. Without it, the accounts ticked below open silently" | toggle, default on, disabled unless autostart |
| | **Open at sign-in** — list of accounts with checkboxes | `AutoOpen` list |
| | **Countdown** — "How long to wait before opening the ticked accounts" | segmented Off / 3 s / 5 s / 10 s, default 5 |
| Quick access | **Shortcut** — "Brings up the account picker from any app" | recorder button (Ctrl+Alt+C default), clear button; errors "Another app already uses this shortcut, choose a different one", "Add Ctrl, Alt or Win so the shortcut doesn't get in the way of typing" |
| | **Ctrl + Alt + number opens an account** | toggle, default on; lists digits taken by other apps |
| | **Accounts on the taskbar** — "Pin the switcher to the taskbar: right-click it to see every account" | info only |
| Window | **Hide after opening an account** — "The picker goes away as soon as Claude opens" | toggle, default on (450 ms delay) |
| | **Stay in the tray when closed** | toggle, default on |
| | **Separate taskbar buttons per account** — "...Experimental" | toggle, default off |
| Language | **Interface language** — System / English / Русский | `Language` = auto/en/ru; auto = Russian when Windows UI language is Russian |
| About | "Claude <ver> · Microsoft Store/installer", "Switcher 1.0. Profiles live in <root>" | button **Profiles folder** |

Every string exists as `Tr(en, ru)`; Russian is complete. Command line (`main.cpp` 1-13): `--launch`, `--restart`, `--close`, `--add-account`, `--tray`, `--startup`, `--install`, `--claude-path`, plus `CPS_DEMO`, `CPS_LANG`, `CPS_THEME` env vars for screenshots.

### A7. CPP dialogs (`dialogs.cpp`, custom ContentDialog-style, Mica)

- **Delete**: title **"Delete “Work”?"**, text *"This signs the account out on this PC. Its profile folder, saved sign-in and shortcuts are deleted; other accounts stay"*, red **Delete** / **Cancel**; focus starts on Cancel and Enter cancels for danger dialogs (l.77-80).
- **Move Code chats** (600 wide): "Chats in “X”", **Select all**, scrollable rows (checkbox, title, right-aligned "folder · when"), **"Copy to"** chip buttons for each other account, note *"The chat stays here too. Carry on in one account at a time: the copies share their history"*, **Copy** / **Cancel**. Pre/post messages: "Nothing to move" / "“X” has no Claude Code chats yet"; "Add a second account first"; "Nothing selected" / "Tick the chats to copy"; "Claude Code hasn't been used in “X” yet" / "Open this account, sign in and visit the Code tab once. Then try again"; "“X” is open" / "The copied chats appear once you close and reopen that Claude window" (Copy); "Copied: N chats" / "They are now in “X” too" or "M chats were already in “X”"; then "Open “X”" / "Later".
- Claude not found (`main.cpp` 29): *"Couldn't find the Claude app on this PC. Point to Claude.exe yourself? The choice is remembered."* → file picker "Where is Claude.exe?".

---

## B. Profile colours

Both apps use the **same 8-colour palette** and the same assignment rule (PS l.536-557, CPP `core.cpp` 710-729):

| idx | RGB | hex |
| --- | --- | --- |
| 0 | 66,133,180 | #4285B4 (blue) |
| 1 | 76,150,96 | #4C9660 (green) |
| 2 | 150,96,180 | #9660B4 (purple) |
| 3 | 190,80,110 | #BE506E (rose) |
| 4 | 60,150,150 | #3C9696 (teal) |
| 5 | 90,110,200 | #5A6EC8 (indigo) |
| 6 | 200,150,50 | #C89632 (amber) |
| 7 | 80,86,100 | #505664 (slate) |

Assignment: on first request for a profile id, count how many *existing* profiles use each index and pick the least-used (ties → lowest index); store `Colors: { "<id>": <index> }` in `settings.json`. A colour never changes afterwards (comments: a hash of the name would collide and a changing colour "would defeat the point"). Default has no badge colour in PS; CPP draws the Main avatar with the Claude logo and uses coral RGB(217,119,87) #D97757 for its card colour (`app.cpp` 193). The Add-account icon uses green RGB(46,125,74) #2E7D4A in both.

Badge letters: PS uses the first alphanumeric character upper-cased (l.559-564); CPP uses initials of first and last word ("Work"→W, "Account 3"→A3, "Рабочий профиль"→РП) (`core.cpp` 732-745). CPP UI theme accents (`gfx.cpp` 53-60): light accent #C15F3C, dark accent #E08A6B; success #0F7B0F/#6CCB5F; warning #9D5D00/#FCE100; danger #C42B1C/#FF99A4; usage bar turns warning at ≥70 % and danger at ≥90 % (`gfx.cpp` 519).

---

## C. Config and metadata files

Both: `%LOCALAPPDATA%\ClaudeProfiles\settings.json` (flat JSON object). PS writes UTF-8 with BOM via `Set-Content -Encoding UTF8` (l.161); CPP deliberately writes a BOM too "so the PowerShell version still reads non-ASCII labels" (`core.cpp` 239) and writes atomically via `.tmp` + `MoveFileEx` (l.106-111).

| Key | Written by | Meaning |
| --- | --- | --- |
| `ClaudePath` | both | manual Claude.exe override |
| `InstallCache` `{Kind, Exe, Version, AppUserModelId, DefaultProfilePath}` | both | last discovery result (only trusted by shortcut-launch paths while `Exe` still exists) |
| `Labels` `{ "<folder id>": "<display name>" }` | both | rename = label only; absent when label == id |
| `Colors` `{ "<id>": <0-7> }` | both | see B |
| `KeepInTray` (bool) | both | default true |
| `TrayHintShown`, `SignInRouting` | PS | balloon shown once; routing on/off |
| `Language` ("auto"/"en"/"ru"), `AskAtStartup`, `CountdownSeconds`, `HideAfterLaunch`, `SplitTaskbar`, `Hotkey` (absent = Ctrl+Alt+C, "" = off), `AccountHotkeys`, `AutoOpen` `[ids]`, `Autostart`, `DemoRunning` | CPP | see A6 |

Example (CPP): `{"ClaudePath":"","InstallCache":{"Kind":"Msix","Exe":"C:\\Program Files\\WindowsApps\\Claude_2.26454.0.0_x64__xxxx\\app\\Claude.exe","Version":"2.26454.0.0","AppUserModelId":"Claude_xxxx!Claude","DefaultProfilePath":"C:\\Users\\me\\AppData\\Roaming\\Claude"},"Labels":{"Account 2":"Work"},"Colors":{"Account 2":0},"AutoOpen":["Account 2"],"CountdownSeconds":5,"Hotkey":"Ctrl+Alt+C"}`

**Profile order**: not stored. PS: Default first, then folder names `Sort-Object -Unique` (l.408). CPP: Default first, then natural sort (`SORT_DIGITSASNUMBERS`, case-insensitive) of folder names (`core.cpp` 483). **Last used**: not stored; derived (see D). **Custom paths**: none per profile; only the global `ClaudePath`.

Profile folders: PS 2.0 (since 2026-10-18) = `%LOCALAPPDATA%\ClaudeProfile-<id>` (flat, l.316) with a one-time migration from the old `%LOCALAPPDATA%\ClaudeProfiles\<id>` leaving a junction behind (l.1286-1342). CPP = `%LOCALAPPDATA%\ClaudeProfiles\<id>` (nested, `core.cpp` 394). Other files: `.switcher\` (installed script/exe, `icons\*.ico`, PS also `sign-in-router.log`, `pending-sign-in.json` `{Profile, Expires}`, `claude-handler-backup.json`), `ClaudeProfiles\switcher-error.log`.

**Inside a Claude profile folder** neither app writes its own files. The only write is chat transfer, which adds `local_<id>.json` files to the target's `claude-code-sessions\<account>\<org>\` (section E). Everything else (config.json, cookies, plan-usage-history.json) is only read.

---

## D. Launch mechanics

**Finding Claude.** PS (l.185-229): `Get-AppxPackage -Name Claude` → `<InstallLocation>\app\Claude.exe` (recursive fallback), kind `Msix`, AUMID `<PackageFamilyName>!Claude`, default data `%LOCALAPPDATA%\Packages\<family>\LocalCache\Roaming\Claude`. Otherwise Uninstall registry entries (HKCU, HKLM, WOW6432Node) with DisplayName like `*Claude*` + `%LOCALAPPDATA%\AnthropicClaude`, `%LOCALAPPDATA%\Programs\Claude`, `%ProgramFiles%\Claude`, `%ProgramFiles(x86)%\Claude`; `Claude.exe` directly or newest within depth 2 (Squirrel `app-<ver>` folders); kind `Classic`, data `%APPDATA%\Claude`. CPP (`core.cpp` 306-365) avoids the slow cmdlet: enumerates `HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages`, keys `Claude_<ver>_<arch>_<res>_<publisher>`, reads `PackageRootFolder`, picks the highest version. Its `MsixData` (l.300-304) prefers the package `LocalCache\Roaming\Claude` but falls back to `%APPDATA%\Claude` because "current [Store] builds write to the real %APPDATA%\Claude". Resolution order in both: `--claude-path` arg → saved `ClaudePath` → `InstallCache` (shortcut paths only) → fresh search; if the cached exe vanished after an update, re-detect (PS l.497, CPP l.560).

**Launching.** Default/Main on Store: `explorer.exe shell:AppsFolder\<AUMID>` so the instance keeps package identity (PS l.502, CPP l.567). Default on installer: run the exe with no args. Other profiles: `Claude.exe --user-data-dir="<profile path>"` — that is the only switch (PS l.527 via `Start-Process -WindowStyle Normal -PassThru`; CPP l.571-574 via `ShellExecuteEx`, working dir = exe folder). Sign-in links add the URL as a second argument (PS l.1512). Both create the folder first. If already running, both bring the window forward (PS `MainWindowHandle` + `SW_RESTORE` + `SetForegroundWindow` l.475-484; CPP first visible unowned non-tool window of the pid l.541-548); if there is no visible window (hidden to tray) they launch again and rely on Electron's per-directory single-instance lock to hand off.

**Running detection.** PS (l.341-362): WMI `Win32_Process Name='Claude.exe'`, drop `--type=` children, require `ExecutablePath` to match the desktop exe's folder pattern (any version of the same package family, l.326-339, to exclude the Claude Code CLI also named `claude.exe`), parse `--user-data-dir=("…"|\S+)`; no switch → Default path. CPP (`core.cpp` 430-454): Toolhelp snapshot + `QueryFullProcessImageName` + `NtQueryInformationProcess(ProcessCommandLineInformation=60)`; same filters; also handles `"--user-data-dir=C:\x y"` quoted as a whole (l.414-426). Map key = lower-cased profile path → pid. CPP refreshes on a background thread every 2 s while a window is visible or a launch is pending (`app.cpp` 476-485).

**Status** = pid present. **Last used** (PS l.381-391, CPP l.463-470) = newest `LastWriteTime` of `Preferences`, `Local State`, `config.json`, `window-state.json`, `Network\Cookies` inside the profile; none → "never"/"Not opened yet". Running profiles show "now"/"Open" instead.

**Restart/Close (CPP only)** `core.cpp` 582-653: builds the process tree (children validated by creation time), suspends all with `NtSuspendProcess`, drops to idle priority, `TerminateProcess` in batches of 16 with a 10 s deadline, then `Start`. Rationale: Claude's own Quit hides to tray; MCP servers can be hundreds of processes and killing them at once froze Explorer.

---

## E. Transfer chats

Identical logic in both (PS l.1192-1282, CPP `core.cpp` 756-826). Source of truth: `<profile>\claude-code-sessions\<account uuid>\<org uuid>\local_<id>.json`, one small JSON per Claude Code chat (the transcript lives outside the profile in `%USERPROFILE%\.claude`, shared by all accounts). Listing reads each file for a title (`title`, `name`, `summary`, `customTitle`, `displayName`, fallback "Chat <8 chars of id>") and a folder (`cwd`, `workingDirectory`, `projectPath`, `folder`, `path`; a `\scratch-workspaces\` path shows "(no folder)"), dedupes by file name keeping the newest, sorts newest first. Target store = the most recently modified `<account>\<org>` directory in the target profile (a profile signed in to several accounts has several). Copy: skip if destination file exists ("exists"), else read the text, replace every quoted `"<source account>"` and `"<source org>"` string with the target's ids (whole JSON strings only, so paths containing the ids are untouched), write UTF-8 without BOM. PS additionally resolves the Store-build LocalCache redirection for profile folders (l.1212-1226); CPP does not. Confirmations: see A2/A7. Risks the apps themselves state: both copies share one transcript, so using both concurrently diverges history; target must have opened the Code tab once; an open target only shows the chats after restart; nothing is ever deleted from the source.

---

## F. Delete profile

PS `Remove-ClaudeProfile` (l.1143-1167): throws **"The Default profile cannot be deleted."**; refuses unless the path is exactly `%LOCALAPPDATA%\ClaudeProfile-<id>` or the legacy `ClaudeProfiles\<id>` and the id has no dot-prefix or slashes (**"Refusing to delete '<path>'."**); refuses while running (**"Close the '<name>' window before deleting it."**); `Remove-Item -Recurse -Force`; deletes the legacy junction; removes `Claude - <label>.lnk` from Desktop and Start menu; deletes `<id>*.ico`; drops the label. The `Colors` entry is left (and its slot counts as free since the folder is gone). CPP `Core::Delete` (`core.cpp` 694-706): **"The main account can't be deleted"**, **"Refusing to delete “…”"**, **"Close the “X” window first"**; `SHFileOperation FO_DELETE` with `FOF_NO_UI` (permanent, no recycle bin), then shortcuts, icons, label, and the `AutoOpen` list is re-saved (`app.cpp` 341). Confirmation wording in A2/A7; the Delete button/menu item is disabled for Default/Main in both.

---

## G. Shortcuts

Both create `Claude - <label>.lnk` (PS: Desktop + Start Menu\Programs in one click; CPP: Desktop only from the card menu, Start menu copy created implicitly in PS when a profile launches without one, l.521-522). Target: PS → `conhost.exe --headless "<powershell.exe>" -NoProfile -ExecutionPolicy Bypass -File "<.switcher\ClaudeSwitcher.ps1>" -Launch "<id>"` (Win10 1809+, else `powershell -WindowStyle Hidden`, l.1020-1039); CPP → `<.switcher\ClaudeProfileSwitcher.exe> --launch "<id>"` (`shell.cpp` 38). Default/Main on Store → `explorer.exe shell:AppsFolder\<AUMID>` directly. Icon = the profile's generated `.ico` in `.switcher\icons` (PS: `<id>-<letter>.ico`, frames 16/24/32/48/256, badge circle 60 % of size with white ring, letter from 24 px; CPP: `<id>-<initials>-v2.ico`, frames 16/20/24/32/40/48/64/256, badge 52 % with gradient and cut-out gap, initials from 32 px, `icons.inc`). The letter is in the file name because Explorer caches icons by path. Description "Open Claude with the '<name>' account"; working directory `.switcher`. PS also stamps the `.lnk` with the profile's AppUserModelID so a pinned copy groups with the window (l.1045-1052). Switcher shortcuts: "Claude Profile Switcher.lnk" (Desktop + Start) and "Claude - Add account.lnk" (Start) with the green "+" icon. Rename deletes and recreates shortcuts under the new label (PS l.1131-1141, CPP l.664-674).

---

## H. Crash handling, auto-restart, memory, window placement

- Crash handling: PS global `trap` logs and shows a message box, exit 1; CPP `wWinMain` catches `std::wstring`/`wchar_t*`/`std::exception` → `Fail()` logs to `switcher-error.log` and shows a message box. Neither restarts itself or watches Claude for crashes. CPP re-adds its tray icon on `TaskbarCreated` (Explorer restart) and has the manual Restart/Close Claude feature.
- Memory: only README claims (PS ~126 MB vs CPP ~16 MB; "budget one Claude per account; 4 GB per local Cowork VM"). No limits enforced.
- Window placement: PS `CenterScreen`; CPP centres the picker on the monitor under the cursor (40 px high), Settings likewise, flyout anchored to the tray icon rect. Nothing persisted. Multi-select is reset on each show.

---

## I. Licences

Both MIT. PS: `Copyright (c) 2026 Priyanshu (PriyanshuGeTRekT)`. CPP's LICENSE carries **two** lines: `Copyright (c) 2026 Piwqust` and `Copyright (c) 2026 Priyanshu (PriyanshuGeTRekT)`, so a derivative must keep both notices. Comments in PS credit *Sukarth (Sukarth/Claude-Code-Desktop-Switcher)* for the taskbar-identity and sign-in-router ideas (l.678, 1368); keeping that attribution is good manners if those designs are reused. Both READMEs state "unofficial, not affiliated with Anthropic; Claude is a trademark".

---

## J. Gotchas and details worth keeping

1. **Folder layout divergence.** PS 2.0 moved profiles to `%LOCALAPPDATA%\ClaudeProfile-<id>` on 2026-10-18 because Cowork's VM service is given only the folder *name* and looks for the VHDX in `%LOCALAPPDATA%\<name>`, refusing junctions. CPP (HEAD 2026-10-23) still uses `ClaudeProfiles\<id>` and will not see migrated profiles. A replacement should support both (and the junction PS leaves at the old place).
2. **Versioned install paths.** Store and Squirrel paths contain the version, so cache only while the exe exists, re-detect on failure, and have shortcuts go through the launcher rather than the exe.
3. **Store data location.** Older Store builds redirect `%APPDATA%\Claude` into `Packages\<family>\LocalCache\Roaming\Claude`; newer ones write to `%APPDATA%\Claude` (CPP checks both). PS also discovers that profile writes under `%LOCALAPPDATA%` can be redirected into `LocalCache\Local\<relative>` and looks there for `config.json`/code sessions (l.1212-1226, 1419-1431).
4. **Process filtering.** Skip `--type=` renderers and the Claude Code CLI (`claude.exe` elsewhere); match the desktop exe's package family regardless of version so an instance still on the old build after an update counts.
5. **Electron single-instance lock is per `--user-data-dir`.** Launching again is the correct way to surface a tray-hidden window.
6. **Single switcher instance.** PS: mutex `Local\ClaudeProfileSwitcher` + event `.Show`; CPP: `Local\ClaudeProfileSwitcherExe` + `.Show/.Exit/.Add` events, with `AllowSetForegroundWindow` handed over. CPP's installer also kills a running PS switcher (`shell.cpp` 79-99).
7. **Taskbar grouping.** All Claude windows share one AUMID; both set `System.AppUserModel.ID` + relaunch command/icon/name on the window's property store from outside the process (PS l.810-823, CPP l.238-259). Windows reads the icon once per AUMID, so PS encodes the badge letter in the AUMID and CPP bumps `IconStyle` in file names. CPP uses a `WinEventHook(EVENT_OBJECT_SHOW)` to tag windows as they appear; PS polls at 150 ms for 25 s after a launch. Both restore/leave Default alone.
8. **Name validation** (identical rules): no `\/:*?"<>|`, no control chars, no leading dot or trailing dot/space, no reserved device names, ≤40 chars, not "Add account", unique against ids and labels, folder must not already exist.
9. **Sign-in routing (PS only)** handles `claude://` sign-in links (`claude://login/…` or `claude://claude.ai/sso-callback…`) by registering a `ClaudeProfileRouter` handler in HKCU and forwarding to: the profile launched while signed out (15-minute `pending-sign-in.json`), the only open signed-out profile, the frontmost Claude window, else Default. Signed-in state is inferred from `config.json` `windowSizeWasSignedIn` or `oauth:tokenCacheV2` length > 100 (l.1433-1448). Claude rewrites the handler on every start so PS retakes it on a 5 s timer. Links are validated against an RFC 3986 character class before touching a command line and never logged.
10. **Usage meters (CPP only)** read `<profile>\plan-usage-history.json` (`samples[{t, u:{fh, sd}}]`) and `%USERPROFILE%\.claude.json` `cachedUsageUtilization` matched by `config.json` `lastKnownAccountUuid`; shown only if ≤15 min old and before `resets_at` (`core.cpp` 879-920). No network.
11. **Startup.** PS: Startup-folder shortcut `-Tray`. CPP: `HKCU\…\Run` value "Claude Profile Switcher" = `"<exe>" --startup`, deleting the PS shortcut to avoid two copies; `--startup` shows the picker with a cancellable countdown or silently opens `AutoOpen` accounts.
12. **Dark native menus** via undocumented uxtheme ordinals 133/135/136 (`shell.cpp` 178-193); Mica/Acrylic via DWM on build ≥22621.
13. **Settings file compatibility**: BOM kept for PS; `Hotkey` parser accepts `OemComma` from an intermediate .NET version (`app.cpp` 579), hinting the keys were designed to be shared across rewrites.
14. **Chat transfer MSIX caveat**: CPP ignores the LocalCache redirection PS handles, so on a Store build with redirection it may find no sessions.
