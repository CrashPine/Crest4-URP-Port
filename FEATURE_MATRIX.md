# Feature preservation matrix

This is an unfinished port. Source presence is not functional acceptance. Paths below
are relative to `Assets/Crest/Crest`. Runtime statuses refer exclusively to the requested
Unity 6000.6.5f1 + URP configuration. Diagnostic 6000.6 results are recorded separately.

| Original subsystem | Source evidence | Port state | Target validation |
|---|---|---|---|
| FFT waves | Scripts/Shapes/FFT/FFTCompute.cs; Shaders/Resources/FFT | Compute algorithms retained | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Analytic Gerstner waves | Scripts/Shapes/ShapeGerstner.cs | GPU generation/readback pass with FFT disabled on DX11/DX12. Local spline also passes weights, raster feather, fixed-time response, moved footprint, public queries, disable/re-enable and prefab/profile reload with old RTs released | NOT TESTED (all options/visual equivalence) |
| Geometry | Scripts/OceanBuilder.cs; OceanChunkRenderer.cs | Retained | NOT TESTED |
| LOD scales/transitions | Scripts/LodData/LodTransform.cs; OceanVertHelpers.hlsl | Retained | NOT TESTED |
| Wave spectrum | Scripts/Shapes/OceanWaveSpectrum.cs | Retained; sampling tests added | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Animated waves/combine | LodDataMgrAnimWaves.cs; ShapeCombine.compute | Retained | NOT TESTED |
| Dynamic waves | LodDataMgrDynWaves.cs; UpdateDynWaves.compute | Zero-to-nonzero GPU height field from moving sphere passed on DX11/DX12 | NOT TESTED (long-term/visual acceptance) |
| Foam simulation | LodDataMgrFoam.cs; UpdateFoam.compute | Four fixtures pass on both APIs in the full 45-test suite: whitecaps/decay, shoreline/toggle, two-axis transport (2.998 m per leg, mass ratio 0.9999497), and zero-delta prewarm (0.25) repeated after scene reload with old RTs released. The surface fixture also passes on both APIs: constant foam, fixed ocean time, 0→3→0 m/s flow with reversible image changes and keyword-off control. Quality/stress, Built-in comparison remain pending; foam profile values/references round-trip is checked separately | NOT TESTED (full acceptance) |
| Flow data and queries | LodDataMgrFlow.cs; QueryFlow.cs | Known uniform vectors, foam transport and isolated surface response pass on both APIs. Original spline ribbon also passes GPU field checks: X→Z turn, 3→−2 m/s, moved points, zero outside, deregistration/re-registration. PNGs inspected. Saved-scene uniform flow and its material GUID survive reimport/reload and toggle with old RT released. Spline settings/point data survive prefab reimport; curved/closed GPU ribbon field also passes 67 circular tangent probes, open arc, moved points, negative speed and toggle (max error 0.02735 m/s at 3 m/s). Variable closed custom data, editor gestures and full visual equivalence pending | NOT TESTED (full acceptance) |
| Sea floor depth inputs | RegisterSeaFloorDepthInput.cs | Retained | NOT TESTED |
| Runtime depth cache/baking | Scripts/LodData/OceanDepthCache.cs | URP capture and known-height readback pass. Both APIs pass real EXR export/rebake/import (negative RFloat), GUID/prefab reference, scene reload without source geometry, relative owner movement and toggle. Editor export restores active RT and releases CPU texture on success/failure. Complex terrain, all cache modes and standalone round-trip pending | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Local water height | RegisterHeightInput.cs; OceanHelpersNew.hlsl | Both APIs pass RGFloat offsets and public height queries for +2/−1 m, moved input, global sea level +3 m, disable/re-enable; eight consecutive query frames per state. Height reporters give correct bounds and deregister. Saved-scene +2 m field/profile/clock references, reporter registration and toggle survive reimport/reload; old RT released. Both APIs also pass y=2+0.25x using tilted geometry and a three-point spline: field offsets, public queries 1/2/3 m, bounds, global sea level and toggle. Original surface occludes below-height markers and shows above-height markers in five states. Edge/LOD transitions, nonlinear profiles and standalone coverage pending | NOT TESTED (full acceptance) |
| Object displacement/interactions | Scripts/Interaction/SphereWaterInteraction.cs | Retained | NOT TESTED |
| Surface shading | Shaders/Ocean.shader | URP surface smoke captures inspected; original-renderer comparison pending | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Normal maps, scattering, Fresnel | OceanNormalMapping/Emission/Reflection.hlsl | Original equations retained | NOT TESTED |
| Reflection probes | OceanReflection.hlsl | URP environment lookup added | NOT TESTED |
| Planar reflection | Scripts/Reflection/OceanPlanarReflection.cs; OceanPlanarReflection.URP.cs | P05 targeted fixtures pass on DX11/DX12: distance/light/shadow options with independent main view, clipping, HDR/stencil/4× MSAA/resize and surface binding, recursion/exception recovery, persistent renderer, UI/Terrain state and Forward/Forward+/Deferred renderer selection. Both Player configurations/APIs pass distance/light smoke with strict matching. Full visual/stress/performance/standalone combinations remain P08–P11 | NOT TESTED (full acceptance); scoped checks passed |
| Refraction/background | OceanEmission.hlsl; URP/CrestBackgroundFeature.cs | Graph color copy replaces named GrabPass | NOT TESTED |
| Shadows/temporal soft shadows | Scripts/LodData/Shadows/LodDataMgrShadow.cs | Graph accumulation and matrix restore implemented; DX11/DX12 occlusion readback passed | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Caustics | OceanEmission.hlsl | URP atlas sampling at refracted world position added; visual acceptance pending | NOT TESTED |
| Underwater mask/composite | Scripts/Underwater/UnderwaterRenderer.* | Graph mask, artifact correction and composite added | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Underwater volumes/meniscus | Shaders/Underwater; Helpers/WaterVolume.hlsl | Portal/Volume confinement and Fly-Through inside-view fixtures added; see current per-API results in TEST_RESULTS.md. Fixed-time surface crossing/meniscus and overlay/order tests passed on DX11/DX12; storm transitions, MSAA/XR and standalone variants remain pending | NOT TESTED |
| GPU height/normal/velocity queries | Scripts/Collision/QueryDisplacements.cs; SamplingHelpers.cs | Retained; graphics test added | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Baked FFT CPU queries | CollProviderBakedFFT.cs; FFTBaker.cs; FFTBakedData.cs | Retained | NOT TESTED |
| Ray queries | RayTraceHelper.cs | Retained | NOT TESTED |
| Buoyancy | SimpleFloatingObject.cs; BoatProbes.cs | Original force model retained | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Watercraft example components | Crest-Examples/BoatDev | Assets retained, materials not comprehensively converted | NOT TESTED |
| Clip surface | LodDataMgrClipSurface.cs; RegisterClipSurfaceInput.cs | Cube/sphere signed-distance fields match original formulas within 0.005; geometry input writes 1 inside/0 outside. Both APIs render square/circle holes (4096/3220 pixels), preserve border and restore the exact baseline after deregistration with zero field. Saved-scene cube field/material configuration survives reload and toggle with old RT released; overlapping +2 m local height correctly changes 3D SDF to 0.5. Overlaps/inversion, convex hulls/textures, wave stress and standalone variants remain pending | NOT TESTED (full acceptance) |
| Albedo inputs | LodDataMgrAlbedo.cs; RegisterAlbedoInput.cs | Both APIs pass original color input: red/blue, moved patch, SrcAlpha blending (RGBA field and linear surface composite), cutoff, deregistration/re-enable. Below cutoff/disabled field is fully zero and surface matches baseline; PNGs inspected. Saved-scene color/alpha/material GUID survive reimport/reload and toggle with old RT released. Textured/overlapping inputs and standalone variants pending | NOT TESTED (full acceptance) |
| Splines/local wave shapes | Scripts/Spline; Scripts/Shapes/ShapeWaves.cs | Original Crest spline flow ribbon generation/point updates pass on both APIs; Unity Splines 2.9.1 dependency retained. Closed three-point spline metadata, references and overrides survive prefab reimport; Undo/Redo and resave preserve radius/GUID. Curved/closed flow GPU fields and sloped height spline fields/queries/surface occlusion pass on both APIs. Straight local Gerstner spline also passes footprint, weights, feather, time/movement, queries and prefab/profile reload. Real DX11 bare-spline Inspector edits, Add Point/Closed and Scene view drag with Undo/Redo/save/reopen pass. Nonlinear/closed local wave shapes and full visual/standalone equivalence remain pending; variable closed custom-data interpolation must be compared to the inherited baseline in P09 | NOT TESTED (full acceptance) |
| Water bodies/culling | Scripts/WaterBody.cs | Retained | NOT TESTED |
| Floating/shifting origin | FloatingOrigin.cs; ShiftingOrigin.cs | Retained, particle/wind modules restored | NOT TESTED |
| Time providers | Scripts/Time | Original provider stack retained | NOT TESTED |
| Editor tooling/serialization | Scripts/Editor and custom inspectors | Both APIs pass foam/depth/spectrum profile values, private arrays and references after unload/reimport; spline point data/overrides and prefab resave pass. Original custom Editor bindings resolve; Undo/Redo checked. Real DX11 original Inspector radius/custom speed/spectrum multiplier edits, Add Point/Closed, Scene view handle drag, Undo/Redo and save/reopen pass on a bare spline/profile fixture. Live GPU updates from GUI, remaining component-specific controls and final Editor regression belong to P11; no DX12 GUI run claimed | NOT TESTED |
| XR support | Scripts/Helpers/XRHelpers.cs | Retained; no XR acceptance testing | NOT TESTED |
| Debug visualization | OceanDebugGUI.cs; VisualiseCollisionArea.cs | Retained | NOT TESTED |
| Seven demonstration scenes | Assets/CrestPortDemos | Seven-scene queries/captures passed in both builds/APIs; complete feature demonstrations pending | NOT TESTED |
| Windows Development/Release | URP/Editor/PortAutomation.cs | Both builds succeed; each runs seven-scene smoke checks on DX11 and DX12 | PASSED (build/launch/smoke scope only) |
| Performance/resource lifetime | All GPU owners and graph pass | 12 scene reloads keep tracked texture footprint stable on both APIs; total VRAM, buffers, GC and timings pending | NOT TESTED |

BLOCKED rows identify missing port implementations or environmental requirements;
these are not claims that source functionality was absent. No required feature has
been marked PASSED merely because its files survived import.

## Связь с планом (без дублирования очереди)

- P04: underwater mask/composite/volume/meniscus.
- P05: planar reflections и их параметры.
- P06: foam, flow/splines, clip, albedo, local height, depth baking.
- P07: queries, baked FFT, ray tests, buoyancy/boats, time/wind contracts.
- P08: geometry/LOD, water bodies, floating origin, cameras/MSAA/resolution/XR.
- P09: surface/refraction/probes/caustics/shadows/визуальная эквивалентность и материалы примеров.
- P10: performance/resource lifetime; P11: финальная регрессия/builds; P12: итоговая приёмка.

NOT TESTED в последнем столбце означает незавершённую **полную** приёмку строки. Уже пройденные ограниченные проверки перечислены отдельно и не должны повторяться без причины. Источник очередности — только PORT_PLAN.md, актуальные измерения — только TEST_RESULTS.md.
