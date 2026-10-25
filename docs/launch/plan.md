# Launch plan (everything free)

Order matters: nothing public before a release exists that was built by CI and hand-checked.

| Step | What | Cost | Status |
| --- | --- | --- | --- |
| 1 | GitHub repo, MIT, NOTICE, README, SECURITY.md, issue templates, Discussions | free | done (private until the first release) |
| 2 | CI on every push; release workflow on a `v*` tag: zips for x64 and arm64, SHA256SUMS, build provenance, draft release | free | done, untested until the first tag |
| 3 | Product build, tests and the real-Claude matrix (docs/tech) | time | open |
| 4 | Tag `v0.1.0`, run the manual checklist on the CI artefacts, publish the draft, flip the repo to public | free | open |
| 5 | GitHub Pages from `docs/` (landing page is `docs/index.html`) | free on a public repo | ready, enable after step 4 |
| 6 | Code signing: apply to SignPath Foundation (`docs/launch/signing.md`) | free for OSS | open |
| 7 | winget pull request (`build/packaging/winget`), Scoop extras bucket (`build/packaging/scoop`), Chocolatey package | free | templates ready |
| 8 | Announce: issue or discussion on both switcher repos crediting them, r/ClaudeAI, Show HN, X with the Reopen GIF (`docs/launch/announcements.md`) | free | drafts ready |
| 9 | Keep: answer the first issues within a day, label good first issues, honest changelog, second release within a month | time | |

What not to do: Microsoft Store (paid developer account and a sandbox that fights the Win32 calls), paid signing certificates, any "download manager" sites.
