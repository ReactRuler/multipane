# Technical package (phase 1, package 2)

Twelve technical documents plus the trust documents, as the brief lists them. Nothing here is code; building starts in a later step after review.

| # | Document | Brief item |
| --- | --- | --- |
| 01 | [Architecture overview](01-architecture.md) | architecture, stack choice, component diagram, dependencies |
| 02 | [Window hosting spike plan](02-hosting-spike-plan.md) | approaches A and B, test matrix, recording, recommendation |
| 03 | [Process and window model](03-process-and-window-model.md) | finding Claude, launching, discovery, re-adoption, kill, hung, memory |
| 04 | [Session state machine](04-session-state-machine.md) | state diagram, restart policy, what is shown |
| 05 | [Layout engine](05-layout-engine.md) | Tabs, Columns, Rows, Grid, Focus; save and restore |
| 06 | [Profile compatibility](06-profile-compatibility.md) | both switchers, both folder layouts, Store vs installer |
| 07 | [Settings and layout schemas](07-settings-and-layout-schemas.md) | the three JSON files, versions, migrations |
| 08 | [Errors and logging](08-errors-and-logging.md) | handling, log location, rotation, what is never logged |
| 09 | [Repository and coding standards](09-repository-and-coding-standards.md) | layout, rules, review order for trust |
| 10 | [Test plan](10-test-plan.md) | unit, dummy app, integration, manual checklist |
| 11 | [Build and release](11-build-and-release.md) | CI, tagged releases, checksums, provenance, reproducibility, signing |
| 12 | [Risks and open questions](12-risks-and-open-questions.md) | each with a mitigation |
| | [SECURITY.md draft](../trust/SECURITY.md) | trust |
| | [What this app touches](../trust/what-this-app-touches.md) | trust |
| | [Licence and attribution](../trust/licence.md) | trust |

Research the documents rest on: [switcher-research.md](../research/switcher-research.md). Design: [DESIGN.md](../design/DESIGN.md) and the canvas linked there.

## Decisions for the owner

The design's twelve decisions were accepted as proposed on 2026-10-25 (name "Multipane" stays the working name). These are the new ones.

| # | Decision | Proposed | Alternative |
| --- | --- | --- | --- |
| D1 | Stack | C# / .NET 10 LTS / WPF (doc 01) | Native C++/Win32 if the 60 MB memory ceiling is a hard requirement rather than a target |
| D2 | Default download | Framework-dependent build (3 MB, needs the .NET Desktop Runtime, which the installer links to but never downloads) | Self-contained (about 70 MB, no prerequisite) |
| D3 | Code signing | Azure Trusted Signing via CI with OIDC, small monthly fee | SignPath Foundation if eligible; or unsigned with checksums and provenance, stated in the README |
| D4 | Hosting approach | B, managed placement with owner link and hide/show (cloaking is refused across processes, spike finding), as the primary candidate; A as fallback | Let the spike decide with no preference |
| D5 | Installer | Inno Setup per-user installer plus a portable zip | MSIX (sandbox and signing complications), or zip only |
| D6 | Re-import of names changed later in the old switcher | Not automatic; an "Import from the old switcher again" button in Profiles | Automatic on every start, old switcher wins |
| D7 | Delete sends the folder to the Recycle Bin | Yes (`FOF_ALLOWUNDO`); the switchers delete permanently | Permanent, like the switchers |
| D8 | Per-profile taskbar identity (setting each Claude window's AppUserModelID from outside, as both switchers do) | Later, not in version 1 | Include in version 1 |
| D9 | ARM64 verification | CI builds and tests on an ARM64 runner; manual checks need an ARM64 machine or VM the owner provides | Ship ARM64 as "built, not hand-tested" with a note |
| D10 | Dummy app window class | Uses the real `Chrome_WidgetWin_1` class name so the finder is tested as-is | A test-only class name plus an injectable constant |
| D11 | Verbose log toggle | In Settings › About, resets at restart | Command-line only |

## Risks

Full table with mitigations in doc 12. The ones that could change the plan:

- **R1** Hosting Electron windows may fail on focus, IME, popups or DPI for both approaches. The spike runs first and every session can fall back to a normal window.
- **R4** WPF may not meet the 60 MB host memory target. Measured in the 8-hour check; Native and Core are separable so a C++ shell remains possible.
- **R3** Chromium's window class or helper switch could change with an Electron update. Both constants live in one file with a loud failure path.
- **R13** Unsigned builds trigger SmartScreen until D3 is settled.

## What is assumed

- The owner's PC is x64 with Windows 11; Windows 10 and ARM64 checks happen on other machines or VMs.
- Claude Desktop keeps honouring `--user-data-dir` and keeps one `Chrome_WidgetWin_1` main window per instance, as both switchers rely on today.
- No telemetry, ever; an update check would be a separate, opt-in decision with its own trust entry.
