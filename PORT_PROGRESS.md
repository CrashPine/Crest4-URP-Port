# Точка продолжения

Обновлено: **2026-11-06**. Цель Unity **6000.6.5f1 + URP17.6.0**, Windows DX11/DX12 Render Graph. Работать только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап: P11 ACTIVE

P00–P10 DONE в bounded/scoped-области PORT_PLAN; P11 ACTIVE/P12 TODO. Единственный порядок задач — PORT_PLAN; результаты/ограничения — TEST_RESULTS/FEATURE_MATRIX. Не переоткрывать закрытый P09.

## Последний завершённый checkpoint

P11.1 scoped DONE: actual original combined Whirlpool damping passes focused1/API. All source wave/flow/displacement/dynamic/foam enabled;120 live warm frames, then seeded dynamic RG history and exactly1 positive120Hz substep per Off/native.01/strong.5/Off trial. Original material radius3 vs quad scale80 recorded. Independent projected both-channel falloff errors.0001885/.0002625, outside0 and whole-LOD0 matched-Off repeat0;4 LOD0 binary pairs identical across APIs.

First unmatched test failure retained separately: distant sphere forces differed despite seeded dynamic RTs. Test-only sphere forcing snapshot replay at order199 before Ocean200 fixes input matching;2 sphere components/registration stay active, global checks/tolerance unchanged. Not a runtime fix or live temporal integration acceptance. Old .015754165 failure remains unresolved/not explained by this control.

P11-Whirlpool-summary/evidence.zip/files.json preserve raw/source/all actual Crest Editor DLLs; before.zip restores canonical focused XML, unmatched-history-failure.zip preserves initial failure. Production/material source and12 normal artifact hashes unchanged, normal full117+interactive2/API checkpoint remains e4cbf02. No new full118 suite/rebuild for test-only addition. Audit1405/rootMD8; no active operation.

## Точное следующее действие

P11.2 ACTIVE — original UnderwaterLighting fill remains unobservable0. Read OriginalExamplesConfigurationsTests relevant branch/failed history, then enumerate only that original config's CustomPassForCamera serialized persistent methods, MaskFill meshes/target and actual material mask consumers. Compare expected source and port fill mask contribution with matched camera/volume reference before RGB assertions. Do not rerun all24 scenes, accepted environment lighting or change thresholds to pass. No inherited-behaviour claim without evidence.

P11.3 supported configurations/P11.4 final full milestone and P12 remain TODO. Full port unfinished. P10 GPU cost/NonLocal/physical Intel/XR remain documented limits; do not reopen accepted FFT/geometry/counter/resource windows without a causal trigger.

## Сохраняемые изменения и ограничения

- Foreign Ocean.mat (4 serialization fields) и baked LightingData/ReflectionProbe folders BoatWakes/boat/threeboats/Examples/main/whirlpool не stage/reset/delete.
- Pre-existing dirty Buoyancy/ResourceLifecycle обоих API, focused DX11 XML и untracked FFT/leak-stack diagnostics не присваивать текущему изменению.
- Test-mutated TransparentPrimitive/OffWhite архивированы перед rebuild/восстановлены; variant whitespace архивируется отдельно. ProjectSettings без фактического diff.
- Historical evictable2490368B NOT REPRODUCED, owner не доказан/not fixed. DX11 NonLocal growth также в Empty; CPU Submit/engine inventories не GPU cost/physical VRAM. Intel/XR unavailable.
- No GenerateScenes, no upstream/historical diagnostic edits, no simultaneous Editors. Runtime change требует full/API, обоих builds/4 smokes; documentation-only не требует rebuild.

В корне8 обязательных Markdown. Не создавать новые status/progress/roadmap документы. Commit checkpoint — git log -1.
