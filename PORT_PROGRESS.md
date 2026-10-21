# Точка продолжения

Обновлено: **2026-10-21**. Цель: Unity **6000.6.5f1**, URP **17.6.0**. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P06

P05 закрыт в целевой области Editor/options и Windows smoke; не повторять его fixtures без изменений или новой причины. Полная визуальная/стресс/производительная приёмка отражений остаётся P08–P11. Очередь и критерии только PORT_PLAN.md.

**Следующее действие:** flow spline geometry. В Editor fixture создать исходный Crest.Spline.Spline с прямым участком из SplinePoint children, Radius и SplinePointDataFlow.FlowVelocity=3; затем RegisterFlowInput на том же owner и UpdateSpline(). Graphics batch opt-in задать до OpenScene; flow feature и material keyword согласовать при подготовке ocean. GPU RGFloat readback должен дать известный вектор внутри ribbon и 0 снаружи, смена FlowVelocity/точек должна менять поле. Проверить отключение и повторное включение регистрации. Использовать исходный FlowSplineGeometry.shader/GenerateMeshFromSpline, не новый алгоритм. Сначала focused DX11, затем DX12. Далее остальные P06 inputs и Editor serialization по PORT_PLAN.md. Не повторять завершённые foam/prewarm/surface fixtures без новой причины.

Важный контекст стендов: serialized feature changes могут перестроить OceanRenderer и сбросить временный time-provider stack (как в оригинале). Устанавливать clock после настройки, проверять `ocean.TimeProvider`. Для точного транспорта уже есть StepClock / AdvanceExactSteps: импульс 1/30 на один реально потребивший его кадр, 0 на остальные. После импульсов при dt=0 дождаться трёх ocean updates перед snapshot ping-pong textures. Не возвращаться к подсчёту только yield ticks или предположению о следующем Unity frame.

Для visual batch fixtures задавать EnableGraphicsInBatchMode=true **до OpenScene**, иначе headless-created Root может остаться без meshes. Проверять видимые Root.GetComponentsInChildren<OceanChunkRenderer>(); глобальный FindObjects пропускает HideAndDontSave. Off-screen chunks могут сохранять предыдущий материал до OnWillRenderObject. FlowSurfaceTests уже учитывает эти контракты; failed harness evidence сохранён. Контракт также в MIGRATION.md.

## Последний проверенный этап

- P05: независимые main-camera light/shadow readbacks; clipping; HDR/stencil/4× MSAA/resize и новая RT в 60 surface chunks; recursion и controlled exception recovery; обычный и persistent renderer; world-space UI/Terrain.
- Исправлены URP force-forward через публичный SetRenderer и повторная регистрация Terrain в distance restoration. Переключение Forward/Forward+/Deferred проверено known-object кадрами в Editor; внутренний fallback reflection camera принадлежит URP.
- Development/Release пересобраны после runtime-правок. Четыре свежих runs с -ReflectionOptions: Errors=0, по 7 Ocean PNG и 4 option PNG. Все четыре выбрали NVIDIA. Fingerprints — Evidence/Build-checkpoint.json. Это не profiling.
- P06: FoamSimulationTests проверяет оригинальные Gerstner whitecaps и decay с фактическим подсчётом substeps. Compute не менялся. Остальные настройки пены ещё не приняты.
- Добавлен и пройден shoreline/toggle fixture на обоих API: known depth -0.4999933 м, expected foam=0.5, measured=0.4985352; выключение освобождает старую RT, включение создаёт пустую. Не повторять этот сценарий без причины.
- Prewarm/reload проверен на обоих API: при dt=0 контроль без prewarm даёт 0, с prewarm — ровно 0.25; восемь paused updates сохраняют 0.25. После реального LoadSceneAsync старые foam/cache RT освобождены; повторно созданный known-depth fixture даёт те же результаты. Это не saved-profile serialization acceptance.
- Пройден двухосевой flow transport: исходные input shaders, пятно пены переносится на 2.998 м по X, затем на 2.998 м по Z после смены направления, итоговая масса сохраняется. RT PNG обоих API просмотрены. Это транспорт симуляции, не приёмка surface shader/splines. Failed harness XML и числа сохранены; runtime/compute не менялись.
- Отдельный FlowSurfaceTests проходит focused 2/2 на DX11/DX12: при time=0.5/dt=0 течение 0→3→0 м/с обратимо меняет 25,366 normal pixels и 40,211 foam pixels. Возврат и keyword-off control дают 0 changed pixels. PNG просмотрены; spline geometry и полная визуальная эквивалентность ещё не приняты.
- Старый shadow fixture на очень малом batch deltaTime квантовал вклад R8 в ноль. Только в этом тесте задан captureFramerate=60 с восстановлением в teardown; runtime и порог не менялись. Failed report сохранён.
- Полный checkpoint: **33/33 DX11 и DX12**, failed=0/skipped=0; start 2026-10-21 16:00:32Z / 16:01:23Z. Включает четыре foam fixtures, в том числе новый prewarm/reload, предварительно прошедший focused 1/1 на каждом API. Старые 30-test XML заменены свежей полной регрессией. Результаты — TEST_RESULTS.md. Изменения после сборок относятся только к Editor tests/документам, Player актуален по runtime.
- В исходниках теперь **35 тестов**. Два surface fixtures добавлены после полного checkpoint: их результаты — отдельные focused XML (start 20:50:10Z / 20:50:37Z), full XML по-прежнему 33/33. Не выдавать focused за full 35/35. Структурный audit новых .cs/.meta: 1256 files, missing meta/duplicate GUID/missing upstream=0.

## Операции и изменения вне доработки

Активных Editor/Player операций после checkpoint нет; перед следующей командой всё равно проверить процессы и timestamps. Последний коммит смотреть через git log -1. Полные отчёты — EditMode-DX11.xml / EditMode-DX12.xml; focused — отдельные.

Markdown проверен: 8 документов deliverable и отдельный workspace AGENTS.md имеют разные роли; лишних активных progress/status/roadmap файлов нет. Не удалять обязательные документы и не заводить новый журнал.

Сохранены нормализация Ocean.mat и baked LightingData/ReflectionProbe папки исходных примеров; не включать в коммит P05/P06 и не делать blanket reset/clean/add. Их оценка — P09. Assets/Resources/Performance* временно создаёт build pipeline. Unity может снова добавить пробелы в новый ReflectionVertexVariants.asset — это сериализация вспомогательного asset, не изменение алгоритма.

Правила: ../../AGENTS.md. Не создавать новые status/roadmap Markdown, не устанавливать 6.3 и не генерировать сцены заново.
