# Точка продолжения

Обновлено: **2026-11-07**. Unity **6000.6.5f1 + URP17.6.0**, Windows DX11/DX12 Render Graph. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап и операции

P00–P10 DONE в bounded/scoped-области PORT_PLAN. P11 ACTIVE: P11.1/P11.2/P11.3-A/B/C/D/F scoped DONE. **P11.3-G NEXT**, затем H, finalP11.4/P12. E optional/non-gating. Единственный порядок/критерии — PORT_PLAN, измерения — TEST_RESULTS, приёмка — FEATURE_MATRIX. Не создавать новый status/roadmap Markdown и не переоткрывать P09/P10 без конкретного нового дефекта.

Нет активных Unity Editor/Player/build операций. Last isolated `Builds/P11-Lightmaps`: reused-fixture build success20:31:11Z, fresh DX11/DX12 two-row reports/errors0/exit0, последний run20:32:20Z. Перед запуском сверять actual processes/timestamps/Git. Build wrappers проверяют target-project occupancy. Не печатать raw GUI Editor command lines: Hub может включать access tokens.

## Точное следующее действие

**P11.3-G:** сначала прочитать source ExamplesController/CamController/OceanDebugGUI/VisualiseCollisionArea и прежние interactive evidence в TEST_RESULTS. Зафиксировать конечный список UI/input states в PORT_PLAN перед действием: original Examples N/M release navigation через реальный supported event/UI path; Inspector/scene/profile изменение с фактическим native GPU update; configured OceanDebugGUI/VisualiseCollisionArea display. Public Next/Previous вA и прямой method invocation не заменяют доставку клавиш/жестов. Проверить доступный OS/device/window path, не создавать fake keyboard для acceptance; unavailable physical input явно описать. Использовать purpose-built tool/CLI если достаточен; для Windows UI — computer-use skill, выбирать ровно target окно и сохранять пользовательскую работу. Исходные scenes/profiles не перезаписывать; использовать owned copies/fixtures и native causal observations.

H затем: prerequisite audit physical XR/Intel-specific runtime path. Не повторять рендер при прежнем отсутствующем hardware; retained source/emulated vendor string не physical acceptance. После G/H — finalP11.4.

F/D/C closed checks не повторять без changed code/конкретного дефекта. Fixture-only — targeted; runtime fixes группировать для P11.4. Никаких simultaneous Editors, не force-close работу. User ранее разрешил normal close/save target Editor; тогда окна уже закрылись до UI input, агент не выполнял close/save/discard.

## Последний checkpoint: F

Two real owned ProgressiveCPU bakes NonDirectional0/CombinedDirectional1, same3 original material clones+Lit floor/UV2/ContributeGI. Preserved source GI/pass flags and saved mode/atlas/index/ST. Player actual640x360/1x: bright/all binding-off changes20436; each material968/1118/1300; all restores RGBA exact0, errors0/API. Actual direction atlas only mode1. Visible source emission remains in all-off image, not asserted black. DX12 Combined on/off inspected. No original scene/static/material/shader change.

First DX11 FAILED after NonDirectional success: Editor enum alias 'Single, Dual' vs Player 'CombinedDirectional' at same value1. Raw/source/DLL/packed failure retained. Numeric modeValue gate fixes fixture only.58 original baked scene/material/mesh/atlas/metadata files reused byte-identically, only manifest schema/canonical mode label changed; no repeated CPU bakes. Original/reused fixtures +raw/source/actual Editor+Player DLLs/packed bytes in P11-Lightmaps-summary/files/evidence.zip; enum-alias-failure/generated-metadata archives separate. Loose fixture/reused copies do not stage duplicates.

BOTH async bake/build and no-bake reuse Editor shutdowns emit2 JobTempAlloc warnings; owner/cause unknown, NOT FIXED. Final Players have0 JobTemp warnings/errors; Unity immediate memory telemetry remains. No leak-free/performance claim; retain for P11.4 resource disposition, no broad P10 replay. Temporary native preload/URP-path/variant whitespace archived and restored after exact-diff checks; PerformanceTestRun Resources auto-cleaned by Unity.

## Обычные сборки и финальный milestone

**Normal Dev/Release устарели относительно production кода B.** Checkpoint0f833b4 =full121+actual interactive2/API/ValidateAssets/Dev+Release/4smokes;12 managed/packed hashes unchanged. B newer3 input guards +GraphicsSettings Custom Plain/Directional/Subtractive/ShadowMask retention have targeted compiled evidence but still require finalP11.4. Strict matching/fog/URP stripping enabled; original scene/bake/physics retained. Subsequent C/D/F diagnostic-only source has no normal runtime change.

После G/H обязательны **P11.4 full/API+actual interactive/ValidateAssets/Dev+Release/4 Player smokes**, source/build fingerprints/resource outcomes, затем P12 mandatory-item accounting/package. Не выдавать old full121 за final acceptance новых исходников. F Editor JobTemp warnings need explicit final disposition, not silently dropped.

## Сохраняемые изменения и ограничения

**Current foreign baseline: P11-SavedInputs-foreign-files.json / foreign-before.zip (36 файлов).** Five LightingData assets BoatWakes/boat/threeboats/Examples/whirlpool differ from historical B baseline after live Editor activity; current bytes unchanged through F builds/runs. Не восстанавливать старые B версии. P11-Scenes-foreign-files.json remains historical only.

Foreign Ocean.mat (4 fields), original baked LightingData/ReflectionProbe dirs,13 untracked historical diagnostics, dirty Buoyancy/ResourceLifecycle/focused XML не stage/reset/delete. Known TransparentPrimitive/OffWhite/PlayerSettings unchanged. Old loose SavedInputs/FFT/Depth fixtures and P11-Scenes-retained-CrestResourceProbe_*/rejected-svc/empty-build-resources.meta уже archived, не stage. Earlier recursive cleanup rejected by policy; verified reversible moves preserved everything.

P10 GPU-cost/driver-memory/Intel/XR limits remain. Historical evictable2490368B NOT REPRODUCED/not fixed; old .015754165 restoration failure not explained by bounded controls. Не повторять неизменённые profiling windows/галерею воды.

Структурный аудит1435 файлов: missing meta0/duplicate GUID0/missing original0;597 protected GUID/compute checks pass,3 normalized sprite metas disclosed. В корне8 обязательных Markdown. Git checkpoint — `git log -1`.
