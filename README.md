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
./Tools/Run-Player.ps1 -Player ./Builds/Release/CrestPort.exe -Label Release -GraphicsAPI DX12 -ReflectionOptions
./Tools/Audit-Workspace.ps1 -Upstream ../../work/crest-upstream
./Tools/Verify-GuidAndCompute.ps1 -Upstream ../../work/crest-upstream
```

Focused XML не заменяет полный набор. `Verify-Source.ps1` — отдельная строгая побайтовая проверка: Unity нормализовала три sprite meta, поэтому её результат отличается от проверки GUID/compute; это не скрывается.

`-ReflectionOptions` добавляет к семи сценам контролируемую проверку дистанции и pixel/vertex lighting отражения с отдельными PNG и `reflection-options.txt`. Скрипт проверяет свежесть этих файлов. Сборка автоматически сохраняет необходимые lighting variants; контракт для другого build pipeline описан в MIGRATION.md.

Player предоставляет переключение сцен, ветер, уровень моря и высоту камеры. Smoke-проверка не доказывает полную визуальную эквивалентность или производительность.


## Воспроизведение Editor UI стенда

При закрытом Editor запуск `Unity.exe -projectPath <этот проект> -force-d3d11 -executeMethod CrestEditorInteractionProbe.Prepare` создаёт отдельную owned scene/profile в Tests/Editor. Редактировать исходные spline/spline-point/spectrum inspectors и Scene view; меню `Crest > Port Validation` выбирает объекты, записывает checkpoint и повторно открывает saved fixture. Observer записывает изменения в Evidence/EditorInteraction.jsonl, но не выполняет проверяемые edits. Горячих клавиш стенд не назначает. После Ctrl+S и закрытия Editor запуск `Unity.exe -batchmode -quit -projectPath <этот проект> -executeMethod CrestEditorInteractionProbe.Cleanup` проверяет owner path/GUID и удаляет только временную папку. Не запускать Prepare повторно, пока существует EditorInteraction-owner.json; для прерванного стенда использовать ReloadSavedFixture.


Для автоматического FFT bake в Editor используется
`Crest.FFTBaker.BakeShapeFFT(shape, "Assets/ExistingFolder/Waves.asset")`:
папка должна существовать, asset — быть новым. Исходный Inspector сохраняет
диалог выбора файла; batch требует явный путь. Контракты и ограничения —
MIGRATION.md. Воспроизведение проверки: `Run-Unity.ps1 -Action EditMode
-GraphicsAPI DX11 -TestFilter BakedFFTTests` с тем же обязательным `-Editor`,
что в командах выше (аналогично DX12).


## Non-batch lifecycle проверки

Обычный `Run-Unity.ps1 -Action EditMode` выбирает batch assembly
`Crest.URP.Tests`. Для EOF/GameView lifecycle используется отдельная Editor-only
`Crest.URP.Interactive.Tests` и non-batch автоматический запуск (ручное управление
не требуется). При закрытом Editor:

```powershell
./Tools/Run-Unity.ps1 -Editor 'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe' -Action EditMode -GraphicsAPI DX11 -TestFilter OriginShiftLifecycleTests -Interactive
```

Повторить с DX12. Суффикс XML/log — `-interactive`; dedicated evidence —
`OriginLifecycle-DX11.xml` / `OriginLifecycle-DX12.xml`. Для всего non-batch набора
убрать `-TestFilter`. На milestones требуются обе assembly на обоих API: один
batch XML не подтверждает EOF. Исходный WaitForEndOfFrame не работает в batch,
поэтому интерактивная fixture отклоняет batch mode явным assertion.
