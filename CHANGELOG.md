# Changelog

## v0.1.0-preview.2 (2026-10-25)

- Fixed: switching to Tabs crashed the app.
- Fixed: a maximised window ran under the taskbar.
- Fixed: profile names and colours from the Claude Profile Switcher were not read.
- New: the last session is restored at start; running profiles are docked again, hidden windows revealed, others wait with a Start button that docks the window when it appears.
- New: Open all running here on the home page; Start into a pane; F11 full screen; Home button; Appearance (Follow Windows, Light, Dark) in the gear menu.
- Checksum file now uses LF line endings.

## v0.1.0-preview.1 (2026-10-25)

First downloadable preview, built by the release workflow from the tagged source. The hosting spike with the product shell on top: not the final product build, no code signing, Restart not in yet. Checksums and build provenance attached to the release.

## Unreleased

- Hosting spike with the product shell: Tabs, Columns, Rows, Grid and Focus; pop out and dock back; hide and show; pane headers with state, memory and actions; home page as profile manager (open here, start, kill, add account, rename, colour, delete); light and dark; app icon.
- Findings recorded in `spikes/hosting/RESULTS.md`: DWM cloaking is refused across processes; maximised windows must be restored before hosting; Chromium enforces its own bounds and the host re-places with `SWP_NOSENDCHANGING`; hosted windows that resize themselves are snapped back.
- Design package (canvas and `docs/design/DESIGN.md`) and technical package (`docs/tech`, `docs/trust`).
