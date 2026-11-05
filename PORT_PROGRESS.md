# Точка продолжения

Обновлено: **2026-11-05**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Работать только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап: P10 ACTIVE

P00–P09 DONE в scoped-области PORT_PLAN; P11/P12 TODO. Единственный порядок задач — PORT_PLAN.md; результаты и ограничения — TEST_RESULTS.md/FEATURE_MATRIX.md. Не повторять закрытые P09 fixtures без изменения пути.

Ранее закрыта оптимизация: lazy cache исходного Helpers.IsIntelGPU в GetMaskColorRTD, сброс OnEnable. Intel RFloat/non-Intel RHalf правило сохранено. Четыре native warm GC windows: Off/On448B Submit на обоих API, все10 underwater buckets0; прежние дополнительные576B устранены в этом пути. Это не whole-asset GC-free и не GPU/VRAM performance acceptance.

## Последний завершённый checkpoint

Активных Unity/Player операций нет. P10-MixedWaves scoped DONE: original FFT-first left native FFT/cache1 after all owners; Gerstner-first left auto swell spectrum. Family Type/count registry dispatches each built-in shared cleanup; global ShapeWaves count retains wind spectrum until last global owner. No component delegates/objects retained in registry. Corrected focused3/API/full113+interactive2/API, both new builds/4 smokes pass; remaining family query8 ready and native output/spectrum survives; all outputs/auto spectra/cache0 after final destruction. Old frozen-time16-layer FFT case still passes. Original runtime/fixture failures retained separately; raw/source/Player+Editor test assemblies -evidence.zip/-files.json, current -checkpoint.json/-cause-summary.json, previous111 normal bytes -before.zip. GUID/compute597 and audit1404/rootMD8 pass. No active operation.

Текущий normal runtime checkpoint — Build-checkpoint.json и P10-MixedWaves-checkpoint.json: full113+interactive2/API, both builds/4 smokes;28 capture frame pairs match, RenderData warnings0. bf92647/full110 — historical. P10-GpuCache/P10-Retention historical acceptance/limits — TEST_RESULTS. Не переоткрывать закрытые windows. Root Markdown8, stable audit — Evidence/workspace-audit.json.

Evidence/Build-checkpoint.json — current source/build/material/report fingerprints. P10-MixedWaves-checkpoint.json — current milestone/mixed/FFT native reports. Older ShadowOrder/GpuCache/Retention/ActiveOwners evidence remains historical in TEST_RESULTS; its canonical full-suite fixture paths are not new acceptance. Current full-suite regenerated reports are archived before restoring historical fixture artifacts. Prior failed FFT control is retained separately.

## Точное следующее действие

Следовать PORT_PLAN разделу «Приёмка P10 — оставшаяся последовательность», пункт3 NEXT: generator re-key/convergence ownership. Прочитать FFTCompute.OnGenerationDataUpdated: ветка существующего newHash удаляет oldHash. Подготовить bounded два набора условий → convergence → remaining old-condition consumer control, отслеживая owner map/native RT/disposal и восстановление потребителя; без утверждения утечки до actual evidence. Не менять algorithm/hash semantics и не повторять accepted cleanup/counter/Calm windows. P10 GPU stability/NonLocal/other configurations и P11 остаются открытыми.

Предыдущие14 Shadow RenderData warnings/Player теперь resolved только для установленной smoke-capture причины; validation не подавлялась. Для обычных пользовательских manual Submit до Ocean update требуется соблюдать порядок обновления, это не автоматическая поддержка любого порядка внешнего rendering. P10 ACTIVE, P11/P12 TODO; полный порт не завершён.

## Сохраняемые изменения и ограничения

- Foreign Ocean.mat (4 serialization fields) и baked LightingData/ReflectionProbe folders исходных BoatWakes/boat/threeboats/Examples/main/whirlpool: не stage/reset/delete.
- Pre-existing dirty Buoyancy/ResourceLifecycle обоих API, focused XML и untracked FFT/leak-stack diagnostics: не присваивать этому изменению.
- Два test-mutated материала TransparentPrimitive/OffWhite архивированы перед rebuild и восстановлены из HEAD. ReflectionVertexVariants.asset имел только trailing-space serialization diff: архивирован и восстановлен. ProjectSettings фактического diff не имеет.
- Historical evictable2490368B NOT REPRODUCED, owner не доказан/not fixed; DX11 retained/growing DXGI usage остаётся P10. CPU Submit/engine inventory не GPU time/точный VRAM. Intel/XR hardware unavailable.
- P11 содержит оставшиеся original examples/configuration/standalone/bake/lighting варианты; UnderwaterLighting fill0 не принят. Не переоткрывать P09 и не выдавать scoped smoke за полную приёмку.

В корне только8 обязательных Markdown. Не создавать дополнительные progress/status/roadmap документы. Не GenerateScenes, не менять work/crest-upstream и historical work/diagnostic-6000.6. Commit checkpoint — git log -1.
