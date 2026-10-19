# Crest 4 → Unity 6.6 URP: work in progress

This is an isolated port of MIT Crest 4.23.0, not a completed or production-accepted release.
The target is Unity 6000.6.5f1 + URP 17.6, as explicitly requested on 2026-10-19. The deliverable passes 13 Editor tests on both DX11/DX12 and seven-scene smoke runs in both Development and Release players. Full feature acceptance remains incomplete.

## Current implementation

Original FFT, simulation compute assets, LOD geometry, physics/query code and GUIDs are retained.
The ocean surface uses URP lighting, fog and environment sampling. `CrestBackgroundFeature`
provides Render Graph background color, underwater mask/composite and temporal-shadow scheduling.
Planar reflections and runtime depth capture use URP camera render requests.

Seven scene fixtures are in `Assets/CrestPortDemos`. They are not yet accepted demonstrations
of the complete original feature set. See FEATURE_MATRIX.md and TEST_RESULTS.md for actual coverage.

## Reproduce

Open this folder with Unity 6000.6.5f1. Allow package resolution/import before testing.
Use the generated `Pipeline.asset` and `Renderer.asset` in Assets/CrestPortDemos.
The active renderer must include CrestBackgroundFeature; camera depth/intermediate color are required.

From PowerShell, `Tools/Run-Unity.ps1 -Editor <absolute Unity.exe path> -Action EditMode -GraphicsAPI DX11`
runs the tests with a real graphics device. DX12 is a separate run. Actions ValidateAssets,
Development and Release perform the corresponding checks/builds. The script enforces the target Editor.
`Tools/Verify-Source.ps1 -Upstream <official source checkout>` verifies original meta/compute bytes.
`Tools/Verify-GuidAndCompute.ps1 -Upstream <official source checkout>` separately verifies preserved GUIDs and compute bytes, reporting importer metadata changes explicitly.

Do not regenerate fixtures over customized scenes: Generate validation scenes currently recreates
its generated assets. No main game project was modified.

## Documents

- SOURCE_BASELINE.md: source tag, commit and licensing.
- PORT_PLAN.md / PORT_PROGRESS.md: implementation plan and current checkpoint.
- FEATURE_MATRIX.md: subsystem preservation and acceptance gaps.
- MIGRATION.md: integration and retained extension contracts.
- TEST_RESULTS.md / Evidence: measured results and limitations.
- THIRD_PARTY_NOTICES.md / LICENSE.Crest: license notices.



Standalone builds provide scene navigation, wind/sea-level sliders and camera height controls.
`Tools/Run-Player.ps1 -Player <absolute CrestPort.exe path> -GraphicsAPI DX11`
runs seven scene smoke captures and GPU queries. Results are written under Evidence/Player-Development-DX11.
This is a smoke test, not full rendering or performance acceptance.
For the Release executable, pass `-Label Release`; use `-GraphicsAPI DX12` for DirectX 12.
Executables are available at `Builds/Development/CrestPort.exe` and `Builds/Release/CrestPort.exe`.
