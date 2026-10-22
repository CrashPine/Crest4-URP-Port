# Результаты проверок

Актуальная сверка: **2026-10-22**. Unity **6000.6.5f1** (`3ff58d469c8a`), URP **17.6.0**, Windows x64, NVIDIA RTX 3050 Ti Laptop GPU, Render Graph. Это единственный документ с итогами измерений; очередь работ находится в PORT_PLAN.md.

## Текущие подтверждённые результаты

| Проверка | DX11 | DX12 | Доказательство |
|---|---|---|---|
| Полный Editor checkpoint, включая все persistence fixtures | PASSED, 42/42 | PASSED, 42/42 | Evidence/EditMode-DX11.xml, EditMode-DX12.xml; 2026-10-22, start 17:26:29Z / 17:27:49Z |
| Input scene persistence, focused | PASSED, 1/1 | PASSED, 1/1 | Evidence/InputPersistence-DX11.xml / InputPersistence-DX12.xml; 2026-10-22, start 17:25:28Z / 17:25:57Z |
| Depth persistence, focused | PASSED, 1/1 | PASSED, 1/1 | Evidence/DepthPersistence-DX11.xml / DepthPersistence-DX12.xml; EXR rebake, prefab/scene reload, owner movement, cleanup |
| Editor profiles/spline data, отдельный focused | PASSED, 1/1 | PASSED, 1/1 | Evidence/EditorPersistence-DX11.xml / EditorPersistence-DX12.xml; 2026-10-22, start 17:15:55Z / 17:16:21Z; также включён в новый full 42/42 |
| SurfaceInputTests, focused (clip/albedo/local height) | PASSED, 3/3 | PASSED, 3/3 | Evidence/SurfaceInputs-DX11.xml / SurfaceInputs-DX12.xml; start 21:11:57Z / 21:11:22Z |
| Surface flow normals/foam, focused | PASSED, 2/2 | PASSED, 2/2 | Evidence/FlowSurface-DX11.xml / FlowSurface-DX12.xml; start 20:50:10Z / 20:50:37Z; сохранены отдельно от следующих focused runs |
| Flow spline, focused | PASSED, 1/1 | PASSED, 1/1 | Evidence/FlowSpline-DX11.xml / FlowSpline-DX12.xml; six field states |
| Clip surface, focused | PASSED, 1/1 | PASSED, 1/1 | Evidence/ClipSurface-DX11.xml / ClipSurface-DX12.xml; cube/sphere/geometry and deregistration |
| Development Player, 7 сцен + reflection options | PASSED, Errors=0 | PASSED, Errors=0 | Evidence/Player-Development-DX11 и DX12; пересобранный checkpoint 2026-10-21, strict variant matching |
| Release Player, 7 сцен + reflection options | PASSED, Errors=0 | PASSED, Errors=0 | Evidence/Player-Release-DX11 и DX12; пересобранный checkpoint 2026-10-21, strict variant matching |
| Состав проекта / GUID | PASSED в указанной структурной области | — | Evidence/workspace-audit.json и guid-compute-preservation.json |

Добавлены UnderwaterBoundaryTests. Runtime cleanup исправлен: RemoveCommandBuffers больше не вызывает CameraEvent API под URP, где буферы не подключались. Полные DX11/DX12 suites проходят; предупреждений Camera.RemoveCommandBuffer в новых логах нет. Существующие player runs не доказывают новые volume fixtures в standalone; перед окончательной приёмкой нужна проверка соответствующих вариантов в Player (P11). Тесты не означают полную приёмку всех исходных функций.

## Область 42 тестов

| Область | Количество | Проверенный результат | Что этим не доказано |
|---|---:|---|---|
| Spectrum | 3 | Детерминизм seed, порядок и границы длин волн | Все настройки спектра и визуальная эквивалентность |
| FFT/underwater/reflection scene smoke | 3 | Конечные изменяющиеся GPU heights, реальные кадры | Полная корректность изображения |
| Depth cache | 1 | Известная высота 3 м возвращается с допуском 0.05 м | Baking/reload всех форматов |
| Depth persistence | 1 | Реальный EXR export/rebake, импорт отрицательных значений, GUID/prefab reference, scene reload без floor, relative movement, toggle и cleanup success/failure | Complex terrain, все cache modes и standalone |
| Input persistence | 1 | Сохранённая сцена: flow/clip/albedo/local height GPU поля, material/profile/clock references после reimport/reload, старые RT освобождены, toggle после загрузки | Все виды inputs, длительный reload stress и standalone |
| Editor persistence | 1 | Profiles/private arrays/references, spline point data и overrides, custom Editor bindings, Undo/Redo, повторное сохранение prefab | Реальные GUI gestures/drawers и curved/closed rendering |
| Shadow atlas | 1 | Ненулевая окклюзия, восстановление матриц камеры после LOD draw | Convergence/cascades/caustics |
| Buoyancy | 1 | 10 секунд симуляции, массы 10/100/1000 кг, ширины 1/2/3 м, ограниченные позиции/скорости и контакт с водой | BoatProbes; новая mass-dependent модель не вводилась (оригинал использует ForceMode.Acceleration) |
| Query pressure | 1 | 128 вызовов без callbacks, ограниченная очередь и восстановление | Все сценарии задержки/нагрузки |
| Flow | 1 | 3 м/с, смена +X на +Z, 120 совпадающих ответов в каждом направлении с допуском 0.1 м/с | Visual advection и splines |
| Gerstner | 1 | FFT отключён, изменяющиеся конечные высоты при fixed timestep | Все варианты analytic waves |
| Dynamic waves | 1 | Нулевое поле до движения сферы; ненулевое поле после, finite GPU readback | Долговременная устойчивость/точность затухания |
| Scene lifecycle | 1 | 12 reload cycles, запросы восстанавливаются; 22 отслеживаемые RT и 51,848,342 native bytes без роста | Общая VRAM, ComputeBuffers, GC, длительный profiling |
| Planar object visibility | 1 | В RT отражения 1908 magenta pixels при видимом объекте, 0 после скрытия; оба API | Spherical culling, pixel-light option, все варианты clipping/HDR/MSAA |
| Planar distance | 1 | На обоих API дальний magenta: 606 → 0 при 100 → 20 м; ближний green: 661; view projection/far сохранены | Стоимость обхода и масштабные сцены |
| Planar additional lights | 1 | На обоих API ROI 16×16: pixel mean 147.3867, vertex mean 0; main view 150.2266 → 150.2266; активный URP asset остаётся PerPixel | Все lighting configurations и визуальная эквивалентность |
| Planar target options | 1 | 128 LDR/1 sample → HDR/stencil при том же размере → 256 HDR/4× MSAA; стабильный descriptor сохраняет тот же объект RT; все 60 surface chunks получают новую RT | Длительное потребление памяти и стресс изменения параметров |
| Planar clipping/recursion/restore | 1 | Надводный marker виден, подводный отсечён; recursive callback даёт ровно один запрос; far renderer, заранее скрытый renderer, GL и ocean root восстановлены | Все типы renderer и camera stacks |
| Planar exception/persistent renderer | 1 | Исключение внутри reflection camera callback, включая скрытый ocean root и два distance-culled renderer; нормальная сцена и DontDestroyOnLoad, GL/quality/guard восстановлены; следующий запрос выполнен | Исключения на каждой стадии URP или разрушение всех владельцев |
| Planar shadows | 1 | Shadow ROI 1.3945 → 45.9375 при выключении теней отражения; основной view 48 → 48; оба API | Все shadow configurations, дополнительный свет с тенями, cascades |
| Planar renderer routing | 1 | Forced Forward, inherited Forward+/Deferred и vertex-forced Forward: по 606 magenta pixels; renderer основной камеры сохранён; оба API | Внутренний Deferred reflection fallback принадлежит URP; все варианты в Player и визуальная эквивалентность |
| Planar world UI/Terrain | 1 | Дальний world UI marker: 289 → 0; ближний green: 394; Terrain draw flags восстановлены (heightmap=true, trees=false); оба API | Визуальная полнота Terrain, все UI layouts и изменяемые иерархии |
| Foam simulation | 4 | Whitecaps/decay, shallow-depth generation/toggle, two-axis transport, zero-delta prewarm/reload; per-API значения ниже | Все surface configurations, stress/quality coverage, визуальная эквивалентность |
| Surface flow | 2 | Изолированные normals/foam, фиксированное время, reversible 0→3→0 м/с и keyword-off negative control | Все surface configurations и Built-in equivalence |
| Spline flow | 1 | Исходный прямой ribbon: поворот, скорость/знак, перенос точек, outside=0, deregistration/re-registration | Curved/closed, Unity Splines adapter, Editor gestures, serialization |
| Clip surface | 1 | Cube/sphere SDF, geometry mask, видимые отверстия, unchanged border, cleared field/restored image при deregistration | Inversion/overlaps, hulls/textures, reload, waves, standalone |
| Albedo input | 1 | Red/blue, перенос, исходный SrcAlpha blend и surface composition, cutoff, disable/re-enable | Textured/overlapping inputs, reload/serialization, standalone |
| Local water height | 1 | RGFloat offset, public GPU query heights, bounds, +2/−1 м, перенос, global sea level +3 м, disable/re-enable | Sloped/spline input, rendered transitions, reload/serialization, standalone |
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

Markdown-аудит активной рабочей области: в deliverable ровно 8 документов — README, PORT_PLAN, PORT_PROGRESS, FEATURE_MATRIX, TEST_RESULTS, MIGRATION, SOURCE_BASELINE и THIRD_PARTY_NOTICES. На уровне workspace отдельно находится AGENTS.md с правилами продолжения. Одинаковых по назначению дополнительных progress/status/roadmap файлов не найдено; удалять эти документы не требуется. Исторические work-копии не являются активным источником плана.

`Tools/Audit-Workspace.ps1` инвентаризирует и хеширует все файлы Assets, Packages, ProjectSettings, Tools и корня репозитория; сравнивает наличие всех оригинальных файлов, ищет missing meta и duplicate GUID. Library/Builds/исторические work-копии исключены как генерируемые/вспомогательные. Это структурная проверка, не построчное семантическое ревью всего upstream-кода.

`Verify-GuidAndCompute.ps1`: 597 оригинальных meta/compute файлов, GUID сохранены, compute bytes не изменены. Строгая `Verify-Source.ps1` остаётся FAILED для трёх Unity-normalized sprite meta (ArrowLeft, ArrowRight, Flask); это явно записано в source-preservation-after-import.json. Исторический source-preservation.json отражает состояние до импорта.

В рабочем дереве обнаружены также нормализация Ocean.mat и новые LightingData/ReflectionProbe файлы исходных example scenes. Они сохранены и перечислены в аудите; не удалены и не выданы за изменения данной доработки. Их дальнейшая оценка относится к P09.

## Оставшиеся ограничения

Полная приёмка незавершена. URP больше не вызывает Camera.layerCullSpherical, а distance/light адаптеры проходят указанные Editor fixtures. Это не полная эквивалентность исходным опциям во всех режимах рендеринга. Пробелы перечислены в FEATURE_MATRIX.md, порядок — PORT_PLAN.md; 30 тестов не закрывают всю матрицу.

## P04 — дополнительные результаты

UnderwaterBoundaryTests прошёл в составе полного 21-test suite на DX11 и DX12. Численные результаты совпадают между API; кадры meniscus, overlay и before/after-transparents просмотрены. Подключение CameraEvent в URP было уже исключено, но отключение ещё вызывало legacy RemoveCommandBuffer. Теперь обе операции согласованы; Render Graph scheduling остаётся прежним.

Первые запуски нового стенда сохранены: Boundary-initial-harness-DX11.xml (EnterPlayMode из вложенного enumerator не выполнился), Boundary-background-fixture-DX11.xml и Boundary-ocean-background-fixture-DX11.xml (в проверяемом фоне оставалась независимо затеняемая поверхность океана). EnterPlayMode вынесен в тесты с явной проверкой Application.isPlaying; overlay fixture исключает ocean layer из камеры и использует соответствующий слой эффекта. Численные критерии не ослаблялись. Overlay-focused-DX11.xml — отдельный успешный focused run, не замена полным suites.

Оба Windows Player пересобраны после runtime cleanup. Свежие Development/Release runs на DX11 и DX12 дали Errors=0, каждый с семью новыми PNG и изменяющимися GPU heights. Fingerprints управляемых Crest.dll и изменённого исходника сохранены в Evidence/Build-checkpoint.json; дата native launcher сама по себе не доказывает свежесть инкрементальной сборки. Дополнительные три запуска выполнялись параллельно для smoke-регрессии и не являются измерением производительности. Проверки всех новых boundary/overlay вариантов в standalone ещё относятся к P11.

## P05 — диагностика и область проверки

Editor DX11/DX12: пять PlanarReflectionTests прошли в полном наборе, новые clipping/distance/light PNG просмотрены. Первоначальный lighting fixture усреднял весь кадр и получал 2.08, включая пустой фон; исправлена область измерения на 16×16 у проекции центра панели. Порог освещённости >5 и отношение vertex/pixel <0.25 сохранены. `ReflectionOptions-initial-DX11.xml` и `ReflectionOptions-no-spherical-call-DX11.xml` сохраняют неудачные эксперименты: простое удаление spherical flag при сохранении layer distances приводило к пустым RT из-за oblique projection.

Первая проверка Player на обоих API выявила реальную ошибку: distance FarChanged=256 / NearChanged=0, но PixelMean=VertexMean=147.3867. Отчёты `ReflectionOptions-Player-initial-DX11.txt` / DX12.txt сохранены. Строгий диагностический Player подтвердил отсутствующие `_ADDITIONAL_LIGHTS_VERTEX` variants (Errors=2771, magenta mean=170); полный отчёт — `ReflectionOptions-Player-strict-initial-DX11.txt`, raw log сохранён локально. Одного дополнительного labeled URP asset недостаточно: ранний engine prefilter уже исключал vertex mode.

В renderer feature добавлено точечное правило ShaderKeywordFilter.SelectIf с overridePriority для обоих lighting modes; официальный IncludeAdditionalRPAssets сохраняет их на позднем URP stripping этапе. Build pipeline включает strictShaderVariantMatching, чтобы отсутствующий вариант давал ошибку вместо незаметной подстановки. Итоговые Player результаты после исправления записываются отдельно от этой истории.

После исправления Development и Release пересобраны. Все четыре запуска на DX11/DX12 прошли с Errors=0, семью свежими Ocean PNG и четырьмя ReflectionOptions PNG каждый. Во всех: FarChanged=256 / NearChanged=0 (ROI 16×16); PixelMean=147.3867 / VertexMean=0; PipelineMode=PerPixel. Strict matching остаётся включённым. Fingerprints Crest.dll, Crest.URP.dll и изменённых runtime sources — Evidence/Build-checkpoint.json. Три последних запуска выполнялись параллельно и не являются профилированием. Development DX11 выбрал Intel Iris Xe, остальные три Player и Editor использовали NVIDIA RTX 3050 Ti; это функциональная проверка, не сравнение производительности адаптеров. Distance PNG Development DX11 и lighting/Ocean_Reflections PNG Release DX12 просмотрены.

Перечисленные выше пробелы P05 закрыты следующими targeted fixtures; полная planar acceptance по-прежнему требует P08–P11 (stress, визуальная эквивалентность, стоимость, standalone-варианты всех стендов).

## P05 — завершение целевых проверок

Основной view имеет независимый pixel-light readback (150.2266 до/после), shadow option меняет только отражение (1.3945 → 45.9375; основной view 48 → 48). После resize 60 surface chunks привязывают текущую RT. Контролируемое исключение в beginCameraRendering проходит через native boundary как ожидаемый LogType.Exception; LogAssert разрешает только сообщение этого стенда. Renderer flags обеих сцен, ocean root, GL/quality и recursion guard восстанавливаются; следующий render request проходит.

Обнаружены и исправлены две runtime-проблемы: Camera.renderingPath не выбирал URP Forward renderer, теперь используется публичный SetRenderer; Terrain мог повторно попадать в список восстановления во время обхода обычной/persistent scene и затирать исходный флаг второй записью false. Списки обхода явно очищаются, уже скрытый Terrain повторно не регистрируется. Неудачный XML сохранён в ReflectionCanvasTerrain-initial-DX11.xml. Временная диагностика также выявила compile error на запрещённом в этой конфигурации GetInstanceID; диагностический вызов заменён, затем диагностические логи убраны.

Known-object кадры маршрутов Forward/Forward+/Deferred, main-light, shadow и world UI просмотрены. До добавления пены checkpoint 29/29 прошёл на обоих API. После runtime-правок оба Player пересобраны и четыре свежих runs с -ReflectionOptions прошли Errors=0; сейчас все четыре выбрали NVIDIA RTX 3050 Ti. Это заменяет прежний checkpoint с Intel для Development DX11. Последующие изменения P06 и стабилизация shadow fixture затрагивают только Editor tests, повторная сборка Player из-за них не нужна.

## P06 — первый численный стенд пены

FoamSimulationTests отключает FFT, flow и shoreline generation, фиксирует фазу исходного Gerstner и проверяет исходную whitecap generation. После отключения источника сравнивает один interior pixel с `foam * (1 - fadeRate / frequency)^30`, fadeRate=2, frequency=30; R16 quantization допускается в пределах 0.005. На DX11: baseline=0, generated peak=1, после 30 substeps=0.124939, ожидается=0.1262128. Первоначальный стенд ошибочно считал 30 yield ticks за 30 simulation steps; исправлен через BuildCommandBufferBase._lastUpdateFrame / LastUpdateSubstepCount. Порог не ослаблялся, compute не менялся. Исходный failed XML и числа — Foam-initial-DX11.xml / .txt.

Первая полная регрессия с новым стендом дала 29/30: сама пена прошла, но старый ShadowAtlasAccumulatesOcclusion получил нулевой R8 readback на очень малом Time.deltaTime. Отчёт сохранён в Foam-suite-initial-DX11.xml. Исходный temporal filter умножает вес на deltaTime; при неограниченной batch-частоте вклад квантовался в ноль. Только shadow fixture теперь задаёт captureFramerate=60 и восстанавливает его в teardown; порог >0, runtime weights и R8 format сохранены. Focused DX11 дал PeakOcclusionByte=8.

Итоговая полная регрессия: 30/30 на обоих API. DX12 повторил baseline=0, generated peak=1, decayed sample=0.124939 при expected=0.1262128. Shadow readback на обоих API — 8. Пена пока проверена только в области whitecaps и decay; полный P06 не закрыт. Порядок новых тестов и очистка состояния проверены именно в полном наборе, не выведены из focused запуска.

После этого добавлен отдельный shoreline/toggle fixture. Focused 1/1 прошёл на DX11 и DX12: плоский floor через реальный OceanDepthCache дал height=-0.4999933 м; при maxDepth=1, strength=1, fade=0 и 30 substeps по 1/30 с ожидается foam=0.5, получено 0.4985352 на обоих API (допуск 0.005). При выключении CreateFoamSim менеджер исчезает и старая RT уничтожается; после включения создаётся новый пустой target, foam=0. Отчёты — FoamShoreline-Direct3D11.txt / Direct3D12.txt. Полные 30-test XML предшествуют добавлению этого 31-го теста; их не заменяли focused результатами. Runtime/shader/scene для этой проверки не менялись.

Оставшаяся область foam acceptance: surface visual advection, разные качества/разрешения и сравнение поверхности с исходным Crest. Транспорт данных и prewarm/reload проверены дополнительными стендами ниже. Успех численных fixtures не переводит всю строку пены в PASSED.

### Перенос локального пятна пены течением

Новый `KnownFlowTransportsFoamPatchByThreeMetres` использует исходные RegisterFoamInput/Override Foam и RegisterFlowInput/Fixed Direction shaders. FFT, wave/shoreline generation и fade выключены. После однократного засева источник удалён из регистрации; измеряются центр массы и сумма interior LOD0, проверяются finite values и [0,1] bounds; RT до/после каждого направления сохранены в PNG.

На DX11 и DX12: flow readback=(3,0), за 30 шагов по 1/30 с ShiftX=2.998027 м / ShiftZ=0. После смены направления readback=(0,3), второй участок ShiftX=0 / ShiftZ=2.998032 м. Суммарная масса: 2304 → 2303.919 после первого участка; после обоих MassRatio=0.9999497. Позиционный допуск 0.1 м, mass bounds 0.95–1.05 не менялись. Снимки обоих API просмотрены: перенос и ожидаемая фильтрационная диффузия видны. Это транспорт данных симуляции, не полная приёмка flow rendering/нормалей поверхности или splines.

Первоначальные неудачные стенды сохранены: FoamFlow-initial-DX12.xml/.txt и PNG (ShiftX=3.197887), FoamFlow-turn-initial-DX11.xml/.txt (ShiftZ=3.997425), FoamFlow-clock-initial-DX11.xml (time pulse не потреблялся). Включение flow через настройки перестраивало OceanRenderer и сбрасывало ранее установленный временный provider; это исходное поведение Crest подтверждено в upstream, не ошибка переноса compute. Clock устанавливается после завершения настройки и проверяется через публичный TimeProvider. Для точного шага тестовый ITimeProvider выдаёт 1/30 только одному реально потребившему его кадру и 0 остальным; перед snapshot проходят три обновления с нулевым dt для стабилизации ping-pong textures. Runtime и исходные алгоритмы не менялись, пороги не ослаблялись. Контракт rebuild/time provider уточнён в MIGRATION.md.

После исправления стенда весь FoamSimulationTests прошёл 3/3 на DX11 и DX12, включая очистку между flow, shoreline/toggle и whitecap/decay сценариями. Теперь в исходниках 32 теста; полный сохранённый checkpoint по-прежнему 30/30 до добавления shoreline/flow, дополнительные проверки отражены отдельными focused XML. В этой сессии менялись только Editor test и документы: runtime/shader/scene не менялись, повторять Player builds не требуется.

## P06 — prewarm при нулевом времени и scene reload

`ShorelinePrewarmAtZeroDeltaSurvivesSceneReload` использует исходный OceanDepthCache с floor height=-0.4999933 м. При maxDepth=1, shoreline strength=1, fade=2 исходная формула prewarm даёт `(1 - 0.5 / 1) / 2 = 0.25`. На обоих API без prewarm при dt=0 измерено 0; после пересоздания foam с prewarm=true — ровно 0.25. Через восемь дополнительных ocean updates при dt=0 остаётся 0.25: начальный вклад не повторяется каждый кадр.

Сцена действительно перезагружается через LoadSceneAsync. Старые foam/depth RT уничтожаются; тест заново создаёт известные floor/cache/settings и повторяет контроль и prewarm с теми же результатами. Это проверка жизненного цикла сцены и воспроизводимости симуляции; сохранение пользовательских настроек/профилей на диск этим не доказано. Time.timeScale временно равен нулю, чтобы fallback clock при rebuild тоже не продвигал симуляцию; значение восстанавливается в finally. Алгоритмы runtime/compute и сцены не менялись.

Измерения — FoamPrewarm-Direct3D11.txt / Direct3D12.txt. Focused 1/1 на каждом API, затем свежая полная регрессия **33/33 на DX11 и DX12**, failed=0, skipped=0. Полные XML теперь включают все четыре foam fixtures и заменяют прежний 30-test checkpoint; исторические результаты выше описывают предыдущие этапы. Свежие smoke PNG волн, отражений и underwater просмотрены; это не сравнение с Built-in. Новых Markdown-файлов и Player builds для Editor-only изменений не создавалось.

## P06 — влияние flow на поверхность

`FlowSurfaceTests` содержит два отдельных сценария с реальным Ocean shader: normal mapping без foam и foam без normal mapping. Ocean time=0.5, delta=0; FFT/underwater/caustics/shadows/transparency выключены для изоляции. Для нормалей используется исходное процедурное небо Crest с заданным цветовым градиентом и normal strength=0.36. Для пены исходный Override Foam input сначала заполняет поле до 0.65 (проверено GPU readback), затем снимается с регистрации; источники и fade выключены, dt=0 удерживает поле постоянным. Это изолирует текстурное течение поверхности от уже проверенного транспорта данных пены.

Исходный Fixed Direction input меняется 0 → 3 → 0 м/с; RGFloat readback подтверждает (3,0). URP SingleCameraRequest снимает неизменную main camera в 320×180 RT. Порог отличия — сумма RGB bytes >12. На **обоих API** flow меняет 25,366 pixels для нормалей и 40,211 для пены. Возврат к 0 м/с даёт 0 изменённых pixels относительно baseline. Независимый negative control: при выключенном _FLOW_ON смена поля 0 → 3 м/с даёт 0 изменённых pixels. PNG baseline/flow/restored/keyword-off сохранены; пары normals/foam просмотрены на обоих API. Это контролируемая визуальная реакция поверхности, не количественное доказательство всех режимов или эквивалентности Built-in.

Failed harness reports сохранены как FlowSurface-*-DX11.xml и initial numerical reports/PNG. Сначала получены validation errors из-за несогласованного foam toggle и выключенного flow keyword при teardown; настройки и очистка исправлены, ошибки не подавлялись. Вариант без foam также выявил пустой root: графический batch opt-in устанавливался после загрузки сцены, а feature changes не обязаны перестраивать геометрию. Белый кадр не был принятым результатом; теперь opt-in задаётся до OpenScene, наличие видимых chunks и правильного материала явно проверяется. Поиск chunks идёт от Root, поскольку они HideAndDontSave; проверка материала ограничена frustum, так как Crest обновляет материал в OnWillRenderObject. Пороги image assertions не ослаблялись; процедурный градиент сам по себе не устранял пустой root.

На этом этапе focused прошёл 2/2 на DX11 и DX12, failed=0/skipped=0; в исходниках было 35 тестов, полный checkpoint ещё оставался 33/33. Эти отчёты сохранены отдельно как FlowSurface-DX11.xml / DX12.xml. Позднейшая общая регрессия 37/37 описана ниже. Runtime/shader/scene не менялись, Player не пересобирался. Структурный audit после добавления .cs/.meta: 1256 files, missing meta=0, duplicate GUIDs=0, missing upstream=0.

## P06 — сплайн течения

`SplineFlowUpdatesDirectionSpeedPositionAndRegistration` использует исходные Crest.Spline.Spline, SplinePointDataFlow, RegisterFlowInput, GenerateMeshFromSpline и FlowSplineGeometry.shader. Две точки −12…12 м, ширина ribbon Radius=8 м, subdivisions=2. LOD0 RGFloat readback проверяет реальные world-space позиции по snapped cascade transform; каждая контрольная скорость усредняется по 3×3 texels. Все значения поля должны быть конечными, peak ≤3.01 м/с; векторный допуск 0.05 м/с.

Оба API дали одинаковый результат во всех шести состояниях: начальное течение (3,0), после поворота точек — (0,3), после изменения FlowVelocity — (0,−2). Перенос обеих точек на +6 м по X оставляет 0 в старом центре и (0,−2) в новом. Снаружи ribbon — 0. Отключение RegisterFlowInput даёт peak=0 по всему LOD0; повторное включение восстанавливает (0,−2). PNG полей просмотрены на обоих API. Это проверка прямого ribbon, программного изменения точек/скорости и регистрации, а не приёмка curved/closed сплайнов, Unity Splines adapter, Editor gestures или serialization.

Отчёты — FlowSpline-Direct3D11.txt / Direct3D12.txt, отдельные FlowSpline-DX11.xml / DX12.xml. Focused 1/1 на каждом API. Исходные runtime/compute/shader не менялись.

## P06 — вырезание поверхности

`CubeSphereAndGeometryClipSurfaceAndDeregisterCleanly` проверяет исходные RegisterClipSurfaceInput и clip shaders на плоском океане с фиксированными time/delta=0. Ортографическая камера сверху, RT 192×192, под непрозрачной водой magenta floor. Cube/sphere scale=(8,4,8), геометрический Plane размером 8×8 м. Сравниваются R8 clip field и итоговый URP surface image.

Примитивы сохраняют **градиент**, а не бинарную маску. Для cube ожидается `saturate(1-max(abs(x),abs(z))/8)`, для sphere — `saturate(1-length(x,z)/8)` при y=0; surface clips при >0.5. Ожидание вычисляется в центре выбранного GPU texel. На обоих API cube center=0.9960785, corner=0.6196079 (expected 0.9947917/0.6197916), sphere center=0.9921569, corner=0.4627451 (expected 0.9926343/0.4623042); геометрический input даёт 1 внутри и 0 снаружи. Допуск 0.005 учитывает R8 quantization. Исходный failed report с неверным предположением о бинарном значении примитивов сохранён в ClipSurface-binary-assumption-DX11.xml/.txt; runtime не менялся, image assertions не ослаблялись.

Cube и geometry открывают 4096 magenta pixels, sphere — 3220; столько же pixels отличаются от baseline, border остаётся неизменным. После снятия каждого input с регистрации peak clip field=0 и restored changed pixels=0. PNG baseline/holes/restored просмотрены на обоих API. Overlaps/inversion, convex hull/texture modes, wave-driven clipping, reload и standalone coverage остаются открытыми; строка clip не объявлена полностью PASSED.

Отчёты — ClipSurface-Direct3D11.txt / Direct3D12.txt, ClipSurface-DX11.xml / DX12.xml. Focused 1/1 на каждом API. Новых Markdown нет. Audit после добавления SurfaceInputTests.cs/.meta: 1258 files, missing meta=0, duplicate GUIDs=0, missing upstream=0.

После добавления spline/clip выполнена полная интеграционная регрессия: **37/37 DX11 и 37/37 DX12**, failed=0/skipped=0. Все четыре foam, два surface flow, новый spline и clip fixtures включены в общий набор. Полные XML действительно обновлены полными runs, не focused. Изменения этой сессии относятся только к Editor tests/документам/evidence; runtime, shader и demo scene не менялись, актуальность Player по runtime сохраняется.

## P06 — albedo и локальный уровень воды

`AlbedoColorBlendCutoffAndRegistrationAffectSurface` использует исходные RegisterAlbedoInput и Albedo/Color shader. При фиксированных time/delta=0, FFT off, без normal mapping/foam/transparency/post-processing/fog проверяются RGBA32 LOD0 (768×768, отдельно от общего разрешения океана 384) и URP surface capture 192×192. Непрозрачные red/blue дают точные значения поля и по 4096 соответствующих pixels поверхности. Перенос patch на +6 м по X очищает старый центр и даёт blue в новом.

Исходный SrcAlpha/OneMinusSrcAlpha применяется ко всем RGBA: при red alpha=0.75 ожидается field=(0.75,0,0,0.5625), получено (0.7490196,0,0,0.5607843), допуск 2/255. Для итогового RGB поверхность сравнивается с `lerp(baselineLinear, measuredAlbedo.rgb, measuredAlbedo.a)` с обратным sRGB преобразованием. Expected RGB≈(0.682,0.048,0.137), actual≈(0.682,0.051,0.137), допуск 0.03. Alpha итоговой непрозрачной поверхности остаётся 1; сравнение здесь относится к RGB, не к промежуточному alpha из Color.Lerp в диагностической записи.

При alpha=0.25 и cutoff=0.5 всё поле RGBA очищено; surface changed pixels относительно baseline=0. Снятие input с регистрации при возвращённой alpha=1 даёт тот же нулевой результат; включение восстанавливает blue patch. Оба API дали одинаковые результаты, PNG просмотрены. Измерения — Albedo-Direct3D11.txt / Direct3D12.txt, первоначальные отдельные passed XML — Albedo-DX11.xml / DX12.xml.

`LocalWaterHeightUpdatesGpuQueriesAndHeightBounds` использует исходный RegisterHeightInput, Set Base Water Height Using Geometry, SimSettingsSeaFloorDepth с allowVaryingWaterLevel=true и публичный SampleHeightHelper. Проверен формат R32G32_SFloat; только канал Y содержит local offset, канал X без floor input законно содержит исходный -infinity sentinel. Все Y в LOD0 проверяются на finite values; контрольные offsets имеют допуск 0.01 м. Публичные queries должны восемь последовательных ocean frames совпадать с ожидаемой высотой в пределах 0.05 м, с ожиданием реальной async готовности.

На обоих API input даёт +2 м, затем −1 м; bounds reporter возвращает соответствующие min/max. Перенос на +6 м очищает старую область. При global sea level=+3 м локальная геометрия остаётся на −1 м: field offset=−4 м, query height=−1 м; вне неё query=+3 м. Отключение даёт peak offset=0 и query=+3 м везде, удаляет input из HeightReporters; включение возвращает offset=−4 м/query=−1 м и регистрацию bounds reporter. Измерения — LocalHeight-Direct3D11.txt / Direct3D12.txt.

Оба новых сценария прошли на обоих API. Весь SurfaceInputTests (clip/albedo/local height) прошёл **3/3 на DX11 и DX12**, failed=0/skipped=0, включая очистку между тестами. На этом историческом этапе в исходниках было **39 тестов**, полный checkpoint оставался **37/37**; позднейший полный 40-test run включает оба сценария (см. текущую таблицу). Runtime/shader/demo assets не менялись; Player builds не повторялись. Последующие saved-profile/depth-cache round-trip результаты приведены ниже. Новых Markdown нет.


## P06 — сохранение запечённой глубины

`BakedDepthSurvivesDiskPrefabReloadAndRelativeMovement` вызывает тот же `OceanDepthCacheEditor.SaveCacheToFile`, что кнопки Inspector. Исходные RGBAHalf readback и EXR OutputAsFloat сохранены; импорт SingleChannel/Red, linear, uncompressed, RFloat, no mipmaps, Bilinear/Clamp/NPOT None проверен. Экспорт вынесен из GUI в Editor-only метод; `finally` теперь восстанавливает прежнюю RenderTexture.active и уничтожает временную CPU Texture2D, включая реальный DirectoryNotFoundException после выделения текстуры. Исходный exporter не освобождал эту Texture2D и сбрасывал active target в null. Отдельно проверен sentinel active target и отсутствие оставшейся экспортной текстуры.

На обоих API исходный legacy floor −3.5 м измерен как −3.500006 м. При owner Y=2 м первый bake сохраняет относительные −5.5 м. После снижения floor на 1 м rebake по прежнему пути сохраняет −6.5 м с тем же asset GUID. EXR и prefab принудительно импортированы повторно; после настоящего LoadSceneAsync и удаления исходной геометрии Baked cache восстанавливает world depth −4.5 м. Перенос owner к Y=5 м даёт −1.5 м. Выключение возвращает исходный negative-infinity sentinel, включение — −1.5 м. Допуск 0.02 м. Старая realtime RT освобождена; восстановленный Baked cache не создаёт realtime CacheTexture. Prefab reference и importer settings сохранены.

Артефакты: DepthBaked-Direct3D11.exr / Direct3D12.exr, DepthPersistence-Direct3D11.txt / Direct3D12.txt и отдельные DepthPersistence-DX11.xml / DX12.xml. Initial failed XML/.txt сохранены: первый стенд читал центр LOD после смены камеры при reload, за пределами cache. Исправлен только стенд — новая камера возвращается к world origin, texel определяется по snapped cascade transform с bounds assertions; допуск не ослаблялся.

После этого полный набор прошёл **40/40 на обоих API**, failed=0/skipped=0; UTC start/end DX11 12:45:14/12:45:40, DX12 17:12:43/17:13:41 (2026-10-22). Это включает albedo/local height, depth fixture и все прежние сценарии. Complex terrain, остальные cache modes, ручная работа Inspector и standalone depth round-trip этим не доказаны.

## P06 — Editor profiles и spline data

`ProfilesSplineReferencesAndUndoSurviveAssetRoundTrip` сохраняет отдельные foam/depth/spectrum assets и inactive prefab, выгружает профили из памяти и принудительно импортирует их и prefab повторно. Проверяет все заданные значения, private fields, spectrum arrays и asset references. Закрытый трёхточечный Crest spline сохраняет позиции, radius multipliers, signed flow speeds и input overrides. Исходные OceanWaveSpectrumEditor/SplineEditor разрешаются через Editor.CreateEditor. Undo возвращает radius 12.5, Redo — 20; повторное сохранение radius=20 сохраняет prefab GUID.

Focused **1/1 на DX11 и DX12**, failed=0/skipped=0; UTC start/end 17:15:55/17:15:56 и 17:16:21/17:16:22 (2026-10-22). Отчёты — EditorPersistence-DX11.xml / DX12.xml и EditorPersistence-Direct3D11.txt / Direct3D12.txt. Первый compile error ошибочного namespace теста сохранён в EditorPersistence-compile-initial-DX11.txt; после исправления проверены свежие XML. На этом этапе в исходниках был **41 тест**, полный checkpoint составлял **40/40**; focused XML его не заменял. Позднейший общий checkpoint 42/42 включает этот сценарий. Это data/binding/Undo проверка, без вызова OnInspectorGUI и без интерактивных Scene view gestures; curved/closed rendering не принят.

Структурный audit 2026-10-22: 1262 files, missing meta/duplicate GUID/missing original files=0. Обе уникальные временные test-owned папки удалены. Новых Markdown нет. Изменение production code ограничено #if UNITY_EDITOR; runtime/compute/shaders/demo scenes не менялись, поэтому Player повторно не собирался. Build-checkpoint source hash OceanDepthCache.cs предшествует Editor-only изменению; это не новый runtime build checkpoint.


## P06 — загрузка сохранённой сцены с четырьмя входами

`SavedSceneRestoresInputFieldsReferencesAndRegistration` создаёт отдельную test-owned сцену на основе Ocean_Calm, сохраняет материалы и профиль, затем принудительно импортирует их и загружает сцену через LoadSceneAsyncInPlayMode. Готовые demos не изменяются. Проверяются material GUIDs, profile references, serialized TimeProviderCustom (time=0.5/delta=0), GPU поля до/после reload, уничтожение прежнего OceanRenderer и height input, освобождение четырёх прежних RT. После reload входы выключаются и снова включаются; height reporter снимается с регистрации и возвращается.

Оба API: flow=(3,0) м/с; local height offset=+2 м; albedo=(0.7490196,0,0,0.5607843) при source red alpha=0.75 (expected 0.75/0.5625, допуск 2/255). Outside samples дают 0. Cube clip учитывает исходный трёхмерный SDF и локальную высоту воды: `1-max(abs(x)/8,abs(z)/8,2/4)` = 0.5 в центре, measured=0.4980392 (допуск 0.005). Это значение поля, не утверждение об отверстии: surface clipping использует >0.5. После выключения всё LOD0 flow/clip/albedo и канал Y depth равны 0; включение восстанавливает значения. Depth X без floor сохраняет допустимый negative-infinity sentinel. Все flow компоненты и height offsets проверены на finite values.

Первый failed XML сохранён в InputPersistence-initial-DX11.xml: стенд искал OceanRenderer.Instance до Editor update, исправлен на поиск компонента сцены. Второе ошибочное ожидание плоского clip SDF сохранено в InputPersistence-overlap-initial-DX11.xml/.txt. Исходный shader действительно добавляет sea-level offset перед вычислением SDF; исправлена формула теста с прежним допуском, runtime/shader не менялись. Focused окончательной версии 1/1 на каждом API: DX11 start/end 17:25:28/17:25:39Z, DX12 17:25:57/17:26:08Z (2026-10-22). Числа — InputPersistence-Direct3D11.txt / Direct3D12.txt, отдельные XML — InputPersistence-DX11.xml / DX12.xml.

Это восстановление конфигурации и GPU регистрации/полей, без новой проверки итоговых surface captures после reload. Textured/overlapping albedo, все clip modes, curved/closed/sloped splines и standalone варианты остаются открытыми. Production runtime/shader/scene assets не менялись; добавлен Editor fixture, Player не пересобирался.


После добавления сохранённой сцены выполнена полная регрессия с проверкой очистки общего состояния между fixtures: **42/42 на DX11 и DX12**, failed=0/skipped=0. UTC start/end: DX11 17:26:29/17:27:25Z, DX12 17:27:49/17:29:00Z (2026-10-22). Новый общий checkpoint включает оба ранее focused-only Editor persistence fixtures; full XML заменены только полными runs. Это заменяет исторический 40-test checkpoint в текущей таблице. Обычные ocean/underwater smoke captures просмотрены; новый reload fixture численный и не заявляет surface-image equivalence.

Структурный audit 2026-10-22T17:29:33.5152770Z: 1264 files, missing meta/duplicate GUID/missing original files=0. Test-owned InputRoundTrip folders удалены; активных Unity операций нет.
