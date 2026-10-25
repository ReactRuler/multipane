# 11 · Build and release process

## Builds

- `dotnet build -c Release` with `Deterministic=true`, `ContinuousIntegrationBuild=true`, `PublishReadyToRun=true`, SourceLink, locked NuGet restore (`packages.lock.json`, `--locked-mode`), SDK pinned by `global.json`.
- Two runtime identifiers: `win-x64`, `win-arm64`.
- Two shapes per RID: **framework-dependent** (requires the .NET Desktop Runtime 10; the installer offers the official Microsoft download link if missing, it never downloads anything itself) and **self-contained** (no prerequisite, larger). Which is the default download is decision D2.
- Portable zip: the self-contained folder with an empty `portable.txt`, so settings live beside the exe.
- Icons: `build/make-icon.ps1` draws the app mark with GDI+ (no external tool) at 16, 20, 24, 32, 40, 48, 64 and 256 px and packs a `.ico`; the output is committed as `Assets/app.ico` so a local build needs nothing. The SVGs in `docs/design/icons/` stay the design source; the script mirrors their geometry.

## Installer

Per-user, no admin: **Inno Setup** script in `build/installer.iss`, `PrivilegesRequired=lowest`, installs to `%LOCALAPPDATA%\Programs\Multipane`, Start menu shortcut, optional desktop shortcut, optional "start with Windows" (which writes the same HKCU Run value the app would). Uninstall removes the program folder and the Run value only; `%LOCALAPPDATA%\Multipane` (settings, logs) is left unless the user ticks "also remove settings". Profile folders are never touched by the installer or uninstaller. An MSIX package is not used because its sandbox complicates the Win32 calls and it adds a signing requirement for sideloading.

## Continuous integration (`.github/workflows/ci.yml`)

On every push and pull request, on `windows-latest` (x64) and `windows-11-arm` runners:

1. `dotnet format --verify-no-changes`
2. Build with warnings as errors
3. Unit tests with coverage threshold
4. Integration tests against the dummy app
5. `dotnet list package --vulnerable --include-transitive` must be clean
6. Licence check: every package's licence must be in the allow-list (MIT, Apache-2.0, BSD-2/3, MS-PL); fails otherwise
7. Grep gates: no `TODO` in `src/`, no `http` outside comments and docs, no `@` in non-code files
8. Dependency review action on pull requests

## Release (`.github/workflows/release.yml`)

Triggered only by a tag `v<major>.<minor>.<patch>` on `main`.

1. Build all four shapes and the installer from that exact commit.
2. `SHA256SUMS.txt` over every artefact.
3. `actions/attest-build-provenance` on each artefact (SLSA provenance; `gh attestation verify <file> --owner <org>` checks it).
4. Draft GitHub Release with the artefacts, the checksums, the attestation note, and release notes generated from the changelog section for that version.
5. The owner publishes the draft by hand after running the manual checklist (doc 10) on the built artefacts, not on a local build.

No other path produces a release; `release.yml` has `permissions: id-token: write, contents: write, attestations: write` and nothing else, and uses pinned action SHAs.

## Reproducible builds

`build/reproduce.ps1 <tag>`: clones at the tag, checks the SDK version, runs the same `dotnet publish` commands with the same properties, and prints SHA-256 of the output next to the published `SHA256SUMS.txt`. Deterministic compilation plus locked restore plus ReadyToRun on the same SDK produces identical managed assemblies; the Inno Setup installer embeds a build time and is therefore compared by *contents* (the script extracts both with `innounp` and compares file hashes). The README states this limitation plainly.

## Code signing

Options, in order of preference:

1. **Azure Trusted Signing** (monthly fee, about the price of a coffee a week; requires identity validation): CI signs via the official action with an OIDC-authenticated managed identity; no certificate file in the repository.
2. **SignPath Foundation** free OSS signing, if the project qualifies.
3. **Unsigned**: the README says so, explains the SmartScreen prompt, and tells users to verify the checksum and attestation instead.

Decision D3. Until signing exists, the release notes carry the SHA-256 lines inline.

## Versioning and changelog

Semantic versions, `CHANGELOG.md` with "Unreleased" at the top, moved under the version at tag time. Settings schema versions are independent integers (doc 07).

## Update check

None in version 1. If ever added: off by default, opt-in in Settings, a single GET of the GitHub releases API, shown as a line in About, never a download. It would be the only network call and would be listed in the trust document first.
