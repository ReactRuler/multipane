# 08 · Error handling and local logging

## Principles

- Handle an error where something can be done about it; otherwise let it reach the one top-level handler, which logs and shows a plain-words dialog.
- Every error shown to the user names the thing and the next step: "Couldn't start Business. Claude.exe was not found at the usual places. Choose Claude.exe…".
- No empty `catch`. A caught exception is either handled (state changes, user told) or rethrown with context.
- Win32 failures carry the API name and `GetLastError` / `HRESULT` into the log; the UI gets the human sentence.

## Error classes

| Class | Examples | Handling |
| --- | --- | --- |
| Expected, per session | launch failed, hosting failed, kill timed out, hung window | Session state changes (Crashed / fallback / Not responding), placeholder or bar explains, log at Warning. Other sessions unaffected. |
| Expected, per action | rename collides, delete refused, folder missing, shortcut write failed | Dialog or inline message with the reason and what to do. Log at Info. |
| Configuration | settings file unparsable, layout off-screen, Claude not found | Reset to safe defaults, keep the bad file, banner once. Log at Warning. |
| Unexpected | any other exception on the UI thread or a background loop | Logged at Error with stack; the background loop restarts; a UI exception shows "Something in Multipane failed: <message>. Details are in the log. Sessions keep running." and the app continues. The host never exits on an unexpected error, because exiting would unhost every session. |

Background loops (discovery, health) run inside a `try` per tick; a failing tick is logged and the next tick runs. Three consecutive failures of the same loop raise a banner.

## Local log

- Location: `%LOCALAPPDATA%\Multipane\logs\multipane-<yyyyMMdd>.log` (portable: `data\logs\`).
- Rotation: a new file per day; keep 7 files or 10 MB total, whichever is reached first; oldest deleted at start.
- Format, one line per event: `2026-10-25 02:11:03.412 W Session account-4 exit code=0xC0000005 classified=crashed pendingRequest=none` (local time, level `D I W E`, component, message with `key=value` pairs).
- Default level Info. `--verbose` or Settings › About › "Verbose log until restart" switches to Debug (window rectangles, placement batches, discovery diffs).
- Written by a 120-line `FileLog` class with a bounded queue and one writer thread; no logging library.

## What is logged

State transitions, launch commands (exe path and switches, profile *id* not name), discovery diffs (pid added/removed per profile id), exit codes, hosting steps and Win32 errors, settings migrations, memory threshold events (numbers only), timings (startup, placement batch).

## What is never logged

- Window titles (Claude's title can contain a conversation name).
- Anything read from a profile folder, including file names of chats; Transfer chats logs only counts.
- Profile display names (ids instead; the Profiles window maps them).
- User name or home path: paths are written with `%LOCALAPPDATA%` / `%APPDATA%` / `%USERPROFILE%` tokens substituted for the real prefixes.
- Command lines of Claude processes beyond the `--user-data-dir` value tokenised as above.
- Clipboard, keystrokes, URLs, including sign-in links when routing is on (only "routed sign-in to <id>").

The log is for the owner's own debugging; nothing uploads it. "Open log" in the UI opens the folder in Explorer.

## Crash of the host itself

An `AppDomain.UnhandledException` / `DispatcherUnhandledException` handler writes the exception to the log and to `logs\last-crash.txt`, calls `WindowHost.ReleaseAll()` so Claude windows get their frames back, and lets the process end. There is no crash upload. On next start, if `last-crash.txt` exists, the title bar shows once: "Multipane closed unexpectedly last time. Sessions were re-adopted. Details in the log." and the file is renamed with its timestamp.
