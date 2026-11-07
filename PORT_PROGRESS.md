# Точка продолжения

Обновлено: **2026-11-07**. Unity **6000.6.5f1 + URP17.6.0**, Windows DX11/DX12 Render Graph. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап и операции

P00–P10 DONE в bounded/scoped-области PORT_PLAN. P11 ACTIVE: P11.1/P11.2/P11.3-A/B/C/D scoped DONE. **P11.3-F NEXT**, затем G/H, finalP11.4/P12. E optional/non-gating. Единственный порядок/критерии — PORT_PLAN, измерения — TEST_RESULTS, приёмка подсистем — FEATURE_MATRIX. Не создавать новый status/roadmap Markdown и не переоткрывать P09/P10.

Нет активных Unity Editor/Player/build операций. Последний diagnostic build `Builds/P11-SavedInputs`: success2026-11-07T14:56:08Z; fresh DX11/DX12 four-row reports/errors0/exit0, последний run завершён14:57:10Z. Перед запуском проверять actual processes/timestamps/Git. Wrapper P11-SavedInputs-build проверяет target-project occupancy; аналогичный guard использовать в следующих build wrappers. Не печатать raw GUI Editor command lines: Hub может включать access tokens.

## Точное следующее действие

**P11.3-F:** прочитать `Assets/Crest/URP/Tests/Editor/ExampleBakeTests.cs`, accepted ExampleBake results в TEST_RESULTS и corresponding matrix row. Frozen finite scope PORT_PLAN: compiled same3 original material clones, explicit fixture ContributeGI/secondary UVs, ProgressiveCPU baseline plus directional/nondirectional retained lighting variants. Existing Editor proof не повторять как новую приёмку; старый disposable baked fixture удалён, поэтому для Player нужны owned saved baked inputs. Не перепекать оригинальные сцены/не менять их static flags; E ProgressiveGPU lightmapper optional и не запускать.

Сохранить original material GI/pass/texture/colour flags; masked dry fixture selects Mask None, как accepted test. Same3 cubes +Lit floor, real baked directional sun, realtime lights/ambient/reflection off for captures. По existing gates: bright>5000, all-binding-off changed>1000, each3 receiver binding-off>20, exact restored RGBA; strict matching/errors0, actual serialized lightmap indices/ST/textures/lighting mode and per-receiver restore. Freeze exact two-mode state list before bake/build, no Cartesian expansion. F must prove compiled retention, not another Editor screenshot.

D1/D2/D3/C closed checks не повторять без changed code/конкретного дефекта. Fixture-only — targeted; runtime fixes группировать для P11.4. Никаких simultaneous Editors на проекте, не force-close пользовательскую работу. User разрешил normal close/save предыдущего target Editor, но окна уже закрылись до UI input; агент не выполнял close/save/discard.

## Последний checkpoint: D3 и закрытие D

D3 initial/reloaded/disabled/reenabled4 states/API Passed. Flow(3,0), height G2, clip.498039 vs original displaced-height cube reference.5, albedo(.7490196,0,0,.5607843) within original2/255. Outside exact0, complete disabled native fields0; height reporter deregister/re-register. Saved profile/material/shader/clock.5/0/camera(0,10,0) bindings; actual scene reload destroys old ocean/height object/four old RTs. Current frame=LOD9/18/23/28; no Player asset/clock reassignment. D1 FFT and D2 exported depth remain accepted separately; wholeD finite transfer scoped DONE, not all profiles/LODs/optical/performance acceptance.

Failed startup with occupied Editor preserved in occupied-editor-attempt.zip; first Player failed frame9/LOD8 before sampling, retained in frame-order-failure.zip. All4 measurements now via existing ModeCapture order300 after Ocean200; current-frame assertion/numeric thresholds unchanged. No production changes. Full logs errors0/exceptions0/missing variants0/active-RT warnings0; Unity immediate memory telemetry/compiler warnings retained, not leak-free acceptance.

Evidence P11-SavedInputs-summary/files/evidence.zip retains accepted raw/source/current serialized fixture/actual Editor+Player DLLs/packed bytes; failures separate. D1/D2 raw/source archives retained. Loose P11-SavedInputs/FFT/Depth-fixture-* copies already archived; do not stage duplicates.

## Обычные сборки и финальный milestone

**Normal Dev/Release устарели относительно production кода B.** Checkpoint0f833b4 =full121+actual interactive2/API/ValidateAssets/Dev+Release/4smokes;12 managed/packed hashes unchanged. B newer3 input guards +GraphicsSettings Custom Plain/Directional/Subtractive/ShadowMask retention have targeted compiled evidence but still require finalP11.4. Strict matching/fog/URP stripping remain enabled; original scene/bake/physics retained.

После F/G/hardware auditH обязателен **P11.4 full/API+actual interactive/ValidateAssets/Dev+Release/4 Player smokes**, затем P12 mandatory-item accounting/package. Не выдавать old full121 за final acceptance новых исходников.

## Сохраняемые изменения и ограничения

**Актуальный foreign baseline: P11-SavedInputs-foreign-files.json / foreign-before.zip (36 файлов, captured after live Editor closed).** Five LightingData assets BoatWakes/boat/threeboats/Examples/whirlpool differ from historical B baseline after live Editor activity; current bytes preserved through both diagnostic builds/runs. Не восстанавливать старые B версии. P11-Scenes-foreign-files.json remains historical provenance, not current restore target. Live snapshot also retained.

Foreign Ocean.mat (4 serialization fields), original baked LightingData/ReflectionProbe dirs,13 untracked historical diagnostics, dirty Buoyancy/ResourceLifecycle/focused XML не stage/reset/delete. Known TransparentPrimitive/OffWhite/PlayerSettings unchanged. Variant whitespace archived before restoring canonical bytes. Old P11-Scenes-retained-CrestResourceProbe_*/rejected-svc/empty-build-resources.meta не stage; earlier recursive shell cleanup rejected by policy, verified reversible moves preserved everything.

P10 GPU-cost/driver-memory/Intel/XR limits remain. Historical evictable2490368B NOT REPRODUCED/not fixed; old .015754165 restoration failure not explained by bounded controls. Не повторять неизменённые profiling windows/галерею воды.

Структурный аудит1431 файлов: missing meta0/duplicate GUID0/missing original0;597 protected GUID/compute checks pass,3 normalized sprite metas disclosed. В корне8 обязательных Markdown. Git checkpoint — `git log -1`.
