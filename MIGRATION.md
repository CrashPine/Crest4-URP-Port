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
acceptance on a known static/moving plane and original Gerstner waves (see
TEST_RESULTS.md); broader latency coverage remains open.

GPU readbacks retain their point and normal counts. Changing a caller's count or
normal-output layout makes old packets unavailable (`RetrieveFailed`) until a
matching packet returns; velocity also needs matching history. Null point outputs
are supported: a normals-only query reads normal samples after the dispatched
point block. This fixes two inherited cases that previously returned successful
status with data interpreted using the wrong layout. Keep output array lengths
equal to the input point count. Positions within an unchanged layout still have
the original asynchronous semantics; these checks do not make queries synchronous.
`RemoveQueryPoints` leaves in-flight packets intact, as in the original implementation.


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

At most128 samples are used. Larger requested counts produce a warning and
replace the step with `length / 127`, so the last sample then reaches the requested
endpoint. Exact zero is treated as positive by the retained sign-change test:
on flat water a ray starting on the surface hits at distance0 when pointing up,
but misses when pointing down; ending exactly on the surface hits from above
and misses from below. A ray lying in the surface also misses. These inherited
endpoint conventions are explicitly tested, not a general geometric contact test.

Changing a ray's origin/direction retains the original asynchronous query
semantics. A ready result can combine heights sampled at the previous XZ positions
with the current ray positions. On the tested slope, moving the origin returned
4.50005m immediately when the current intersection was3.5m, then recovered with
fresh data. A true return value therefore does not establish an intersection with
the current ray at the current instant. Continuous fast movement and waves need
their own error/latency budget.

## Public time pause and resume

Assign a custom clock with `OceanRenderer.PushTimeProvider(clock)` and restore
the previous clock with `PopTimeProvider(clock)`. In Play Mode,
`TimeProviderCustom._paused=true` stops its accumulated CurrentTime while its
DeltaTime still follows Unity. To stop the delta supplied to dynamic simulations
as well, set `_overrideDeltaTime=true` and `_deltaTime=0`. Resume with
`_paused=false` and `_overrideDeltaTime=false`; the accumulated ocean clock
continues from its paused value without catching up the elapsed Unity time.

Pausing Unity with `Time.timeScale=0` also stops query timestamps. After pending
readbacks settle, GPU height retrieval remains valid but a query requesting
velocity returns `InvalidDtForVelocity` (16). Require the full status to return0
after time resumes before using velocity. Default/custom clock pause/resume and
real Gerstner/query response are tested; Timeline/networked clocks, arbitrary
time jumps and full dynamic-wave/foam pause integration remain separate coverage.

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
not arbitrary live local-height or dynamic-wave inputs. The scoped P07 fixtures
check chop=0 direct CPU queries and chop=1.6 through automatic OceanRenderer
settings selection. Choppy heights are compared with independent Newton inversion
of saved frames. Strongly folded mappings and wider configurations remain open.
Temporal interpolation follows the original per-keyframe inversion contract;
it is not inversion of a newly interpolated continuous GPU field.


## Original BoatProbes force and contact contracts

`BoatProbes` normalizes probe weights by their total and uses regular Rigidbody
force for buoyancy. For four submerged horizontal equal-weight probes in flat
water, equilibrium immersion is `mass / (1000 * _forceMultiplier)` metres.
Query positions include `_centerOfMass.y` in addition to each local probe offset;
the root height therefore includes that offset. This differs from the original
`SimpleFloatingObject` acceleration-based behavior; the two models were retained.
The buoyancy force cap applies separately to each probe, not to the summed force.

The original `BoatProbes.InWater` always returns true. It is not a wet-contact
sensor. The numerical fixture determines contact from the returned water height
at the four actual query points. The new wave fixture has a test-only GPU fence
to keep unthrottled batch physics and queries paced; no such synchronization was
added to production. Its results do not establish unpaced query performance.
Public wind/spectrum and custom ocean-time controls were exercised with runtime
spectrum updates enabled (`_spectrumFixedAtRuntime=false` in the Inspector
configuration); the default fixed-spectrum setting is not a live-edit contract.


`_engineBias` and `_turnBias` work with `_playerControlled=false`, without a
keyboard. Engine/drag use ForceMode.Acceleration: on flat water with fixed
orientation, terminal forward velocity is enginePower*engineBias/dragForward,
independent of Rigidbody mass. Steering adds angular acceleration along
`transform.up + _turningHeel * transform.forward`. The cap remains Newtons per
probe: four fully immersed probes capped at1000N produce4000N total; infinity
restores the original weighted force and zero disables buoyancy.

`SimSettingsAnimatedWaves.CollisionSource` participates in the ocean settings
hash. Switching source through BakedFFT/None/ComputeShaderQueries rebuilds the
provider and releases the previous buffers/jobs. Assign `_bakedFFTData` before
selecting BakedFFT, preserve its asset reference and match its wind configuration.
This test does not prove hot-swapping only the data reference, nor persistence
of a scene binding: after scene reload the fixture explicitly reassigns settings.
A rebuild resets temporary time-provider stack entries; push the custom clock
after selection. CPU queries then use that clock, including the ocean sea-level
offset for heights; raw displacement queries remain relative and uninverted.


Probe weight totals are calculated in Start in Player code. The original Editor
also recalculates them in each FixedUpdate for Inspector edits. Configure weights
before Start for Player use; the Editor's live weight edits are not proof of a
runtime weight-update API.


Query normals describe the finite-difference gradient of the simulated wave
field, including its generator texel sampling and LOD filtering. They are not the
unfiltered continuous wave equation or the material's texture normal mapping.
The Gerstner query fixture therefore validates two stages independently: native
generator texels against the original cosine equations, then public query results
against scalar samples of combined ocean RT snapshots at dispatch timestamps.
Using only the continuous equation, or only generator interpolation, skips LOD
resampling and can produce false normal/height failures. This fixture uses a
GPU readback fence for a controlled reference and proves no unpaced latency.

## BoatDev example components

`BoatAlignNormal` retains its acceleration-based cubic buoyancy:
`acceleration = buoyancyCoeff * bottomDepth^3`, where
`bottomDepth = waterHeight - rootY - bottomH`. Its flat-water equilibrium is
`rootY = waterHeight - bottomH - cbrt(gravityMagnitude / buoyancyCoeff)`,
independent of mass. Despite its name, `_maximumBuoyancyForce` clamps this
acceleration before ForceMode.Acceleration; cap3 means3m/s² and therefore
3000N/15000N at1000kg/5000kg. This differs from BoatProbes' per-probe Newton cap.
At bottomDepth<=0 the example is dry and returns before thrust/steering/drag.

With `_useBoatLength`, the original controller adds two projected-normal torques.
Do not expect its final up vector always to equal a single sampled normal. On a
uniform X slope at fixed heading, its equilibrium is along the summed projected
normals: the tested slope.25 gives approximately atan(.125) roll in dual-normal
mode, versus atan(.25) in single-normal mode. Both modes are preserved.

The original example reads raw keyboard input even with `_playerControlled=false`.
The batch fixture provides a temporary Input System keyboard if none exists,
uses only biases and removes its device afterward. This is not proof that the
example is safe on a focused headless process with no keyboard, or keyboard-input
acceptance. The production component was not rewritten for the test environment.

`LerpCam` clamps the target Y to sampled water height plus its minimum offset,
then interpolates by `clamp01(lerpAlpha * OceanDeltaTime * 60)`. This is not an
instantaneous clamp of the follower's actual height. Zero ocean delta freezes
position following while LookAt can still rotate toward a moving target. The
numerical fixture uses the original Update on an auxiliary Transform and a moving
procedural target; saved example cameras, rendered camera transitions, waves and
multiple-camera rendering still need separate acceptance.

`RippleGenerator` uses absolute OceanRenderer.CurrentTime for warm-up, then
enables its RegisterDynWavesInput when
`Repeat(CurrentTime - warmUp, period) < onTime`. Before warm-up it returns without
changing the input or its property block: start the input disabled if a quiet
warm-up is required. With `_animate=false`, the existing manual input state is
retained while the material parameters still update. Disabling the input removes
the source; waves already present continue to propagate.

The original Add Bump shader uses `_SimDeltaTime=1/simulationFrequency` and divides
its velocity increment by the active sim count supplied by RippleGenerator. Source
draws occur only on positive simulation substeps. A zero ocean delta therefore
submits no new forces even if the input is enabled; it is different from disabling
the time gate. Controlled60/30Hz impulse/propagation checks use the original
component, shader and compute solver. They do not establish long-term stability,
all grid/LOD ranges or performance.


The original networked clock adds TimeOffsetToServer to Unity time; it does not
implement transport. The cutscene clock uses active Playing director time plus
its offset. Paused/stopped/disabled directors fall back to default Unity time,
even with a networked provider below it. Its delta remains Unity delta: freezing
a manual director time does not freeze dynamic simulation. Disabling the cutscene
component with restore enabled exposes the exact previous provider. Automatic
retry on re-enable lives in original Editor-only Update; Editor coverage does not
establish Player re-enable behaviour.

BoatAlignNormal responds to wave orbital velocity plus flow; its instantaneous
hull velocity need not equal flow speed. Joint numerical coverage uses actual
Rigidbody motion and main-camera LerpCam, without accepting saved example material
equivalence or full camera stress.
