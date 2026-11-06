# Точка продолжения

Обновлено: **2026-11-06**. Цель Unity **6000.6.5f1 + URP17.6.0**, Windows DX11/DX12 Render Graph. Работать только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап: P11 ACTIVE

P00–P10 DONE в bounded/scoped-области PORT_PLAN; P11 ACTIVE/P12 TODO. Единственный порядок задач — PORT_PLAN; результаты/ограничения — TEST_RESULTS/FEATURE_MATRIX. Не переоткрывать закрытый P09.

## Последний завершённый checkpoint

P11.2 scoped DONE. Only original UnderwaterLighting config: native fill0 exactly matches original CG shader source under shared URP front-depth/matrices,4 views including old inside pose(0,-1,-3). Front-depth positive outside/0 inside original Volume mode, which does not support interior views. No surviving source fill contribution in tested views; prior0-RGB failure is not a valid positive-control expectation, not relabeled green.

Required positive backface producer probe retains original consumer/camera/volume, gives7056 source/native mask pixels/API (error0),7043/7042 changed RGB pixels, outside-mask221008 interior pixels unchanged/restoration0. Focused1/API Passed, no production fix or screenshots. Original3 source/helper expansion hashes checked; no complete Built-in renderer/RGB/all-view parity claim. Inventory/three-view intermediate and teardown-only failure archived separately; null-owned-RT cleanup fixed without changing functional thresholds.

P11-Fill-summary/evidence.zip/files.json preserve raw/source/all actual Crest Editor DLLs and provenance. Before.zip restores focused XML/two known materials; foreign files preserved. Production/material hashes and12 normal artifacts unchanged; current normal e4cbf02 full117+interactive2/API remains. No new full119/rebuild for test-only addition. Audit1405/rootMD8; no active operation.

## Точное следующее действие

P11.3 ACTIVE — freeze a finite supported-configuration acceptance inventory before running more tests. Read relevant FEATURE_MATRIX/TEST_RESULTS gaps and actual source/renderer/quality settings. Map4 underwater modes (Volume interior unsupported by contract), Forward/Forward+/Deferred, MSAA/overlays/quality, original standalone/GUI/input, GPU baker and Player lightmap variants to exact prior evidence vs missing configurations. For each required row record fixture/settings/acceptance/dependency in PORT_PLAN; choose first uncovered row, no arbitrary Cartesian-product expansion or repeated accepted Editor windows.

P11.1 andP11.2 scoped DONE; P11.4 final full/API+interactive/build/Player milestone andP12 TODO. Full port unfinished; XR/Intel/driver/GPU-cost limits remain explicit, not passed. Old .015754165 history-restoration failure is preserved/not explained by new bounded controls. No generic FFT/geometry/counter/resource reruns.

## Сохраняемые изменения и ограничения

- Foreign Ocean.mat (4 serialization fields) и baked LightingData/ReflectionProbe folders BoatWakes/boat/threeboats/Examples/main/whirlpool не stage/reset/delete.
- Pre-existing dirty Buoyancy/ResourceLifecycle обоих API, focused DX11 XML и untracked FFT/leak-stack diagnostics не присваивать текущему изменению.
- Test-mutated TransparentPrimitive/OffWhite архивированы перед rebuild/восстановлены; variant whitespace архивируется отдельно. ProjectSettings без фактического diff.
- Historical evictable2490368B NOT REPRODUCED, owner не доказан/not fixed. DX11 NonLocal growth также в Empty; CPU Submit/engine inventories не GPU cost/physical VRAM. Intel/XR unavailable.
- No GenerateScenes, no upstream/historical diagnostic edits, no simultaneous Editors. Runtime change требует full/API, обоих builds/4 smokes; documentation-only не требует rebuild.

В корне8 обязательных Markdown. Не создавать новые status/progress/roadmap документы. Commit checkpoint — git log -1.
