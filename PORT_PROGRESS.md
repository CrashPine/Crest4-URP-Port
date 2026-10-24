# Точка продолжения

Обновлено: **2026-10-24**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P08

P00–P07 DONE в определённых PORT_PLAN областях, не весь порт. P07 functional milestone подтверждён full66/66 DX11/DX12; единственный план — PORT_PLAN.md, acceptance/limitations — FEATURE_MATRIX/TEST_RESULTS.

**Следующее действие:** P08.2 — nonzero wave field transitions + actual surface seam coverage, конечный порядок P08.1–P08.5 теперь в PORT_PLAN. Flat transforms fixture P08.1 закрыт, не повторять. Для нового пути frozen original wave field/public TimeProviderCustom overrideTime, actual camera translation/altitude; сравнить сохранение world phase и query recovery при смене LOD и осмотреть контролируемые seam captures реальной поверхности. Учесть minSlice/filtering при смене scale: прежний central LOD0/1 sampler нельзя переносить на все точки/LOD. Query sampler сам по себе не доказывает отсутствие mesh seams. После этого local-height/input edges, затем origin/body/camera/settings по порядку плана.

**Обнаруженный original contract:** global MeshScaleLerp задаётся в RunUpdate до LateUpdateScale, поэтому на переходах содержит previous-update alpha/scale flag. LodTransitionTests проверил8 отличающихся кадров; это не исправлялось. Surface/seam impact на ненулевых волнах ещё НЕ проверен. Source unchanged.

## Последний подтверждённый checkpoint

- **P08.1 LodTransitionTests focused1/1 DX11/DX12**, failed0/skipped0: UTC2026-10-24 10:23:13Z–10:23:17Z /10:23:47Z–10:23:50Z.26 camera states/131 observed updates, scales8–256,5 up/4 down/one multi-octave jump. Max signed snap error1.1920929e-7m, history error0;129 ready three-point query frames and26,836,992 finite zero RGB native texels/API. Original scale/nudge/buffer logic retained; temporary depth settings disable smoothing only in this isolated fixture. Initial wrong default-settings assumption failed report preserved.
- **Full66/66 DX11/DX12** remains UTC2026-10-24 10:14:58Z–10:16:19Z /10:16:42Z–10:18:20Z, P07 scoped DONE. New single test has focused evidence, full67/67 NOT claimed. P08 ACTIVE, full port incomplete. No new PNG/full suite/Player rebuild for this Editor-only addition.
- **Открыто P10:** both full66 Editors at shutdown reported «Persistent allocates 3 individual allocations». Allocation stacks/owner unknown; no causal attribution or zero-leak claim. New focused LOD runs have no such warning. P10 must reproduce with NativeLeakDetection.EnabledWithStackTrace, not hide warning behind green XML. Evidence/P07-Milestone.json preserves excerpts/hash.
- Latest runtime checkpoint2afb4a4 +Development/Release/four Player runs remains current; runtime/shaders/saved scenes unchanged. Eight root Markdown files, ProjectSettings clean; foreign Ocean.mat/example lighting preserved. No active Unity/Player at checkpoint; verify status/processes/timestamps on resume. Commit — git log -1.

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
