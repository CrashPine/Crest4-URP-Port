# Crest 4 → Unity 6000.6.5f1 / URP 17.6.0

Рабочий порт открытого Crest 4.23.0. **Полная приёмка не завершена.** Версия выбрана пользователем; установка Unity 6.3 больше не требуется.

## Навигация — у каждого документа одна роль

| Документ | Что в нём искать |
|---|---|
| [PORT_PROGRESS.md](PORT_PROGRESS.md) | Начать здесь: текущая задача и точный следующий шаг. |
| [PORT_PLAN.md](PORT_PLAN.md) | Единственный упорядоченный план с ID задач и критериями закрытия. |
| [FEATURE_MATRIX.md](FEATURE_MATRIX.md) | Статус функциональной приёмки каждой исходной подсистемы. |
| [TEST_RESULTS.md](TEST_RESULTS.md) | Фактические проверки, измерения, версии и ограничения. |
| [MIGRATION.md](MIGRATION.md) | Подключение renderer feature и сохранённые API. |
| [SOURCE_BASELINE.md](SOURCE_BASELINE.md) | Исходный tag/SHA и история начального импорта. |
| [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), [LICENSE.Crest](LICENSE.Crest) | Лицензии и атрибуция. |

Исходное задание требует план, прогресс, матрицу, результаты, миграцию и notices; поэтому они сохранены как разные представления, а не дублирующие журналы. История изменений хранится в Git. Не создавать новые status/roadmap/checkpoint Markdown-файлы.

## Папки

- `Assets/Crest/Crest`: исходное ядро и изменения порта.
- `Assets/Crest/URP`: Render Graph feature, automation, demos controller, тесты.
- `Assets/CrestPortDemos`: семь сцен и их настройки URP; не перегенерировать поверх ручных изменений.
- `Tools`: воспроизводимые команды проверки и сборки.
- `Evidence`: XML, численные отчёты, PNG и локальные raw logs. Старые/failed результаты не являются текущей приёмкой.
- `Builds/Development` и `Builds/Release`: локальные Windows builds; состояние относительно текущего кода указано в PORT_PROGRESS.
- `Library`, `Temp`, `Logs`: генерируемые Unity файлы, не исходники.

Соседние `work/crest-upstream` и `work/diagnostic-6000.6` — оригинал для сравнения и историческая диагностическая копия. Продолжать разработку только здесь, в `outputs/Crest4-URP-Port`.

## Воспроизведение

Открыть проект Unity 6000.6.5f1. Renderer в `Assets/CrestPortDemos` содержит CrestBackgroundFeature; Render Graph, depth и intermediate camera color необходимы.

PowerShell из этого каталога:

```powershell
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe'
./Tools/Run-Unity.ps1 -Editor $editor -Action EditMode -GraphicsAPI DX11
./Tools/Run-Unity.ps1 -Editor $editor -Action EditMode -GraphicsAPI DX12 -TestFilter UnderwaterVolumeTests
./Tools/Run-Unity.ps1 -Editor $editor -Action Development -GraphicsAPI DX11
./Tools/Run-Unity.ps1 -Editor $editor -Action Release -GraphicsAPI DX12
./Tools/Run-Player.ps1 -Player ./Builds/Release/CrestPort.exe -Label Release -GraphicsAPI DX12
./Tools/Audit-Workspace.ps1 -Upstream ../../work/crest-upstream
./Tools/Verify-GuidAndCompute.ps1 -Upstream ../../work/crest-upstream
```

Focused XML не заменяет полный набор. `Verify-Source.ps1` — отдельная строгая побайтовая проверка: Unity нормализовала три sprite meta, поэтому её результат отличается от проверки GUID/compute; это не скрывается.

Player предоставляет переключение сцен, ветер, уровень моря и высоту камеры. Smoke-проверка не доказывает полную визуальную эквивалентность или производительность.
