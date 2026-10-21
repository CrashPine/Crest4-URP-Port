# Результаты проверок

Актуальная сверка: **2026-10-21**. Unity **6000.6.5f1** (`3ff58d469c8a`), URP **17.6.0**, Windows x64, NVIDIA RTX 3050 Ti Laptop GPU, Render Graph. Это единственный документ с итогами измерений; очередь работ находится в PORT_PLAN.md.

## Текущие подтверждённые результаты

| Проверка | DX11 | DX12 | Доказательство |
|---|---|---|---|
| Полный Editor suite | PASSED, 25/25 | PASSED, 25/25 | Evidence/EditMode-DX11.xml, EditMode-DX12.xml; 2026-10-21, start 10:34:54Z / 10:35:39Z |
| Development Player, 7 сцен + reflection options | PASSED, Errors=0 | PASSED, Errors=0 | Evidence/Player-Development-DX11 и DX12; пересобранный checkpoint 2026-10-21, strict variant matching |
| Release Player, 7 сцен + reflection options | PASSED, Errors=0 | PASSED, Errors=0 | Evidence/Player-Release-DX11 и DX12; пересобранный checkpoint 2026-10-21, strict variant matching |
| Состав проекта / GUID | PASSED в указанной структурной области | — | Evidence/workspace-audit.json и guid-compute-preservation.json |

Добавлены UnderwaterBoundaryTests. Runtime cleanup исправлен: RemoveCommandBuffers больше не вызывает CameraEvent API под URP, где буферы не подключались. Полные DX11/DX12 suites проходят; предупреждений Camera.RemoveCommandBuffer в новых логах нет. Существующие player runs не доказывают новые volume fixtures в standalone; перед окончательной приёмкой нужна проверка соответствующих вариантов в Player (P11). Тесты не означают полную приёмку всех исходных функций.

## Что именно проверяют 25 тестов

| Область | Количество | Проверенный результат | Что этим не доказано |
|---|---:|---|---|
| Spectrum | 3 | Детерминизм seed, порядок и границы длин волн | Все настройки спектра и визуальная эквивалентность |
| FFT/underwater/reflection scene smoke | 3 | Конечные изменяющиеся GPU heights, реальные кадры | Полная корректность изображения |
| Depth cache | 1 | Известная высота 3 м возвращается с допуском 0.05 м | Baking/reload всех форматов |
| Shadow atlas | 1 | Ненулевая окклюзия, восстановление матриц камеры после LOD draw | Convergence/cascades/caustics |
| Buoyancy | 1 | 10 секунд симуляции, массы 10/100/1000 кг, ширины 1/2/3 м, ограниченные позиции/скорости и контакт с водой | BoatProbes; новая mass-dependent модель не вводилась (оригинал использует ForceMode.Acceleration) |
| Query pressure | 1 | 128 вызовов без callbacks, ограниченная очередь и восстановление | Все сценарии задержки/нагрузки |
| Flow | 1 | 3 м/с, смена +X на +Z, 120 совпадающих ответов в каждом направлении с допуском 0.1 м/с | Visual advection и splines |
| Gerstner | 1 | FFT отключён, изменяющиеся конечные высоты при fixed timestep | Все варианты analytic waves |
| Dynamic waves | 1 | Нулевое поле до движения сферы; ненулевое поле после, finite GPU readback | Долговременная устойчивость/точность затухания |
| Scene lifecycle | 1 | 12 reload cycles, запросы восстанавливаются; 22 отслеживаемые RT и 51,848,342 native bytes без роста | Общая VRAM, ComputeBuffers, GC, длительный profiling |
| Planar object visibility | 1 | В RT отражения 1908 magenta pixels при видимом объекте, 0 после скрытия; оба API | Spherical culling, pixel-light option, все варианты clipping/HDR/MSAA |
| Planar distance | 1 | На обоих API дальний magenta: 606 → 0 при 100 → 20 м; ближний green: 661; view projection/far сохранены | Terrain, world-space UI, persistent objects, стоимость обхода |
| Planar additional lights | 1 | На обоих API ROI 16×16: pixel mean 147.3867, vertex mean 0; активный URP asset остаётся PerPixel | Основной view readback; другие renderer modes; standalone проверяется отдельно |
| Planar target options | 1 | 128 LDR/1 sample → HDR/stencil при том же размере → 256 HDR/4× MSAA; стабильный descriptor сохраняет тот же объект RT | Привязка RT в океанском material property block после resize и длительное потребление памяти |
| Planar clipping/recursion/restore | 1 | Надводный marker виден, подводный отсечён; recursive camera callback даёт ровно один запрос; far renderer, заранее скрытый renderer, GL и ocean root восстановлены | Исключения во время camera rendering; все типы renderer |
| Underwater volumes | 3 | Portal/Volume/Fly-Through меняют центр и сохраняют border; Fly-Through имеет эффект изнутри | XR, standalone stripping; surface/meniscus/overlay/order covered by the separate fixtures below |
| Surface crossing / meniscus | 1 | Fixed ocean time, FFT off: камера 3 → -3 → 0.1 → 3 м; submerged effect меняет 57600 pixels, meniscus — 1920, surfacing восстанавливает исходный кадр | Storm/wave-driven transition stress и side-by-side сравнение |
| Overlay | 1 | 5208 pixels геометрии изменены, 0 фоновых; Ocean surface исключена из камеры для изоляции нулевой глубины | Настоящий URP camera stack и MSAA (P08) |
| Before/after transparents | 1 | 5208 transparent pixels изменены, 0 opaque; стандартный Unlit без Crest fog API остаётся красным после before-transparent composite | Все сторонние transparent shaders и XR |

Численные результаты каждого запуска находятся в соответствующих `Evidence/*-Direct3D11.txt` и `*-Direct3D12.txt`; XML содержит фактические start/end timestamps UTC. Таблица описывает область проверки, а не заменяет отчёты.

## Новые графические проверки и просмотр PNG

Volume fixture размещён глубоко под поверхностью, чтобы сравнивать именно ограничение тумана: режим объёма намеренно меняет также clipping поверхности океана, поэтому исходный стенд у поверхности не изолировал проверяемое поведение. Численные пороги не снижались. На DX11: Portal — 12996 changed pixels, Volume и Fly-Through — по 12100; border changed pixels = 0. Изображения Portal/Volume снаружи и Fly-Through изнутри просмотрены. Результат DX12 записан отдельно, а не выведен из DX11.

Ранее просмотрены все семь Development DX11 captures и surface/underwater/reflection Release DX12 captures. Reflection RT с контрольным объектом также просмотрен. Это не side-by-side сравнение с Built-in Crest.

## История ошибок — не путать с текущим результатом

- `UnderwaterVolume-initial-DX11.xml`: 15/18, три первоначальные проверки границы не прошли из-за изменения поверхности вне тестируемого тумана; исправлен стенд, не ослаблены пороги. Этот исторический запуск заменён текущими полными XML из таблицы выше.
- `Flow-initial-DX11.xml`, `Gerstner-initial-DX11.xml`: исправлены ожидание асинхронного GPU и fixed timestep.
- `Player-Development-DX11-attempt1`: первоначальная ошибка переполнения очереди; runtime исправлен backpressure в QueryBase, последующие Player runs проходят.
- `diagnostic-6000.6`, `Target-DX11.xml`: исторические результаты более раннего состояния. Не использовать как текущую приёмку.

Raw logs и большой failed player run сохранены локально, исключены из Git. Скрипты отвергают старые XML/PNG, пустые suites, skipped tests и ненулевые smoke errors. Focused tests пишут `*-focused.xml`, не заменяя полный отчёт.

## Аудит файлов

`Tools/Audit-Workspace.ps1` инвентаризирует и хеширует все файлы Assets, Packages, ProjectSettings, Tools и корня репозитория; сравнивает наличие всех оригинальных файлов, ищет missing meta и duplicate GUID. Library/Builds/исторические work-копии исключены как генерируемые/вспомогательные. Это структурная проверка, не построчное семантическое ревью всего upstream-кода.

`Verify-GuidAndCompute.ps1`: 597 оригинальных meta/compute файлов, GUID сохранены, compute bytes не изменены. Строгая `Verify-Source.ps1` остаётся FAILED для трёх Unity-normalized sprite meta (ArrowLeft, ArrowRight, Flask); это явно записано в source-preservation-after-import.json. Исторический source-preservation.json отражает состояние до импорта.

В рабочем дереве обнаружены также нормализация Ocean.mat и новые LightingData/ReflectionProbe файлы исходных example scenes. Они сохранены и перечислены в аудите; не удалены и не выданы за изменения данной доработки. Их дальнейшая оценка относится к P09.

## Оставшиеся ограничения

Полная приёмка незавершена. URP больше не вызывает Camera.layerCullSpherical, а distance/light адаптеры проходят указанные Editor fixtures. Это не полная эквивалентность исходным опциям во всех режимах рендеринга. Пробелы перечислены в FEATURE_MATRIX.md, порядок — PORT_PLAN.md; 25 тестов не закрывают всю матрицу.

## P04 — дополнительные результаты

UnderwaterBoundaryTests прошёл в составе полного 21-test suite на DX11 и DX12. Численные результаты совпадают между API; кадры meniscus, overlay и before/after-transparents просмотрены. Подключение CameraEvent в URP было уже исключено, но отключение ещё вызывало legacy RemoveCommandBuffer. Теперь обе операции согласованы; Render Graph scheduling остаётся прежним.

Первые запуски нового стенда сохранены: Boundary-initial-harness-DX11.xml (EnterPlayMode из вложенного enumerator не выполнился), Boundary-background-fixture-DX11.xml и Boundary-ocean-background-fixture-DX11.xml (в проверяемом фоне оставалась независимо затеняемая поверхность океана). EnterPlayMode вынесен в тесты с явной проверкой Application.isPlaying; overlay fixture исключает ocean layer из камеры и использует соответствующий слой эффекта. Численные критерии не ослаблялись. Overlay-focused-DX11.xml — отдельный успешный focused run, не замена полным suites.

Оба Windows Player пересобраны после runtime cleanup. Свежие Development/Release runs на DX11 и DX12 дали Errors=0, каждый с семью новыми PNG и изменяющимися GPU heights. Fingerprints управляемых Crest.dll и изменённого исходника сохранены в Evidence/Build-checkpoint.json; дата native launcher сама по себе не доказывает свежесть инкрементальной сборки. Дополнительные три запуска выполнялись параллельно для smoke-регрессии и не являются измерением производительности. Проверки всех новых boundary/overlay вариантов в standalone ещё относятся к P11.

## P05 — диагностика и область проверки

Editor DX11/DX12: пять PlanarReflectionTests прошли в полном наборе, новые clipping/distance/light PNG просмотрены. Первоначальный lighting fixture усреднял весь кадр и получал 2.08, включая пустой фон; исправлена область измерения на 16×16 у проекции центра панели. Порог освещённости >5 и отношение vertex/pixel <0.25 сохранены. `ReflectionOptions-initial-DX11.xml` и `ReflectionOptions-no-spherical-call-DX11.xml` сохраняют неудачные эксперименты: простое удаление spherical flag при сохранении layer distances приводило к пустым RT из-за oblique projection.

Первая проверка Player на обоих API выявила реальную ошибку: distance FarChanged=256 / NearChanged=0, но PixelMean=VertexMean=147.3867. Отчёты `ReflectionOptions-Player-initial-DX11.txt` / DX12.txt сохранены. Строгий диагностический Player подтвердил отсутствующие `_ADDITIONAL_LIGHTS_VERTEX` variants (Errors=2771, magenta mean=170); полный отчёт — `ReflectionOptions-Player-strict-initial-DX11.txt`, raw log сохранён локально. Одного дополнительного labeled URP asset недостаточно: ранний engine prefilter уже исключал vertex mode.

В renderer feature добавлено точечное правило ShaderKeywordFilter.SelectIf с overridePriority для обоих lighting modes; официальный IncludeAdditionalRPAssets сохраняет их на позднем URP stripping этапе. Build pipeline включает strictShaderVariantMatching, чтобы отсутствующий вариант давал ошибку вместо незаметной подстановки. Итоговые Player результаты после исправления записываются отдельно от этой истории.

После исправления Development и Release пересобраны. Все четыре запуска на DX11/DX12 прошли с Errors=0, семью свежими Ocean PNG и четырьмя ReflectionOptions PNG каждый. Во всех: FarChanged=256 / NearChanged=0 (ROI 16×16); PixelMean=147.3867 / VertexMean=0; PipelineMode=PerPixel. Strict matching остаётся включённым. Fingerprints Crest.dll, Crest.URP.dll и изменённых runtime sources — Evidence/Build-checkpoint.json. Три последних запуска выполнялись параллельно и не являются профилированием. Development DX11 выбрал Intel Iris Xe, остальные три Player и Editor использовали NVIDIA RTX 3050 Ti; это функциональная проверка, не сравнение производительности адаптеров. Distance PNG Development DX11 и lighting/Ocean_Reflections PNG Release DX12 просмотрены.

Остаются непроверенными exception-path cleanup, отдельный main-camera lighting readback, shadow option, renderer mode/force-forward contract, terrain/world-space UI/DontDestroyOnLoad, привязка заменённого RT в surface draw и стоимость обхода сцены. Поэтому P05 и полная planar acceptance не закрыты.
