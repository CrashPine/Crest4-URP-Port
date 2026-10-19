# Third-party notices

## Crest Ocean System

Source: https://github.com/wave-harmonic/crest
Tag: 4.23.0; commit: db0658ff0b2e93e4a9e28cc2867509658b0ecc00.
Copyright (c) 2019 Wave Harmonic and contributors.
The original MIT text is preserved in LICENSE.Crest and
Assets/Crest/Crest/LICENSE. Original file headers remain intact.
No commercial Crest URP or Crest 5 source was used.

## Files with separate upstream notices

The MIT repository is not a claim that every bundled file is MIT. In particular:

* `Shaders/Helpers/BIRP/Core.hlsl` and `Common.hlsl` identify Unity Companion License
  adaptations and link their exact Unity Graphics sources. Headers are preserved.
* `Crest-Examples/Shared/Effects/LightFlares/License.txt` identifies Unity Standard
  Assets under Unity Companion License; its notice is preserved.
* `Crest-Examples/Examples/Sprites/MaterialIcons/LICENSE` contains Apache License 2.0.
* `Crest-Examples/Examples/Audio/License.txt` attributes WaterPier to
  https://freesound.org/s/39901/ under CC BY 3.0 and UnderwaterWhiteNoise to
  https://freesound.org/s/448460/ under CC0. Keep these notices with redistribution.

## Unity packages and project scaffold

The initial scaffold was extracted from Unity's installed official URP blank template.
Universal/Core RP and the other dependencies in Packages/manifest.json are official
Unity packages and retain their own package licenses/notices. Their use is within a
Unity project. Unity Editor licensing and any commercial eligibility requirements
are separate from the MIT license on Crest. No third-party networking or MCP package
has been introduced.

This inventory records the notices observed in the imported tree; it is not legal
certification of a future game's complete distribution.

`Shaders/FullScreenTriangle.hlsl` also retains its original Unity Companion License
header and upstream Unity Graphics revision link. URP adapters use the corresponding
official package helper rather than copying commercial Crest rendering code.
