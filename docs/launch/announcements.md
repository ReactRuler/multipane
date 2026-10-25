# Announcement drafts

Post only after the first public release. Replace the link once the repo is public.

## Discussion on the two switcher repositories

Title: Multipane: one window for all the profiles your switcher creates (built on your work)

Hi. I use your switcher daily with four accounts, and I built a companion that goes one step further: it hosts all the running Claude windows inside one window, with tabs, columns, rows, a grid or a focus view, and keeps a crashed session in place with a Reopen button. It reads the same profile folders and the same settings.json you use, changes nothing in them, and works side by side with the switcher. It is MIT, credits both your projects in its NOTICE, and makes no network calls at all. Repo: <link>. Happy to link back to you from the README in any way you prefer.

## r/ClaudeAI

Title: I made a free, open-source window that holds all your Claude Desktop profiles at once (Windows)

If you run more than one Claude account, you probably tile windows by hand and lose a session now and then when one crashes. Multipane puts every profile in one window: tabs or a grid, start and kill from a header, and a crashed session stays as a placeholder with a Reopen button. It uses the profiles the Claude Profile Switcher already created, so no new sign-ins. No network, no reading of your Claude data; the repo has a page listing every folder and API it touches. Free, MIT. <link>

## Show HN

Title: Show HN: Multipane – one window for several Claude Desktop profiles on Windows

Text: Hosts running Electron windows inside one WPF window using owner links and SetWindowPos (no injection, no debug ports), with a layout engine for tabs, columns, rows, grid and focus, and a small state machine for crash detection and auto-restart with back-off. Findings from the window-hosting spike are in RESULTS.md (DWM cloaking is refused across processes; Chromium enforces its own bounds unless you skip WM_WINDOWPOSCHANGING). MIT, no network calls. <link>

## X

One window for all your Claude Desktop profiles. Tabs, grid, focus. Crashed session? It stays put with a Reopen button. Free, open source, no network calls. <link> (attach the Reopen GIF)
