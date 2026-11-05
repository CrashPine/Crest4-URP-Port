# Точка продолжения

Обновлено: **2026-11-05**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Работать только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап: P10 ACTIVE

P00–P09 DONE в scoped-области PORT_PLAN; P11/P12 TODO. Единственный порядок задач — PORT_PLAN.md; результаты и ограничения — TEST_RESULTS.md/FEATURE_MATRIX.md. Не повторять закрытые P09 fixtures без изменения пути.

Ранее закрыта оптимизация: lazy cache исходного Helpers.IsIntelGPU в GetMaskColorRTD, сброс OnEnable. Intel RFloat/non-Intel RHalf правило сохранено. Четыре native warm GC windows: Off/On448B Submit на обоих API, все10 underwater buckets0; прежние дополнительные576B устранены в этом пути. Это не whole-asset GC-free и не GPU/VRAM performance acceptance.

## Последний завершённый checkpoint

Активных Unity/Player операций нет. P10-ActiveOwners scoped DONE:2 active Interactions enable/disable/re-enable/unload cycles/API, errors0. FFT128/Foam/Dynamic +diagnostic Flow manager/query (no nonzero Flow input acceptance).8 live native buffers/198592 logical bytes;44 tracked wrappers/API invalid after unload.8 FFT RTs reused on input toggle,7 simulation RTs destroyed;70 tracked RT native objects/API destroyed by final unload, engine snapshot8 RT. Generator dictionary entries1→2 retained after native cleanup, source identical upstream: managed cache policy unresolved, no native leak claim. Raw/source/3DLLs/commands hash-indexed in P10-ActiveOwners-evidence.zip/-files.json; summary -summary.json. Default12 build hashes unchanged. Earlier P10-Counters capability and prior bounds remain TEST_RESULTS, not repeated.

Последний normal runtime checkpoint bf92647: full110+interactive2/API, both builds/4 smokes;28 capture frame pairs match, RenderData warnings0. P10-GpuCache/P10-Retention historical acceptance/limits — TEST_RESULTS. Не переоткрывать закрытые windows. Root Markdown8, stable audit — Evidence/workspace-audit.json.

Текущие source/build/material/report fingerprints — Evidence/Build-checkpoint.json и P10-ShadowOrder-checkpoint.json. Причинный контроль — -cause-summary.json; raw/full reports/source/assembly — -evidence.zip/-files.json, previous normal bytes — -before.zip. Historical P10-GpuCache и P10-Retention evidence остаются в TEST_RESULTS: cache optimization принята, retention10-cycle ограниченная область закрыта; это не general VRAM/GPU acceptance. Не повторять закрытые windows. Old full-suite reports восстановлены после сохранения новых bytes в архиве; их canonical paths не являются новой полной visual acceptance всех функций.

## Точное следующее действие

Следовать PORT_PLAN разделу «Приёмка P10 — оставшаяся последовательность», пункт3 NEXT: managed FFT cache policy. Прочитать ShapeWaves.OnDestroy/DestroySharedResources, ShapeFFT.DestroySharedResources, FFTCompute.CleanUpAll/GetWaveData и upstream comparison. Проверить stale entry versus active shared generator, влияние spectrum identity/reload и границу очистки; подготовить focused shared-generator/reload control перед runtime correction. Не очищать _generators ради нулевого счётчика; нативные ресурсы уже освобождаются. Не повторять Calm/counter/active-native accepted windows. GPU timing stability/attribution, NonLocal/other owners и P11 configs остаются открытыми.

Предыдущие14 Shadow RenderData warnings/Player теперь resolved только для установленной smoke-capture причины; validation не подавлялась. Для обычных пользовательских manual Submit до Ocean update требуется соблюдать порядок обновления, это не автоматическая поддержка любого порядка внешнего rendering. P10 ACTIVE, P11/P12 TODO; полный порт не завершён.

## Сохраняемые изменения и ограничения

- Foreign Ocean.mat (4 serialization fields) и baked LightingData/ReflectionProbe folders исходных BoatWakes/boat/threeboats/Examples/main/whirlpool: не stage/reset/delete.
- Pre-existing dirty Buoyancy/ResourceLifecycle обоих API, focused XML и untracked FFT/leak-stack diagnostics: не присваивать этому изменению.
- Два test-mutated материала TransparentPrimitive/OffWhite архивированы перед rebuild и восстановлены из HEAD. ReflectionVertexVariants.asset имел только trailing-space serialization diff: архивирован и восстановлен. ProjectSettings фактического diff не имеет.
- Historical evictable2490368B NOT REPRODUCED, owner не доказан/not fixed; DX11 retained/growing DXGI usage остаётся P10. CPU Submit/engine inventory не GPU time/точный VRAM. Intel/XR hardware unavailable.
- P11 содержит оставшиеся original examples/configuration/standalone/bake/lighting варианты; UnderwaterLighting fill0 не принят. Не переоткрывать P09 и не выдавать scoped smoke за полную приёмку.

В корне только8 обязательных Markdown. Не создавать дополнительные progress/status/roadmap документы. Не GenerateScenes, не менять work/crest-upstream и historical work/diagnostic-6000.6. Commit checkpoint — git log -1.
