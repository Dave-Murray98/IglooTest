# NWH Dynamic Water Physics 2

Water-object interaction simulator with mesh-based buoyancy and hydrodynamics.

## Documentation

- **Online**: https://dynamicwaterphysics.com
- **Package Manager**: Window > Package Manager > NWH Dynamic Water Physics 2 > Documentation (local DocFX)

## Samples

### Prerequisites
Before importing NWH Dynamic Water Physics 2 samples, you **must** first import:
- **NWH Common Package > Common Base Sample** (required for all NWH package samples)

### Importing Samples
Import samples via **Package Manager > In Project > NWH Dynamic Water Physics 2 > Samples**.

**Note**: Samples are found in the Package Manager under the **"In Project"** category in the left sidebar, NOT under "My Assets" or other categories.

**NWH Dynamic Water Physics 2 Base Sample** is required before importing other Dynamic Water Physics samples (Multiplayer, water integration samples). Water integration samples are available for KWS, Crest, Stylized Water 3, R.A.M, Lux Water, and Unity HDRP Water.

## Requirements

- Unity 6000 or newer
- Universal Render Pipeline (URP)
- Compatible water asset (or use included simple water)

## Important Notes

- **Render Pipeline**: This package requires URP.
- **Assembly Definitions**: Package uses assembly definitions. Custom scripts referencing this package must add `NWH.DWP2` to their assembly definition references.
- **Water Provider**: Must configure WaterDataProvider for your water asset. Import appropriate integration sample.
- **Samples**: Not included by default - import via Package Manager as needed.

## Support

- Discord: https://discord.gg/ubFBbM8
- Email: nwhcoding@gmail.com
- Forum: https://forum.unity.com/threads/704405/
