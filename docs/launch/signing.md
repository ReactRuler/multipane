# Code signing without paying

Unsigned downloads trigger SmartScreen and lose users on the first run. Two routes cost nothing:

**SignPath Foundation** signs open-source Windows builds for free. Requirements, as of their published policy: a public repository under an OSI licence, builds produced by a public CI pipeline (ours is `release.yml`), a maintainer identity, and the project's own release page. Apply at signpath.org/foundation after the first public release exists. Signing then runs as a step in the release workflow through their GitHub Action; the certificate never leaves their service.

**Until then**: SHA-256 checksums and GitHub build provenance attestations are published with every release, and the README explains the SmartScreen prompt in one line.

What to put in the application: the repo URL, the release workflow URL, the licence (MIT), what the app does in two sentences, and that it makes no network calls.
