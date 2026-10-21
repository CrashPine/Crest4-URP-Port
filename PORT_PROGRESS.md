# Точка продолжения

Обновлено: **2026-10-21**. Цель: Unity **6000.6.5f1**, URP **17.6.0**. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P06

P05 закрыт в целевой области Editor/options и Windows smoke; не повторять его fixtures без изменений или новой причины. Полная визуальная/стресс/производительная приёмка отражений остаётся P08–P11. Очередь и критерии только PORT_PLAN.md.

**Следующее действие:** расширить FoamSimulationTests: FFT и whitecaps выключены, известный плоский floor на -0.5 м через OceanDepthCache, shoreline max depth=1, strength=1, fade=0, prewarm=false. Через 30 реальных substeps по 1/30 с ожидается foam=0.5 в interior LOD0. Проверить реальный depth readback перед выводом о foam. Затем проверить toggle/recreate foam и reload, после этого flow visual advection/splines и остальные P06 inputs. Начать с focused DX11; focused результаты не заменяют полный XML.

## Последний проверенный этап

- P05: независимые main-camera light/shadow readbacks; clipping; HDR/stencil/4× MSAA/resize и новая RT в 60 surface chunks; recursion и controlled exception recovery; обычный и persistent renderer; world-space UI/Terrain.
- Исправлены URP force-forward через публичный SetRenderer и повторная регистрация Terrain в distance restoration. Переключение Forward/Forward+/Deferred проверено known-object кадрами в Editor; внутренний fallback reflection camera принадлежит URP.
- Development/Release пересобраны после runtime-правок. Четыре свежих runs с -ReflectionOptions: Errors=0, по 7 Ocean PNG и 4 option PNG. Все четыре выбрали NVIDIA. Fingerprints — Evidence/Build-checkpoint.json. Это не profiling.
- P06: FoamSimulationTests проверяет оригинальные Gerstner whitecaps и decay с фактическим подсчётом substeps. Compute не менялся. Остальные настройки пены ещё не приняты.
- Старый shadow fixture на очень малом batch deltaTime квантовал вклад R8 в ноль. Только в этом тесте задан captureFramerate=60 с восстановлением в teardown; runtime и порог не менялись. Failed report сохранён.
- Полный набор после этих правок: **30/30 DX11 и DX12**. XML и численные измерения — TEST_RESULTS.md; PNG P05 просмотрены. Последующие изменения после сборок относятся только к Editor tests, поэтому Player остаётся актуальным по runtime.

## Операции и изменения вне доработки

Активных Editor/Player операций после checkpoint нет; перед следующей командой всё равно проверить процессы и timestamps. Последний коммит смотреть через git log -1. Полные отчёты — EditMode-DX11.xml / EditMode-DX12.xml; focused — отдельные.

Сохранены нормализация Ocean.mat и baked LightingData/ReflectionProbe папки исходных примеров; не включать в коммит P05/P06 и не делать blanket reset/clean/add. Их оценка — P09. Assets/Resources/Performance* временно создаёт build pipeline. Unity может снова добавить пробелы в новый ReflectionVertexVariants.asset — это сериализация вспомогательного asset, не изменение алгоритма.

Правила: ../../AGENTS.md. Не создавать новые status/roadmap Markdown, не устанавливать 6.3 и не генерировать сцены заново.
