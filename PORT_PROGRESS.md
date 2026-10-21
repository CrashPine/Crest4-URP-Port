# Точка продолжения

Обновлено: **2026-10-21**. Цель: Unity **6000.6.5f1**, URP **17.6.0**. Только `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущая задача: P06

P05 закрыт в целевой области Editor/options и Windows smoke; не повторять его fixtures без изменений или новой причины. Полная визуальная/стресс/производительная приёмка отражений остаётся P08–P11. Очередь и критерии только PORT_PLAN.md.

**Следующее действие:** depth cache baking/reload и Editor round-trip. Прочитать export/import блок OceanDepthCacheEditor.OnInspectorGUI (в OceanDepthCache.cs, около строки 530): оригинал сохраняет EXR OutputAsFloat из RGBAHalf readback, импортирует SingleChannel linear/uncompressed/no mipmaps, назначает _savedCache/_type=Baked. На known-height fixture проверить реальное сохранение/импорт на диск, serialized reference, переход Realtime→Baked и повторную загрузку после удаления исходной floor geometry. Проверить relative depth при перемещении cache owner. Временные assets создавать в новой уникальной test-owned папке с cleanup; не трогать demos и не запускать GenerateScenes. Отделять проверку asset pipeline от нажатия Inspector button, не заявлять UI acceptance без неё. Сначала focused DX11, затем DX12; далее оставшийся Editor settings/profile/spline round-trip по PORT_PLAN.md. Не повторять завершённые foam/flow/clip/albedo/local-height fixtures без новой причины.

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
- Два surface fixtures в FlowSurfaceTests прошли на DX11/DX12: при time=0.5/dt=0 течение 0→3→0 м/с обратимо меняет 25,366 normal pixels и 40,211 foam pixels. Возврат и keyword-off control дают 0 changed pixels. PNG просмотрены; полная визуальная эквивалентность ещё не принята.
- SplineFlowUpdatesDirectionSpeedPositionAndRegistration проходит на обоих API: исходный прямой ribbon даёт (3,0) → (0,3) → (0,−2), перенос на +6 м переносит поле, outside=0; отключение даёт peak=0 по всему LOD0, включение восстанавливает поле. PNG просмотрены. Curved/closed, Editor gestures и saved settings остаются открытыми.
- Clip cube/sphere/geometry проходят на обоих API: исходные SDF/mask соответствуют формулам с допуском 0.005; отверстия 4096/3220/4096 pixels, border не меняется, deregistration даёт peak=0 и полное восстановление baseline. Primitive clip field — градиент, не бинарная маска; ошибочное первоначальное ожидание сохранено в failed XML. Остальные clip modes/reload/stress не приняты.
- Albedo проходит на обоих API: red/blue patch 4096 pixels, перенос, исходный SrcAlpha blend (field alpha=0.5607843 при source alpha=0.75), корректный linear surface composite, cutoff=0.5, disable/re-enable. Ниже cutoff и после deregistration всё поле пусто, surface baseline восстанавливается точно. PNG просмотрены; textured/overlapping/reload/standalone варианты не приняты.
- Local height проходит на обоих API: RGFloat offset и public SampleHeightHelper для +2/−1 м, переноса и global sea level=+3 м; локальная геометрия сохраняет −1 м с offset=−4 м. Восемь последовательных matching query frames на состояние; height bounds и регистрация reporter проверены. Sloped/spline height, rendering и serialization/reload остаются открытыми.
- Старый shadow fixture на очень малом batch deltaTime квантовал вклад R8 в ноль. Только в этом тесте задан captureFramerate=60 с восстановлением в teardown; runtime и порог не менялись. Failed report сохранён.
- Полный checkpoint: **37/37 DX11 и DX12**, failed=0/skipped=0; start 2026-10-21 21:01:07Z / 21:02:10Z, end 21:01:42Z / 21:02:57Z. Включает все новые surface/spline/clip fixtures и четыре foam fixtures. Полные 33-test XML заменены свежей полной регрессией. Per-feature focused XML сохранены отдельно: FlowSurface-DX*.xml, FlowSpline-DX*.xml, ClipSurface-DX*.xml. Изменения после сборок относятся только к Editor tests/документам, Player актуален по runtime.
- В исходниках теперь **39 тестов**: albedo/local height добавлены после полного checkpoint. Весь SurfaceInputTests (clip/albedo/local height) проходит focused **3/3 на обоих API**, start 21:11:57Z / 21:11:22Z; Evidence/SurfaceInputs-DX*.xml и текущие focused XML. Не выдавать этот результат за full 39/39. Новых Asset/.meta файлов в этой сессии нет, структурный audit 1258 остаётся последним.
- Структурный audit новых .cs/.meta: 1258 files, missing meta/duplicate GUID/missing upstream=0. Результаты и ограничения — TEST_RESULTS.md; полная функциональная приёмка ещё не завершена.

## Операции и изменения вне доработки

Активных Editor/Player операций после checkpoint нет; перед следующей командой всё равно проверить процессы и timestamps. Последний коммит смотреть через git log -1. Полные отчёты — EditMode-DX11.xml / EditMode-DX12.xml; focused — отдельные.

Markdown проверен: 8 документов deliverable и отдельный workspace AGENTS.md имеют разные роли; лишних активных progress/status/roadmap файлов нет. Не удалять обязательные документы и не заводить новый журнал.

Сохранены нормализация Ocean.mat и baked LightingData/ReflectionProbe папки исходных примеров; не включать в коммит P05/P06 и не делать blanket reset/clean/add. Их оценка — P09. Assets/Resources/Performance* временно создаёт build pipeline. Unity может снова добавить пробелы в новый ReflectionVertexVariants.asset — это сериализация вспомогательного asset, не изменение алгоритма.

Правила: ../../AGENTS.md. Не создавать новые status/roadmap Markdown, не устанавливать 6.3 и не генерировать сцены заново.
