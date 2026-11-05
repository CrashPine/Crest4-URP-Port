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

`-ReflectionOptions` добавляет к семи сценам контролируемую проверку дистанции и pixel/vertex lighting отражения с отдельными PNG и `reflection-options.txt`. Скрипт проверяет свежесть этих файлов, совпадение кадра снимка с текущим LOD и отсутствие RenderData validation warnings. Сборка автоматически сохраняет необходимые lighting variants; контракт для другого build pipeline описан в MIGRATION.md.

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
`OriginLifecycle-DX11.xml` / `OriginLifecycle-DX12.xml` сохраняют исходный lifecycle
checkpoint. Для wave phase/foam history использовать `-TestFilter OriginWaveTransportTests`;
численные отчёты — `OriginWaveTransport-Direct3D11.txt` / `Direct3D12.txt`.
Для всего non-batch набора
убрать `-TestFilter`. На milestones требуются обе assembly на обоих API: один
batch XML не подтверждает EOF. Исходный WaitForEndOfFrame не работает в batch,
поэтому интерактивная fixture отклоняет batch mode явным assertion.

P08.4 водоёмы можно воспроизвести обычным batch-профилем с
`-Action EditMode -GraphicsAPI DX11 -TestFilter WaterBodyTests` (повторить DX12).
Численные отчёты — `Evidence/WaterBodies-Direct3D11.txt` / `Direct3D12.txt`;
`WaterBodies-checkpoint.json` связывает исходники с XML и ограничениями проверки.
После runtime-правок требуется полный набор обоих профилей, не только этот filter.

P08.5 camera/target/underwater-stack coverage: обычный batch runner с
`-Action EditMode -GraphicsAPI DX11 -TestFilter CameraSettingsTests` (также DX12).
Все три UnityTest входят в PlayMode на верхнем уровне и проверяют этот режим явно.
Численные отчёты — `Evidence/CameraSettings-{Cameras,Targets,Underwater}-Direct3D11.txt`
и соответствующие Direct3D12; диагностический shader находится только в Tests/Editor.
Для actual URP stack и hardware DRS используется StandardRequest, для независимой
одной камеры — SingleCameraRequest. Старые basic-editmode snapshots не являются
PlayMode приёмкой; current full milestone указан в TEST_RESULTS.

P09 original reference: run `Tools/Run-Unity.ps1 -Editor <6000.6.5f1 Unity.exe> -Action EditMode -GraphicsAPI DX11 -TestFilter BuiltInReferenceTests` (then DX12). Requires the existing read-only original4.23.0 checkout at `../../work/crest-upstream`;20 source hashes are verified against Evidence/P09-Reference-inspection.json. The Editor-only fixture compiles the original shader in memory, switches actual pipelines and restores URP. Results/limits: TEST_RESULTS.md and Evidence/P09-OpaqueReference-checkpoint.json. This focused check does not replace the full regression checkpoint.

P09 optics: use the same runner with `-TestFilter OpticalReferenceTests` on DX11/DX12. It creates temporary checker-floor/box geometry, verifies the native floor height, compares original perspective transparency/refraction/scattering/unshadowed caustics, and restores pipeline state. Editor-only float probes diagnose normals/UV/depth/background sampling and retain negative controls. Measured results and limits are in TEST_RESULTS.md and Evidence/P09-Optical-checkpoint.json; the checkpoint includes the passed full75+2 per API and rebuilt Player integration checks.

P09 shadow inputs: `-TestFilter ShadowReferenceTests` with the same runner on DX11/DX12. Uses actual Built-in Standard and URP Lit receivers, independently generated native shadow resources, and the original hash-verified UpdateShadow shader. Validates off/on/off restoration and analytic hard-shadow interiors across all7 LOD layers; boundary differences are retained. Reproduction conditions and limits: TEST_RESULTS.md and Evidence/P09-ShadowReference-checkpoint.json. This does not accept final shadowed caustics/underwater RGB parity.

P09 underwater reference: `-TestFilter UnderwaterReferenceTests` with the same runner on DX11/DX12. Requires the existing original checkout; sources/driver hashes in Evidence/P09-UnderwaterReference-sources.json. The Editor-only reference invokes retained original BIRP mask/effect callbacks through a camera shim for hidden batch mode and restores URP/legacy buffers. It accepts only the unshadowed fullscreen/meniscus scope described in TEST_RESULTS.md and Evidence/P09-UnderwaterReference-checkpoint.json; shadowed flat/fullscreen reference is reproduced separately below.

P09 shadowed optical reference: `-TestFilter ShadowedOpticalReferenceTests` on DX11/DX12 with the same runner. Uses hash-verified original surface/underwater/UpdateShadow shaders, independent native shadow resources, matched opaque receiver colour, original white screen-mask default and same-pipeline native donor history for the below view.10 states/API; scope/limits and initialized-history contract: TEST_RESULTS.md and Evidence/P09-ShadowedOpticalReference-checkpoint.json. Original example-material read-only inventory: Evidence/P09-ExampleMaterial-inventory.json; migration/render acceptance remains P09.5.

P09 original example materials: menus `Crest/URP/Audit original example materials` and `Migrate original Standard and terrain materials` audit/migrate eligible source materials only, preserving the report on an unchanged rerun. Do not run broad scene generation or conversion over the examples. Reproduce original Main with `-TestFilter OriginalExampleSceneTests`, auxiliary controls with `-TestFilter MaskedExampleTests.Auxiliary`, transparent fog controls with `-TestFilter MaskedExampleTests.Transparent`. Current full runtime acceptance is90+actual non-batch2/API and rebuilt Player integration; scoped checkpoints are P09-Material/P09-MaskedExamples/P09-AuxiliaryExamples/P09-TransparentExamples in Evidence. Acceptance/limits belong in TEST_RESULTS; remaining original scene work is PORT_PLAN P09.5.4.

Для диагностики native allocations добавьте `-LeakStacks` к `Tools/Run-Unity.ps1`: он включает process-local `EnabledWithStackTrace` и записывает отдельные `*-leak-stacks.log/xml`. Режим не является измерением производительности и не меняет EditorPrefs; обычные запуски его не включают.

### P10 resource-shutdown diagnostic Player

`Crest.URP.Editor.ResourceShutdownProbeBuild.Build` builds only `Builds/P10-ResourceProbe/ResourceProbe.exe`, using a temporary URP camera control scene and unchanged Ocean_Calm. The control now includes a disabled MeshRenderer holding the exact diagnostic underwater material variant as a serialized build dependency. It adds `CREST_RESOURCE_PROBE` only to that build; normal builds exclude the diagnostic entry point. Do not replace Development/Release outputs or run GenerateScenes.

Example Editor invocation: Unity6000.6.5f1 with `-batchmode -quit -projectPath <repository> -executeMethod Crest.URP.Editor.ResourceShutdownProbeBuild.Build -logFile <diagnostic-build.log>`.

Player flags: `-crestResourceProbe -crestProbeMode Empty|Ocean|OceanUnload -crestProbeOutput <absolute-report.json>`, with `-force-d3d12` or `-force-d3d11`, optional `-batchmode`, `-crestProbeSubmit`, `-crestProbeReadPixels`, `-crestProbeDisableFeature`; set `-screen-fullscreen 0 -screen-width 960 -screen-height 540`. Launch sequentially with Start-Process -WindowStyle Hidden -Wait. ReadPixels is used only with Submit. No screenshot is written. The3s warmup is a resource diagnostic, not profiling. Results and limitations: TEST_RESULTS.md/P10-ResourceShutdown-results.json. Do not repeat the completed matrix without a new trigger.

### P10 offscreen profiling

Build the diagnostic Player with ResourceShutdownProbeBuild.Build as above (frame-timing stats enabled for this build and restored afterwards). Add `-crestProfile -crestProfileSeconds 60 -crestProbeMode Empty` or `Ocean` to `-crestResourceProbe`, force one API, set960x540/windowed, omit batchmode and launch one Player at a time with Start-Process -WindowStyle Hidden -Wait. Profiling explicitly renders to a reusable offscreen target and waits on one-pixel readback.5s warmup; CPU Submit timing is not whole-frame/GPU time or FPS. Missing/unusable counters remain unavailable.

`-crestProfileGCCheck` runs known-allocation controls instead of timing. Native CurrentValue around Submit is calibrated; System.GC thread allocation delta is not trusted. Use `-crestProfileSeconds 10` for dedicated native Submit-GC controls. Raw60s/10s reports, sampled-build source/assembly and failed pilots are in Evidence/P10-Profile-evidence.zip; portable summary in Evidence/P10-Profile-summary.json. Exact scope/limits in TEST_RESULTS.md. Do not repeat completed baselines without relevant changes.

### P10 ownership diagnostic

Build the same isolated diagnostic Player, then run `Tools/Run-ResourceOwnership.ps1 -Prefix <unused-prefix>` for the ten short sequential DX11/DX12 controls. It refuses existing raw evidence and active Unity/ResourceProbe processes. Individual flags: `-crestResourceProbe -crestOwnership -crestProbeMode Empty|Ocean -crestOwnershipReadback None|Full|Crop`, optional `-crestOwnershipNoHDR`; Empty controls add `-crestProbeDisableFeature`. Use960x540/windowed and one forced API, hidden launch. Ocean runs three actual load/unload cycles. This is not a performance window.

Before archiving, run `py Tools/Summarize-ResourceOwnership.py <prefix>` against the raw reports/logs. Existing P10-Ownership raw reports are inside Evidence/P10-Ownership-evidence.zip, with entry SHA256 in -files.json; extract to a separate directory preserving Tools/Evidence layout to revalidate without overwriting workspace evidence. Summary and exact limitations: TEST_RESULTS.md and P10-Ownership-summary.json. DXGI fields describe current-process Local/NonLocal segment budgeting, not independently measured physical VRAM; wrapper invalidation does not prove driver memory return.

### P10 underwater cost windows

Rebuild the isolated ResourceProbe, then `Tools/Run-UnderwaterCost.ps1 -Prefix <unused-prefix>` runs six sequential10s windows after5s warmup: underwater Off/On1x/On4x per API. Optional `-GraphicsAPI DX11|DX12`, `-Workload UnderwaterOff|Underwater1|Underwater4` and `-Seconds` narrow a diagnostic; they do not replace six-case evidence. Individual probe flag `-crestCost <workload>` accompanies `-crestResourceProbe -crestProfile -crestProbeMode Ocean`. The builder retains the exact runtime-enabled Calm+underwater variant without disabling strict matching or editing saved scenes/materials.

Validate raw reports with `py Tools/Summarize-UnderwaterCost.py <prefix>`. Existing raw/source/assembly and failed runs are archived in Evidence/P10-UnderwaterCost-evidence.zip, entry SHA256 in -files.json; extract to a separate directory preserving Tools/Evidence layout to revalidate. P10-UnderwaterCost-final-summary.json and TEST_RESULTS distinguish sampled-window costs from the unresolved first-render SetGlobalTexture warning. These figures are not whole-frame CPU, GPU time or isolated copy-back cost. Conditional counters are absent from default builds.

Cold startup regression: build ResourceProbe, then `Tools/Run-ColdBindings.ps1 -Prefix <unused-prefix> -GraphicsAPI DX11 -Workload Underwater1`; use DX12 and Underwater4 for the other three cases. Each fresh process renders three requests and fails on warnings. Historical omission controls1–4 exist only in P10-ColdBindings-diagnosis.zip; current build permits control0 only. Results/full regression/rebuilt Player fingerprints: TEST_RESULTS.md and P10-ColdBindings-checkpoint.json. Do not rerun completed profiling windows to check a cold binding.

Reflection CPU probe: build ResourceProbe, launch hidden/windowed960x540 with `-crestResourceProbe -crestProbeMode Ocean -crestReflectionCost -crestReflectionEnabled 0|1 -force-d3d11` (or12), `-crestProbeOutput <absolute.json> -logFile <absolute.log>`. One process at a time;5s warmup+10s measured; refuses acceptance unless actual camera counts match and no automatic renders occur. Exact source/reports/assembly in P10-ReflectionCost-evidence.zip, entry hashes in -files.json; limits in TEST_RESULTS. Do not repeat completed windows without a relevant trigger.

GC site probe: add `-crestGcSites 1` to the existing diagnostic profile with `-crestCost UnderwaterOff|Underwater1 -crestProfileSeconds 3`, Ocean mode, one forced API and960x540.5s warmup retained. Four parent buckets and nested fine regions are native managed-byte deltas, not timing; never sum parent and child buckets. DX12 calibration separates getter/lowercase/helper/cached-name ordinal comparison over200 calls. Raw source/assembly variants and results: P10-GcSites-evidence.zip/-summary.json. Optimization is pending; no need to repeat completed timing windows.

P10 GPU classification cache: current runtime/build/regression fingerprints and scope are in `Evidence/P10-GpuCache-checkpoint.json` and `Evidence/Build-checkpoint.json`; native window reports/source/diagnostic assemblies are hash-indexed by `Evidence/P10-GpuCache-files.json`. This scoped allocation correction is not complete performance acceptance.

GPU/draw diagnostic capability: `Evidence/P10-Counters-summary.json`; exact raw/source/assemblies/commands are indexed by `Evidence/P10-Counters-files.json`. Recorder lookup requires category Render as well as name (UI Toolkit duplicates exist). Positive draw restoration passes; GPU timing drift is not performance acceptance.
