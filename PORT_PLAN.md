# План выполнения порта Crest 4

Единственный список задач и их очередности. Дата ревизии: 2026-10-23.
Цель: полный перенос открытого Crest 4.23.0 на **Unity 6000.6.5f1 / URP 17.6.0**, Windows x64, DX11/DX12, Render Graph. Пользователь заменил исходную цель 6.3 на свою установленную версию. Полная приёмка пока не достигнута.

## Как продолжать

1. Прочитать PORT_PROGRESS.md и проверить фактические изменения Git/процессы Unity.
2. Выполнить первый незакрытый доступный пункт ниже. При объективной блокировке записать причину и продолжить независимый пункт, сохранив незакрытый статус. Изменять статус только по указанному критерию.
3. Записать результаты в TEST_RESULTS.md, границы приёмки — в FEATURE_MATRIX.md.
4. Обновить PORT_PROGRESS.md: следующий конкретный шаг, незавершённые команды, ограничения и коммит.

Статусы задач: DONE — конкретный этап выполнен; ACTIVE — текущая работа; TODO — впереди. DONE этапа не означает PASSED всей подсистемы: функциональная приёмка ведётся только в FEATURE_MATRIX.md.

## Выполненные этапы — не начинать заново

| ID | Статус | Результат / проверка |
|---|---|---|
| P00 | DONE | Изолированный репозиторий, исходный tag/SHA и лицензии зафиксированы. Установлена требуемая Unity. Не скачивать исходники и не устанавливать 6.3 повторно. |
| P01 | DONE | Реализованы URP surface/background, SRP callbacks, depth capture, graph underwater/shadows, reflection camera request. Это реализация, а не полная визуальная приёмка. |
| P02 | DONE | Исправлено переполнение GPU-query очереди; regression test и реальные player runs подтвердили восстановление. |
| P03 | DONE | Созданы 7 демо; Development/Release собраны и проверены на DX11/DX12. Проверены spectrum, FFT, Gerstner, flow, dynamic input, buoyancy, depth, shadow readback, 12 reload cycles. Точные результаты — TEST_RESULTS.md. |

## Оставшаяся работа — выполнять в этом порядке

| ID | Статус | Конкретное действие | Критерий закрытия / зависимости |
|---|---|---|---|
| P04 | DONE | Portal/Volume/Fly-Through confinement, surface crossing в обе стороны, meniscus, overlay и before/after-transparent проверены. URP cleanup CameraEvent исправлен. | 21/21 на DX11/DX12, PNG просмотрены; оба Player пересобраны, 4 smoke runs Errors=0. Полная standalone-вариантность и stress остаются в P08/P11. |
| P05 | DONE | Целевые Editor fixtures проверили distance, pixel/shadow options с независимой основной камерой, clipping, HDR/stencil/MSAA/resize и surface binding, recursion/exception cleanup, persistent renderer, world UI/Terrain, выбор Forward и наследование renderer. Исправлены Terrain restoration и force-forward через публичный URP SetRenderer. | Полный checkpoint 29/29 DX11/DX12 и 4 свежих Player runs с distance/light options, Errors=0; PNG просмотрены. Forward/Forward+/Deferred проверены в Editor в описанной области, внутренний fallback Deferred reflection принадлежит URP. Полная визуальная эквивалентность — P09, стоимость — P10, standalone-варианты всех fixtures — P11. |
| P06 | DONE | Проверены foam generation/decay/prewarm, flow surface и curved/closed GPU splines, clip/albedo/local height и sloped surface, depth baking/reload, local Gerstner spline. Editor profiles/data и реальный DX11 Inspector/Scene view workflow с Undo/Redo/save/reopen проходят. | Для входов получены численные/визуальные контроли, toggle и reload; references/values сохранены. Полный milestone 45/45 DX11/DX12; UI snapshots отдельно от NUnit. Исправлено ожидание асинхронного GPU в resource fixture, assertions усилены. Полная визуальная эквивалентность/неравномерные closed custom data — P09; stress/edge/LOD — P08/P10; остальные Editor controls и standalone variants — P11. Это не PASSED всех строк матрицы. |
| P07 | ACTIVE | Known-plane normals/2 м/с velocity, rays на sloped/moving water и real baked FFT CPU jobs/reload проверены на DX11/DX12 в scoped области; BoatProbes flat/storm stability и смена mass/sea level/wind/spectrum/frozen time проверены focused; controls/turning/cap и baked CPU settings selection/choppy inversion также проверены focused; real-wave GPU queries, ray edges/time resume и BoatDev integration остаются открыты. Проверить нормали/скорости и задержку queries, baked FFT CPU provider, ray queries, BoatProbes/watercraft; управление временем/ветром/спектром/уровнем моря через публичные API. Проверить контакт с поверхностью, крен/дифферент и стабильность Rigidbody при крупных волнах и смене параметров. | Сравнение с исходной моделью, finite/stability bounds, численная GPU latency, несколько масс/размеров и сценарии изменения параметров; не вводить новую физику или FishNet. |
| P08 | TODO | Стресс LOD/движения камеры, швы, floating origin, water bodies, несколько камер, MSAA, качество/разрешение/dynamic resolution. XR отдельно при доступности устройства. | Проверки границ и снимки; корректное восстановление ресурсов/камер. Недоступный XR явно BLOCKED с причиной, не PASSED. |
| P09 | TODO | Визуальное сравнение с исходным Built-in Crest и миграция необходимых материалов примеров. | Сопоставимые условия/камеры, зафиксированные расхождения по foam/refraction/scattering/caustics/shadows/underwater. Проверка материалов не равна успешной компиляции. |
| P10 | TODO | Длительное профилирование CPU/GPU, draw calls, GC, RT/ComputeBuffer/VRAM и shader variants. | Отчёт с настройками, длительностью и измерениями на одном API за запуск. 12 reload cycles — только ограниченная предварительная проверка, не итоговый leak test. |
| P11 | TODO | Финальная регрессия и пересборка Development/Release после runtime/shader/scene изменений. | Полный набор DX11/DX12, 4 player runs, свежие PNG/XML, отсутствие критических ошибок; проверки исходных GUID/compute. Зависит от P04–P10. |
| P12 | TODO | Финальная упаковка, документы, матрица и Git checkpoint. | Каждый обязательный пункт исходного задания имеет подтверждённый PASSED; никаких незаявленных FAILED/BLOCKED/NOT TESTED. Только тогда объявлять порт завершённым. |

## Архитектурные ограничения

Ревизия по исходному заданию: порядок P00–P12 сохранён, повторного выполнения DONE этапов не требуется. Уточнены ранее неявные обязательства P05–P07; отдельные новые планы не создаются. Полное покрытие исходных подсистем отслеживается строками FEATURE_MATRIX.md. Успешная компиляция, smoke или сохранение исходного кода не заменяют функциональную приёмку. Финальная проверка P11 включает ValidateAssets (missing references, shader errors) и Play Mode/Editor сценарии; P09 включает материалы, caustics/refraction и сравнение исходных уравнений освещения.

Сохранять FFT/compute dispatch и оригинальные алгоритмы; не переносить их механически в per-camera Render Graph. Simulation input shaders с CGPROGRAM могут работать через прямые command-buffer draws — менять только доказанную несовместимость. CameraEvent/GrabPass и BIRP camera rendering требуют URP путей с явными ресурсами. Сохранять GUID, публичные контракты, лицензии и оригинальную модель плавучести.

## Правило выбора проверки

- Исправление одного тестового стенда: сначала `Run-Unity.ps1 -Action EditMode -GraphicsAPI DX11 -TestFilter UnderwaterVolumeTests` (отдельный focused XML).
- Изменение runtime/shader или закрытие этапа: полный набор DX11 и DX12.
- Только документация: структурный аудит и проверка ссылок/статусов, без повторной сборки Player.
- Runtime/shader/scene изменены после сборки: обязательно пересобрать оба Player перед итоговой приёмкой.
- Не запускать GenerateScenes поверх готовых сцен. Не запускать два Editor на одном проекте.
- Не повторять базовые ocean/грузики PNG без изменения соответствующего rendering path или новой visual failure. Для численных query/physics fixtures достаточно результатов и XML; картинка сама по себе не доказывает физику. После Editor-test-only изменений использовать focused проверки, сохраняя full checkpoint отдельно.
