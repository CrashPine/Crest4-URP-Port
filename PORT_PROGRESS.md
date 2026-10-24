# Точка продолжения

Обновлено: **2026-10-24**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P08

P00–P07 DONE в определённых PORT_PLAN областях, не весь порт. P07 functional milestone подтверждён full66/66 DX11/DX12; единственный план — PORT_PLAN.md, acceptance/limitations — FEATURE_MATRIX/TEST_RESULTS.

**Следующее действие:** P08.3 — nonzero wave phase/compatibility и simulation texture history после actual origin shift. Runner выбран и доказан: Tools/Run-Unity.ps1 -Action EditMode -Interactive -GraphicsAPI DX11/DX12 -TestFilter OriginShiftLifecycleTests запускает отдельную Crest.URP.Interactive.Tests без batchmode; оригинальный EOF действительно выполняется (main GameView/23 render events, native WaitForEndOfFrame). Не повторять flat lifecycle fixture ради тех же случаев и не возвращаться к ручному reset private flags.

Начать новый focused non-batch wave fixture в Tests/InteractiveEditor. Frozen original waves/relative world query points перед/после shift, fresh GPU packets, actual cascade metadata/RT history. Source AnimWavesGerstner.shader: wavePos=axis projection(worldXZ), compatibility mode затем subtracts global floating-origin offset.xz; mode задаётся serialized `_waveCompatibiltyMode` в ShiftingOrigin (именно такое spelling). Сравнить поддерживаемый periodic/default case и требующий compatibility setup; не объявлять default bug только потому, что threshold не совместим с периодом текстуры. Частицы/wind module settings и реальное содержимое simulation history ещё не приняты. Обычный camera teleport — не origin shift.

Фиксированный current/previous centre transport и EOF lifecycle уже scoped accepted. FloatingOrigin.cs — empty rename marker; actual implementation ShiftingOrigin. Не создавать второй progress plan: конечный P08 порядок в PORT_PLAN. После P08.3 — water bodies/culling.

## Последний подтверждённый checkpoint

- **OriginShiftLifecycleTests non-batch1/1 DX11/DX12**, failed0/skipped0: UTC2026-10-24 15:18:58Z–15:18:59Z /15:22:38Z–15:22:39Z.8 threshold/XYZ cases,6 actual shifts; root/child, world-playing/world-paused/local particles, notification/sleepThreshold contracts.112 all-buffered centre and112 native source/target metadata checks/API.23 main-camera render events; LateUpdate flags true on shifts and cleared by real observed EOF (recorded next Unity frame). No private shift/flag calls. Flat waves only; GPU field contents/phase/compatibility still pending.
- Tools/Run-Unity.ps1 -Interactive => separate non-batch Crest.URP.Interactive.Tests. Default batch selects unchanged Crest.URP.Tests explicitly. SpectrumTests3/3 DX11 verified selector, discovered core69 cases; no old tests removed. Future milestones require both profiles on both APIs; do not equate core batch XML with lifecycle coverage. Native fixture fails if invoked in batch instead of silently ignoring.
- P08.2 scoped DONE committed18e90fb (shared-vertex slope/compact/choppy check, max gap.000102114m/geometry-normal gap.000340532; grid shading remains P09). P08.1/previous wave coverage stay accepted.
- **Full66/66** remains UTC2026-10-24 10:14:58Z–10:16:19Z /10:16:42Z–10:18:20Z, P07 DONE. Three later batch P08 fixtures +one separate non-batch lifecycle fixture focused only; no fresh full69/combined70 claimed. P08/full port incomplete.
- **Open P10:** full66 shutdown «Persistent allocates 3 individual allocations», owner/stacks unknown, P07-Milestone.json retained. No zero-leak claim. P09 compact-patch grid shading requires Built-in comparison.
- Latest runtime checkpoint2afb4a4 +Development/Release/four Player runs remains current; additions only Editor tests/assembly/runner/docs/evidence. No runtime or production shader/scene change, no Player rebuild. Root Markdown8, ProjectSettings clean; foreign Ocean.mat/example lighting preserved. No active Unity/Player at checkpoint; verify status/processes/timestamps before resuming. Commit — git log -1.

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
