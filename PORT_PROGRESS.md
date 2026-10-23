# Точка продолжения

Обновлено: **2026-10-23**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P07

P00–P06 DONE в областях PORT_PLAN.md; не начинать их заново. Полная приёмка подсистем — FEATURE_MATRIX.md. Здесь только checkpoint и действие продолжения; история измерений/ошибок находится в TEST_RESULTS.md, полный порядок — PORT_PLAN.md.

**Следующее действие:** P07 — пункт3 «Rays on real waves» в PORT_PLAN. Пункты 1 и 2 закрыты focused DX11/DX12; не повторять их без изменения пути. Прочитать RayQueryEdgeTests.Probe и original RayTraceHelper.cs: дополнительный public Query с тем же owner/points без yield раскрывает ready status, Trace bool сам совмещает unready/miss. Existing RayEdges покрывает плоскость/endpoints/async movement, но не real waves.

Новый numerical fixture: original Gerstner на реальных волнах, fixed ray затем смена origin/direction. Для каждого returned packet reference из поля на actual dispatch timestamp и позиций того пакета; учесть original minSpatialLength=step*4, дискретное пересечение/ceil overshoot и использование helper текущих endpoints с устаревшим packet. Можно переиспользовать test-only capture для точности, но не выдавать его за latency check. Проверять все ready результаты, отдельно раскрыть immediate moved-ray inherited semantics и fresh recovery. Прежний ChoppyQueryTests ограничен central LOD0/1 и minLength0: не переносить его sampler вслепую на другие LOD weights. Production алгоритмы сохранять.

Оставшийся порядок — только PORT_PLAN (пункты3–5, затем milestone full DX11/DX12). Materials/saved-demo visuals — P09, stress/performance — P08/P10, standalone/GUI — P11, итоговая упаковка — P12. Focused для Editor-only additions; full при runtime fix/закрытии этапа; Players после runtime/shader/scene changes.

## Последний подтверждённый checkpoint

- **ChoppyQueryTests focused1/1 DX11/DX12**, UTC18:20:10Z–18:20:19Z /18:20:45Z–18:20:56Z. Original Gerstner chop1.6/octave9/seed27; scalar RGB bilinear sampler + independent Newton inverse (residual<.00005m, positive local Jacobian determinant>.1).204 vector frames×5 points и1025 individual heights/API;32 distinct samples spaced≥.1simsec. Horizontal peak.456383m, height span.882549m. Max errors displacement.000181094m, height.000129342m, original normal.00253617, vector velocity.0171046m/s. Original4 fixed-point iterations unchanged. Test fence, no folds/seams/latency claim.
- **AsyncQueryDelayTests focused1/1 DX11/DX12**, UTC18:23:00Z–18:23:01Z /18:23:23Z–18:23:24Z. No GPU fence/readback helper, captureFramerate0, actual wall clock.4 owners1/3/7/13 points+normals:64 fresh packets each per initial/recovery phase, all status0 values numerically checked.24 idle frames remove all4 test owners; original camera query remains active. Observed queue peak2, source limit7; max caller-observed wall ages.0196705s DX11/.0166233s DX12, max2 frames. Initial first-ready3/2frames, recovery3/3. Not GPU execution/performance/saturation acceptance.
- Failed AsyncQueryDelay-idle-initial-DX11 XML/txt preserved: initial harness incorrectly required the entire queue to empty despite OceanRenderer.LateUpdateViewerHeight polling continuously. Read-only owner metadata now verifies the four idle test owners expire while the background viewer query remains; no production changes or relaxed numeric tolerances.
- Earlier RippleGenerator1, BoatAlignNormal2, LerpCam1, RayEdges1, PublicTime1/API remain scoped accepted. **Full55/55 DX11/DX12** remains UTC15:44:23Z–15:45:08Z /15:45:30Z–15:46:23Z. Eight subsequent tests have focused evidence; full63/63 НЕ заявлен. No new PNG/Player builds/full reruns.
- Runtime checkpoint **2afb4a4**: QueryBase layout/size/normals-only fix; Development/Release rebuilt+4API runs Errors0. Build-checkpoint.json runtime hashes актуальны; additions only Editor fixtures/docs/evidence. Existing capture and production source untouched.
- Audit/checkpoint final counts see Evidence/workspace-audit.json and git log -1. Root Markdown8; TimeManager/other ProjectSettings unchanged. Foreign Ocean.mat/example lighting changes preserved. No active Unity/Player operations at checkpoint. Full port/P07 remain incomplete.

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
