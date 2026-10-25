# Hosting spike results

Machine: Windows 11 24H2 (build 26300), 150 % scaling, Claude Desktop Store build 3.6556.2.0. Spike built with the .NET 8 SDK (the only SDK on the machine; the product's .NET 10 choice stands, the SDK needs installing before phase 2 proper).

## Automated self-test (dummy windows, 2026-10-25)

`Multipane.exe --selftest`, 41 of 41 checks pass (24 hosting checks, one of them informational, 2 snap-back checks, 2 for the maximised host staying inside the work area, 2 for the Tabs switch, 3 for a maximised size-clamping window, 5 for main-window identification and recovery of a frameless one, 3 layout-engine shape checks), log in `selftest-latest.txt`. The spike's shell now follows the design canvas (see `RUN.md`, demo mode), so the matrix can be run in the real chrome. Both approaches attach, strip the frame, follow resize and move to the pixel, hide and show, release cleanly with the original styles, owner and rectangle, and the target processes survive, including losing their owner handle (the host-crash case for B).

### Findings that change the plan

1. **`DWMWA_CLOAK` is refused across processes** (`DwmSetWindowAttribute` returns 0x80070005). Cloaking was the planned way to hide a pane's window under approach B without a visibility change. It is not available to us. Hiding uses `ShowWindow(SW_HIDE)` / `SW_SHOWNA` instead, which works for both approaches. Electron will see `WM_SHOWWINDOW`; whether that pauses rendering (good) or causes anything visible (to be checked) is now matrix row 23.
2. **Placement must follow the native pane window, not the WPF size event.** `HwndHost.OnWindowPositionChanged` is the right hook; `SizeChanged` reads the rectangle one layout pass too early.
3. **Placement must never re-show a hidden window and must be skipped while the host is minimised.** Any layout pass triggers placement; `SWP_SHOWWINDOW` in that path re-showed hidden windows and showed an owned window during the minimise animation. Fixed by a hidden flag and a last-rectangle cache; the product's `WindowHost` carries both.
4. Owned windows (B) do hide and restore with a minimised owner, but the host hides them explicitly anyway, so the behaviour does not depend on timing.
5. `WS_EX_TOOLWINDOW` on an owned window applies cleanly and the window can still take the foreground.
6. **A pane's native host window exists only after a layout pass.** Attaching with approach A in the same instant the pane is created reparents the target to the desktop. The product's `WindowHost` must wait for the pane's HWND (the spike forces a layout pass first).
7. **An `ItemsControl` must never be given the pane controls as items**: WPF then hosts the control directly and parents it twice. Tabs bind to a wrapper object.
8. **A real Claude window did not take the pane's rectangle** (owner's own trial, 2026-10-25): it sat at its restore bounds inside the pane, frameless. Two causes, both now handled and covered by the self-test's "Stubborn" dummy: a maximised window keeps `WS_MAXIMIZE` after the style change and ignores placement, so the host restores it first (`SW_RESTORE`); and an app that clamps its size in `WM_WINDOWPOSCHANGING` (Chromium enforces its own bounds) is placed again with `SWP_NOSENDCHANGING`, which skips that message. The log line "placement refused by … retry … worked" records which case occurred. Confirmed by the owner's second trial: Account 3 filled its pane.
12. **A crashed host leaves main windows frameless and owned by a dead handle**, and the stricter rule from finding 11 then ignored them (owner's report: only "Show window", and Open all did nothing). A large frameless window (500×300 px or more) is accepted as a main window left behind, a ghost owner is ignored, and when such a window is hosted the host records a standard frame as its "before" state so Release gives it a real frame back. Self-test covers both. The product's crash handler releases windows first, so this is a recovery path, not the normal one.
11. **Claude Desktop has two `Chrome_WidgetWin_1` windows per instance**: the main window and the hidden quick-entry bar ("What can I help you with today?"). With the main windows already docked, the finder picked the hidden bars, revealed them and hosted them (owner's report). A main window is now one with `WS_THICKFRAME` and `WS_MAXIMIZEBOX`, or one the host already stripped; the largest candidate wins. The self-test checks a non-resizable look-alike is rejected and a hosted window still counts. This replaces the "first visible window" rule in docs/tech/03.
10. **Switching to Tabs crashed the shell** (owner's report): laying out Tabs activates the visible pane, the activation handler asked for another layout in the middle of the first, and the grid ended up with a pane parented twice. A re-entrancy guard around the layout fixed it; the self-test now switches to Tabs and back. Two side findings from the same log: both switchers write their `settings.json` with a UTF-8 byte-order mark that `JsonDocument` rejects, so their labels were never read until the mark was skipped; and attaching a window to a pane that has not been laid out yet forced it to 10×10 px for a frame, now skipped.
9. **A hosted Electron window can still be resized and moved by the user**: Electron draws its own frame and answers the hit-test for its edges itself, so removing `WS_THICKFRAME` changes nothing. The host now listens to `EVENT_OBJECT_LOCATIONCHANGE` for the hosted window (out-of-context WinEventHook, own process skipped) and snaps it back to the pane with `SWP_NOSENDCHANGING` whenever it differs. The log reports how often that happened on release. Covered by the self-test with a dummy that resizes itself.

#Flakiness note: with four busy Claude sessions on the machine, one run showed three timing misses (a dummy not found within 10 s, a snap-back not yet applied after 400 ms, an owned window still visible 500 ms into minimise); the rerun passed. The product's tests must wait on conditions rather than fixed delays.

## Matrix with the dummy windows

Rows that do not need Chromium, recorded from the self-test and a short manual pass with the dummy's own buttons: rows 5 (focus between panes), 9 (minimise and restore), 10 (maximise), 15 (host exit releases), 16 (host crash: targets survive), 18 (pop out / dock back = release and re-attach) pass for A and B. Row 19 (hung window via the dummy's "Hang 5 s") shows "NOT RESPONDING" in the spike header for both and the spike stays responsive.

## Matrix against real Claude

Not run yet. The owner has live Claude sessions on this machine and the spike must not touch them (hosting strips the frame until Release). To be run by the owner, or in a later session when a test profile can be spared, following `RUN.md`. Each cell: `pass`, `note` or `fail` plus a short reason.

| # | Case | Win11 Store A | Win11 Store B | Win11 Installer A | Win11 Installer B | Win10 Store A | Win10 Store B | Win10 Installer A | Win10 Installer B |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Typing | | | | | | | | |
| 2 | IME | | | | | | | | |
| 3 | Clipboard | | | | | | | | |
| 4 | Drag and drop | | | | | | | | |
| 5 | Focus between panes | | | | | | | | |
| 6 | Claude popups | | | | | | | | |
| 7 | File pickers | | | | | | | | |
| 8 | Notifications | | | | | | | | |
| 9 | Minimise and restore | | | | | | | | |
| 10 | Maximise and snap | | | | | | | | |
| 11 | Per-monitor DPI | | | | | | | | |
| 12 | GPU artefacts | | | | | | | | |
| 13 | Sleep and resume | | | | | | | | |
| 14 | Claude update | | | | | | | | |
| 15 | Host exit | | | | | | | | |
| 16 | Host crash | | | | | | | | |
| 17 | Claude quit from inside | | | | | | | | |
| 18 | Pop out and dock back | | | | | | | | |
| 19 | Hung window | | | | | | | | |
| 20 | Alt+Tab and taskbar | | | | | | | | |
| 21 | Second instance | | | | | | | | |
| 22 | Memory readout | | | | | | | | |
| 23 | Hide/show via ShowWindow: no reload, no flash, rendering resumes | | | | | | | | |

## Decision

Open until the real-Claude rows are filled. The mechanics of B hold with the hide/show substitute for cloaking, so the recommendation in `docs/tech/02` stands: B primary, A fallback.
