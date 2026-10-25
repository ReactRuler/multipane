# Licence recommendation and attribution

## Recommendation: MIT

The project builds on two MIT projects and borrows their ideas, strings and behaviour. MIT keeps the obligations identical for everyone downstream, is what the owner's current switcher uses, and asks nothing of a user beyond keeping the notice. Apache-2.0 would add an explicit patent grant and a NOTICE mechanism; neither matters for a tool of this kind, and it would make combining with the upstream MIT code slightly noisier. GPL would stop people embedding pieces in their own closed tools, which is not a goal here.

`LICENSE` at the repository root: the MIT text with `Copyright (c) 2026 <owner's chosen name or handle>`.

## Attribution (NOTICE file)

Both upstream projects are MIT and require their copyright notices to be kept where their code or derivatives appear. The C++ project's licence already carries two lines. `NOTICE` therefore reads:

```
Multipane builds on ideas, user-interface text and behaviour from:

Claude Profile Switcher (Claude-Code-Desktop-Switcher)
  Copyright (c) 2026 Priyanshu (PriyanshuGeTRekT)
  https://github.com/PriyanshuGeTRekT/Claude-Code-Desktop-Switcher  (MIT)

claude-switcher
  Copyright (c) 2026 Piwqust
  Copyright (c) 2026 Priyanshu (PriyanshuGeTRekT)
  https://github.com/Piwqust/claude-switcher  (MIT)

The taskbar-identity and sign-in-routing ideas in those projects are credited
there to Sukarth (Sukarth/Claude-Code-Desktop-Switcher).

Claude is a trademark of Anthropic, PBC. This project is not affiliated with
or endorsed by Anthropic.
```

Where a function is a direct translation of upstream code (the profile-name rules, the chat transfer, the kill-tree sequence), the file header names the upstream file.

## Disclaimer text (README and About)

"Multipane is an independent, open-source tool. It is not made by, affiliated with, or endorsed by Anthropic. Claude is a trademark of Anthropic, PBC. Using more than one Claude account is subject to Anthropic's terms of service; check them before you rely on this tool."

## Third-party components

| Component | Licence | Where |
| --- | --- | --- |
| .NET runtime and WPF | MIT | runtime |
| Microsoft.Windows.CsWin32 | MIT | build-time only |
| xunit | Apache-2.0 | tests only |
| resvg (icon rendering) | MIT / Apache-2.0 | CI only, output committed |
| Inno Setup | Inno Setup licence (free, attribution in the installer's About) | installer build |

CI's licence gate allows MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause and MS-PL for NuGet packages; anything else fails the build until reviewed here.
