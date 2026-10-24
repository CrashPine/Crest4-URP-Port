# Точка продолжения

Обновлено: **2026-10-24**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P08

P00–P07 DONE в определённых PORT_PLAN областях, не весь порт. P07 functional milestone подтверждён full66/66 DX11/DX12; единственный план — PORT_PLAN.md, acceptance/limitations — FEATURE_MATRIX/TEST_RESULTS.

**Активных операций нет:** full69/69 batch +2/2 non-batch обеих API, свежие Development/Release builds и4 Player smoke runs завершены. Каждый Player:7 scenes +4 reflection-options PNG, Errors=0; UTC20:24–20:26. Build-checkpoint.json фиксирует source/packed-assets/smoke hashes. DX12 shutdown evictable-resource warnings сохранены для P10, не считать leak-free.

**Следующее действие:** P08.4 — water bodies/culling. P08.3 scoped DONE: actual lifecycle/EOF, nonzero wave phase/compatibility, native frozen foam history, WindZone state. Не повторять generic ocean/грузики captures. Создать один focused WaterBody fixture: finite independent bounds/strict tile overlap, несколько областей/material dominance, enable/disable и restoration, controlled surface coverage/camera positions. Сначала выяснить исходный static/move contract: WaterBody.CalculateBounds вызывается OnEnable и Editor gizmo, clip-transform updates Editor-only; не выдавать live movement Editor за Player API. Без GenerateScenes.

## Последний подтверждённый checkpoint

- Исправлен **AnimWavesGerstner.shader**: world origin offset проецируется в тот же rotated wave basis. До исправления heading30°/compatibility=true давал native RGB jump .296020508m после X2048; failed XML/report сохранены. Compute/GUID неизменны.
- OriginWaveTransportTests:7 cases/API, headings0/30/90/-45, actual X/Z2048, default periodic и long-wave negative control/compatibility.112 fresh query packets/API;7 native RGB/foam layers. После fix max preserved RGB difference .000610351563m, queries0, source-off frozen foam/sum0; WindZone transform/4parameters сохраняются. Это delta0 history transport, не positive-delta dynamic simulation/wind forces/all shapes/standalone numerical acceptance.
- **Full69/69 batch DX11/DX12**, failed0/skipped0: UTC2026-10-24 15:52:43–15:54:12 /15:54:56–15:56:42. **Full2/2 non-batch**:15:54:27–15:54:43 /15:56:58–15:57:14. Все test-case XML nodes Passed. OriginWaveTransport-checkpoint.json содержит hashes/source/scope; dedicated OriginWaveTransport-DX11/12.xml — snapshot non-batch2/2, включая lifecycle. Не путать с прежними axis-only3case focused reports.
- Tools/Run-Unity.ps1 -Interactive выбирает Crest.URP.Interactive.Tests с настоящим GameView/EOF. Default выбирает Crest.URP.Tests batch. Milestones требуют обе assembly на обеих API; private shift/flag reset не применять.
- **Open P10:** обе full69 batch shutdown снова «Persistent allocates3 individual allocations», owner/stacks неизвестны. Нет zero-leak claim. P09 compact-patch grid shading/Built-in materials comparison остаётся открытым. P08.1/P08.2 не повторять без изменения соответствующего пути.
- 597 original GUID/compute checks PASSED; root Markdown8. Foreign Ocean.mat/example lighting сохранены; ProjectSettings без изменений на момент старта сборок. Commit — git log -1; shader/tests/docs/evidence подготовлены к checkpoint commit; проверить git log/status.

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
