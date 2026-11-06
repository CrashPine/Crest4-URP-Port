# Точка продолжения

Обновлено: **2026-11-06**. Цель **Unity6000.6.5f1 + URP17.6.0**, Windows DX11/DX12 Render Graph. Работать только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап

P00–P10 DONE в bounded/scoped-области PORT_PLAN. P11 ACTIVE: P11.1/P11.2/P11.3-A scoped DONE; **P11.3-B NEXT**. P11.3-E optional/non-gating, альтернативный Unity GPU lightmapper не является обязательной функцией Crest. Остальные обязательства/порядок — только PORT_PLAN, измерения — TEST_RESULTS, приёмка подсистем — FEATURE_MATRIX. Не переоткрывать P09.

## Активные операции

Нет. Последовательный P11-Examples-milestone.ps1 завершён, operation.json: Milestone complete/complete. Перед новым Unity запуском всё равно проверить фактические процессы/логи; не запускать второй Editor на проекте.

## Последний завершённый checkpoint

P11.3-A: original Examples Windows Development Player,24 конфигурации/API, Forward/PC/1x/renderScale1/640x360. Public Next/wrap/Previous,3 исходных fill On/Off/On контроля, точное восстановление, errors0. Это материал/render smoke плюс указанные контроли, не вся функциональность24 примеров и не физический N/M/GUI.

Исправлены два доказанных дефекта: CamController/ExamplesController безопасно обрабатывают отсутствующие устройства ввода; RegisterClipSurfaceInput выбирает shape keyword при создании/пересоздании материала, включая Sphere enum0. Shader/compute алгоритмы не менялись. Первые1852 ошибок (1180 input +672 shader stages), затем672 shader errors и неуспешные тестовые fixtures сохранены отдельно, не засчитаны как успех.

Текущий runtime milestone: **full121/121 +actual interactive2/2 на каждом API**, ValidateAssets без asset errors, Dev/Release Success,4 последовательных свежих Player runs Errors0 (7 сцен+4 reflection captures каждый). XML test-case nodes прочитаны; оригинальные River On/Off и свежие underwater/reflections изображения просмотрены.597 GUID/compute checks Passed; прежние3 нормализованных sprite meta раскрыты. Хеши источников/сборок/отчётов — Build-checkpoint/P11-Examples-summary. Это не финальная приёмка P11.4/P12.

## Точное следующее действие

**P11.3-B:** прочитать фиксированные6 scene criteria в PORT_PLAN и существующие Original* Editor fixtures, затем реализовать их перенос в отдельный conditional Player. Не переписывать исходные сцены, не запускать GenerateScenes, не повторять все Editor per-source/physics experiments.

Предварительный source audit: BoatAlignNormal читает Keyboard.current даже при _playerControlled=false; аналогичные незащищённые пути остаются в BoatProbes и OceanDebugGUI. Это пока source finding, не записанный Player failure. В B воспроизвести эти пути вместе на отсутствующем устройстве и сгруппировать нейтральные input fixes; не прятать ошибки добавлением фиктивной клавиатуры/выключением исходных компонентов. Native throttle/buoyancy/GUI должны сохраняться. Позднейшие runtime fixes сгруппировать для финального P11.4 full/build milestone; diagnostic-only изменения проверять адресно. Не повторять A или полный набор после правки только документов/fixture.

## Evidence и сохраняемые изменения

- P11-Examples-summary/regression-files +regression-evidence.zip: текущие raw/source/actual DLLs и fingerprint manifest. Два production failure archives и fixture-attempts.zip — отдельно. Before archives содержат прежний checkpoint и точные необходимые snapshots; дублирующая большая галерея baseline удалена из архива, исходник в Git98f3084.
- Исторические отчёты восстановлены после сохранения свежих результатов. Чужие4 Buoyancy/ResourceLifecycle и focused DX11 XML восстановлены побайтно;13 untracked diagnostics сохранены. При исправлении archive filter собственные API folders восстановлены из accepted raw bytes, хеши отчётов совпали; повторных тестов для этого не было.
- Foreign Ocean.mat (4 serialization fields) и baked LightingData/ReflectionProbe dirs BoatWakes/boat/threeboats/Examples/main/whirlpool не stage/reset/delete. Test-mutated TransparentPrimitive/OffWhite восстановлены перед rebuild; variant whitespace архивирован/убран; ProjectSettings без фактического diff.
- P10 GPU-cost/driver-memory/Intel/XR ограничения остаются явными; исторический evictable2490368B NOT REPRODUCED/not fixed. Старый .015754165 history-restoration failure не объяснён новыми bounded контролями. Не объявлять полную приёмку по smoke.

В корне8 обязательных Markdown; новые status/progress/roadmap не создавать. Commit checkpoint — `git log -1`.
