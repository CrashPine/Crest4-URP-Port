# Точка продолжения

Обновлено: **2026-10-20**. Цель: Unity **6000.6.5f1**, URP **17.6.0**. Разработка только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P05

P04 закрыт в области Editor-проверок: Portal/Volume/Fly-Through, пересечение поверхности в обе стороны, meniscus, overlay и before/after-transparent. Полная регрессия и Windows smoke checkpoint обновлены; результаты и ограничения только в TEST_RESULTS.md.

**Следующее действие:** добавить в PlanarReflectionTests контролируемые near/far объекты и дополнительный источник света, чтобы воспроизвести поведение `_farClipPlane` / `_disablePixelLights`. Затем адаптировать `OceanPlanarReflection.ForceDistanceCulling` и настройку света через поддерживаемый URP API с восстановлением состояния основной камеры. Существующий visibility/readback fixture уже проходит на обоих API — не повторять его как новую работу. Сначала focused DX11; после runtime-правки полный набор обоих API и пересборка Player.

Технический контекст: стандартный UniversalRenderer sealed; прямой Camera.layerCullSpherical под SRP выдаёт предупреждение. Проверены локальные Unity API XML и URP sources: публичные ScriptableCullingParameters позволяют per-layer distances, но прямого spherical toggle там не найдено. Не использовать private reflection/package fork или удаление опции как готовое решение. Функциональную эквивалентность нужно доказать отдельным тестом, а стоимость адаптера — проверить в P10.

## Последний этап

- Добавлен UnderwaterBoundaryTests: фиксированное время/FFT off, camera 3 → -3 → 0.1 → 3 м, localized meniscus; overlay с реальным пустым фоном; transparent scheduling.
- Unity также автоматически обновила сериализацию ProjectSettings/TimeManager.asset до версии 2 при работе со временем в стенде; m_TimeScale сохранён равным 1. Это изменение относится к текущему этапу и включено в его checkpoint.
- Исправлен runtime cleanup: под URP не вызывается снятие BIRP CameraEvent-буферов, которые там не подключались. После исправления в полных логах нет этого предупреждения.
- Оба Windows Player пересобраны; выполнены четыре свежих seven-scene smoke runs. Managed runtime fingerprints — Evidence/Build-checkpoint.json. Новые boundary/overlay варианты в standalone пока не проверялись: это P11.
- Failed fixture runs сохранены с явными именами; focused XML не заменяет полные результаты. Графические PNG просмотрены.

## Операции и изменения вне текущей доработки

- Активных тестовых Editor/Player операций на момент checkpoint нет; перед новой командой всё равно проверить процессы и git status.
- Сохранены изменения Ocean.mat и новые baked LightingData/ReflectionProbe папки исходных примеров, появившиеся между продолжениями. Они не включены в коммит этой доработки; не делать blanket reset/clean/add. Их оценка — P09.
- Исторические initial/diagnostic/Target XML не являются текущей приёмкой. Полные текущие отчёты — EditMode-DX11.xml / EditMode-DX12.xml; точные количества и даты только в TEST_RESULTS.md.
- Последний актуальный коммит смотреть через git log -1. Исторические базовые коммиты: dff5e8d, b02a049, 9bcf32f, cbefd4e; систематизация документов — 445a191.

Правила продолжения: ../../AGENTS.md. Единственная очередь и критерии — PORT_PLAN.md. Не создавать дополнительные status/roadmap/checkpoint файлы, не устанавливать 6.3, не генерировать сцены заново и не продолжать разработку в work/diagnostic-6000.6.
