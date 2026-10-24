# Точка продолжения

Обновлено: **2026-10-24**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P08

P00–P07 DONE в определённых PORT_PLAN областях, не весь порт. P07 functional milestone подтверждён full66/66 DX11/DX12; единственный план — PORT_PLAN.md, acceptance/limitations — FEATURE_MATRIX/TEST_RESULTS.

**Активных операций нет.** P08.3 и P08.4 scoped DONE. Full70/70 batch +2/2 non-batch DX11/DX12 PASSED, failed0/skipped0. Development/Release после обеих runtime правок собраны заново,4 fresh Player runs:7 scenes +4 reflection-options PNG каждый, Errors=0 (UTC20:46–20:47). Текущие source/assembly/packed-assets/report hashes — Build-checkpoint.json; WaterBodies-checkpoint.json связывает новую source/regression acceptance.

**Следующее действие: P08.5 cameras/settings.** Сначала прочитать этот пункт PORT_PLAN и относящиеся строки FEATURE_MATRIX/TEST_RESULTS; создать focused Editor fixture `CameraSettingsTests` (ещё НЕ создана). Начать с двух независимых камер и URP base/overlay stack: фактическое изображение, camera-specific binding и отсутствие лишнего simulation update на камеру. Затем конечные resize/MSAA/render-scale/dynamic-resolution states по плану и capability check XR. Перед изменениями изучить CrestBackgroundFeature/OceanChunkRenderer/UnderwaterRenderer и installed URP camera-stack contracts. Не возвращаться к body/origin fixtures без runtime/соответствующих изменений. Без GenerateScenes и второго Editor.

## Последний подтверждённый checkpoint

- **P08.4:** WaterBodyTests —11 renderer states ×88 tiles=968 comparisons,130386 native first-cascade clip samples,385032 interior mask pixels/API. Field error0, interior mismatch0, default visible/hidden reverse cycle проходит. Две области14 336 visible pixels, одна9216, перемещённая4608, empty/disabled0, восстановленный ocean65 536. Scoped acceptance flat water; не underwater fog/waved edges/GUI/standalone numerical acceptance.
- **Runtime fix:** LodDataMgrClipSurface кеш очистки теперь учитывает default clear value. Before-fix field оставался1 вместо0 после EverythingClipped→NothingClipped при пустом input list; failed XML/report сохранены. Initial fixture foam-validation failure сохранён отдельно, исправлена конфигурация fixture без подавления ошибок. WaterBody bounds/registration в Player обновлять disable/change/re-enable; Editor gizmo/Update не runtime API. Контракт — MIGRATION.md.
- **Full70/70 batch:** UTC2026-10-24 20:32:25–20:36:03 DX11 /20:37:36–20:42:12 DX12. **Full2/2 non-batch:**20:36:27–20:37:15 /20:42:36–20:43:20. Все test-case XML nodes Passed. Dedicated WaterBodies-DX11/12.xml — full70 snapshots; firstfix focused1/1 хранится отдельно. Оба профиля обязательны на milestones.
- P08.3 fix/проверки committed4076ec1: rotated global Gerstner origin projection, до .296m jump, после max .000611m;7 actual X/Z cases,112 fresh packets и frozen source-off foam history/API. Не повторять phase/sampler harness initial mistakes; failures сохранены. Это не continuous dynamic/wind-force/all-shapes acceptance.
- **Открыто P09/P10:** body boundary-mask API differences10 pixels в двух captures внутри заранее исключённой edge band. Нет pixel-perfect/shading parity claim. Patch-grid shading тоже P09. Обе full70 batch shutdown снова Persistent allocates3 individual allocations; оба DX12 Player shutdown evictable-resource warnings сохранены в Build-checkpoint. Owners/stacks неизвестны, никакого leak-free claim.
- 597 original GUID/compute PASSED, root Markdown8. Structural audit1323 files PASSED: missing meta/duplicate GUID/missing upstream0. ProjectSettings clean, чужие Ocean.mat/example baked-lighting сохранены и не stage/reset/delete. Commit — git log -1; current working changes проверить перед продолжением.

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
