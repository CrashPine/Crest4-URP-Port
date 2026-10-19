# Feature preservation matrix

This is an unfinished port. Source presence is not functional acceptance. Paths below
are relative to `Assets/Crest/Crest`. Runtime statuses refer exclusively to the requested
Unity 6000.6.5f1 + URP configuration. Diagnostic 6000.6 results are recorded separately.

| Original subsystem | Source evidence | Port state | Target validation |
|---|---|---|---|
| FFT waves | Scripts/Shapes/FFT/FFTCompute.cs; Shaders/Resources/FFT | Compute algorithms retained | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Analytic Gerstner waves | Scripts/Shapes/ShapeGerstner.cs | GPU generation/readback passed with FFT disabled on DX11/DX12 | NOT TESTED (all options/visual equivalence) |
| Geometry | Scripts/OceanBuilder.cs; OceanChunkRenderer.cs | Retained | NOT TESTED |
| LOD scales/transitions | Scripts/LodData/LodTransform.cs; OceanVertHelpers.hlsl | Retained | NOT TESTED |
| Wave spectrum | Scripts/Shapes/OceanWaveSpectrum.cs | Retained; sampling tests added | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Animated waves/combine | LodDataMgrAnimWaves.cs; ShapeCombine.compute | Retained | NOT TESTED |
| Dynamic waves | LodDataMgrDynWaves.cs; UpdateDynWaves.compute | Zero-to-nonzero GPU height field from moving sphere passed on DX11/DX12 | NOT TESTED (long-term/visual acceptance) |
| Foam simulation | LodDataMgrFoam.cs; UpdateFoam.compute | Retained; explicit graphical batch opt-in | NOT TESTED |
| Flow data and queries | LodDataMgrFlow.cs; QueryFlow.cs | Known 3 m/s vector and runtime direction change passed on DX11/DX12; visual advection/splines pending | NOT TESTED |
| Sea floor depth inputs | RegisterSeaFloorDepthInput.cs | Retained | NOT TESTED |
| Runtime depth cache/baking | Scripts/LodData/OceanDepthCache.cs | URP request added; diagnostic height readback passed | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Local water height | RegisterHeightInput.cs; OceanHelpersNew.hlsl | Retained | NOT TESTED |
| Object displacement/interactions | Scripts/Interaction/SphereWaterInteraction.cs | Retained | NOT TESTED |
| Surface shading | Shaders/Ocean.shader | URP surface port implemented, validation pending | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Normal maps, scattering, Fresnel | OceanNormalMapping/Emission/Reflection.hlsl | Original equations retained | NOT TESTED |
| Reflection probes | OceanReflection.hlsl | URP environment lookup added | NOT TESTED |
| Planar reflection | Scripts/Reflection/OceanPlanarReflection.cs | URP render request, exception-safe root restoration | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Refraction/background | OceanEmission.hlsl; URP/CrestBackgroundFeature.cs | Graph color copy replaces named GrabPass | NOT TESTED |
| Shadows/temporal soft shadows | Scripts/LodData/Shadows/LodDataMgrShadow.cs | Graph accumulation and matrix restore implemented; DX11/DX12 occlusion readback passed | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Caustics | OceanEmission.hlsl | URP atlas sampling at refracted world position added; visual acceptance pending | NOT TESTED |
| Underwater mask/composite | Scripts/Underwater/UnderwaterRenderer.* | Graph mask, artifact correction and composite added | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Underwater volumes/meniscus | Shaders/Underwater; Helpers/WaterVolume.hlsl | Original volume/meniscus algorithms reused by graph; no volume coverage yet | NOT TESTED |
| GPU height/normal/velocity queries | Scripts/Collision/QueryDisplacements.cs; SamplingHelpers.cs | Retained; graphics test added | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Baked FFT CPU queries | CollProviderBakedFFT.cs; FFTBaker.cs; FFTBakedData.cs | Retained | NOT TESTED |
| Ray queries | RayTraceHelper.cs | Retained | NOT TESTED |
| Buoyancy | SimpleFloatingObject.cs; BoatProbes.cs | Original force model retained | NOT TESTED (full acceptance); scoped checks passed — see TEST_RESULTS.md |
| Watercraft example components | Crest-Examples/BoatDev | Assets retained, materials not comprehensively converted | NOT TESTED |
| Clip surface | LodDataMgrClipSurface.cs; RegisterClipSurfaceInput.cs | Retained | NOT TESTED |
| Albedo inputs | LodDataMgrAlbedo.cs; RegisterAlbedoInput.cs | Retained | NOT TESTED |
| Splines/local wave shapes | Scripts/Splines; ShapeWaves.cs | Retained, official Splines dependency | NOT TESTED |
| Water bodies/culling | Scripts/WaterBody.cs | Retained | NOT TESTED |
| Floating/shifting origin | FloatingOrigin.cs; ShiftingOrigin.cs | Retained, particle/wind modules restored | NOT TESTED |
| Time providers | Scripts/Time | Original provider stack retained | NOT TESTED |
| Editor tooling/serialization | Scripts/Editor and custom inspectors | Retained; old importer warnings need target review | NOT TESTED |
| XR support | Scripts/Helpers/XRHelpers.cs | Retained; no XR acceptance testing | NOT TESTED |
| Debug visualization | OceanDebugGUI.cs; VisualiseCollisionArea.cs | Retained | NOT TESTED |
| Seven demonstration scenes | Assets/CrestPortDemos | Seven-scene queries/captures passed in both builds/APIs; complete feature demonstrations pending | NOT TESTED |
| Windows Development/Release | URP/Editor/PortAutomation.cs | Both builds succeed; each runs seven-scene smoke checks on DX11 and DX12 | PASSED (build/launch/smoke scope only) |
| Performance/resource lifetime | All GPU owners and graph pass | 12 scene reloads keep tracked texture footprint stable on both APIs; total VRAM, buffers, GC and timings pending | NOT TESTED |

BLOCKED rows identify missing port implementations or environmental requirements;
these are not claims that source functionality was absent. No required feature has
been marked PASSED merely because its files survived import.
