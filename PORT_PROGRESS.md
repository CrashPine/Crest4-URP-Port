# Точка продолжения

Обновлено: **2026-10-20**. Цель: Unity **6000.6.5f1**, URP **17.6.0**. Разработка только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P04

Basic Portal/Volume/Fly-Through fixtures завершены на DX11 и DX12. Planar object visibility fixture тоже прошёл на обоих API. Полный набор — 18/18; подробности и границы только в TEST_RESULTS.md. Удалять эти тесты или начинать их заново не требуется.

**Следующее действие:** расширить UnderwaterVolumeTests отдельным тестом пересечения поверхности и meniscus на фиксированной камере/времени; затем проверить overlay и before/after-transparent. Начать с чтения текущего fixture и оригинальных UnderwaterRenderer.Mask.cs / Effect.cs. Сначала focused DX11, при новой runtime-правке — полный набор обоих API. После закрытия всех критериев P04 переходить к P05, не перескакивать на повторную генерацию демо.

## Что сделано в последнем этапе

- Добавлены PlanarReflectionTests и UnderwaterVolumeTests; у volume fixture исправлена изоляция поверхности (камера/объём глубоко под водой).
- Подтверждены 18/18 на DX11/DX12, сохранены PNG и failed initial XML.
- Документы получили одну роль: PORT_PLAN — единственная очередь задач P00–P12; этот файл — только checkpoint; TEST_RESULTS — только доказательства; FEATURE_MATRIX — приёмка.
- Добавлены правила продолжения в `../../AGENTS.md` и структурный аудит Tools/Audit-Workspace.ps1. Новые status/roadmap Markdown больше не создавать.
- Run-Unity поддерживает TestFilter и отдельный focused XML; полный отчёт не заменяется частичной выборкой.

## Операции и состояние файлов

- Текущие Unity тестовые процессы завершены; перед следующей командой всё равно проверить процессы и git status.
- Runtime/shader/scene код этого этапа не менялся. Пересборка Player только ради документации/Editor tests не нужна. Новые volume scenarios в standalone ещё не проверены (P11).
- Нормализация `Assets/Crest/Crest/Materials/Ocean.mat` и новые папки baked LightingData/ReflectionProbe в исходных example scenes появились между продолжениями. Они **сохранены, не включены в коммит данной доработки**; не делать blanket reset/clean/add. Для P09 проверить назначение и референсы до принятия решения.
- `Evidence/*initial*`, `diagnostic-6000.6`, `Target-DX11.xml` — исторические; текущие полные результаты только `EditMode-DX11.xml` / `EditMode-DX12.xml`.
- Коммиты предыдущих этапов: b02a049 (рендер), 9bcf32f (query backpressure/tests), 2480dda (evidence), cbefd4e (lifecycle). Последний актуальный коммит смотреть через `git log -1`, не считать эти SHA HEAD.

## Не повторять

Не устанавливать 6.3, не скачивать Crest заново, не продолжать в work/diagnostic-6000.6, не запускать GenerateScenes поверх готовых сцен, не считать warning-free compilation полной приёмкой. Полный порядок и критерии — PORT_PLAN.md, без параллельного списка здесь.
