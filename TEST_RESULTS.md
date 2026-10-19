# Test results — 2026-10-19

Target: Unity **6000.6.5f1** (3ff58d469c8a), URP **17.6.0**, Windows x64, NVIDIA RTX 3050 Ti Laptop GPU. The user selected this installed Editor. Render Graph is enabled.

## Deliverable checkout results

| Check | DX11 | DX12 | Evidence |
|---|---|---|---|
| Editor test suite | PASSED: 13/13 | PASSED: 13/13 | EditMode-DX11.xml / EditMode-DX12.xml |
| Development player, seven scenes | PASSED: Errors=0 | PASSED: Errors=0 | Player-Development-DX11 / DX12 |
| Release player, seven scenes | PASSED: Errors=0 | PASSED: Errors=0 | Player-Release-DX11 / DX12 |

Both Windows builds succeeded. Development was built with the DX11 Editor, Release with DX12; each executable was launched with both APIs. All four runs produced seven PNGs and changing, finite GPU height-query results in every scene. These are smoke checks, not complete feature or performance acceptance. Latest test-only additions do not change the player runtime.

## Scope of the 13 tests

- Three deterministic spectrum cases, including wavelength ordering/bounds.
- Three scene capture/query checks: FFT surface, underwater, planar reflection path.
- Known-height depth cache: measured 3.000003 m for a 3 m surface.
- Shadow atlas: nonzero occlusion readback after accumulation. Camera matrix/viewport restoration prevents LOD shadow drawing from changing subsequent camera projection.
- Buoyancy: 10 simulated seconds, masses 10/100/1000 kg, widths 1/2/3 m; bounded positions, water contact and velocity. Original SimpleFloatingObject uses ForceMode.Acceleration; this is not a mass-dependent displacement model.
- GPU query pressure: 128 submissions without readback callbacks, bounded outstanding requests, then recovery. The original queue discarded in-flight requests without releasing their registrars; the port applies backpressure before submitting more work. The initial player failure remains locally in Player-Development-DX11-attempt1.
- Flow: a 3 m/s input sampled through the original GPU provider; changing direction from +X to +Z yields 120 matching readbacks in each direction on both APIs, within 0.1 m/s. Visual advection and splines are not covered.
- Gerstner: FFT disabled, original ShapeGerstner enabled, fixed simulation timestep. Changing heights span about -2.96 to +1.41 m on DX12. This covers GPU generation/readback, not every analytic-wave option.
- Dynamic waves: FFT disabled, zero initial dynamic field, a 2 m sphere moves at 3 m/s. Height-field peak rises from 0 to 0.06384277 on DX11 and 0.06378174 on DX12; all readback values are finite. Long-term stability, damping accuracy and visual wake quality are not established.

The initial flow test used too few ticks for asynchronous GPU completion; it now allows five real seconds while retaining numerical and sample-count requirements. Gerstner timing now uses a fixed simulation timestep instead of machine speed. Failed fixture runs are retained as Flow-initial-DX11.xml and Gerstner-initial-DX11.xml.

## Visual review and limitations

All seven Development DX11 captures were inspected. Surface, underwater and reflection captures from Release DX12 were also inspected. They contain rendered content rather than the earlier black batch-mode screen captures. Current captures use an explicit URP camera render request. This is smoke-level review, not a side-by-side comparison with Built-in Crest.

Planar reflection still emits the unsupported Camera.layerCullSpherical warning. Spherical distance-culling and the old pixel-light quality option need URP equivalents. Underwater volume/portal/meniscus transitions, shadow convergence/cascade stability, caustic occlusion comparison, baking/reload, BoatProbes, clip/albedo/local-height fixtures, baked FFT CPU queries, LOD/seam stress, long-running GPU/native-memory profiling and full feature equivalence remain NOT TESTED.

## Source preservation

The original strict byte check passed for 597 meta/compute files before import. Unity upgraded three sprite metadata files (ArrowLeft, ArrowRight, Flask); their GUIDs are unchanged. The separate final GUID/compute check passes for all 597 files and explicitly lists metadata changes in guid-compute-preservation.json. The strict byte check must not be described as passing after importer normalization.

Raw logs and the large failed player attempt remain on disk and are excluded from Git. Structured reports, XML and current captures provide checked-in evidence. Earlier work-copy evidence is historical and does not supersede these deliverable results.
