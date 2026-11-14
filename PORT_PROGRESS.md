# Точка продолжения

Обновлено: **2026-11-14**. Unity **6000.6.5f1 + URP17.6.0**, Windows DX11/DX12 Render Graph. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап и операции

P00–P10 DONE в bounded/scoped-области PORT_PLAN. P11.1/P11.2/P11.3-A/B/C/D/F scoped DONE. G1b mouse controls and G2 live DX11 Editor scoped DONE. **P12 ACTIVE**. P11.4 and the Windows shader follow-up regression are complete. G1a M/N and G1b G/F keyboard delivery remain open. H Intel format scoped DONE and physical XR blocked.

The first P11.4 Release DX11 attempt exited 1 with no fresh summary. Targeted retry on the same binary passed, then Release DX12 passed. The failure archive and accepted reports are separate. Check actual processes and artifact timestamps before any further run.

## Точное следующее действие

**P12:** the 125+2/API regression and four normal Player smokes passed. Updated Development and Release archives carry suffix `066a5c0`. The combined strict probe build strips the optional variants before the first shader callback. Separate builds (`Evidence/P12-CompiledProbes-run.ps1 -Probe Box` or `-Probe Atlas`) retain the active variant and log no missing shader variants. Both still report zero response on DX11/DX12. Turning the Forward probe off also gives zero changed pixels, including non-batch runs on the same binary. Next inspect actual non-clear water pixels, camera culling's visible probe textures and a URP/Lit comparison using the same probes before editing the production shader. The diagnostic runs are complete and archived. Native M/N and G/F and physical XR remain open.

H is bounded: Intel Iris Xe DX11/DX12 actual mask R32_SFloat; same-build NVIDIA DX11 R16_SFloat. XR remains NOT TESTED until a physical display and Unity provider are available. Older mask probe logs include 7 LOD frame warnings and 1 active depth-cache RT release warning each; keep the latter in final resource disposition alongside F JobTempAlloc. No broad P10 replay.

F/D/C closed checks не повторять без changed code/конкретного дефекта. Fixture-only — targeted; runtime fixes группировать для P11.4. Никаких simultaneous Editors, не force-close работу. User ранее разрешил normal close/save target Editor; тогда окна уже закрылись до UI input, агент не выполнял close/save/discard.

## Последний checkpoint: F

Two real owned ProgressiveCPU bakes NonDirectional0/CombinedDirectional1, same3 original material clones+Lit floor/UV2/ContributeGI. Preserved source GI/pass flags and saved mode/atlas/index/ST. Player actual640x360/1x: bright/all binding-off changes20436; each material968/1118/1300; all restores RGBA exact0, errors0/API. Actual direction atlas only mode1. Visible source emission remains in all-off image, not asserted black. DX12 Combined on/off inspected. No original scene/static/material/shader change.

First DX11 FAILED after NonDirectional success: Editor enum alias 'Single, Dual' vs Player 'CombinedDirectional' at same value1. Raw/source/DLL/packed failure retained. Numeric modeValue gate fixes fixture only.58 original baked scene/material/mesh/atlas/metadata files reused byte-identically, only manifest schema/canonical mode label changed; no repeated CPU bakes. Original/reused fixtures +raw/source/actual Editor+Player DLLs/packed bytes in P11-Lightmaps-summary/files/evidence.zip; enum-alias-failure/generated-metadata archives separate. Loose fixture/reused copies do not stage duplicates.

BOTH async bake/build and no-bake reuse Editor shutdowns emit2 JobTempAlloc warnings; owner/cause unknown, NOT FIXED. Final Players have0 JobTemp warnings/errors; Unity immediate memory telemetry remains. No leak-free/performance claim; retain for P11.4 resource disposition, no broad P10 replay. Temporary native preload/URP-path/variant whitespace archived and restored after exact-diff checks; PerformanceTestRun Resources auto-cleaned by Unity.

## Обычные сборки и финальный milestone

**Normal Dev/Release:** `Build-checkpoint.json` records the published 125+2 tests per API, ValidateAssets, both builds and four passing smokes. These binaries include the current box projection and atlas shader code. The demo pipeline uses the single-probe path.

P12 follows the fresh build evidence and resource warning review. F Editor JobTempAlloc warnings remain in the resource record.

## Сохраняемые изменения и ограничения

**Current foreign baseline: P11-SavedInputs-foreign-files.json / foreign-before.zip (36 файлов).** Five LightingData assets BoatWakes/boat/threeboats/Examples/whirlpool differ from historical B baseline after live Editor activity; current bytes unchanged through F builds/runs. Не восстанавливать старые B версии. P11-Scenes-foreign-files.json remains historical only.

Foreign Ocean.mat (4 fields), original baked LightingData/ReflectionProbe dirs,13 untracked historical diagnostics, dirty Buoyancy/ResourceLifecycle/focused XML не stage/reset/delete. Known TransparentPrimitive/OffWhite/PlayerSettings unchanged. Old loose SavedInputs/FFT/Depth fixtures and P11-Scenes-retained-CrestResourceProbe_*/rejected-svc/empty-build-resources.meta уже archived, не stage. Earlier recursive cleanup rejected by policy; verified reversible moves preserved everything.

P10 GPU-cost/driver-memory/Intel/XR limits remain. Historical evictable2490368B NOT REPRODUCED/not fixed; old .015754165 restoration failure not explained by bounded controls. Не повторять неизменённые profiling windows/галерею воды.

Структурный аудит1443 файлов: missing meta0/duplicate GUID0/missing original0;597 protected GUID/compute checks pass,3 normalized sprite metas disclosed. В корне8 обязательных Markdown. Git checkpoint — `git log -1`.
