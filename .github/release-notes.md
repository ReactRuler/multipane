Preview build of Multipane. Needs Windows 10 or 11 and the .NET 8 Desktop Runtime.

**Verify the download.** Compare the file's SHA-256 with `SHA256SUMS.txt`, or run `gh attestation verify <zip> --owner ReactRuler` to check the build provenance. The build is not code-signed yet, so Windows SmartScreen will ask once; the checksum and the attestation are the way to be sure what you have.

**What it never does.** No network, no reading inside Claude profile folders, no code injection, no admin rights. See SECURITY.md.

**Known limits.** This is the hosting spike with the product shell on top, not the final build. Restart is not in yet. Mica backdrop is off. See `spikes/hosting/RESULTS.md` for what has and has not been tested against real Claude windows.
