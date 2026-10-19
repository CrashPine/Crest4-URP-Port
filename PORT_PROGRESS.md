# Port progress — 2026-10-19

Required target: the user's installed **Unity 6000.6.5f1**, URP **17.6.0**, branch `port/unity-6.6-urp`. Unity 6.3 is no longer required. The Unity CLI install path was restored to its original Program Files directory.

Implemented: URP ocean surface/background copy; SRP camera callback; planar reflection render request; graph underwater masks, original artifact-fix compute and composite/volume passes; depth-only URP cache capture and owned-resource cleanup; temporal-shadow graph accumulation with original jitter/history and camera matrix restoration; caustic occlusion using the URP shadow atlas. GPU queries apply backpressure to preserve in-flight registrars under CPU submission bursts.

Verified in the deliverable checkout: **14/14 Editor tests on DX11 and DX12**; Windows **Development and Release** builds; all seven scenes in each executable on both APIs with **Errors=0** and seven captures per run. Extended tests cover a known flow vector and runtime direction change, Gerstner generation without FFT, and dynamic-wave injection from a moving sphere. Details and limits are in TEST_RESULTS.md.

All 597 original meta/compute files pass the dedicated GUID/compute check. Three sprite importer metadata files were normalized by Unity; this is disclosed separately from strict byte preservation.

Next work: reflection culling/lighting equivalence, remaining volume/portal and simulation-input fixtures, foam and LOD transition measurements, baked queries and BoatProbes, sustained resource/performance profiling, and comparison with the original renderer. XR remains NOT TESTED. The port is incomplete and is not production-accepted.

Additional checkpoint: 12 underwater scene reloads pass on DX11 and DX12 with stable tracked texture count/native size (22; 51,848,342 bytes). This adds bounded lifecycle coverage, not a blanket no-leak claim. Launch scripts reject stale reports/captures.
