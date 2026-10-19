# Crest 4.23.0 → Unity 6.6 URP

## Source and architecture

Source: https://github.com/wave-harmonic/crest at tag `4.23.0`, commit
`db0658ff0b2e93e4a9e28cc2867509658b0ecc00` (2026-06-18).
The official remote's 4.x tags were inspected before choosing this release.
`Assets/Crest/Crest` contains the runtime, editor tools, shaders and resources;
`Assets/Crest/Crest-Examples` contains the original sample assets and components.
Original GUIDs and paths are retained. The baseline commit precedes all port changes.

`OceanRenderer.RunUpdate` flips LOD buffers, updates viewpoint/scale, writes global
shader data, and executes `BuildCommandBuffer.BuildAndExecute`. FFT, Gerstner,
foam, dynamic waves and GPU queries do not need to become per-camera Render Graph
passes. Preserve their dispatch sequence and public API. GPU validation must still
check texture-array formats, readback latency, barriers and disposal.

## Rendering dependencies found by source audit

* `Shaders/Ocean.shader`: named GrabPass, ForwardBase tag, UnityCG and Lighting includes,
  legacy fog macros; original vertex displacement and shading are reusable.
* `OceanReflection.hlsl`: Built-in reflection probes; retain Fresnel, planar blending
  and directional highlight and adapt only the environment lookup.
* `OceanShaderHelpers.hlsl`: single-argument LinearEyeDepth; preserve orthographic path.
* `UnderwaterRenderer.cs`, `.Mask.cs`, `.Effect.cs`: CameraEvent injection, camera
  target/depth built-ins, mask and stencil volumes, temporary color texture lifetime.
  These require explicit graph resources and graph-scheduled mask/composite passes.
  Do not wrap the old buffers blindly: CameraTarget is not a graph texture handle.
* `LodDataMgrShadow`: LightEvent callbacks and Built-in shadow maps. Needs URP
  main-light shadow sampling while retaining temporal/soft-shadow data behavior.
* `OceanPlanarReflection`: manual Camera.Render; requires supported SRP camera request,
  recursion protection, reflection matrix, oblique clip plane and exception-safe state restore.
* `OceanDepthCache`: replacement-camera rendering. Needs URP depth renderer pass or
  explicit supported render request with equivalent depth semantics.
* Simulation input shaders use direct command-buffer draws. Do not mechanically
  replace their passes or shader tags just because they contain CGPROGRAM.

## Implementation order and acceptance

1. Pin source, preserve assets and establish reproducible Unity 6000.6.5f1 import.
2. Compile core and verify compute resources; keep algorithms intact.
3. Port full surface shader and graph background capture. Compare displaced geometry,
   refraction, normal mapping, foam, scattering and lighting with original.
4. Port depth cache, planar reflections, shadows, underwater mask/composite/volumes.
5. Generate all seven requested scenes. Exercise queries/buoyancy with different
   rigidbody sizes/masses; vary spectra in Play Mode using original supported controls.
6. Run DX11 and DX12 graphics tests, capture and inspect images, profile resources and
   frame times. Test repeated enable/disable and camera/quality/resolution changes.
7. Build and run Windows Development and Release players; fill feature matrix from evidence.

Successful import is not successful rendering. Every missing check stays NOT TESTED;
environment impediments are BLOCKED with an explicit reason. No completion claim until
the full matrix passes.

## Target revision — 2026-10-19

The user explicitly changed the target to their installed Unity 6000.6.5f1. The project now pins that Editor and URP 17.6.0; no 6.3 installation is required. The previous 6.3 installer download completed but installation required UAC. The CLI install directory has been restored to C:/Program Files/Unity/Hub/Editor. Existing 6000.6 diagnostic evidence remains valid for its recorded revision, with acceptance repeated in the deliverable checkout.

