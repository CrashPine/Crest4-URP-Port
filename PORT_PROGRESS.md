# Точка продолжения

Обновлено **2026-11-23**. Unity **6000.6.5f1 / URP 17.6.0**, Windows x64 DX11/DX12, Render Graph. Ветка `port/unity-6.6-urp`.

## Текущий статус

**P13 и P13.7 завершены в согласованном настольном объёме без VR.** Кандидат `2bd0f9c` прошёл 147 пакетных тестов и 3 теста Game View на каждом API, ValidateAssets и 597 проверок GUID/compute. Development и Release прошли четыре запуска из распакованных архивов на DX11/DX12.

- `Builds/Crest4-URP-Windows-Release-2bd0f9c.zip`
- `Builds/Crest4-URP-Windows-Development-2bd0f9c.zip`
- `Evidence/Build-checkpoint.json` связывает контрольные суммы, исходный коммит и проверенные отчёты.
- `Evidence/P13-Final-Release-evidence.zip` содержит XML, журналы и кадры. Отчёты ранней неудачной проверки LakesAndRivers и прерванной сборки сохранены там же.

Отдельная сцена `Ocean_LargeWaves` доступна в Editor. Порядок запуска и отдельной диагностической сборки приведён в README. Границы сценариев и измерений указаны в FEATURE_MATRIX и TEST_RESULTS.

Открытых шагов согласованного плана нет. Для будущих изменений используйте критерии PORT_PLAN и сведения о воспроизведении в README.
