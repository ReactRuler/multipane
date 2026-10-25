# 06 · Profile compatibility with the existing switchers; Store versus installer

Hard requirement: every existing account opens signed in, with nothing moved, copied, renamed or rewritten. This document says exactly what is read, from where, and what is never touched. It is based on reading both switchers' source (`docs/research/switcher-research.md`).

## Where profiles live today

| Source | Profile folders | Metadata file |
| --- | --- | --- |
| Original account, Store build | `%APPDATA%\Claude` on current builds; older builds redirect to `%LOCALAPPDATA%\Packages\Claude_<id>\LocalCache\Roaming\Claude` | none |
| Original account, installer build | `%APPDATA%\Claude` | none |
| Claude Profile Switcher ≤ 1.x and claude-switcher (C++) | `%LOCALAPPDATA%\ClaudeProfiles\<id>` (`Account 2`, `Account 3`, …) | `%LOCALAPPDATA%\ClaudeProfiles\settings.json` |
| Claude Profile Switcher 2.0 (since 2026-10-18) | `%LOCALAPPDATA%\ClaudeProfile-<id>` (flat), with a junction left at the old `ClaudeProfiles\<id>` path | same `settings.json` |

Both switchers share one `settings.json` (UTF-8 with BOM) with the keys that matter here:

```json
{
  "ClaudePath": "",
  "InstallCache": { "Kind": "Msix", "Exe": "...", "Version": "...", "AppUserModelId": "Claude_xxxx!Claude", "DefaultProfilePath": "..." },
  "Labels": { "Account 2": "Work" },
  "Colors": { "Account 2": 0 },
  "KeepInTray": true,
  "Language": "auto",
  "Hotkey": "Ctrl+Alt+C",
  "AutoOpen": ["Account 2"],
  "Autostart": true
}
```

Neither switcher stores profile order (Default first, then folder names, naturally sorted) or "last used" (derived from file times inside the profile).

## First-run import (read-only)

1. Resolve the Claude install (doc 03) to learn the original account's data folder and whether it is the Store build.
2. Read `%LOCALAPPDATA%\ClaudeProfiles\settings.json` if it exists. Never written.
3. Enumerate `%LOCALAPPDATA%\ClaudeProfile-*` (2.0 layout) and `%LOCALAPPDATA%\ClaudeProfiles\*` directories (legacy and C++ layout). A junction at the legacy path that points at a 2.0 folder is recognised (`FILE_ATTRIBUTE_REPARSE_POINT`, resolved target) and listed once, at its real path. Folders named `.switcher` are skipped.
4. For each id: label = `Labels[id]` or the folder name; colour = `Colors[id]` mapped onto our first eight palette entries, else least-used; path = the real folder; source = which switcher layout it came from.
5. Also list any Claude process already running with a `--user-data-dir` not in the list above, as a profile "found running", path from its command line.
6. Show the first-run screen (design, artboard 16). Nothing is written until Continue, which writes only `%LOCALAPPDATA%\Multipane\profiles.json`.

Missing folder for a listed id: shown as "Folder not found" with Locate. The entry stays; no folder is created.

## Launching imported profiles

Exactly as the switchers do (doc 03): the original through `shell:AppsFolder\<AUMID>` on the Store build or `Claude.exe` on the installer; every other profile with `--user-data-dir="<path as found>"`. Paths are passed verbatim, so a junction keeps working and Cowork's VHDX lookup (the reason for the 2.0 flat layout) is unaffected.

## Side by side

The old switcher may stay installed. Both read the same folders; neither needs the other. If the old switcher is used to add or rename a profile later, Multipane notices new folders at start and offers to import them (same first-run list, only the new rows); renamed labels are not re-imported automatically, to avoid overwriting a name chosen in Multipane (decisions doc, D6). Multipane's own metadata lives only in its own folder, so uninstalling it leaves everything as it was.

## New profiles created by Multipane

Folder `%LOCALAPPDATA%\ClaudeProfile-<id>` (the 2.0 layout, so the current PowerShell switcher sees them too), id = `Account N` with the lowest free N across both layouts. Created empty; Claude fills it on first launch. Name rules identical to the switchers: no `\ / : * ? " < > |`, no control characters, no leading dot, no trailing dot or space, no reserved device names, at most 40 characters, unique against ids and labels, not "Add account".

## Delete

Refused unless: not the original account, not running, and the path is exactly one of the two layouts with a plain id (no dots, no separators). Deletes the folder (with `FOF_ALLOWUNDO` to the Recycle Bin, the one deliberate difference from the C++ switcher, see D7), the profile's shortcuts, and its entry in `profiles.json`. Never touches the old switcher's `settings.json`, so the old switcher shows the entry as missing, which is what it does today for a removed folder.

## Transfer chats

Kept as the switchers do it, because it is the only way the Claude Code tab in one profile learns about a chat from another:

- Source files: `<profile>\claude-code-sessions\<account uuid>\<org uuid>\local_<id>.json`, one small JSON per chat. The transcript itself is in `%USERPROFILE%\.claude`, shared by every profile, and is never touched.
- Target: the most recently modified `<account>\<org>` folder in the target profile.
- Copy: skip if the destination exists; else read, replace the quoted source account and org uuid strings with the target's, write UTF-8 without BOM.
- Store-build redirection (`LocalCache\Local\...`) is checked for both source and target, like the PowerShell version (the C++ version misses it).

This is the one documented write inside a profile folder. The dialog says so. The code lives in `ChatTransfer.cs` alone.

## Store versus installer differences

| Concern | Store (MSIX) | Installer (Squirrel) |
| --- | --- | --- |
| Exe location | `%ProgramFiles%\WindowsApps\Claude_<ver>_<arch>__<pub>\app\Claude.exe`, unreadable directory listing but the registry key names the root | `%LOCALAPPDATA%\AnthropicClaude\app-<ver>\Claude.exe` (or the other roots) |
| Launch of original | `explorer.exe shell:AppsFolder\<family>!Claude` | run the exe |
| Original's data | `%APPDATA%\Claude`, older builds `Packages\...\LocalCache\Roaming\Claude`; check both, prefer the one with `config.json` present (existence only, not contents) | `%APPDATA%\Claude` |
| Profile data redirection | writes under `%LOCALAPPDATA%` may land in `Packages\...\LocalCache\Local\...` | none |
| Updates | silent, path changes with version | Squirrel, `app-<ver>` folder changes |
| Process image check | match on package family, any version | match on install root, any `app-*` |
| Taskbar identity | all windows share one AUMID; `shell:AppsFolder` keeps it | shared relaunch command |

Per-profile taskbar grouping (setting each Claude window's `System.AppUserModel.ID` from outside, as both switchers do) is listed as a later feature (D8). It is a property-store write on a window the app does not own, so it stays out of version 1 unless the owner wants it.

## What is never read

Cookies, Local Storage, `config.json` contents, `Preferences`, `Local State`, chat files, tokens. "Last used" comes from Multipane's own record of launches and adoptions (design decision 5, accepted). The only checks inside a profile folder are *existence* checks (`config.json` present? `claude-code-sessions` present?) and the Transfer chats read above.
