# Точка продолжения

Обновлено: **2026-11-07**. Unity **6000.6.5f1 + URP17.6.0**, Windows DX11/DX12 Render Graph. Работать только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап и операции

P00–P10 DONE в bounded/scoped-области PORT_PLAN. P11 ACTIVE: P11.1/P11.2/P11.3-A/B/C scoped DONE. **P11.3-D NEXT**, затем F/G/H, finalP11.4/P12. E optional/non-gating. Единственный порядок/критерии — PORT_PLAN, измерения — TEST_RESULTS, приёмка подсистем — FEATURE_MATRIX. Не переоткрывать P09/P10 и не создавать новый status/roadmap Markdown.

Нет активных Unity Editor/Player/build операций. Последняя диагностическая сборка `Builds/P11-ReflectionModes`, build success2026-11-07T09:45:10Z; свежие DX11/DX12 Player exit0, reports7 rows/API/errors0. Проверять реальные processes/timestamps перед новым запуском.

## Точное следующее действие

**P11.3-D:** прочитать `BakedFFTIntegrationTests.cs`, `DepthCachePersistenceTests.cs`, `InputPersistenceTests.cs` и соответствующие строки FEATURE_MATRIX/результаты TEST_RESULTS. В PORT_PLAN зафиксировать конечные Player states этих существующих fixtures: saved baked FFT GPU/CPU/None provider switching/public query recovery/old native-owner release; exported depth cache reload without source geometry; saved local-height/flow bindings and fields. Затем отдельный serialized diagnostic Player transfer с owned assets и фактическими GUID/profile/query/native данными. Не перегенерировать demos, не перепекать исходные сцены, не вводить новую wave inversion/model law и не повторять Editor proof.

C1/C2/C3 не повторять без изменений или конкретного дефекта. Для fixture-only правок — адресный контроль; runtime fixes группировать для P11.4. Перед новой операцией читать TEST_RESULTS и relevant FEATURE_MATRIX row. Никаких одновременно запущенных Editors на этом проекте.

## Последний checkpoint: P11.3-C

- C1: пять mode/view states/API,320x180/1x/Forward; FullScreen/Portal/Volume outside, FlyThrough outside/inside. Changed57600/12996/12100/12100/57600, exterior border0, exact RGBA restores0, actual D32S8/composite execution. Volume inside исходно не поддерживается. Mobile исключён из Standalone; Windows PC-only inventory проверен, cross-tier switch NOT APPLICABLE. Исходный failed fixture assumption архивирован, настройки качества не менялись.
- C2: before/after transparents ×1x/4x,256²; actual Base/Overlay descriptors. Geometry/fog4096 pixels, above/background/projected exclusions/stack removal0 при исходном summedRGB15 threshold (не byte-identity assertion). Positive delta1/30,147456 finite foam samples peak.30249, exact native max0/same identity/frame46.
- C3: четыре renderer branches и LDR128/HDR-stencil128/MSAA256 reuse. Actual Forward/ForwardPlus/Deferred наблюдаются; cube152/609 pixels, supported4x, resized texture bound44/48 chunks. Owned serialized3-renderer build pipeline; исходные assets/settings сохранены. Все7 rows/API errors0.

Evidence: `P11-Modes-*`, `P11-Stack-*`, `P11-ReflectionModes-*` summary/files/raw-source-actual-DLL archives. C3 также packed diagnostic assets. Полные логи прочитаны; C1 сохраняет OceanDepthCache shutdown active-RT release warning, все группы раскрывают Unity immediate memory telemetry. Это не leak-free/performance/XR/full original-renderer acceptance. No C production renderer/shader/scene/quality edits.

## Обычные сборки и обязательный финальный milestone

**Обычные Dev/Release устарели относительно текущего production кода.** Normal checkpoint0f833b4 =full121+actual interactive2/API/ValidateAssets/Dev+Release/4smokes;12 managed/packed hashes сохранены. После него B изменил3 production input files и GraphicsSettings: отсутствующая Keyboard даёт neutral input, source physics/bias/GUI maintenance сохранены. Custom lightmap retention Plain/Directional/Subtractive/ShadowMask сохраняет нужную Main mixed-light комбинацию; unused dynamic modes excluded, strict matching/fog/URP stripping остаются. B20 compiled reports Passed; это не финальный full regression.

После D/F/G и hardware auditH обязателен **P11.4 full/API+actual interactive/ValidateAssets/Dev+Release/4 Player smokes**, затем P12 original mandatory-item accounting/package. Старую full121 приёмку не выдавать за приёмку более новых исходников.

## Сохраняемые изменения и ограничения

Foreign Ocean.mat (4 serialization fields), original baked LightingData/ReflectionProbe dirs,13 untracked historical diagnostics, dirty Buoyancy/ResourceLifecycle/focused XML не stage/reset/delete.36 fingerprints в `P11-Scenes-foreign-files.json` проверены. Known TransparentPrimitive/OffWhite/PlayerSettings unchanged. Generated variant whitespace архивирован перед восстановлением canonical bytes.

Loose backups outside Assets (`P11-Scenes-retained-CrestResourceProbe_*`, rejected-svc, empty-build-resources.meta) не stage: нужные bytes уже в raw archives. Ранее recursive shell cleanup отклонён automatic policy; после проверки путей/GUID сделаны безопасные обратимые moves, всё сохранено. Не удалять неизвестные файлы.

P10 GPU-cost/driver-memory/Intel/XR limits явны. Historical evictable2490368B NOT REPRODUCED/not fixed; old .015754165 history-restoration failure не объяснён новыми bounded controls. Не повторять неизменённые profiling windows/галерею воды.

Структурный аудит1419 файлов: missing meta0/duplicate GUID0/missing original0;597 GUID/compute checks pass,3 normalized sprite metas disclosed. В корне8 обязательных Markdown. Точный Git checkpoint — `git log -1`.
