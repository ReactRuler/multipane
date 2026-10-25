# Launch plan (everything free)

Order matters: nothing public before a release exists that was built by CI and hand-checked.

| Step | What | Cost | Status |
| --- | --- | --- | --- |
| 1 | GitHub repo, MIT, NOTICE, README, SECURITY.md, issue templates, Discussions | free | done, public since 2026-10-25 |
| 2 | CI on every push; release workflow on a `v*` tag: zips for x64 and arm64, SHA256SUMS, build provenance, draft release | free | done, used for the preview releases |
| 3 | Product build, tests and the real-Claude matrix (docs/tech) | time | open; previews ship from the spike meanwhile |
| 4 | Preview releases (`v0.1.0-preview.N`) published by hand after checking the CI artefacts; `v0.1.0` after step 3 | free | preview.1 and preview.2 published |
| 5 | GitHub Pages from `docs/` (landing page is `docs/index.html`) | free on a public repo | live at https://reactruler.github.io/multipane/ |
| 6 | Code signing: apply to SignPath Foundation (`docs/launch/signing.md`) | free for OSS | open |
| 7 | winget pull request (`build/packaging/winget`), Scoop extras bucket (`build/packaging/scoop`), Chocolatey package | free | templates ready |
| 8 | Announce: issue or discussion on both switcher repos crediting them, r/ClaudeAI, Show HN, X with the Reopen GIF (`docs/launch/announcements.md`) | free | drafts ready |
| 9 | Keep: answer the first issues within a day, label good first issues, honest changelog, second release within a month | time | |

What not to do: Microsoft Store (paid developer account and a sandbox that fights the Win32 calls), paid signing certificates, any "download manager" sites.
