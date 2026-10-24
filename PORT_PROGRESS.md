# Точка продолжения

Обновлено: **2026-10-24**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P08

P00–P07 DONE в определённых PORT_PLAN областях, не весь порт. P07 functional milestone подтверждён full66/66 DX11/DX12; единственный план — PORT_PLAN.md, acceptance/limitations — FEATURE_MATRIX/TEST_RESULTS.

**Следующее действие:** P08 — начать focused LOD scale/snap/transition fixture. Прочитаны LodTransform.cs и OceanRenderer.LateUpdateScale: texelWidth=4*CalcLodScale(lod)/resolution, snapped centre=root−Repeat(root,texelWidth), scale power-of-two из viewer height с band4m/min/max clamp и wave displacement adjustment; Current/Previous buffers и ScaleDifferencePow2 должны соответствовать действительным переходам. Проверить camera translation через positive/negative snap boundaries и height ascent/descent через scale boundaries, history transforms/finite GPU fields и query recovery. Не выдавать такую численную проверку за отсутствие visual seams: seam captures/edge/LOD images — отдельное покрытие P08. Не пересоздавать сцены GenerateScenes и не повторять P07 numerical fixtures без изменений соответствующего пути.

Дальнейшие P08 критерии — PORT_PLAN: floating/shifting origin, water bodies/culling, multicamera, MSAA/resolution/dynamic resolution; XR только если доступен. P09 visual/material comparison, P10 profiling, P11 final standalone/GUI, P12 packaging остаются. Сразу сохранить/учесть memory warning ниже; P10 должен начать с его allocation stacks, нельзя объявлять leak acceptance по зелёному XML.

## Последний подтверждённый checkpoint

- **Full66/66 DX11/DX12**, failed0/skipped0: UTC2026-10-24 10:14:58Z–10:16:19Z /10:16:42Z–10:18:20Z. Все testcase result nodes прочитаны. Evidence/P07-Milestone.json фиксирует XML hashes и warnings. Scoped P07 пункты1–5 DONE. Full порт не завершён.
- **Открыто P10:** оба полных Editor при shutdown пишут «Persistent allocates 3 individual allocations». Стеков/owner пока нет; NativeArray196608 в shutdown label не доказывает размер/владельца этих утечек. Focused new runs предупреждения не дали. Требуется воспроизведение с NativeLeakDetection.EnabledWithStackTrace/изоляция; не скрывать и не считать zero-leak.
- BoatCameraJoint focused1/1 обоих API (UTC10:14:19Z–10:14:22Z /10:14:39Z–10:14:42Z): original physical1000kg hull на chop1.6 Gerstner и flow+/-.75, actual main-camera LerpCam. По401 fixed samples на8s phase, hull dx+4.99361/-5.42013m;802 force checks max.00100662N;972 camera Updates max position/look errors2.39e-7/3.32e-7. Initial invalid flow-only velocity oracle replaced by full force oracle including wave velocity; failed evidence preserved.
- WaveRay/RemainingClock focused1/1 каждого API подтверждены и вошли в full. Failed coroutine-post bookkeeping/autoplay fixture reports preserved, source/tolerances unchanged. Details — TEST_RESULTS.
- Последний runtime checkpoint2afb4a4 +Development/Release/four Player runs remains current: только Editor fixtures/docs/evidence добавлены, runtime/shaders/saved scenes не менялись. New Player builds не требовались. Current reflection regression PNGs просмотрены; это не P09 visual equivalence.
- Root Markdown8, ProjectSettings unchanged. Foreign Ocean.mat/example lighting preserved. Active Unity/Player operations отсутствуют на checkpoint; проверить status/processes/timestamps перед продолжением. Последний commit — git log -1.

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
