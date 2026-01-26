# NWH Common Package

Shared utilities and components used across all NWH packages.

## Documentation

- **Package Manager**: Window > Package Manager > NWH Common Package > Documentation (local DocFX)

## Samples

Import **Common Base Sample** via **Package Manager > In Project > NWH Common Package > Samples**.

**IMPORTANT**: This sample is **required by all other NWH package samples** to function properly.

**Note**: Samples are found in the Package Manager under the **"In Project"** category in the left sidebar, NOT under "My Assets" or other categories.

## Requirements

- Unity 6000 or newer
- Universal Render Pipeline (URP)

## Important Notes

- **Assembly Definitions**: Package uses assembly definitions. Custom scripts referencing this package must add `NWH.Common` to their assembly definition references.
- **Automatic Installation**: This package is automatically included as a dependency when installing other NWH packages.

## Used By

- NWH Vehicle Physics 2
- Dynamic Water Physics 2
- NWH Aerodynamics
- Wheel Controller 3D
