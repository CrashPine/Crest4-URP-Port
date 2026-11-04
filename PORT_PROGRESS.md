# Точка продолжения

Обновлено: **2026-11-04**. Цель: Unity **6000.6.5f1**, URP **17.6.0**, Windows DX11/DX12 Render Graph. Работать только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап: P10 ACTIVE

P00–P09 DONE в scoped-области PORT_PLAN; P11/P12 TODO. Единственный порядок задач — PORT_PLAN.md; результаты и ограничения — TEST_RESULTS.md/FEATURE_MATRIX.md. Не повторять закрытые P09 fixtures без изменения пути.

Завершено в текущем checkpoint: lazy cache исходного Helpers.IsIntelGPU в GetMaskColorRTD, сброс OnEnable. Intel RFloat/non-Intel RHalf правило сохранено. Четыре native warm GC windows: Off/On448B Submit на обоих API, все10 underwater buckets0; прежние дополнительные576B устранены в этом пути. Это не whole-asset GC-free и не GPU/VRAM performance acceptance.

## Последний завершённый checkpoint

Активных Unity/Player операций нет. GPU classification cache принят в указанной области: focused mask DX11 1/1; full110/110 +actual interactive2/2 каждого API; обе свежие Development/Release сборки Success;4 sequential Player smokes Errors0 (7 demos+4 reflection captures each). DX11/GC evidence2026-10-30; возобновлённый DX12/builds/Players2026-11-04. Native face fields25600→0 fullscreen→exact restore. GUID/compute597 PASSED. Последняя стабильная структурная проверка — Evidence/workspace-audit.json; root Markdown8.

Текущие runtime/build/material/report fingerprints: Evidence/Build-checkpoint.json и P10-GpuCache-checkpoint.json. Raw/source/diagnostic assemblies/full-suite reports — P10-GpuCache-evidence.zip, hash index -files.json; предыдущие build/report bytes — -before.zip. Две прерванные попытки DX12 без свежего XML сохранены в -interrupted.zip/-interrupted-resume.zip и не приняты как pass. Исторические изменившиеся full-suite fixture reports восстановлены после архивирования, это не текущая повторная visual acceptance всех функций.

## Точное следующее действие

Следовать PORT_PLAN разделу «Приёмка P10 — оставшаяся последовательность», пункт2: ограниченное исследование DX11 retained/growing driver accounting. Сначала прочитать TEST_RESULTS P10 ownership и текущий ResourceShutdownProbe.Ownership.cs; расширить diagnostic-only цикл до10 load/render/unload с фиксированными idle checkpoints и final drain, matched Empty control. Не повторять 60s baseline/reflection/cold/P09 без причины. Проверить plateau/reclaim против продолжающегося роста, не называть DXGI process usage точным Crest VRAM. Следующий контролируемый шаг выбрать по результату; не запускать бесконечные однотипные проверки.

Отдельно остаются исходные Player Shadow RenderData frame-validation warnings:14 в каждом из4 текущих логов и14 в каждом pre-fix архиве. Cache fix их не устраняет; нужна причинная проверка render/update order, не подавление validation. P10 остаётся ACTIVE, P11/P12 TODO; полный порт не завершён.

## Сохраняемые изменения и ограничения

- Foreign Ocean.mat (4 serialization fields) и baked LightingData/ReflectionProbe folders исходных BoatWakes/boat/threeboats/Examples/main/whirlpool: не stage/reset/delete.
- Pre-existing dirty Buoyancy/ResourceLifecycle обоих API, focused XML и untracked FFT/leak-stack diagnostics: не присваивать этому изменению.
- Два test-mutated материала TransparentPrimitive/OffWhite архивированы перед rebuild и восстановлены из HEAD. ReflectionVertexVariants.asset имел только trailing-space serialization diff: архивирован и восстановлен. ProjectSettings фактического diff не имеет.
- Historical evictable2490368B NOT REPRODUCED, owner не доказан/not fixed; DX11 retained/growing DXGI usage остаётся P10. CPU Submit/engine inventory не GPU time/точный VRAM. Intel/XR hardware unavailable.
- P11 содержит оставшиеся original examples/configuration/standalone/bake/lighting варианты; UnderwaterLighting fill0 не принят. Не переоткрывать P09 и не выдавать scoped smoke за полную приёмку.

В корне только8 обязательных Markdown. Не создавать дополнительные progress/status/roadmap документы. Не GenerateScenes, не менять work/crest-upstream и historical work/diagnostic-6000.6. Commit checkpoint — git log -1.
