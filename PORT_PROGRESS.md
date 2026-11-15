# Точка продолжения

Обновлено **2026-11-15**. Unity **6000.6.5f1 / URP 17.6.0**, Windows x64 DX11/DX12, Render Graph. Репозиторий `outputs/Crest4-URP-Port`, ветка `port/unity-6.6-urp`.

## Текущий статус

Windows-поставка P12 завершена в пределах проверенных конфигураций. По уточнённой цели **P13 ACTIVE**: функциональная полнота открытого Crest 4.23.0 на URP без VR. В `Builds` лежат действующие архивы Development и Release с суффиксом `066a5c0`. `Evidence/Build-checkpoint.json` связывает код, артефакты, ValidateAssets, 125 batch + 2 Game View на каждом API и четыре запуска Player. Оба архива прошли проверку после распаковки.

Отдельные проверки прошли для M/N и G/F, локальных отражений с обычной камерой и ручным render request, ветра в Ocean_Storm, накопления и переноса истории теней и мягкой границы. Результаты и точные границы — в TEST_RESULTS.md и FEATURE_MATRIX.md.

## Следующее действие

**P13.1 ACTIVE:** DX11 подтвердил отсутствие отклика в URP при включении исходных prefab Curtain/Meniscus. Контроль Built-in остался непригодным для сравнения цвета. Точное состояние двух попыток в TEST_RESULTS.md. Unity сейчас закрыт. Инвентаризация P13.0 завершена, `Ocean_LargeWaves` запланирована в P13.4-S.

Следующий шаг: восстановить `LegacyUnderwaterTests.cs` и `.meta` из `Evidence/P13-LegacyUnderwater-DX11-baseline.zip` в `Assets/Crest/URP/Tests/Editor`. Для Built-in убрать поверхность порта из маски камеры и проверить захват оригинального `_BackgroundTexture` и глубины на непрозрачной контрольной геометрии. В URP переиспользовать копию `_BackgroundTexture` из `CrestBackgroundFeature` и проверить, что Curtain рисуется до поверхности океана, а Meniscus остаётся поверх её края. Сначала прогнать класс `LegacyUnderwaterTests` через `-force-d3d11 -testPlatform EditMode -testFilter LegacyUnderwaterTests` без `-nographics`. Контроли над водой, смена положения камеры и сосуществование с современным Renderer входят в P13.1.

## Состояние артефактов и сохранённые ограничения

- В итоговых Windows-запусках известных предупреждений JobTempAlloc, Persistent и освобождения активной RenderTexture нет. Подтверждение исправления depth cache и отдельные предупреждения Editor/lightmap и старых диагностических запусков остаются в TEST_RESULTS.md. Раздел P10 сохраняет ограничения измерения GPU и памяти драйвера.
- Сверка исходных GUID и compute-шейдеров прошла все 597 проверок. Отчёт структурного аудита находится в `Evidence/workspace-audit.json`, сведения об исходном релизе — в SOURCE_BASELINE.md.
- Чужие изменения сверяются с `Evidence/P11-SavedInputs-foreign-files.json`. Сохранены Ocean.mat, оригинальные LightingData и ReflectionProbe, отчёты Buoyancy/ResourceLifecycle/focused XML и исторические диагностические артефакты. Исторический `P11-Scenes-foreign-files.json` относится к более раннему снимку. `git log -1` показывает текущий коммит.
