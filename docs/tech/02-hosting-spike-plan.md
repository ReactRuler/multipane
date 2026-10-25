# 02 · Window hosting: spike plan, test matrix, recommendation

Hosting another process's windows is the main technical risk. Nothing else is built until this spike has a recorded result for both Claude builds.

## The two approaches

### A. Reparenting

`SetParent(claudeHwnd, hostSurfaceHwnd)`, strip `WS_CAPTION | WS_THICKFRAME | WS_SYSMENU`, add `WS_CHILD`, size to the pane with `SetWindowPos`.

Known costs, from Microsoft's own guidance and from Chromium's window code:

- Cross-process parent/child links tie the two threads' input queues together (`AttachThreadInput` semantics). A hang in either side stalls the other, and keyboard focus rules become hard to reason about.
- Chromium's main window (`Chrome_WidgetWin_1`) creates popup windows (menus, tooltips, file pickers) as owned top-level windows positioned in screen coordinates; after reparenting it still computes those positions correctly, but IME candidate windows and drag-and-drop sometimes do not.
- DPI: a child window inherits the parent's DPI context. If Claude's process is per-monitor-v2 aware (Electron is) and the host is too, this is fine; mixed awareness produces blurry or mis-scaled content.
- Electron re-applies some styles on its own events (maximise, restore, DPI change), which can bring the caption back.

### B. Managed placement (owned top-level windows)

Claude's main window stays top-level. The host sets itself as the window's *owner* (`SetWindowLongPtr(GWLP_HWNDPARENT)`), removes the caption and sizing frame styles, and positions the window over its pane with one `DeferWindowPos` batch per frame. When a pane is hidden, maximised away, or the host is minimised, the window is hidden with `ShowWindow(SW_HIDE)` and shown again with `SW_SHOWNA`. (The plan first said DWM cloaking; the spike's self-test showed `DWMWA_CLOAK` is refused across processes, so hide/show it is. Whether Electron reacts visibly to `WM_SHOWWINDOW` is matrix row 23.)

Properties of owner links that this relies on: owned windows stay above their owner in z-order, minimise and restore with it, never get their own taskbar button, and are destroyed if the owner is destroyed. The last one is a hazard: the host must clear the owner link (`GWLP_HWNDPARENT = 0`) before it exits, and the Session must outlive the host regardless. A crash of the host that skips cleanup leaves the owner link pointing at a dead HWND; Windows tolerates that (the window simply becomes unowned when the handle is reused), and the spike verifies it.

Known costs:

- The host must track every rectangle change itself: move, resize, DPI change, monitor change, pane drag. One batch per frame keeps it at refresh rate.
- Alt+Tab shows each owned window as its own entry unless it gets `WS_EX_TOOLWINDOW`, which also removes it from the taskbar (already the case for owned windows). Spike checks whether tool-window style breaks anything in Electron (notifications, focus restore).
- Claude's own popups are owned by Claude's main window and need no handling, but their z-order relative to *other* panes must be checked.

### Recommendation before the spike

**B, managed placement, is the primary candidate; A is the fallback.** B keeps both processes' input queues independent, keeps Claude's DPI handling intact, and makes "fall back to a normal window" trivial: clear the owner, restore the styles, done. The spike exists to prove the z-order, focus and hide/show behaviour, which are B's weak points. If B fails on more than one matrix row that A passes, the recommendation flips.

Either way, each session falls back on its own: a session whose hosting fails stays a normal window with the "in its own window" stub in its pane (design, artboard 17).

## Spike: what is built

A throw-away console/WPF app under `spikes/hosting/`, not shipped, about 400 lines:

1. Launch or find a Claude instance (installer and Store build, by `--user-data-dir`).
2. Host its main window with approach A or B (`--mode A|B`), inside a two-pane test window.
3. Drive the matrix below by hand, record results in `spikes/hosting/RESULTS.md`.

The same harness is run against the dummy app from the test plan (doc 10) so that regressions can be caught in CI later, but the spike itself runs against real Claude, because the failures of interest are Electron's.

## Test matrix

Each row is run on **Windows 11 24H2 and Windows 10 22H2**, with the **Store build and the installer build**, for **A and B**. Result codes: `pass`, `pass-with-note`, `fail`. A row is complete when all four build/OS cells are recorded.

| # | Case | What to check | Pass means |
| --- | --- | --- | --- |
| 1 | Typing | Type into Claude's input, including Shift, Ctrl+V, AltGr characters, dead keys | Every character lands; no dropped or doubled keys |
| 2 | IME | Windows IME (Japanese) and the emoji panel (Win+.) | Candidate window appears next to the caret, composition commits |
| 3 | Clipboard | Copy from Claude, paste into Claude, both directions with Notepad | Works unchanged |
| 4 | Drag and drop | Drop a file from Explorer onto Claude's chat | Claude accepts it; cursor feedback correct |
| 5 | Focus between panes | Click pane 1, type; click pane 2, type; Ctrl+Tab | Keystrokes go to the pane that was clicked; caret visible in exactly one |
| 6 | Claude popups | Model menu, right-click menu, tooltips, settings dialog | Appear at the right place, above the right pane, close on outside click |
| 7 | File pickers | Attach a file (common dialog) | Dialog is modal to the right Claude window, host stays responsive |
| 8 | Notifications | Trigger a Claude notification | Appears; clicking it focuses the right pane |
| 9 | Minimise and restore | Minimise host from taskbar; restore | Claude windows vanish and return with it; no flash of unhosted windows |
| 10 | Maximise | Maximise and un-maximise host; snap with Win+Left | Panes re-lay out; no caption reappears |
| 11 | Per-monitor DPI | Move host between 100% and 150% monitors; change scaling live | Claude content sharp; sizes correct after 1 frame |
| 12 | GPU artefacts | Resize fast for 10 s; switch modes rapidly | No black frames, no tearing, no stuck frames |
| 13 | Sleep and resume | Sleep the PC 1 minute with 4 hosted sessions | All still hosted and responsive |
| 14 | Claude update | Let Claude update while hosted (installer build); relaunch | New exe path found; sessions re-hosted after Claude's own restart |
| 15 | Host exit | Close host with 4 sessions running | Sessions keep running as normal windows with their frames back |
| 16 | Host crash | Kill host from Task Manager | Same as 15; on next start all four are re-adopted and re-hosted |
| 17 | Claude exit from inside | Quit from Claude's menu (hides to tray on current builds) | Host shows the right state (Not running vs still running hidden) |
| 18 | Pop out and dock back | 20 cycles | No leaked styles, no focus loss |
| 19 | Hung window | Suspend Claude's process 10 s (`pssuspend`) | Host stays responsive; "Not responding" shown; recovers |
| 20 | Alt+Tab and taskbar | Alt+Tab list, taskbar buttons | One host entry; hosted Claude windows not listed twice |
| 21 | Second instance | Launch the same profile again while hosted | Electron hands off to the running instance; nothing duplicates |
| 22 | Memory readout | Compare host's figure with Task Manager's private bytes for the tree | Within 5% |
| 23 | Hide and show | Hide a pane (ShowWindow) for a minute, show it again; also during a mode switch | No reload, no white flash, rendering resumes at once |

Rows 1 to 8 and 11 are the ones most likely to separate A from B.

## How results are recorded

`spikes/hosting/RESULTS.md`, one table per approach, one row per case, cells `OS × build`, each cell `pass | note | fail` plus a short note and, for failures, the Win32 error or a one-line description. The commit that records the final table also records the decision in doc 12 and this document's "Recommendation" section is updated to "Decided: A|B on <date>".

## Exit criteria

- B (or A) passes rows 1, 3, 5, 6, 7, 9, 11, 15 and 16 on all four cells with at most notes.
- Any `fail` on a row has a written fallback: either per-session fallback to a normal window, or a documented limitation shown in the UI.
- The host's per-frame placement work stays under 1 ms for 6 panes (measured with `Stopwatch` around the `DeferWindowPos` batch).
