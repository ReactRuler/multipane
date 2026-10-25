# Design package (phase 1, package 1)

Working name in the mockups: **Multipane** (see "Decisions for the owner", item 1).
Visual screens: the design canvas at https://claude.ai/artifact/SfetcCPEa7BdizHjQQc8iv (24 artboards, each screen in light and dark).
This file holds the written parts: rationale, tokens, interaction notes, shortcut map, icon notes, and the decisions the owner has to make.

## 1. Design rationale

The app is a frame around four or more Claude Desktop windows that run for hours. Its job is to make those windows easy to see, switch, arrange and recover, and then to get out of the way. Three ideas follow from that.

**Thin chrome, one row.** The whole host is a 40 px title bar and 30 px pane headers. In Tabs mode the tabs sit inside the title bar, like a browser, so switching modes never adds a second toolbar row. Panes have a 1 px stroke and a 3 px colour stripe on top, nothing else. Every pixel below the header belongs to Claude.

**State you can read without looking closely.** Each profile has a colour and a two-letter badge, and each state has a shape as well as a colour: filled dot (Running), ring with a dot (Busy), dashed ring (Starting), triangle (Crashed), square (Killed), hollow ring (Not running), clock (Not responding), slashed ring (Auto-restart stopped). The same glyphs appear on tabs, pane headers, the profile list, the tray flyout and the command palette, so one glance tells you what is going on in any of them. Colour-blind users get the same information from the shape and the word.

**Nothing disappears, nothing is hidden from you.** A session that crashes, is killed, or was closed from inside Claude stays in its slot as a placeholder that says what happened, when, and what it means in plain words ("Lab crashed at 02:11. Your conversations are kept. Work that was running inside this session stopped."). The host writes nothing into profile folders, says so on the first-run screen, and the About page links to "What this app touches".

The look is Windows 11: Mica window, Segoe UI Variable, Fluent-style stroke icons, system accent for focus and primary buttons, 4/6/8 px radii, proper light and dark. It is deliberately not Claude's look: no coral, no serif, no Anthropic marks. An original icon (four panes, one active) uses slate, warm paper and teal, which sit next to Claude's warm tones without borrowing them.

## 2. Tokens (v2, 2026-10-25)

Version 2 replaced the Windows-grey palette with a cool, slightly tinted ground, a single indigo accent with a violet partner for the app mark, dark mode as a first-class theme, pill-shaped controls and softer cards. Checked with a WCAG contrast script; every text token and every profile colour is 4.5:1 or better against the surfaces it is used on.

### Surfaces and text

| Token | Light | Dark | Use |
| --- | --- | --- | --- |
| mica | #F3F4F8 | #111317 | window background (flat colour; Mica is open question Q6) |
| surface | #FFFFFF | #1A1D24 | panes, cards, menus, dialogs |
| surface2 | #F6F7FA | #15181E | placeholder area, dialog footers, list boxes |
| text | #15171C | #F2F3F7 | primary text |
| text2 | #555C6B | #AEB4C2 | secondary text (6.1:1 on light mica, 8.1:1 on dark surface) |
| text3 | #666D7C | #8E95A5 | tertiary text, paths, hints (4.7:1 on light mica, 5.6:1 on dark surface) |
| stroke / stroke2 | 8% / 16% of #141A2E | 8% / 14% white | borders, dividers |
| hover / press | 5% / 9% of #141A2E | 6% / 3% white | control states |
| accent | #4A56D2 | #8E97FF | focus, selection, primary button, active segment icon |
| accent2 | #7B5BE8 | #9C7CFF | second stop of the app-mark gradient only |
| accent-soft | accent at 18% | accent at 22% | focus glow around the active pane |
| accent-text | #FFFFFF | #0E1030 | text on accent |

### Shapes

Title bar 40 px. Mode switch: 26 px pills in a 30 px pill track, centred on the window. Tabs: 28 px pills, active one on the surface colour with a 1 px stroke. Panes: 10 px radius, 1 px stroke, 3 px colour stripe. Buttons 8 px radius. Menus 10 px, dialogs and toasts 12 px, window 12 px. Status chips: glyph and word on a 12% tint of the status colour, 11 px semibold. App mark: the four-pane glyph in white on a 135° accent → accent2 gradient tile (20 px in the title bar, 72 px on the home page).

### Status

| Token | Light | Dark | Glyph |
| --- | --- | --- | --- |
| ok | #1A7F45 | #5BD38B | Running: filled dot |
| busy | #4A56D2 | #8E97FF | Busy: ring with dot |
| warn | #945C00 | #F2C14E | Starting: dashed ring. Not responding: clock. Memory warnings |
| err | #C2332A | #FF8B82 | Crashed: triangle. Auto-restart stopped: slashed ring. Kill button |
| neutral (text3) | #666D7C | #8E95A5 | Killed: square. Not running: hollow ring |

### Profile palette (10)

Each colour has a light-theme shade (white initials) and a dark-theme shade (near-black initials). The old switchers' eight colour indexes map onto the first eight so imported profiles keep their colours.

| Name | Light | Dark | Old index |
| --- | --- | --- | --- |
| Blue | #2460C4 | #7FB2FF | 0 |
| Olive | #5B7A12 | #A9C85A | 1 |
| Plum | #8A3E85 | #D58BD0 | 2 |
| Rose | #B4325F | #F28BB0 | 3 |
| Teal | #0F7A7A | #4FC3C3 | 4 |
| Indigo | #5146C8 | #A39BFF | 5 |
| Amber | #8A5A00 | #E8B04B | 6 |
| Slate | #4E5E6E | #9FB0C0 | 7 |
| Coral | #B8472F | #F08A72 | new |
| Brown | #7A4E2E | #C79A74 | new |

New profiles get the least-used colour, as the old switchers do.

### Type (Segoe UI Variable, Fluent scale)

Caption 12/16 (headers, status, paths). Body 14/20 (menus, lists, dialogs, settings). Body strong 14/20 semibold (profile names). Subtitle 16/22 semibold (placeholder titles). Title 20/28 semibold (dialog titles). Display 28/36 semibold (first run).

### Spacing, radii, elevation

4 px grid: 4, 8, 12, 16, 24, 32. Pane gap 6. Pane header 30. Title bar 40. List row 48. Dialog padding 24.
Radii: 4 (controls), 6 (panes, tabs), 8 (window, menus, dialogs), full (badges).
Elevation: 0 = 1 px stroke only (panes); 1 = small shadow (tooltip, raised segment); 2 = menus, flyouts, dialogs.

### Motion

All under 200 ms, none when Windows "animation effects" is off.

| What | Duration | Easing |
| --- | --- | --- |
| Hover, press, focus | 83 ms | linear |
| Mode or arrangement switch | 150 ms | panes move and resize, ease-out; no fade |
| Menu, flyout, palette | 120 ms open, 83 ms close | fade + 8 px slide |
| Placeholder after a crash | 150 ms | fade in, pane does not move |
| Drag settle | 150 ms | ease-out to slot |
| Starting ring | 1.2 s per turn | linear; static with animations off |

## 3. Screens and states

All on the canvas, numbered 00–24. 00 is the home page shown while no pane is open: the app mark, one line of explanation, three actions (Open a running profile, Start a profile, Add account) and the profile list with a per-row action. Main window: Tabs (1920×1080 and 1366×768), Columns, Rows, Grid (4, 3 and 6 sessions, the last at 2560×1440), Focus. Controls: mode switch with hover, focus and tooltip, Shown profiles menu, pane header in every state with its menu, six placeholders, kill confirmation, memory warning (toast and banner), not responding bar. Profiles: manager window with context menu, Add account, Rename, Change colour, Delete, Add shortcuts, Transfer chats. Settings. Tray menu and flyout. First run. Three error states. Popped-out window. Command palette. Tokens, components, icons, shortcuts, interaction notes.

Copy rules used throughout: specific and calm, no exclamation marks, no emoji, time of the event in the title, what it means in the body, one primary action.

## 4. Interaction notes

- **Drag to reorder.** Press and move a header 8 px: the pane lifts (elevation 1, 85% opacity) and follows; others slide into the free slot as the pointer crosses their midpoint; release settles in 150 ms; Escape cancels. The hosted window is hidden during the drag so it never tears.
- **Resize.** Dividers are 6 px gaps with a 10 px hit area and show a 2 px accent line while dragged. Live at refresh rate, one DeferWindowPos batch per frame. Minimum pane 320×240. Double-click a divider to equalise. Sizes are stored as fractions per arrangement.
- **Maximise.** Double-click the header or Ctrl+Shift+M; Escape restores. Others stay in the layout underneath.
- **Pop out and dock back.** The Claude window is detached into a thin frame with the same header; the slot stays as a stub. Dock back from the stub, the pop-out's button, Ctrl+Shift+O, or the pop-out's close button (which never kills). Position and size remembered per profile.
- **Mode switch.** One click or Ctrl+1…5; panes move and resize in 150 ms, nothing reloads, focus stays put. Tabs → arrangement restores last order; arrangement → Tabs makes the focused pane the active tab.
- **Hide and show.** Hide hides the window and keeps the process; the title bar counts "n hidden". Show returns it to its last slot.
- **Focus.** 1 px accent outline on the focused pane (2 px from keyboard). F6 cycles title bar → pane headers → hosted window.
- **Crash and recovery.** Placeholder appears in place within 150 ms. Reopen starts in place; Starting counts seconds. Auto-restart shows countdown with Restart now and Stop; after the limit the placeholder explains and waits.
- **Host restart.** Quitting leaves sessions running (the menu says so). Next start re-adopts them by command line into the saved layout.
- **Reduce motion and scaling.** Durations become 0 with animations off. All sizes in DIPs; at 200% header buttons are still 28×26 logical, everything else 32+.

## 5. Keyboard shortcut map

Global: Ctrl+Alt+C bring forward; Ctrl+Alt+1…9 switch to profile N (start if needed).
Modes: Ctrl+1…5 Tabs, Columns, Rows, Grid, Focus. Ctrl+Tab / Ctrl+Shift+Tab next/previous session. Alt+1…9 focus session N.
Panes: Ctrl+Shift+M maximise; Ctrl+Shift+O pop out / dock back; Ctrl+Shift+H hide (or open Shown menu); Ctrl+Shift+A show all; Ctrl+Shift+arrows move pane; Ctrl+Alt+arrows resize (Shift ×4); Ctrl+Shift+0 equalise; F6 cycle focus regions.
Lifecycle: Ctrl+Shift+S start/reopen; Ctrl+Shift+R restart; Ctrl+Shift+K kill; Ctrl+Shift+Alt+R restart all; Ctrl+Shift+Alt+S start selected.
App: Ctrl+K palette; Ctrl+P profiles; Ctrl+N add account; Ctrl+, settings; F2 rename; Del delete (in Profiles); Shift+F10 context menu; Esc close/restore; Ctrl+Shift+Q quit keeping sessions.
Keys reach Claude when a hosted window has focus, except the global ones and Ctrl+Tab, Ctrl+1…5, Alt+1…9 (see decision 6).

## 6. Icons

`docs/design/icons/app-icon.svg` (256 master), `app-icon-16.svg` (pixel-aligned variant for 16 and 20), `tray-mono.svg` (monochrome tray glyph, currentColor). Ship 16, 20, 24, 32, 48, 256 (plus 40 and 64 for 250% scaling) in the .ico, rendered at build time. Tray icon in white and black at 16, 20, 24, 32, chosen from the taskbar theme; the attention dot is drawn by the app. Per-profile shortcut icons: the app mark with the profile badge over the bottom-right pane, one file per profile so Explorer's icon cache stays correct.

## 7. What was kept from the current switchers

Read in full: PriyanshuGeTRekT/Claude-Code-Desktop-Switcher (PowerShell 2.0, MIT) and Piwqust/claude-switcher (C++, MIT, keeps both copyright lines). Kept: the profile list columns (Profile, Status, Last used, data folder) and status bar text; buttons Add account, Rename, Add shortcuts, Transfer chats, Delete; right-click menu; "Keep running in the tray"; Start with Windows via the per-user Run key; Ctrl+Alt+C and Ctrl+Alt+1…9; tray menu with every profile plus Restart/Kill submenus and flyout rows with restart/close; English and Russian; the 8-colour palette as indexes 0–7; Delete wording ("signs the account out on this PC, profile folder, saved sign-in and shortcuts are deleted, other profiles stay"), Delete disabled for the original and refused while running; Transfer chats behaviour and its note ("the chat stays here too; both copies continue the same history"); shortcuts that go through the app rather than Claude.exe so they survive updates; name rules (no path characters, ≤40 chars, unique, not "Add account"). Added on top: strong Delete confirmation by typing the name; both folder layouts (`ClaudeProfile-<id>` from PowerShell 2.0 and `ClaudeProfiles\<id>` from the C++ build) shown as found.

## 8. Decisions for the owner

1. **Name.** Mockups say "Multipane". Alternatives: "Panes" (plainest), "Tessera" (tiles). All three avoid any Anthropic association. Pick one or send another; it is a string change.
2. **Pane header in Tabs mode.** Kept (30 px) so Kill, Restart, Pop out and the memory readout are always in the same place. Alternative: hide it in Tabs mode and put those actions in the tab's menu, saving 30 px.
3. **Profile colours.** Ten instead of eight, with a separate dark-theme shade per colour (the old single shades fail contrast on one theme or the other). Imported profiles keep their index. Fine to cut back to eight if you prefer exact parity.
4. **Kill without a dialog when idle.** Busy sessions get the confirmation; idle ones are killed at once because the placeholder with Reopen is the undo. Flip to "always ask" if you want.
5. **"Last used" source.** The old switchers derive it from file timestamps inside the profile folder. To honour "never read inside a Claude data folder" strictly, the design shows the app's own record (last start or adoption time) and "—" before the first run. If reading timestamps (not contents) is acceptable to you, parity is exact.
6. **Shortcut capture.** Ctrl+Tab, Ctrl+1…5 and Alt+1…9 are taken by the host even while a Claude window has focus. Claude does not use them today; if a future Claude does, you lose those inside Claude. Alternative: only Ctrl+Alt+… combinations, which are safer but slower.
7. **Sign-in routing** (PowerShell switcher only: claude:// links go to the profile that is signing in). Kept as an off-by-default toggle in Settings › Tray. It registers a URL handler in HKCU, which must be listed under "What this app touches". Drop it if you never used it.
8. **Usage meters** (C++ switcher shows 5 h / 7 d usage from files inside the profile). Left out: they require reading `plan-usage-history.json` and `config.json` inside the profile folder, which the trust rules forbid. Could return later as an explicit opt-in.
9. **Memory threshold defaults.** 85% of physical memory total, 6 GB per session, sampled every 3 s. Tune in Settings.
10. **Auto-restart defaults.** Off; 5 s delay, doubling, stop after 5 crashes in 10 minutes.
11. **Quit semantics.** Two menu items: "Quit, keep sessions running" (default, Ctrl+Shift+Q) and "Quit and kill all sessions". The window's close button hides to the tray when that setting is on, otherwise it is the default quit.
12. **Transfer chats.** Kept exactly as the switchers do it (copies `local_<id>.json` session index files into the target profile's `claude-code-sessions` folder, rewriting only the account and org ids). It is the one documented write inside a profile folder and the dialog says so.

## 9. Not verified yet

- Mica/acrylic over a reparented Electron window and DPI changes mid-drag are assumptions to be proven in the hosting spike. Hiding during drags uses ShowWindow (DWM cloaking is refused across processes, spike finding).
- "Busy" detection is a heuristic (CPU over 15% for 10 s or active helper processes); the design never blocks on it.
- Segoe UI Variable is assumed present on Windows 10 as a fallback to Segoe UI; both are listed in the font stack.
