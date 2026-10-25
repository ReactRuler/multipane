# 07 · Settings, profiles and layout: JSON schemas

Three small, human-readable files in `%LOCALAPPDATA%\Multipane\` (portable mode: `data\` next to the exe, enabled by an empty `portable.txt` beside the exe). Each has a `version` integer and is written atomically (write `.tmp`, `MoveFileEx` with `MOVEFILE_REPLACE_EXISTING`). UTF-8 without BOM, 2-space indent, `camelCase` keys, unknown keys preserved on round-trip so a newer build's file survives an older build.

## settings.json (version 1)

```json
{
  "version": 1,
  "claudePath": null,
  "startup": { "startWithWindows": false, "openProfiles": ["account-2"], "restoreLayout": true },
  "tray": { "keepRunning": true, "routeSignIns": false },
  "shortcuts": {
    "bringForward": "Ctrl+Alt+C",
    "profileNumbers": true,
    "commandPalette": "Ctrl+K"
  },
  "autoRestart": {
    "enabled": false, "initialDelayMs": 5000, "backoff": 2.0, "maxDelayMs": 60000,
    "maxCrashes": 5, "windowMs": 600000, "resetAfterMs": 600000
  },
  "memory": { "totalWarnPercent": 85, "perSessionWarnMb": 6144, "sampleIntervalMs": 3000 },
  "appearance": { "theme": "system", "backdrop": "mica", "animations": "system" },
  "language": "auto",
  "firstRunDone": true
}
```

| Key | Type | Notes |
| --- | --- | --- |
| `claudePath` | string or null | Folder the user picked when Claude was not found. Never a versioned exe path. |
| `startup.openProfiles` | profile ids | Started at host start, in order. |
| `tray.routeSignIns` | bool | Registers the `claude://` handler described in the trust doc when true; unregisters when set false. |
| `shortcuts.*` | gesture strings | Parsed as `Modifier+…+Key`; a gesture without Ctrl, Alt or Win is rejected in the UI. |
| `autoRestart` | object | Global defaults; per-profile override in `profiles.json`. |
| `appearance.theme` | `system` / `light` / `dark` | |
| `appearance.backdrop` | `mica` / `acrylic` / `plain` | Ignored on Windows 10. |
| `language` | `auto` / `en` / `ru` | |

## profiles.json (version 1)

```json
{
  "version": 1,
  "profiles": [
    { "id": "original", "name": "Personal", "color": "blue", "kind": "original", "path": null, "createdAt": "2026-10-25T09:00:00Z", "lastLaunchedAt": "2026-10-25T09:12:00Z", "autoRestart": null },
    { "id": "account-2", "name": "Business", "color": "coral", "kind": "folder", "path": "%LOCALAPPDATA%\\ClaudeProfile-Account 2", "importedFrom": "claude-profile-switcher-2", "createdAt": "...", "lastLaunchedAt": "...", "autoRestart": { "enabled": true } }
  ]
}
```

| Key | Notes |
| --- | --- |
| `id` | Stable internal id. `original` for the original account; otherwise a slug of the folder name. Never shown. |
| `name` | Display name, validated by the rules in doc 06. |
| `color` | One of the ten palette names. Imported from the switcher's colour index 0–7. |
| `kind` | `original` (launched through the shell or the plain exe, no `--user-data-dir`) or `folder`. |
| `path` | Stored with `%LOCALAPPDATA%` / `%APPDATA%` tokens so the file is readable and survives a user-name change. Expanded at use. `null` for the original. |
| `importedFrom` | `claude-profile-switcher-2`, `claude-profile-switcher-1`, `claude-switcher-cpp`, `found-running`, or absent for profiles created here. Informational. |
| `lastLaunchedAt` | Set on launch or adoption; this is "Last used". |
| `autoRestart` | Partial override of the global object, or `null`. |

Order in the array is the order in the profile list and for Ctrl+Alt+N.

## layout.json (version 1)

```json
{
  "version": 1,
  "mode": "grid",
  "order": ["original", "account-2", "account-3", "account-4"],
  "hidden": [],
  "activeTab": "original",
  "focusProfile": "account-2",
  "maximized": null,
  "sizes": {
    "columns": [0.25, 0.25, 0.25, 0.25],
    "rows": [0.25, 0.25, 0.25, 0.25],
    "grid": { "cols": [0.5, 0.5], "rows": [0.5, 0.5] },
    "focus": { "main": 0.78, "strip": [0.3333, 0.3333, 0.3334] }
  },
  "poppedOut": { "account-3": { "x": 2100, "y": 80, "w": 1100, "h": 760, "monitor": "\\\\.\\DISPLAY2" } },
  "window": { "x": 40, "y": 40, "w": 1840, "h": 1000, "state": "maximized", "monitor": "\\\\.\\DISPLAY1" },
  "lastStates": { "account-4": { "state": "crashed", "at": "2026-10-25T02:11:00Z", "exitCode": -1073741819 } }
}
```

`lastStates` lets a pane show "crashed at 02:11" after a host restart instead of a blank Not running. Fractions are validated on load (sum to 1 ± 0.01, each ≥ 0.05) and reset to equal shares if not.

## Migrations

`SettingsStore.Load<T>(file)` reads `version`; if lower than the current, applies the migration chain `v1→v2→…` (each a small function in `Migrations.cs` with a test), writes the migrated file, and keeps a one-time backup `settings.v1.json`. Unknown higher versions are loaded as-is with unknown keys preserved and a log warning; nothing is deleted. A file that fails to parse is renamed `settings.broken-<timestamp>.json` and defaults are used; the UI shows a banner once: "Settings could not be read and were reset. The old file is kept next to it."

## logs

`logs\multipane-<yyyyMMdd>.log`, see doc 08.
