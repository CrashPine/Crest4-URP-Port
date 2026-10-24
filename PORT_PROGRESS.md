# Точка продолжения

Обновлено: **2026-10-24**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P08

P00–P07 DONE в определённых PORT_PLAN областях, не весь порт. P07 functional milestone подтверждён full66/66 DX11/DX12; единственный план — PORT_PLAN.md, acceptance/limitations — FEATURE_MATRIX/TEST_RESULTS.

**Следующее действие:** P08.3 — actual ShiftingOrigin shifts по PORT_PLAN. Прочитан ShiftingOrigin.cs: FloatingOrigin.cs — пустой historical rename marker, не отдельная реализация. `_threshold` power-of-two/min2048, FixedUpdate не чаще одного раза/Unity frame; каждый превышенный X/Y/Z сдвигается floor(position/threshold)*threshold (negative asymmetry учитывать). MoveOrigin сдвигает только root transforms, world-space particles с сохранением play/pause, all LodTransform buffered positions, global floating-origin offset и IShiftingOrigin children; старый ShapeGerstnerBatched отдельно; physics sleepThreshold меняется по distance.

**Важный контракт стенда:** original Start coroutine очищает HasTeleportedThisFrame/TeleportOriginThisFrame через WaitForEndOfFrame; сам source предупреждает, что EOF не работает в batch mode. Не подменять очистку private flags/reflection и не выдавать batch shift за полную lifecycle acceptance. Для итоговой EOF/flag reset проверки нужен реально исполняющий end-of-frame runner (non-batch Editor/Game view либо подходящий Player). Можно независимо проверить transforms/particles/GPU history в focused batch, явно оставив EOF coverage pending. Frozen real waves/relative query points, current/previous cascades и compatibility mode — отдельно от обычного camera teleport. Прежде реализации выбрать рабочий runner, не повторять бессмысленно batch EOF ожидание.

P08.2 sampled geometry/coverage DONE: новый diagnostic GPU probe читает реальные boundary vertices, вызывает original vertex/depth/wave helpers, сверяет affine reference и shared pairs на local-height edge/choppy cases. Он не заменяет production shader и не доказывает окончательный fragment shading; visible compact-patch grid shading требуется сравнить в P09. P08.1/previous wave-pixel fixture не повторять без изменений пути.

## Последний подтверждённый checkpoint

- **LodHeightSeamTests focused1/1 DX11/DX12**, failed0/skipped0: UTC2026-10-24 10:43:41Z–10:43:45Z /10:44:42Z–10:44:46Z.7 states/40 real tiles each,53760 boundary vertices/11766 adjacent-LOD pairs/API; all4 sides, both0/1 and1/2 bands, inside/outside/near-input-edge counts asserted. Max world gap.000102114m, geometry-normal gap.000340532. Independent affine height/normal errors.000034254m/.000045286. Choppy wave peaks>.4 horizontal/>.25 vertical; local offset outside clears. Original helper compute path, not runtime replacement. Compile identifier failure evidence retained, tolerances unchanged.
-Three actual opaque512² captures/API zero clear pixels and byte-identical pairs; images inspected. Compact/choppy grid shading visibly present; no artifact-free/Built-in equivalence claim, required P09 comparison. Scope excludes final pixel/detail normal equivalence/all profiles. P08.2 DONE in defined sampled geometry/coverage scope, P08.3 NEXT.
- P08.1 and LodWaveSurface scoped checks remain accepted. **Full66/66** remains UTC2026-10-24 10:14:58Z–10:16:19Z /10:16:42Z–10:18:20Z, P07 DONE. Three subsequent P08 tests focused only; full69/69 NOT claimed. P08/full port incomplete.
- **Open P10:** full66 shutdown «Persistent allocates 3 individual allocations», owner/stacks unknown; preserved P07-Milestone.json. New focused runs do not repeat it; no zero-leak claim. Reproduce with NativeLeakDetection.EnabledWithStackTrace.
- Latest runtime checkpoint2afb4a4 +Development/Release/four Player runs remains current. Additions only Editor tests and Editor-only diagnostic compute/docs/evidence; production/runtime shaders/saved scenes unchanged. No Player rebuild required for unreferenced Editor diagnostic. Root Markdown8, ProjectSettings clean; foreign Ocean.mat/example lighting preserved. No active Unity/Player at checkpoint; verify status/processes/timestamps on resume. Commit — git log -1.

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
