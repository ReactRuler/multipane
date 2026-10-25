# Changelog

## v0.1.0-preview.1 (2026-10-25)

First downloadable preview, built by the release workflow from the tagged source. The hosting spike with the product shell on top: not the final product build, no code signing, Restart not in yet. Checksums and build provenance attached to the release.

## Unreleased

- Hosting spike with the product shell: Tabs, Columns, Rows, Grid and Focus; pop out and dock back; hide and show; pane headers with state, memory and actions; home page as profile manager (open here, start, kill, add account, rename, colour, delete); light and dark; app icon.
- Findings recorded in `spikes/hosting/RESULTS.md`: DWM cloaking is refused across processes; maximised windows must be restored before hosting; Chromium enforces its own bounds and the host re-places with `SWP_NOSENDCHANGING`; hosted windows that resize themselves are snapped back.
- Design package (canvas and `docs/design/DESIGN.md`) and technical package (`docs/tech`, `docs/trust`).
