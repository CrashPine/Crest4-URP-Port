# Точка продолжения

Обновлено: **2026-11-07**. Unity **6000.6.5f1 + URP17.6.0**, Windows DX11/DX12 Render Graph. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап и операции

P00–P10 DONE в bounded/scoped-области PORT_PLAN. P11 ACTIVE: P11.1/P11.2/P11.3-A/B/C scoped DONE. **P11.3-D ACTIVE: D1/D2 scoped DONE, D3 NEXT**. Затем F/G/H, finalP11.4/P12; E optional/non-gating. Единственный порядок/критерии — PORT_PLAN, измерения — TEST_RESULTS, приёмка подсистем — FEATURE_MATRIX. Не создавать новый status/roadmap Markdown и не переоткрывать P09/P10.

Нет активных Unity Editor/Player/build операций. Последняя build `Builds/P11-SavedDepth`: success2026-11-07T14:41:46Z; fresh DX11/DX12 five-row reports/errors0/exit0, последний run завершён14:42:54Z. Перед новым запуском проверять actual processes/timestamps/Git.

## Точное следующее действие

**P11.3-D3:** прочитать `Assets/Crest/URP/Tests/Editor/InputPersistenceTests.cs`, соответствующие TEST_RESULTS/FEATURE_MATRIX строки. Перенести только existing saved-scene input fixture в отдельный compiled Player: четыре states/API initial/reloaded/disabled/reenabled, без повторного Editor proof.

Owned saved materials Flow/Albedo/Height/Surface, Depth profile varying-height=true, serialized clock.5/delta0, camera(0,10,0), ShapeWaves disabled. Flow native(3,0), height offset+2, original cube clip SDF учитывает displaced height2 (не плоская ошибочная формула), albedo premultiplied(.75,0,0,.5625), outside0. Оригинальные tolerances vector/height.01, clip.005, RGBA2/255; disable clearing/height reporter deregister, re-enable recovery. Actual scene reload destroys old ocean/height input/four old flow/clip/albedo/depth RTs. Editor GUID/source serialized graph + actual Player object/profile/clock/native bindings; runtime AssetDatabase lookup недоступен. Freeze exact state list in existing PORT_PLAN before building.

Не GenerateScenes/не перепекать исходные сцены. D1/D2/C закрытые checks не повторять без изменённого кода/конкретного дефекта. Fixture-only — focused/targeted; runtime fixes группировать для финального P11.4. Никаких simultaneous Editors на проекте.

## Последний checkpoint: D1/D2

- D1:7 states/API Passed; real FFT16²/two LODs/16 frames/period4/chop1.6/one octave7/wind60 saved scene/profile/data/clock.32768 raw halves hash0e26db3e... preserved through actual scene reload without profile/data assignment.40 CPU reference points/API, max height2.99514e-6m; original .01/.02/.1/.0001 limits. GPU->CPU->None->GPU->CPU/+1.25sea/reload controls: three2-buffer GPU releases and two16-position CPU releases/API, public source-off GPU/None recovery. GPU velocity unrequested under timeScale0 (metric-1/velocityChecked=false); CPU ocean-time derivative remains checked.
- D1 initial build FAILED before bake/Player because CustomMonoBehaviour defers runInEditMode=true via Invoke. Fixture uses the same supported property to invoke normal Unity lifecycle; no singleton/reflection injection/production changes. Exact failed source/log/Editor DLLs in initialization-failure.zip.
- D2: existing accepted DepthBaked-Direct3D11.exr reused byte-for-byte (86f77cab...), no repeated Editor bake. Owned SingleChannel/Red/linear/RFloat/uncompressed import/prefab/scene;4096 imported floats hashb97c88ce... match every state/API. Native-4.5/-1.5/-Infinity/-1.5/reload-4.5, original.02m; source layer30 absent, CacheTexture null throughout; scene reload destroys old ocean/depth RT. No new realtime-cache release claim (prior Editor control only).

Evidence: P11-SavedFFT and P11-SavedDepth summary/files/evidence.zip contain raw/source/serialized fixture/actual Editor+Player DLLs/packed bytes. Complete Player logs errors0/exceptions0/missing variants0/active-RT warnings0; Unity immediate memory telemetry/compiler warnings retained, not leak-free/performance/all-profile acceptance. No production runtime/shader/source scene edits in D1/D2. C1/C2/C3 prior details stay in TEST_RESULTS, not repeated here.

## Обычные сборки и финальный milestone

**Normal Dev/Release устарели относительно production кода B.** Checkpoint0f833b4 =full121+actual interactive2/API/ValidateAssets/Dev+Release/4smokes;12 managed/packed hashes unchanged. B newer3 input guards +GraphicsSettings Custom Plain/Directional/Subtractive/ShadowMask retention have targeted compiled evidence but still require finalP11.4. Strict matching/fog/URP stripping remain enabled; original scene/bake/physics retained.

После D3/F/G/hardware auditH обязателен **P11.4 full/API+actual interactive/ValidateAssets/Dev+Release/4 Player smokes**, затем P12 mandatory-item accounting/package. Не выдавать old full121 за final acceptance новых исходников.

## Сохраняемые изменения и ограничения

Foreign Ocean.mat (4 serialization fields), original baked LightingData/ReflectionProbe dirs,13 untracked historical diagnostics, dirty Buoyancy/ResourceLifecycle/focused XML не stage/reset/delete.36 fingerprints `P11-Scenes-foreign-files.json` checked. Known TransparentPrimitive/OffWhite/PlayerSettings unchanged. Variant whitespace archived before restoring canonical bytes.

Loose P11-SavedFFT/SavedDepth-fixture-* copies already in raw archives; do not stage duplicates. Old P11-Scenes-retained-CrestResourceProbe_*/rejected-svc/empty-build-resources.meta тоже не stage. Ранее recursive shell cleanup rejected by automatic policy; verified reversible moves preserved everything. Не удалять неизвестные файлы.

P10 GPU-cost/driver-memory/Intel/XR limits remain. Historical evictable2490368B NOT REPRODUCED/not fixed; old .015754165 restoration failure not explained by bounded controls. Не повторять неизменённые profiling windows/галерею воды.

Структурный аудит1427 файлов: missing meta0/duplicate GUID0/missing original0;597 protected GUID/compute checks pass,3 normalized sprite metas disclosed. В корне8 обязательных Markdown. Git checkpoint — `git log -1`.
