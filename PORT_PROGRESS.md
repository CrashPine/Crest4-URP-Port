# Точка продолжения

Обновлено: **2026-10-23**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P07

P00–P06 DONE в областях PORT_PLAN.md; не начинать их заново. Полная приёмка подсистем — FEATURE_MATRIX.md. Здесь только checkpoint и действие продолжения; история измерений/ошибок находится в TEST_RESULTS.md, полный порядок — PORT_PLAN.md.

**Следующее действие:** P07 — real-wave GPU height/normal/velocity accuracy и ownership/count changes. Прочитать QueryContractTests, GerstnerGraphicsTests и QueryBase/QueryDisplacements: current normal/velocity acceptance — известная плоскость, не реальные волны. Сделать новый focused fixture с контролируемой оригинальной wave shape, фиксированными world query points и независимой численной reference. Проверять полный status, distinct result timestamps и height на dispatch time. Для velocity требуется положительный Unity Time.time dt: не использовать Time.timeScale=0 как доказательство GPU velocity. При смене owner/count требовать восстановление валидных результатов, учитывать исходные invalidation flags; не ослаблять assertions ради ready bool.

После этого внутри P07 остаются ray endpoint/sample-cap/moving endpoint checks, public time pause/resume (freeze уже проверен), original BoatDev BoatAlignNormal/RippleGenerator/LerpCam integration. Материалы примеров и полная visual equivalence относятся к P09; стресс/стоимость — P08/P10, standalone/остаточные Editor controls — P11. Только после критериев P07 запускать full milestone и менять статус.

## Последний подтверждённый checkpoint

- **BoatProbesControlsTests: focused 2/2 DX11 и DX12**, failed=0/skipped=0. Прямой throttle/coast/reverse, удвоение массы, signed turning/turningHeel и отпускание руля; реальные накопленные силы/моменты считывает пассивный observer после original FixedUpdate. Per-probe cap 1000 N даёт total4000 N; zero/infinity и unequal weights проходят. XML: BoatControls-DX11.xml / DX12.xml; числа — BoatControls-* и BoatBuoyancyCap-*.
- **BakedFFTIntegrationTests: focused 1/1 DX11 и DX12**. Реальный choppy bake (chop1.6), settings asset unload/reimport, автоматический GPU→CPU→None→GPU→CPU через OceanRenderer settings hash, освобождение прежних buffers/CPU arrays. CPU heights/normals/velocities сравнены с независимым scalar sampler + Newton inversion, displacement — с raw data sampling. Sea level+1.25 и реальный LoadSceneAsync с повторным назначением settings проходят; это не saved-scene binding acceptance. XML BakedIntegration-DX11.xml / DX12.xml, txt BakedIntegration-*.
- Ранее P07: BoatProbes flat mass/righting и FFT/wind/spectrum/frozen time — по1/1 на API, отдельные BoatProbes/BoatProbesWaves reports; known-plane queries, rays и real FFT bake также закрыты в своих scoped checks. Не повторять их без новой причины. Wave boat fixture содержит test-only GPU fence; production pacing/performance не заявлены.
- **Последняя полная регрессия остаётся 48/48 DX11 и DX12** от 2026-10-23, UTC DX11 10:36:57Z–10:38:02Z / DX12 10:38:48Z–10:40:08Z. Full XML EditMode-DX11.xml / DX12.xml сохранены. Последующие5 новых тестов прошли focused; свежего full53/53 нет. После добавления batch-mode guard в BakedFFTTests была отдельная focused DX11 перепроверка.
- Последние изменения только Editor tests/.meta и документация/численные evidence. Runtime/shader/сохранённые demo scenes не менялись: Player/full suite и базовые ocean PNG не повторялись. Предыдущие четыре Development/Release×API Player runs относятся к их runtime checkpoint, не новым Editor-only fixtures.
- Control fixture не присваивает fixedDeltaTime: float round-trip сдвигал Unity rational TimeManager count на1. Только generated изменение восстановлено; финальные focused2/2 DX11 (11:19:12Z–11:19:15Z) / DX12 (15:13:16Z–15:13:19Z) подтверждают исходный canonical TimeManager blob. Перед full учитывать аналогичный setter в старых fixtures, не записывать случайную нормализацию в deliverable.
- Initial control fixture 0/2: вложенный Setup с EnterPlayMode не дал Start/query readiness. Отчёт BoatControls-play-transition-initial-DX11.xml и два txt сохранены. EnterPlayMode перенесён в top-level UnityTest, добавлен Assert Application.isPlaying; пороги/production code не менялись. Новые CPU integration runs прошли с первого раза.
- Audit **2026-10-23T15:14:28.9016017Z**: **1282 files**, missing meta/duplicate GUID/missing upstream=0. Root Markdown count=8, временный BakedIntegration asset folder удалён с проверкой absolute parent/GUID. Активных Unity Editor/Player операций нет. Последний коммит смотреть `git log -1`.

## Контракты, которые уже выяснены

- QueryBase.RetrieveSucceeded проверяет только RetrieveFailed; helper bool не подтверждает velocity. Timestamp GPU запросов — Unity Time.time. CPU baked provider использует OceanRenderer.CurrentTime и backward height difference0.06s, возвращает только vertical velocity; paused Unity time в его fixture допустим. Сильные folds/все bake configurations этим не приняты.
- Serialized feature changes могут перестроить OceanRenderer и сбросить time-provider stack. Устанавливать clock после настройки, проверять ocean.TimeProvider. Settings source toggle пересоздаёт provider через rebuild; не приписывать это произвольной смене baked data reference.
- EnableGraphicsInBatchMode=true задавать до OpenScene. Для камерных fixtures meshes проверять под OceanRenderer.Root (HideAndDontSave), off-screen chunk material может быть старым до OnWillRenderObject. RT world coordinates брать из snapped cascade, не предполагать центр после reload.
- Foam StepClock/AdvanceExactSteps считает реально потреблённые simulation impulses; yield ticks не являются substeps. При dt0 после impulses ожидать три ocean updates перед ping-pong snapshot. Контракты — MIGRATION.md и существующие fixtures.
- RayTraceHelper false совмещает unready/miss; minLength/ceil sampling сохраняют original overshoot. BoatProbes.InWater всегда true: contact проверять по actual probe heights, не по этому флагу. Probe COM offset, ForceMode и per-probe cap описаны в MIGRATION.md.
- P06 реальные DX11 Inspector/Scene view gestures/Undo/Redo/save/reopen закрыты; owner JSON/owned assets удалены, errors file пуст. Helper prepare/select/observe не подменял UI edits. Не запускать Prepare повторно ради тех же проверок. Остальные controls/live GPU editing — P11.
- ResourceLifecycle12 reloads:22 tracked RT/51,848,342 bytes — bounded fixture, не VRAM/leak profiling. Устаревшее ожидание60 ticks заменено wall deadline+60 attempts+8 ready; failed evidence сохранён. Все остальные failed harness runs также находятся в Evidence/TEST_RESULTS.md.
- 597 original GUID/meta/compute checks PASSED; три Unity-normalized sprite meta ArrowLeft/Right/Flask остаются известным отдельным strict byte-preservation FAILED. Не приобретать source заново. Последние production code changes FFTBaker и OceanDepthCache только #if UNITY_EDITOR; source hash может отличаться от Player checkpoint без изменения runtime assembly.

## Операции и чужие изменения

Перед новой Unity командой снова проверить процессы и свежесть XML. Не запускать два Editor на проекте. Не запускать GenerateScenes поверх demos. Сценарии этой сессии временные, project assets удалять только по проверенным parent/GUID.

Сохранены изменения вне доработки: нормализация `Assets/Crest/Crest/Materials/Ocean.mat` и baked LightingData/ReflectionProbe folders исходных BoatWakes/boat/threeboats/Examples/main/whirlpool. Их оценка — P09; не stage/reset/delete и не выполнять blanket add/clean. Assets/Resources/Performance* создаёт build pipeline; ReflectionVertexVariants.asset может получать сериализационные пробелы от Unity.

Не создавать новые progress/status/roadmap Markdown. Повторные картинки базового океана/грузиков не нужны для Editor-only numerical fixtures; graphics acceptance требует своих контролируемых изображений при изменении соответствующего rendering path. План не дублировать здесь: PORT_PLAN.md, результаты — TEST_RESULTS.md.
