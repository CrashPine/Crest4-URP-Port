# Точка продолжения

Обновлено **2026-11-15**. Unity **6000.6.5f1 / URP 17.6.0**, Windows x64 DX11/DX12, Render Graph. Репозиторий `outputs/Crest4-URP-Port`, ветка `port/unity-6.6-urp`.

## Текущий статус

Windows-поставка P12 завершена в пределах проверенных конфигураций. По уточнённой цели **P13 ACTIVE**: функциональная полнота открытого Crest 4.23.0 на URP без VR. В `Builds` лежат действующие архивы Development и Release с суффиксом `066a5c0`. `Evidence/Build-checkpoint.json` связывает код, артефакты, ValidateAssets, 125 batch + 2 Game View на каждом API и четыре запуска Player. Оба архива прошли проверку после распаковки.

Отдельные проверки прошли для M/N и G/F, локальных отражений с обычной камерой и ручным render request, ветра в Ocean_Storm, накопления и переноса истории теней и мягкой границы. Результаты и точные границы — в TEST_RESULTS.md и FEATURE_MATRIX.md.

## Следующее действие

**P13.1:** снять исходный контроль для `UnderWaterCurtainGeom` и `UnderWaterMeniscus` из `Assets/Crest/Crest/Data` на одной камере в Built-in и URP с одинаковым уровнем воды и глубиной. В текущем URP `UnderwaterCurtain.shader` всё ещё содержит `GrabPass`. Сохраните исходные кадры до правок. Затем адаптируйте legacy-путь и проверьте критерии P13.1. План дальнейшей приёмки находится только в PORT_PLAN.md. Физический VR/XR исключён из цели. Активных операций Unity нет.

## Состояние артефактов и сохранённые ограничения

- В итоговых Windows-запусках известных предупреждений JobTempAlloc, Persistent и освобождения активной RenderTexture нет. Подтверждение исправления depth cache и отдельные предупреждения Editor/lightmap и старых диагностических запусков остаются в TEST_RESULTS.md. Раздел P10 сохраняет ограничения измерения GPU и памяти драйвера.
- Сверка исходных GUID и compute-шейдеров прошла все 597 проверок. Отчёт структурного аудита находится в `Evidence/workspace-audit.json`, сведения об исходном релизе — в SOURCE_BASELINE.md.
- Чужие изменения сверяются с `Evidence/P11-SavedInputs-foreign-files.json`. Сохранены Ocean.mat, оригинальные LightingData и ReflectionProbe, отчёты Buoyancy/ResourceLifecycle/focused XML и исторические диагностические артефакты. Исторический `P11-Scenes-foreign-files.json` относится к более раннему снимку. `git log -1` показывает текущий коммит.
