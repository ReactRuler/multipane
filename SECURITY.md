# Security

## What this app is

Multipane is an unofficial Windows desktop tool that opens, arranges and watches several Claude Desktop windows, one per profile, inside one window. It is not made by, affiliated with, or endorsed by Anthropic. Running several accounts is subject to Anthropic's terms.

## What it never does

- **No network.** No HTTP client, no sockets. No telemetry, analytics, crash upload, update check or remote configuration.
- **No access to Claude's data.** It never reads, copies, decrypts or sends cookies, tokens, passwords, chats or anything else inside a Claude profile folder. The only operations on a profile folder are creating a new empty one when you add an account, deleting one when you confirm a delete, checking whether two files exist, and, only when you run "Transfer chats", copying small chat index files between two of your own profiles.
- **No injection.** It never patches Claude's files, hooks its functions, loads code into its process or uses debugging ports. It passes Electron's documented `--user-data-dir` switch and uses standard Windows window and process APIs.
- **No hidden code.** No obfuscation, no packed or encoded strings, no binary blobs in the repository, no code downloaded or executed at runtime, no plugin or scripting system.
- **No elevation.** No admin rights, services, drivers or scheduled tasks. "Start with Windows" writes one per-user `Run` registry value, only when you turn it on.

## How to check this in under an hour

1. Read `docs/trust/what-this-app-touches.md`: every folder, registry key, file and Windows API the app uses, with the reason.
2. In the spike, read `spikes/hosting/HostingSpike/NativeMethods.cs`: the complete list of Windows APIs it calls. In the product this becomes `NativeMethods.txt` with a banned-API list beside it.
3. Read the hosting, finder, profile and kill code: `Hosting.cs`, `ClaudeFinder.cs`, `Profiles.cs`. They are the only code that starts or ends a process, moves a window or deletes a folder.
4. Run `grep -ri "http" spikes/hosting/HostingSpike/*.cs` and expect no hits outside comments.
5. Verify a release against `SHA256SUMS.txt` and its build provenance (`gh attestation verify <file> --owner ReactRuler`), or build it yourself.

## Reporting a problem

Open a private security advisory on GitHub (Security › Report a vulnerability). Say what you found and how to reproduce it. Expect an acknowledgement within 7 days. Fixes ship as a normal tagged release with a note in the changelog; there is no bug bounty.

## Supported versions

The latest release only.
