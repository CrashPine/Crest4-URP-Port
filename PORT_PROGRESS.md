# Точка продолжения

Обновлено: **2026-10-24**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P08

P00–P07 DONE в определённых PORT_PLAN областях, не весь порт. P07 functional milestone подтверждён full66/66 DX11/DX12; единственный план — PORT_PLAN.md, acceptance/limitations — FEATURE_MATRIX/TEST_RESULTS.

**Следующее действие:** P08.2 остаётся ACTIVE — local-height/input edges на LOD boundaries и displacement/normal continuity соседних mesh boundaries. Frozen global-wave query+pixel-coverage часть уже закрыта LodWaveSurfaceTests на обоих API; не повторять её без изменения пути. Прочитать existing SurfaceInputTests/sloped height controls и OceanVertHelpers.hlsl: vertex ComputeLodAlpha использует linear radius ratio, а QueryDisplacements PosToSliceIndices — log2. Новый query reference НЕ равен vertex morph oracle и не доказывает mesh continuity. Требуется контролируемая плавная local-height/slope crossing actual LOD borders с known geometry/field reference и видимостью поверхности; отделять намеренный край input от ложного mesh seam. После P08.2 — P08.3 origin по PORT_PLAN.

В LodWaveSurfaceTests initial cross-scale invariant оказался неверным для filtered representation. Same-scale .025m сохранён; добавлен independent filtered RT/Newton .01m для всех ready responses, coarse baseline difference.0279683m явно раскрыт. Не объявлять ideal source phase/amplitude across scales PASSED только по query conformance. Production source unchanged.

## Последний подтверждённый checkpoint

- **LodWaveSurfaceTests focused1/1 DX11/DX12**, failed0/skipped0: UTC2026-10-24 10:33:58Z–10:34:00Z /10:34:26Z–10:34:28Z.5 camera states/scales8/16/32, frozen original Gerstner chop1.6.39 ready×5 displacement packets/API match independent three-cascade scalar sampling/Newton at actual dispatch timestamps (max.0000758275m); same-scale translation/return error.00163775m, cross-scale baseline difference.0279683m retained. Height span.472412m/horizontal peak.346924m.
-11 PNG/API: disabled-ocean clear control262144/262144 magenta; ten first/sixth-update actual surface captures zero clear pixels, visible LOD0/1/2 each. All API PNG pairs byte-identical; representative control/initial/translated/transition/settled/return images inspected. Opaque512² sampled coverage ONLY, not subpixel/height/normal/input-edge acceptance. Evidence/LodWaveSurface-checkpoint.json hashes. Initial wrong cross-scale oracle failure/report/captures fully preserved in Evidence/LodWaveSurface-phase-initial.zip (lossless; SHA256 entry index in LodWaveSurface-phase-initial.json).
- P08.1 LodTransitionTests remains focused accepted. **Full66/66** remains UTC2026-10-24 10:14:58Z–10:16:19Z /10:16:42Z–10:18:20Z, P07 scoped DONE. Two subsequent P08 tests have focused evidence; full68/68 NOT claimed. P08/full port incomplete.
- **Open P10:** full66 shutdown warning «Persistent allocates 3 individual allocations» retained in P07-Milestone.json, owner/stacks unknown. These focused runs do not repeat it; no zero-leak claim. Reproduce with NativeLeakDetection.EnabledWithStackTrace.
- Latest runtime checkpoint2afb4a4 +Development/Release/four Player runs remains current: only Editor tests/docs/evidence added; runtime/shaders/saved scenes unchanged, no Player rebuild. Root Markdown8, ProjectSettings clean; foreign Ocean.mat/example lighting preserved. No active Unity/Player at checkpoint; verify status/processes/timestamps on resume. Commit — git log -1.

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
