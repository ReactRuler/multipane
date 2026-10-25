# 12 · Risks and open questions

Each risk has an owner action, a mitigation already in the design, and a trigger that would make it a problem.

| # | Risk | Likelihood | Impact | Mitigation | Verified by |
| --- | --- | --- | --- | --- | --- |
| R1 | Hosting an Electron window fails on some matrix rows (focus, IME, popups, DPI) for both approaches | Medium | High: the core feature | Two approaches in the spike; per-session fallback to a normal window with the stub; Tabs mode still works with un-hosted windows placed over the pane | Spike (doc 02) before any other code |
| R2 | Owner link makes Claude windows die with a crashing host | Low | High | `ReleaseAll` in the crash handler; sessions are not in a job object; spike row 16 confirms owned windows survive a `TerminateProcess` of the owner | Spike row 16, integration "host restart" |
| R3 | Chromium window class or `--type=` switch changes with an Electron update | Low per year | Medium: discovery or window finding breaks | Both constants isolated in `ClaudeFacts.cs`; discovery falls back to "any visible top-level window of the pid"; a loud log line and a banner "Claude's window could not be recognised" | Unit test with current constants; manual checklist after Claude updates |
| R4 | Host memory above 60 MB with WPF | High | Low: a target, not a function | Trim XAML resources, no images in memory, ReadyToRun; measure; if above 80 MB, move the shell to a lighter WPF surface or accept and document; native C++ port remains possible because Native and Core are separable | Manual checklist, 8-hour run |
| R5 | Claude's "Quit" hides to tray, so Kill via `WM_CLOSE` never ends the process | Certain | Low | Kill always proceeds to the suspend-and-terminate step after 3 s; state shown as "Closing" | Integration "Killed vs closed", "Quit-to-tray" |
| R6 | Hundreds of MCP helper processes make kill slow or freeze Explorer | Medium | Medium | Suspend the tree first, idle priority, batches of 16 with a deadline (the C++ switcher's proven approach) | Integration `spawn 200` then Kill |
| R7 | Store build redirects profile writes into `LocalCache`, so a path we pass is not where files end up | Medium | Medium: Transfer chats or "folder not found" wrong | Resolve the redirection for existence checks and for Transfer chats exactly as the PowerShell switcher does; never for launching (pass the path as found) | Manual on a Store install |
| R8 | The two switchers diverge further (folder layouts, settings keys) | Medium | Low | Import reads both layouts and tolerates unknown keys; new profiles use the 2.0 flat layout, the layout the owner's switcher uses | Catalog unit tests with both fixtures |
| R9 | "Busy" heuristic is wrong (idle session flagged busy, or busy one idle) | Certain sometimes | Low | It only decides whether Kill asks first; the dialog states the evidence (CPU, helpers) so the user can judge | None needed |
| R10 | Global shortcuts collide with another app | Medium | Low | `RegisterHotKey` failure shows "Another app already uses this shortcut, choose a different one" in Settings (switcher wording); never silently taken | Unit: settings validation; manual |
| R11 | Per-monitor DPI change mid-drag produces a frame with wrong rectangles | Medium | Low | Relayout on `WM_DPICHANGED` before the next placement batch; the spike's row 11 | Spike row 11, integration DPI test |
| R12 | Sign-in routing (if enabled) must re-register the `claude://` handler after Claude rewrites it on every start | Certain when on | Medium: a stale handler sends links to the wrong profile | Off by default; when on, re-check on a 5 s timer while a sign-in is pending (switcher behaviour); fully documented in the trust doc | Manual with two profiles |
| R13 | Unsigned releases trigger SmartScreen and scare users | High until signing | Medium | Decision D3; checksums and provenance in the meantime; README explains | n/a |
| R14 | ARM64 runner availability or behaviour differences | Low | Medium | CI matrix includes `windows-11-arm`; the owner has x64 only, so ARM64 manual checks need a volunteer or a cloud VM (D9) | CI |
| R15 | Reading process command lines is denied for a Claude process started elevated or by another user | Low | Low | Fall back to WMI; if still denied, show the profile as "Running (cannot inspect)" and do not try to host it | Unit: finder with denied access fake |

## Open questions

| # | Question | Needed by | Proposed answer |
| --- | --- | --- | --- |
| Q1 | `DWMWA_CLOAK` is refused across processes (spike self-test). Does `ShowWindow(SW_HIDE)` on a Chromium window pause its compositor, and does showing it again flash or reload? | Spike row 23 | Measure GPU time with 4 hidden windows; if show flashes, keep the window visible but moved behind the host instead of hidden |
| Q2 | Does Electron re-apply `WS_CAPTION` after `WM_DPICHANGED` or maximise? | Spike row 10, 11 | If yes, re-strip in the host's `EVENT_OBJECT_LOCATIONCHANGE` handler |
| Q3 | Can a Store-build Claude be launched with `--user-data-dir` through `shell:AppsFolder` with arguments, so that *all* profiles keep package identity? | Spike | Both switchers say no (they launch the exe directly for profiles); test once with `IApplicationActivationManager::ActivateApplication` and arguments; if it works, prefer it for Store builds |
| Q4 | What exit code does Claude use when the user quits for real from the tray menu? | Integration against real Claude | Needed for the clean-close classification; record in `ClaudeFacts.cs` |
| Q5 | Is the 20 s Starting timeout enough on a cold HDD or first launch after an update? | Manual | Make it a setting if it is not |
| Q6 | Mica in WPF: showing the backdrop needs the glass frame extended into the client area, and DWM then paints its own caption buttons under the custom title bar (seen in the spike). | Shell build | Try `DwmExtendFrameIntoClientArea` with a top margin only plus `WM_NCHITTEST` returning `HTMAXBUTTON` etc. so the DWM buttons are the real ones; or ship the flat token colour (the design's "Plain" backdrop) and revisit |
