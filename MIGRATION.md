# Migration notes — unfinished implementation

Do not integrate this checkout into a production project yet. Import and graphics
acceptance under Unity 6000.6.5f1 are outstanding; see FEATURE_MATRIX.md.

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
Dedicated-server behavior remains the default.

## Existing extension contracts retained

* Simulation time: `ITimeProvider`, `PushTimeProvider`, `PopTimeProvider`; retain a
  monotonic time source and appropriate delta times for persistent simulations.
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

Underwater rendering now records graph mask/composite passes and reuses the original volume and artifact-fix algorithms. Temporal shadows import the original ping-pong arrays and sample the URP main-light atlas. Simulation matrices are restored before subsequent camera passes. Depth-cache capture uses a depth-only URP render request. Original example materials outside the ocean have not all been migrated. Spherical reflection culling emits an SRP warning and needs a supported equivalent. Full volume, XR, visual-equivalence and resource-lifetime acceptance remain outstanding.
