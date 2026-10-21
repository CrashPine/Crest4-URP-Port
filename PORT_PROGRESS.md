# Точка продолжения

Обновлено: **2026-10-21**. Цель: Unity **6000.6.5f1**, URP **17.6.0**. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P06

P05 закрыт в целевой области Editor/options и Windows smoke; не повторять его fixtures без изменений или новой причины. Полная визуальная/стресс/производительная приёмка отражений остаётся P08–P11. Очередь и критерии только PORT_PLAN.md.

**Следующее действие:** проверить транспорт локального пятна пены известным flow. В FoamSimulationTests выключить FFT/Gerstner/shoreline generation/fade/prewarm; включить flow как в уже пройденном AdditionalSimulationTests. Засеять ограниченное пятно через исходный `Crest/Inputs/Foam/Override Foam` (`_FoamValue`) и RegisterFoamInput, затем убрать источник. Flow 3 м/с по +X, 30 фактических substeps по 1/30 с: центр массы пятна должен сместиться на 3 м. Снимать RT/readback до/после, проверить finite/mass bounds; считать реальные substeps через готовый AdvanceSteps, не yield ticks. Размер texel LOD0 можно получить из публичного CalcLodScale и формулы LodTransform (4 * scale / resolution). Начать с focused DX11; готовую query-проверку flow не повторять как новую работу. Далее foam prewarm/reload, surface advection/splines и остальные P06 inputs по PORT_PLAN.md.

## Последний проверенный этап

- P05: независимые main-camera light/shadow readbacks; clipping; HDR/stencil/4× MSAA/resize и новая RT в 60 surface chunks; recursion и controlled exception recovery; обычный и persistent renderer; world-space UI/Terrain.
- Исправлены URP force-forward через публичный SetRenderer и повторная регистрация Terrain в distance restoration. Переключение Forward/Forward+/Deferred проверено known-object кадрами в Editor; внутренний fallback reflection camera принадлежит URP.
- Development/Release пересобраны после runtime-правок. Четыре свежих runs с -ReflectionOptions: Errors=0, по 7 Ocean PNG и 4 option PNG. Все четыре выбрали NVIDIA. Fingerprints — Evidence/Build-checkpoint.json. Это не profiling.
- P06: FoamSimulationTests проверяет оригинальные Gerstner whitecaps и decay с фактическим подсчётом substeps. Compute не менялся. Остальные настройки пены ещё не приняты.
- Добавлен и пройден focused shoreline/toggle fixture на обоих API: known depth -0.4999933 м, expected foam=0.5, measured=0.4985352; выключение освобождает старую RT, включение создаёт пустую. Не повторять этот сценарий без причины. Prewarm и reload пока не проверены.
- Старый shadow fixture на очень малом batch deltaTime квантовал вклад R8 в ноль. Только в этом тесте задан captureFramerate=60 с восстановлением в teardown; runtime и порог не менялись. Failed report сохранён.
- Полный checkpoint: **30/30 DX11 и DX12**; после него добавлен 31-й shoreline/toggle test, проверенный отдельными focused 1/1 на обоих API. Полные XML не заменялись. Результаты — TEST_RESULTS.md; PNG P05 просмотрены. Изменения после сборок относятся только к Editor tests, поэтому Player остаётся актуальным по runtime.

## Операции и изменения вне доработки

Активных Editor/Player операций после checkpoint нет; перед следующей командой всё равно проверить процессы и timestamps. Последний коммит смотреть через git log -1. Полные отчёты — EditMode-DX11.xml / EditMode-DX12.xml; focused — отдельные.

Сохранены нормализация Ocean.mat и baked LightingData/ReflectionProbe папки исходных примеров; не включать в коммит P05/P06 и не делать blanket reset/clean/add. Их оценка — P09. Assets/Resources/Performance* временно создаёт build pipeline. Unity может снова добавить пробелы в новый ReflectionVertexVariants.asset — это сериализация вспомогательного asset, не изменение алгоритма.

Правила: ../../AGENTS.md. Не создавать новые status/roadmap Markdown, не устанавливать 6.3 и не генерировать сцены заново.
