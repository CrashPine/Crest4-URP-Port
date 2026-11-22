# Точка продолжения

Обновлено **2026-11-22**. Unity **6000.6.5f1 / URP 17.6.0**, Windows x64 DX11/DX12, Render Graph. Репозиторий `outputs/Crest4-URP-Port`, ветка `port/unity-6.6-urp`.

## Текущий статус

Windows-поставка P12 завершена в пределах проверенных конфигураций. По уточнённой цели **P13 ACTIVE**: функциональная полнота открытого Crest 4.23.0 на URP без VR. В `Builds` лежат действующие архивы Development и Release с суффиксом `066a5c0`. `Evidence/Build-checkpoint.json` связывает код, артефакты, ValidateAssets, 125 batch + 2 Game View на каждом API и четыре запуска Player. Оба архива прошли проверку после распаковки.

Отдельные проверки прошли для M/N и G/F, локальных отражений с обычной камерой и ручным render request, ветра в Ocean_Storm, накопления и переноса истории теней и мягкой границы. Результаты и точные границы — в TEST_RESULTS.md и FEATURE_MATRIX.md.

## Следующее действие

P13.1 завершён в Editor. Целевые проверки прошли 2/2 на каждом API, полный DX11 EditMode прошёл 131/131. Поставка Player входит в P13.7.

P13.2 завершён по критериям Editor на DX11/DX12. Последние проверки подтвердили смещение воды под корпусом и переопределение обоих каналов теней. Результаты приведены в TEST_RESULTS.md.

P13.3 завершён. Исходная LakesAndRivers прошла составной сценарий в Editor и Windows Player на DX11/DX12. Сохранённый WaterBody прошёл отдельные Editor-проверки локальной высоты, материала и подводного тумана до и после повторной загрузки.

P13.4-S завершён. Ocean_LargeWaves сохранена со спектром Пирсона–Московица, лодкой и маршрутом камеры. Оценка H1/3 по 62 волнам составила 4,973 м на DX11/DX12. Пройден маршрут в Editor и отдельный Windows smoke на обоих API.

**P13.4 ACTIVE.** Meniscus и подводный туман на движущейся поверхности Ocean_LargeWaves прошли проверку в Development Windows Player на DX11/DX12. Следующее действие — проверить видимую границу Portal/Volume/Fly-Through в сочетании с прозрачным потребителем Shader API. Взять существующие UnderwaterVolumeTests и CameraSettingsTests за основу. Затем сверить оставшиеся контроли каустик и теней с ShadowedOpticalReferenceTests.

## Состояние артефактов и сохранённые ограничения

- В итоговых Windows-запусках известных предупреждений JobTempAlloc, Persistent и освобождения активной RenderTexture нет. Подтверждение исправления depth cache и отдельные предупреждения Editor/lightmap и старых диагностических запусков остаются в TEST_RESULTS.md. Раздел P10 сохраняет ограничения измерения GPU и памяти драйвера.
- Сверка исходных GUID и compute-шейдеров прошла все 597 проверок. Отчёт структурного аудита находится в `Evidence/workspace-audit.json`, сведения об исходном релизе — в SOURCE_BASELINE.md.
- Чужие изменения сверяются с `Evidence/P11-SavedInputs-foreign-files.json`. Сохранены Ocean.mat, оригинальные LightingData и ReflectionProbe, отчёты Buoyancy/ResourceLifecycle/focused XML и исторические диагностические артефакты. Исторический `P11-Scenes-foreign-files.json` относится к более раннему снимку. `git log -1` показывает текущий коммит.
