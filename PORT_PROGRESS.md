# Точка продолжения

Обновлено: **2026-10-23**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P07

P00–P06 DONE в областях PORT_PLAN.md; не начинать их заново. Полная приёмка подсистем — FEATURE_MATRIX.md. Здесь только checkpoint и действие продолжения; история измерений/ошибок находится в TEST_RESULTS.md, полный порядок — PORT_PLAN.md.

**Следующее действие:** P07 — расширить RayQueryTests отдельным focused regression для endpoints и 128-sample cap. Сначала прочитать RayTraceHelper.cs, существующие RayQueryTests и строку Ray queries в FEATURE_MATRIX/TEST_RESULTS: восемь прежних sloped/moving cases уже приняты, не повторять их без причины. Проверить точные start/end intersections с исходным Mathf.Sign поведением, 128 samples при ceil(length/step)>128 (ожидаемый warning и изменённый step), moved origin/direction. Для динамических endpoints учитывать, что returned heights относятся к предыдущим XZ positions, а оригинальный Trace сравнивает их с текущими ray positions; не скрывать расхождение private readiness gate и не обещать синхронный ray. Сохранять исходную ceil overshoot семантику, не вводить новый алгоритм без доказанного дефекта.

После ray edges внутри P07 остаются public time pause/resume (freeze уже проверен), original BoatDev BoatAlignNormal/RippleGenerator/LerpCam integration; broader choppy GPU query/latency coverage по FEATURE_MATRIX. Материалы примеров и полная visual equivalence — P09; стресс/стоимость — P08/P10, standalone/остаточные Editor controls — P11. Только после критериев P07 менять статус.

## Последний подтверждённый checkpoint

- **QueryLayoutTests: focused1/1 DX11 и DX12**, UTC15:48:49Z–15:48:50Z /15:49:20Z–15:49:21Z. Два владельца, десять фаз с8 distinct status0 timestamps: рост/уменьшение1–5 points, equal-total/different-layout packets, optional outputs, velocity-only growth, оба overload, owner remove/re-add. Проверяются все caller-visible успешные результаты; первый несовместимый layout обязан вернуть RetrieveFailed до нового dispatch. Max errors height/displacement0.00002336502m, normal0.000198831272, static velocity0. Fence только в test, не latency acceptance.
- **Runtime исправление QueryBase.cs:** сохранены point/normal counts каждого GPU packet, matching layout/output-size guards перед копированием, normal offset по dispatched point block, velocity layout/history guard. До исправления подтверждены два status0 с неверными данными: normals-only и equal-total смена packet layout. Оба failed XML/txt сохранены QueryLayout-*-initial-DX11. Public API/GUID/compute/формулы не менялись; same-layout position changes остаются асинхронными.
- **Свежая полная регрессия55/55 DX11 и55/55 DX12**, failed0/skipped0: UTC15:44:23Z–15:45:08Z /15:45:30Z–15:46:23Z. Прочитаны XML, все предыдущие Gerstner/boat/baked fixtures входят в full. После full добавлен только более строгий first-call status assertion в тест; финальные focused выше его подтверждают. Runtime после full не менялся.
- **Development/Release Players пересобраны**, четыре свежих runs×API с reflection options: exit0/Errors0,7 scene+4 option captures/run. Четыре buoyancy PNG просмотрены. Это smoke, не standalone numerical layout acceptance/performance. Some Player runs overlapped. Build-checkpoint.json содержит свежие source/assembly hashes и QueryBase. Повторная сборка из-за documentation-only edits не нужна.
- Audit1286 files: missing meta/duplicate GUID/missing original0;597 GUID/compute checks PASSED с прежними3 normalized sprite meta. Root Markdown8. Build pipeline убрал Performance assets; единственный generated ReflectionVertexVariants whitespace diff восстановлен. ProjectSettings/TimeManager и ProjectSettings/ProjectSettings без substantive diff. Активных Unity/Player операций нет; последний коммит смотреть git log -1. Полный порт/P07 ещё не завершены.

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
