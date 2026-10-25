# 10 · Test plan

Three layers: pure unit tests for the Core, integration tests that drive the real Native layer against a dummy app, and a manual checklist against real Claude for what cannot be automated without an account.

## Unit tests (`tests/Multipane.Core.Tests`, xunit, run in CI on every push)

| Area | Cases |
| --- | --- |
| LayoutEngine | For each mode and n = 1..9 at three window sizes: rectangles tile the content area exactly, gaps are 6 px, minimums respected or `tooSmall`; Grid shapes 2→2×1, 3→2+1, 5→3+2, 6→3×2, 7→3×3; reorder is a permutation; resize keeps sums at 1 and respects minimums; hide keeps order; JSON round-trip of fractions |
| RestartPolicy | Table: attempt 0..6 → delays 5, 10, 20, 40, 60, 60 s; 5 crashes in 10 min → Stop; 5 crashes over 11 min → Restart; reset after quiet period; per-profile override wins over global |
| Session state machine | Every transition in doc 04 as a scripted sequence with a fake clock, fake finder, fake launcher; crash classification (requested kill, clean close, non-zero exit, exit with window alive); adoption at start; Starting timeout; hung on/off |
| ProfileCatalog | Fixture folders built in a temp directory: 2.0 flat layout, legacy nested layout, junction from legacy to flat, `.switcher` ignored, settings.json with labels and colours (and with BOM), colour index mapping, least-used colour choice, name validation (every rule), lowest free `Account N` across layouts, missing folder stays listed |
| SettingsStore | Defaults when missing; unknown keys preserved; broken JSON renamed and defaults used; migration chain v1→v2 with backup; atomic write leaves no `.tmp`; path tokenisation `%LOCALAPPDATA%` both ways |
| HealthMonitor | Threshold events fire once per crossing, not per sample; heaviest-idle suggestion picks by memory among sessions idle > 10 min |
| FileLog | Rotation by day and by size; redaction of user-profile prefixes and of window titles passed by mistake (the API takes no title parameter, test asserts the type) |
| Command parsing | `--user-data-dir` with quotes, without, quoted whole, trailing slash, mixed case |

Target: Core line coverage above 85 %, enforced in CI (coverlet, threshold in the test project).

## Dummy app (`tests/DummyClaude`)

A tiny WPF exe named `Claude.exe` inside `tests/DummyClaude/bin/app-0.0.1/` so the install-root matching is exercised. It:

- Accepts `--user-data-dir=<path>` and `--type=renderer` (spawns itself as a helper child, so tree logic is tested).
- Creates a top-level window of class `Chrome_WidgetWin_1` (registered manually via `RegisterClassEx`) with a title, and a second owned tool window after 1 s (simulating a popup).
- Listens on a named pipe `\\.\pipe\dummy-claude-<pid>` for commands: `crash <code>` (exit with code), `hang <ms>` (block the message loop), `popup`, `filedialog`, `hide` (Claude's quit-to-tray behaviour), `close` (clean exit), `spawn <n>` (n helper children), `alloc <mb>`.
- Honours the Electron single-instance rule per `--user-data-dir` with a named mutex, handing off to the running instance.

No account, no network, no Electron. About 400 lines.

## Integration tests (`tests/Multipane.Integration.Tests`, CI on Windows runners, x64 and ARM64)

Each test starts a fresh host in demo-free mode with `--claude-path` pointing at the dummy, `--data-dir` in a temp folder, and drives it through its own automation seam (`Multipane.App --automation <pipe>` exposes the same commands the palette has) plus UI Automation for a few visual checks.

| Test | Steps | Asserts |
| --- | --- | --- |
| Launch and host | start profile A | state Running within 5 s; dummy window has no caption; its rectangle equals the pane's inner rectangle ± 1 px |
| Four up, each arrangement | start A–D; switch Tabs → Columns → Rows → Grid → Focus | rectangles match LayoutEngine output; no window reloaded (same hwnd) |
| Crash | pipe `crash 5` to B | state Crashed with code 5 within 2 s; placeholder visible; others unchanged |
| Auto-restart | enable for B, delay 1 s, `crash 5` ×2 | restarts after 1 s then 2 s; attempt counter 2; `crash` ×3 more → Auto-restart stopped |
| Killed vs closed | Kill C; pipe `close` to D | C Killed, D Not running "closed by you" |
| Quit-to-tray | pipe `hide` to A; Bring forward | still Running; second launch hands off; window visible again |
| Hung | pipe `hang 8000` | Not responding after 5 s; Running after it returns; host UI responsive throughout (automation round-trip < 200 ms) |
| Host restart | start A–C, kill host process, start host | all three adopted, same hwnds, hosted into saved slots, no new dummy processes |
| Host exit | quit host keeping sessions | dummies still running with captions restored |
| Hosting failure fallback | dummy started with `--refuse-owner` (rejects style changes) | "in its own window" stub; others hosted |
| Pop out, dock back | 10 cycles on B | hwnd unchanged; styles correct at the end |
| Memory warning | `alloc 7000` in B with a 6 GB limit | banner once; Restart suggestion names B |
| Kill confirmation | `spawn 6` then Kill | confirmation dialog appears; Cancel leaves it running |
| First run import | fixture folders and settings.json | first-run list matches; after Continue only `profiles.json` written; fixture folders byte-identical (hash before and after) |
| Nothing written in profiles | whole suite | a file-system watcher on every fixture profile folder records zero writes except the Transfer chats test |
| Transfer chats | fixture `claude-code-sessions` in A, copy to B | exactly the expected files appear in B with ids rewritten; A unchanged |
| DPI | run the host under `--dpi 150` emulation on a runner that supports it | rectangles scale; no window smaller than minimum |

## Manual checklist against real Claude (before each release, both builds, Windows 10 and 11)

The hosting spike matrix (doc 02) rows 1–22, plus:

- Every imported profile opens without a sign-in screen after first run; `profiles.json` is the only new file; a hash of each profile folder taken before first run matches after (excluding files Claude itself changes while running: compare with Claude closed).
- Add account opens the sign-in screen in a new pane; the folder appears in the old switcher too.
- Delete a test profile: folder in the Recycle Bin, shortcuts gone, other profiles untouched.
- Global shortcuts from another app, with the host hidden in the tray.
- 200 % scaling on a laptop panel with an external 100 % monitor; drag a pane across.
- Sleep/resume with six sessions.
- Run for 8 hours with four sessions: host private bytes do not grow more than 10 MB (checked from Task Manager at start and end).

Results go into `docs/tech/release-checks/<version>.md`.
