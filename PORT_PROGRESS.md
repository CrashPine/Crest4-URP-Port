# Точка продолжения

Обновлено: **2026-11-05**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Работать только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап: P10 ACTIVE

P00–P09 DONE в scoped-области PORT_PLAN; P11/P12 TODO. Единственный порядок задач — PORT_PLAN.md; результаты и ограничения — TEST_RESULTS.md/FEATURE_MATRIX.md. Не повторять закрытые P09 fixtures без изменения пути.

Ранее закрыта оптимизация: lazy cache исходного Helpers.IsIntelGPU в GetMaskColorRTD, сброс OnEnable. Intel RFloat/non-Intel RHalf правило сохранено. Четыре native warm GC windows: Off/On448B Submit на обоих API, все10 underwater buckets0; прежние дополнительные576B устранены в этом пути. Это не whole-asset GC-free и не GPU/VRAM performance acceptance.

## Последний завершённый checkpoint

Активных Unity/Player операций нет. P10-Counters scoped capability DONE:4 visible nonbatch regular-camera Empty/Ocean xDX11/DX12 controls, positive64 cubes дают точные+64 draws/+768 triangles/+1536 vertices и полное geometry restoration. Render-category lookup устраняет коллизию UI Toolkit; GPU timings ненулевые, но дрейфуют после restoration — cost/API/FPS acceptance нет. Hidden0-render pilots и name-only UI collision pilots rejected/preserved. User authorized temporary visible windows; all closed. Exact raw/source/3DLLs/hashes — P10-Counters-evidence.zip/-files.json, accepted summary -summary.json; pilot archives рядом. Ordinary12 managed/packed fingerprints unchanged; default source/runtime не изменён (conditional probe +Editor builder only).

Последний normal runtime checkpoint bf92647: full110+interactive2/API, both builds/4 smokes;28 capture frame pairs match, RenderData warnings0. P10-GpuCache/P10-Retention historical acceptance/limits — TEST_RESULTS. Не переоткрывать закрытые windows. Root Markdown8, stable audit — Evidence/workspace-audit.json.

Текущие source/build/material/report fingerprints — Evidence/Build-checkpoint.json и P10-ShadowOrder-checkpoint.json. Причинный контроль — -cause-summary.json; raw/full reports/source/assembly — -evidence.zip/-files.json, previous normal bytes — -before.zip. Historical P10-GpuCache и P10-Retention evidence остаются в TEST_RESULTS: cache optimization принята, retention10-cycle ограниченная область закрыта; это не general VRAM/GPU acceptance. Не повторять закрытые windows. Old full-suite reports восстановлены после сохранения новых bytes в архиве; их canonical paths не являются новой полной visual acceptance всех функций.

## Точное следующее действие

Следовать PORT_PLAN разделу «Приёмка P10 — оставшаяся последовательность», пункт3 NEXT: remaining active resource owners. Сначала прочитать TEST_RESULTS ResourceLifecycle/ownership и FEATURE_MATRIX; сопоставить активные FFT/DynamicWaves/Flow/Foam native owners в existing Waves/Interactions fixtures с уже принятым Calm. Выбрать один bounded enable/disable/unload сценарий для непроверенных владельцев, сохранить logical bytes/IsValid/disposal/RT и walker limits. Не повторять Calm10-cycle/counter capability/60s CPU windows. GPU timing repeatability/attribution и NonLocal ownership остаются открытыми, не выдавать nonzero counter за accepted benchmark.

Предыдущие14 Shadow RenderData warnings/Player теперь resolved только для установленной smoke-capture причины; validation не подавлялась. Для обычных пользовательских manual Submit до Ocean update требуется соблюдать порядок обновления, это не автоматическая поддержка любого порядка внешнего rendering. P10 ACTIVE, P11/P12 TODO; полный порт не завершён.

## Сохраняемые изменения и ограничения

- Foreign Ocean.mat (4 serialization fields) и baked LightingData/ReflectionProbe folders исходных BoatWakes/boat/threeboats/Examples/main/whirlpool: не stage/reset/delete.
- Pre-existing dirty Buoyancy/ResourceLifecycle обоих API, focused XML и untracked FFT/leak-stack diagnostics: не присваивать этому изменению.
- Два test-mutated материала TransparentPrimitive/OffWhite архивированы перед rebuild и восстановлены из HEAD. ReflectionVertexVariants.asset имел только trailing-space serialization diff: архивирован и восстановлен. ProjectSettings фактического diff не имеет.
- Historical evictable2490368B NOT REPRODUCED, owner не доказан/not fixed; DX11 retained/growing DXGI usage остаётся P10. CPU Submit/engine inventory не GPU time/точный VRAM. Intel/XR hardware unavailable.
- P11 содержит оставшиеся original examples/configuration/standalone/bake/lighting варианты; UnderwaterLighting fill0 не принят. Не переоткрывать P09 и не выдавать scoped smoke за полную приёмку.

В корне только8 обязательных Markdown. Не создавать дополнительные progress/status/roadmap документы. Не GenerateScenes, не менять work/crest-upstream и historical work/diagnostic-6000.6. Commit checkpoint — git log -1.
