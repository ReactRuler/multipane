# Running the hosting spike

Throw-away tool for `docs/tech/02-hosting-spike-plan.md`. It hosts foreign windows in two panes with approach A (reparent) or B (owned top-level window) and logs every Win32 step.

Build (needs the .NET 8 SDK or newer with the Windows Desktop workload):

```bash
dotnet build spikes/hosting/HostingSpike -c Debug
```

## Automated self-test (safe: uses two dummy windows of its own, never Claude)

```bash
spikes/hosting/HostingSpike/bin/Debug/net8.0-windows/Multipane.exe --selftest
```

Writes `selftest-<timestamp>.txt` next to the exe and exits 0 when every check passes. It covers attach, styles, placement after resize and move, hide and show, foreground, minimise and restore, release, and that the targets survive losing their owner.

## Demo mode (safe: dummy windows, the shell from the design)

```bash
spikes/hosting/dist/Multipane.exe --demo grid
```

The Home button at the top left (the app tile, also Ctrl+H) opens the home page over the panes at any time; Back to panes or Escape returns. Started with no arguments, the shell shows the home page, which is also the profile manager: every profile found in both switcher layouts plus the original account, each with Open here (running, moves the window into a pane), Show window (running but hidden to the tray: reveals the hidden window, or relaunches the profile so the running instance shows itself), Start, Kill (asks first), and a menu with Rename, Change colour, Open data folder and Delete (typed-name confirmation, Recycle Bin, never the original or a running profile). Add account creates an empty `ClaudeProfile-Account N` folder and opens Claude at the sign-in screen. Names and colours are stored in `%LOCALAPPDATA%\Multipane\profiles.json`; the old switcher's labels and colours are read as defaults and never written. With `--demo` it opens with three dummy windows hosted as "Personal", "Business" and "Studio" plus an empty "Lab" pane. `--demo tabs|columns|rows|grid|focus` picks the starting arrangement. The shell follows the design canvas: 40 px title bar with the mode switch (Ctrl+1…5), tabs in Tabs mode, pane headers with stripe, badge, state glyph and memory, pop out and dock back, hide from layout (Ctrl+Shift+H) and show all (Ctrl+Shift+A), maximise a pane (Ctrl+Shift+M, Escape restores), Ctrl+Tab between panes, Ctrl+K for the window picker, light or dark from Windows, or your own choice under the gear menu › Appearance (Follow Windows, Light, Dark; saved in `%LOCALAPPDATA%\Multipane\settings.json`); flat token colour, not Mica: see Q6 in docs/tech/12. Screenshots in `screenshots/`. The gear menu's "How windows are hosted" items are the spike plan's approach B (window kept on top of its pane, recommended) and approach A (window embedded in the pane). Restart is not in the spike yet; Kill is, behind the confirmation dialog, on the home page and in a pane's menu. In Focus mode, clicking a strip pane's header makes it the large pane.

## Manual run against real Claude (the matrix rows)

Only when no Claude instance you care about is running in the panes you pick. Hosting changes a window's frame styles until Release; the spike restores them on Release all and on close, but if the spike is killed from Task Manager the styles stay stripped until that Claude instance is restarted.

1. Start `Multipane.exe` with no arguments.
2. Open "Shown" (or Ctrl+K): it lists Claude instances found by command line (profile folder name, pid, window title) and other top-level windows filtered by title.
3. Choose approach A or B and the tool-window option in the gear menu, then double-click an instance to host it in a new pane.
4. Work through the matrix in `docs/tech/02-hosting-spike-plan.md` and record each row in `RESULTS.md`. The log at the bottom and `spike-<timestamp>.log` next to the exe hold the style, parent and owner values at every step.
5. "Release all" before closing. The dummy window (`Multipane.exe --dummy Name`) has buttons for a popup, a file dialog, a 5-second hang and an exit with code 5, for rows that do not need Chromium.

Launch of a profile from the spike uses the same command line as the switchers (`Claude.exe --user-data-dir="<folder>"`, or `explorer.exe shell:AppsFolder\<AUMID>` for the original account on the Store build).
