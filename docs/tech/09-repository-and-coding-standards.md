# 09 · Repository structure and coding standards

The repository is laid out so that a stranger can confirm in under an hour that the app does nothing hidden: the risky code is in one small folder, the rest is UI and pure logic.

## Layout

```
/
  README.md                 what it is, screenshots (demo mode), "What this app touches" summary, disclaimer
  SECURITY.md               reporting, what the app does and does not do (draft in docs/trust)
  LICENSE                   MIT, both upstream copyright lines in NOTICE
  NOTICE                    attribution to the two switcher projects
  Directory.Build.props     warnings as errors, nullable on, deterministic build, analyzers
  global.json               pinned .NET SDK
  .editorconfig             formatting rules enforced by `dotnet format --verify-no-changes`
  Multipane.sln
  src/
    Multipane.App/        WPF: windows, views, view models, resources, localisation (.resx en, ru)
    Multipane.Core/       SessionManager, LayoutEngine, ProfileCatalog, RestartPolicy, HealthMonitor, SettingsStore, FileLog
    Multipane.Native/     the risky capabilities, one file each, plus NativeMethods.txt (CsWin32 allow-list)
      ProcessLauncher.cs    starts Claude (ShellExecuteEx); the only place a process is created
      ProcessFinder.cs      process and window discovery (Toolhelp, NtQueryInformationProcess, EnumWindows)
      ProcessKiller.cs      WM_CLOSE, suspend, terminate tree; the only place a process is ended
      WindowHost.cs         owner link, styles, placement, hide and show, release
      ProfileDeleter.cs     the only place a directory is deleted
      ShortcutWriter.cs     .lnk files via IShellLink
      StartupRegistry.cs    HKCU Run value, and the claude:// handler keys when routing is on
      ChatTransfer.cs       the one write inside a profile folder
      ClaudeFacts.cs        the Electron/Chromium constants (window class, --type=, install roots)
  tests/
    Multipane.Core.Tests/ unit tests (xunit)
    Multipane.Integration.Tests/  drives the dummy app through the real Native layer
    DummyClaude/            the fake "Claude" used by integration tests (doc 10)
  spikes/hosting/           the hosting spike and RESULTS.md (kept for the record, not built in CI releases)
  docs/                     design/, tech/, trust/, research/
  build/                    release scripts, icon rendering, installer definition
  .github/workflows/        ci.yml (build, test, format, licence and dependency scan), release.yml (tag → artefacts)
```

`Multipane.Native` has no project reference except `Multipane.Core`'s small value types, and `Multipane.App` never references it directly: everything goes through `Core`, which keeps the "who may kill a process" question answerable by grep.

## Conventions

- C# 13, nullable reference types on, `TreatWarningsAsErrors`, .NET analyzers at `AnalysisLevel=latest-recommended`, plus `Microsoft.CodeAnalysis.BannedApiAnalyzers` with a `BannedSymbols.txt` that bans `System.Net.*`, `System.Diagnostics.Process.Start` (so only `ProcessLauncher` can start processes, through its own P/Invoke), `Directory.Delete`/`File.Delete` outside `ProfileDeleter` and `SettingsStore`, `Registry` outside `StartupRegistry` and `ProcessFinder`, and `Assembly.Load`.
- P/Invoke only through CsWin32; `NativeMethods.txt` lists every API by name and is reviewed in pull requests like a dependency file.
- Files under 300 lines, methods under 40; names say what, not how (`KillTree`, not `DoCleanup`).
- Comments explain why; no banners, no commented-out code, no `TODO` in `main` (CI greps for it).
- No speculative abstractions: an interface exists only if there are two implementations or a test double that needs it (`IProcessFinder`, `IWindowHost`, `IClock` are the expected ones).
- Errors: see doc 08. No empty catch (analyzer CA1031 configured as error for empty bodies).
- Threading: UI thread for WPF, background loops post to it via `Dispatcher`; Core types are not thread-safe and are only touched from the UI thread except the sampling loops, which hand over immutable snapshots.
- Localisation: every user-visible string in `.resx`, English source, Russian carried over from the switchers' strings where they match; no string concatenation of sentences.
- Demo mode: `--demo` makes `ProcessFinder` and `ProcessLauncher` return fakes with the design's made-up profiles, so screenshots and the docs never show real accounts. No real names or emails anywhere in the repository (CI greps for `@` in non-code files and for the owner's user name).

## Pull request checklist

- `dotnet format --verify-no-changes`, build with warnings as errors, all tests green, on x64 and ARM64 runners.
- Any change to `src/Multipane.Native/` or `NativeMethods.txt` or `BannedSymbols.txt` needs a second reviewer and an update to `docs/trust/what-this-app-touches.md`.
- Any new NuGet package needs a line in doc 01's dependency table with purpose and licence.

## Code review for trust

A reviewer with one hour reads, in this order: `docs/trust/what-this-app-touches.md`, `NativeMethods.txt`, `BannedSymbols.txt`, the eight files in `Multipane.Native`, then `grep -r "http" src/` (expected: none outside comments). That order is written at the top of `SECURITY.md`.
