# Точка продолжения

Обновлено: **2026-10-21**. Цель: Unity **6000.6.5f1**, URP **17.6.0**. Разработка только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P05

План сверен с исходным заданием: порядок и ID сохранены, уточнены критерии P05–P07, Editor/serialization, физика крупных волн и финальная проверка материалов. P04 не повторять.

**Следующее действие:** расширить PlanarReflectionTests существующим lighting fixture: отдельным render request основной камеры доказать, что её per-pixel освещение не меняется при переключении опции отражения. Затем добавить контролируемое исключение в reflection camera callback и проверить finally-восстановление renderer flags, ocean root и GL. Начать с focused DX11, читать фактический XML и LogAssert для ожидаемого исключения; не скрывать неожиданные ошибки. После этого проверить shadow option и renderer-mode/force-forward contract. Полная очередность/критерии только в PORT_PLAN.md.

Не повторять уже пройденные distance, lighting, resize и clipping fixtures без изменений или новой причины. Также остаются terrain/world-space UI/DontDestroyOnLoad и фактическая привязка заменённого RT в surface draw. Стоимость обхода сцены измеряется в P10, а не выводится из smoke.

## Последний проверенный этап

- URP distance adapter заменяет неподдерживаемый spherical layer switch: временно скрывает объекты вне консервативной bounding sphere и восстанавливает свои изменения в finally. Для scene traversal используются переиспользуемые списки, есть profiler marker; нулевые GC/стоимость пока не измерялись.
- Per-camera additional-light policy через renderer feature, recursive refresh guard, HDR/stencil/MSAA/resize и исправление кэша заменённой RT. Публичные ReflectionDistance, DisablePixelLights и RefreshReflection сохраняют исходные serialized options.
- Полные Editor suites: 25/25 DX11 и DX12, новые PNG просмотрены. Область измерений и ограничения — TEST_RESULTS.md.
- Новый Player fixture выявил stripping regression: без vertex variant Unity молча подставляла pixel variant. Strict matching подтвердил отсутствие варианта. Failed reports и raw logs сохранены.
- Исправлены оба этапа stripping: официальный additional labeled URP asset для URP stripper плюс точечный ShaderKeywordFilter на renderer feature для раннего engine prefilter. Strict matching оставлен включённым; MIGRATION описывает build contract.
- Development/Release пересобраны, четыре runs с -ReflectionOptions прошли Errors=0; каждый содержит семь свежих Ocean PNG и четыре option PNG. Fingerprints — Evidence/Build-checkpoint.json. Реальный GPU записан в каждом player-smoke.txt; не считать параллельные runs профилированием.

## Операции и изменения вне доработки

Активных Editor/Player операций после checkpoint нет; перед новой командой проверить процессы и timestamps. Последний коммит смотреть через git log -1. Полные XML — EditMode-DX11.xml / EditMode-DX12.xml; focused XML не заменяет их.

Сохранены нормализация Ocean.mat и baked LightingData/ReflectionProbe папки исходных примеров. Они не относятся к P05 и не включаются в его коммит; не делать blanket reset/clean/add. Их оценка — P09. Временные Assets/Resources/Performance* создаются и удаляются самим build pipeline, не принимать их за новые исходники.

Правила продолжения: ../../AGENTS.md. Единственная очередь — PORT_PLAN.md; результаты — TEST_RESULTS.md; приёмка — FEATURE_MATRIX.md. Не создавать дополнительные status/roadmap Markdown, не устанавливать 6.3 и не генерировать сцены заново.
