# Migration notes — unfinished implementation

Do not integrate this checkout into a production project yet. Import/build and scoped graphics tests under Unity 6000.6.5f1 pass. Full functional acceptance is still incomplete; see FEATURE_MATRIX.md.

The original ocean shader asset and GUID remain in place, so original ocean
materials resolve to the ported shader. The surface now uses `UniversalForwardOnly`,
URP Core/Lighting, URP environment lookup and fog. Displacement, LOD snapping, foam,
scattering and Fresnel equations remain the original implementation. Original
Built-in simulation input passes are not mechanically retagged.

Add `CrestBackgroundFeature` to the active Universal Renderer. It copies camera
color before transparents and publishes `_BackgroundTexture`. Depth and intermediate
camera color are required. The generated validation renderer includes this feature.
Its background texture is created per graph; no persistent background RTHandle allocation is
introduced. Multi-camera/MSAA/dynamic-resolution behavior still requires testing.

The Crest runtime assembly now references official Universal/Core runtime assemblies
for planar camera render requests. The reflection matrix and clipping calculation
are unchanged. Ocean-root active state is restored in `finally`, including exceptions.
`OceanRenderer` now subscribes/unsubscribes to SRP begin-camera callbacks.

The graphical batch-mode control is `OceanRenderer.EnableGraphicsInBatchMode`.
It defaults to false and explicitly enables visual simulations in GPU-enabled batch
tests; it does not create a graphics device or override `RunningWithoutGPU`.
Set it before loading/creating a scene's ocean. Setting it after ocean initialization
does not itself rebuild a headless-created root, which has no surface meshes.
Graphical fixtures should verify that visible ocean chunks exist before interpreting
camera captures; simulation data alone does not establish surface rendering.
Dedicated-server behavior remains the default.

## Existing extension contracts retained

* Simulation time: `ITimeProvider`, `PushTimeProvider`, `PopTimeProvider`; retain a
  monotonic time source and appropriate delta times for persistent simulations.
  Rebuilding an ocean resets its stack to the default and serialized provider,
  as in the original Crest. Apply feature/quality configuration before pushing a
  temporary provider, or reapply it after a rebuild. Check `TimeProvider` when
  coordinating configuration changes with external simulation time.
* Wind: `_globalWindZone`, `_globalWindSpeed` (km/h), `_globalWindDirectionAngle`,
  `_globalWindTurbulence`; individual shapes also support overrides.
* Wave shape: original `OceanWaveSpectrum` and `ShapeWaves` APIs/settings. Spectrum
  edits are not a promise of instantaneous physically continuous sea-state changes.
* Sea level: ocean transform height and original local-height inputs.
* Sampling: original collision/flow providers and sampling helpers; respect query
  readiness and GPU readback latency, rather than treating pending queries as zero.
* Quality: existing simulation settings, LOD count/resolution and per-feature controls.

No FishNet, Steam, weather, flooding or vessel-gameplay dependency has been added.

## Outstanding breaking rendering differences

Underwater rendering now records graph mask/composite passes and reuses the original volume and artifact-fix algorithms. Temporal shadows import the original ping-pong arrays and sample the URP main-light atlas. Simulation matrices are restored before subsequent camera passes. Depth-cache capture uses a depth-only URP render request. Original example materials outside the ocean have not all been migrated. Basic volume confinement/fly-through and bounded scene-reload tests are recorded in TEST_RESULTS.md; wave-driven transitions, XR, visual-equivalence and sustained resource acceptance remain outstanding. Task ordering belongs only in PORT_PLAN.md.

URP teardown skips legacy Camera.RemoveCommandBuffer because graph passes are registered by the renderer feature, not camera events. Built-in retains its matching add/remove path. Fixed-time Editor fixtures now cover surface crossings in both directions, a localized meniscus, geometry-only overlay and before/after-transparent scheduling. A standard transparent shader without the Crest fog API is intentionally drawn after the before-transparent composite; integrations using that mode must apply underwater fog in their transparent shader.

## Planar reflection options

URP skips the Built-in-only spherical layer-culling switch. During the synchronous
reflection request, a conservative bounding-sphere distance adapter temporarily
excludes distant scene renderers, terrains and world-space canvases; its `finally`
restores only the states it changed. Loaded scenes and DontDestroyOnLoad are searched.
This scene traversal has a profiling marker `Crest.Reflection.DistanceCulling`;
its cost and all renderer types have not yet received full acceptance.

`OceanPlanarReflection.ReflectionDistance`, `DisablePixelLights` and
`RefreshReflection()` expose the existing distance/light controls and immediate
refresh. The light option sets per-camera URP additional-light data through
`CrestBackgroundFeature`; the tested rendering mode is Forward. The active pipeline
asset and Built-in quality settings are not changed for URP reflection requests.
The force-forward option now selects a Forward renderer from the active URP asset
through `UniversalAdditionalCameraData.SetRenderer`; `Camera.renderingPath` alone
does not select a URP renderer. The vertex-light option also selects Forward,
because Forward+/Deferred lighting cannot use this per-vertex policy. Include a
Forward Universal Renderer with `CrestBackgroundFeature` in the asset's renderer
list when using either option. Missing Forward configuration produces an explicit
error. With both options disabled, reflection inherits the viewpoint's renderer,
including a non-default selection. Editor fixtures render a known object through
Forward, inherited Forward+ and inherited Deferred; URP may internally fall back
for reflection camera types. This does not claim full visual equivalence between
these renderer modes or standalone coverage of each combination.

Player builds must retain both per-pixel and per-vertex additional-light variants.
`Crest.URP.Editor.ReflectionVariantSetup.Configure()` creates an auxiliary URP asset
and includes it using the official `IncludeAdditionalRPAssets` label setting.
The supplied build commands call this method. Integrators using another build
entry point must run it before building and retain the auxiliary labeled asset.
The renderer feature also declares a `ShaderKeywordFilter.SelectIf` rule for both
lighting modes: the auxiliary asset alone does not retain them during early
engine keyword prefiltering. Supplied builds enable strict shader variant matching
to report missing variants instead of silently substituting another lighting mode.
It is not selected as an active runtime quality pipeline. HDR/stencil/MSAA and
size changes recreate the reflection target; unchanged descriptors reuse it.


## Editor depth-cache export

`Crest.OceanDepthCacheEditor.SaveCacheToFile(cache, optionalAssetPath)` runs the same
export/import pipeline as the Inspector buttons. With no explicit path, rebaking
updates the existing saved asset and preserves its GUID; a new cache gets a unique
EXR asset path. The original relative-depth conversion, RGBAHalf readback,
OutputAsFloat EXR and linear/uncompressed SingleChannel RFloat import are retained.
The method restores the caller's active RenderTexture and releases its temporary
CPU texture in `finally`, including write/import failures. It is inside
`#if UNITY_EDITOR` and is unavailable in Player code. Supplying an explicit path
selects the export destination; it does not retarget an already assigned SavedCache.


## Query readiness and velocity timestamps

The retained `QueryBase.RetrieveSucceeded` checks only the `RetrieveFailed` bit.
Consequently, `SampleHeightHelper.Sample(..., out velocity)` returning true does
not establish that a fresh velocity was calculated. Integrations requiring valid
velocity should inspect the full `ICollProvider.Query` status, including
`NotEnoughDataForVels`, `VelocityDataInvalidated` and `InvalidDtForVelocity`.
The original velocity finite difference timestamps readbacks with Unity
`Time.time`, rather than the ocean's custom time provider. Freezing Unity time or
advancing only a custom ocean clock is therefore not a valid velocity test. A
height-only query does not need velocity history. P07 now has scoped numerical
acceptance on a known static/moving plane (see TEST_RESULTS.md); real-wave and
broader ownership/latency coverage remain open.


## Ray sampling contract

The original `RayTraceHelper.Trace` returns false both while GPU results are
unavailable and when a ready ray misses. A false value alone does not prove a
miss. Supply a normalized direction and initialize/query each frame. The helper
interpolates sign changes between sampled heights, including crossings from
below the surface. Its original sample count is `ceil(length / step) + 1`:
when length is not a multiple of step, the final sample extends beyond the
requested length and a returned intersection may do so too. This inherited
behavior is preserved and explicitly characterized in RayQueryTests; callers
requiring a strict range should reject distances greater than their range.

## Offline FFT baking in the Editor

`FFTBaker.BakeShapeFFT(shape, "Assets/ExistingFolder/Waves.asset")` performs the
original FFT generation, multi-resolution compute copy and RGBAHalf bake using
an explicit new asset destination. It is Editor-only and requires the existing
mathematics package. The destination must be a new `.asset` inside an existing
Assets folder; existing files/assets and traversal paths are rejected before
baking. The original one-argument call retains the interactive Save dialog;
in batch mode, callers must supply the explicit path.

During generation/readback, a `finally` restores the caller's active
RenderTexture and releases temporary GPU/CPU textures; the command buffer is
disposed. Cancellation or save exceptions discard an unsaved generated data
object. FFT compute algorithms, frame layout, half precision, time sampling,
GUIDs and CPU provider equations are unchanged. The CPU provider's original
velocity output is **vertical only**, derived from its 0.06-second backward
height difference. Baked data loops in ocean time and represents the baked FFT,
not arbitrary live local-height or dynamic-wave inputs. The scoped P07 fixture
checks the direct CPU provider with chop=0; automatic scene provider selection,
choppy displacement inversion and the wider combinations remain unaccepted.
