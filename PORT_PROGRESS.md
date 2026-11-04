# Точка продолжения

Обновлено: **2026-11-04**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Работать только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап: P10 ACTIVE

P00–P09 DONE в scoped-области PORT_PLAN; P11/P12 TODO. Единственный порядок задач — PORT_PLAN.md; результаты и ограничения — TEST_RESULTS.md/FEATURE_MATRIX.md. Не повторять закрытые P09 fixtures без изменения пути.

Ранее закрыта оптимизация: lazy cache исходного Helpers.IsIntelGPU в GetMaskColorRTD, сброс OnEnable. Intel RFloat/non-Intel RHalf правило сохранено. Четыре native warm GC windows: Off/On448B Submit на обоих API, все10 underwater buckets0; прежние дополнительные576B устранены в этом пути. Это не whole-asset GC-free и не GPU/VRAM performance acceptance.

## Последний завершённый checkpoint

Активных Unity/Player операций нет. P10-ShadowOrder: smoke capture before LateUpdate caused14 Shadow warnings; queued capture at controller order300 after preserved Ocean200 fixes the cause. Diagnostic7 stale→7 current captures/14→0 warnings; exact variants/3DLLs preserved in before/after-trace.zip. Core OceanRenderer/shadow/builder restored; only demo controller +strict Run-Player check changed. Current full110+interactive2/API, both rebuilt Players and4 smokes passed;28 frame pairs match/RenderData warnings0. GUID/compute597 PASSED, stable audit1402/root Markdown8.

Текущие source/build/material/report fingerprints — Evidence/Build-checkpoint.json и P10-ShadowOrder-checkpoint.json. Причинный контроль — -cause-summary.json; raw/full reports/source/assembly — -evidence.zip/-files.json, previous normal bytes — -before.zip. Historical P10-GpuCache и P10-Retention evidence остаются в TEST_RESULTS: cache optimization принята, retention10-cycle ограниченная область закрыта; это не general VRAM/GPU acceptance. Не повторять закрытые windows. Old full-suite reports восстановлены после сохранения новых bytes в архиве; их canonical paths не являются новой полной visual acceptance всех функций.

## Точное следующее действие

Следовать PORT_PLAN разделу «Приёмка P10 — оставшаяся последовательность», пункт3 NEXT: GPU/draw capability control. Сначала прочитать прошлые failed/zero counters TEST_RESULTS P10 и ResourceShutdownProbe.Profile.cs; не повторять CPU60s/reflection/GC windows. В diagnostic Player перечислить реально доступные counters и проверить ограниченный regular-camera/nonbatch positive workload versus Empty с actual GPU/draw response. Zero/unavailable не считать принятой производительностью; сохранить support flags/raw. Затем решить конкретный следующий контроль по evidence. NonLocal attribution/remaining resource owners и P11 configurations остаются открытыми.

Предыдущие14 Shadow RenderData warnings/Player теперь resolved только для установленной smoke-capture причины; validation не подавлялась. Для обычных пользовательских manual Submit до Ocean update требуется соблюдать порядок обновления, это не автоматическая поддержка любого порядка внешнего rendering. P10 ACTIVE, P11/P12 TODO; полный порт не завершён.

## Сохраняемые изменения и ограничения

- Foreign Ocean.mat (4 serialization fields) и baked LightingData/ReflectionProbe folders исходных BoatWakes/boat/threeboats/Examples/main/whirlpool: не stage/reset/delete.
- Pre-existing dirty Buoyancy/ResourceLifecycle обоих API, focused XML и untracked FFT/leak-stack diagnostics: не присваивать этому изменению.
- Два test-mutated материала TransparentPrimitive/OffWhite архивированы перед rebuild и восстановлены из HEAD. ReflectionVertexVariants.asset имел только trailing-space serialization diff: архивирован и восстановлен. ProjectSettings фактического diff не имеет.
- Historical evictable2490368B NOT REPRODUCED, owner не доказан/not fixed; DX11 retained/growing DXGI usage остаётся P10. CPU Submit/engine inventory не GPU time/точный VRAM. Intel/XR hardware unavailable.
- P11 содержит оставшиеся original examples/configuration/standalone/bake/lighting варианты; UnderwaterLighting fill0 не принят. Не переоткрывать P09 и не выдавать scoped smoke за полную приёмку.

В корне только8 обязательных Markdown. Не создавать дополнительные progress/status/roadmap документы. Не GenerateScenes, не менять work/crest-upstream и historical work/diagnostic-6000.6. Commit checkpoint — git log -1.
