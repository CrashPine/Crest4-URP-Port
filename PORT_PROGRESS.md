# Точка продолжения

Обновлено: **2026-10-25**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P09

P00–P08 DONE в определённых PORT_PLAN областях; полный порт не завершён. Единственный план — PORT_PLAN.md, acceptance/limitations — FEATURE_MATRIX/TEST_RESULTS.

**Активные операции:** нет. Development/Release build Success UTC2026-10-25 21:46–21:47; все4 последующие Player smokes завершились Errors=0. Unity/Player процессов перед следующим запуском проверить снова.

**Следующее действие:** P09 — реализовать изолированный Built-in reference render с original shader/include closure из Evidence/P09-Reference-inspection.json и одинаковыми waves/time/camera/light/color-space. Source dependencies прочитаны; Ocean.mat diff проверен как serialization metadata без изменения water properties. Visual acceptance ещё нет. Не изменять work/crest-upstream и не запускать GenerateScenes. Сначала решить изоляцию original shader dependencies, затем сравнить поверхность/foam/refraction/scattering/caustics/shadows/underwater и оценить материалы исходных примеров. Patch-grid shading и10 boundary pixels остаются вопросами этого этапа.

## Последний подтверждённый checkpoint

- Runtime fix: underwater composite возвращает цвет в persistent camera target для non-final stack/MSAA. До исправления overlay терял40432 background pixels вне quad; после —0. Четыре сочетания1x/4x ×before/after-transparent:4096 foreground pixels,0 above/background/outside changes, точное восстановление после удаления stack на обеих API.
- Full73/73 batch DX11/DX12 +2/2 actual non-batch, все nodes Passed, failed0/skipped0. P08-Milestone.json и4 XML snapshots фиксируют fingerprints/метрики. CameraSettingsTests3/3 proper PlayMode; initial nested EditMode harness и failed runs сохранены, не считаются PlayMode acceptance.
- Camera/resize/MSAA/renderScale checks прошли;147456 native foam samples/sequence error0 исключают лишние simulation updates. Uniform hardware DR .75/.75 DX12 offscreen: независимый control49152 green/black0, background192x144. DX11 DR unsupported. Nonuniform/live-backbuffer не приняты; XR runtime0 display subsystems, rendered XR BLOCKED/NOT TESTED.
- Обе сборки после runtime fix и4 fresh smokes7 scenes+4 reflection PNG, Errors=0; Build-checkpoint.json содержит source/assembly/packed asset/report/image hashes. Выбранные fresh Dev/Release underwater/reflection PNG просмотрены. Не повторять эти smokes без соответствующего изменения.
- Предыдущие P08.1–4 результаты сохранены в TEST_RESULTS: LOD seams geometry scope, rotated Gerstner origin fix и stale clip-default fix. Их проверки входят в новый full73 checkpoint.
- P10 открыт: обе full batch shutdown Persistent allocates3 individual allocations; DX12 Player evictable-resource warnings. Owners/stacks неизвестны; leak-free/performance claim отсутствует. Extra underwater copy cost ещё не профилировалась.
- Original GUID/compute597 PASSED; известные3 normalized sprite meta не byte-identical. Structural audit1327 files, missing meta/duplicate GUID/missing upstream0; root Markdown8. Чужие Ocean.mat/example baked lighting сохранены; не stage/reset/delete. ProjectSettings content unchanged. Commit — git log -1.

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
