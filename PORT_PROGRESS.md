# Точка продолжения

Обновлено: **2026-11-05**. Цель Unity **6000.6.5f1 + URP17.6.0**, Windows DX11/DX12 Render Graph. Работать только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап: P10 ACTIVE

P00–P09 DONE в scoped-области PORT_PLAN; P11/P12 TODO. Единственный порядок задач — PORT_PLAN; результаты/ограничения — TEST_RESULTS/FEATURE_MATRIX. Не переоткрывать закрытый P09.

## Последний завершённый checkpoint

P10-Geometry scoped DONE. Game geometry tracks258 meshes (30 templates+228 tile clones),228 tiles/3 roots/API over density/LOD2/7→4/4→2/7, all old objects destroyed and vertices restored. Editor proxy owned plane/material previously survived cleanup; Editor-only tracking/cleanup fixed,2 recreate cycles/API pass and borrowed built-in mesh survives. Initial fixture template/clone assumption error retained separately from production failure.

Focused2/API/full117+actual interactive2/API, both normal builds/4 smokes Passed.28 capture pairs current, RenderData warnings0. GUID/compute597, audit1404/rootMD8 pass. Current fingerprints Build-checkpoint/P10-Geometry-checkpoint; raw/source/5 actual assemblies -evidence.zip/-files.json; previous115 normal bytes -before.zip. Historical fixture reports archived/restored; exact pre-existing foreign reports restored. No active operation.

## Точное следующее действие

PORT_PLAN P10.3 NEXT — stable GPU cost window. Read P10-Counters-summary.json and ResourceShutdownProbe.Profile.cs CounterCapability before changes. Local nvidia-smi supports clock/load/temperature/power sampling; availability CSV is archived, not a cost result. Fix one visible/focused normal-rendered Player configuration (scene/camera/resolution/warm-up), capture focus/render counts and available clock/load telemetry; bounded A–B–A window, reject attribution if baseline drifts. Existing counter capability is accepted: do not repeat hidden0-render/duplicate-name pilots, Calm/FFT/geometry/retention generic controls. P10 finite ownership inventory and explicit limits are in PORT_PLAN; no new unbounded owner hunt.

GPU stability/cost and driver NonLocal attribution remain open; physical Intel/XR unavailable. P11 Whirlpool combined damping, UnderwaterLighting fill0, supported configuration list and final acceptance remain TODO. Complete P10 criteria with evidence or documented external limits before P11.

## Сохраняемые изменения и ограничения

- Foreign Ocean.mat (4 serialization fields) и baked LightingData/ReflectionProbe folders BoatWakes/boat/threeboats/Examples/main/whirlpool не stage/reset/delete.
- Pre-existing dirty Buoyancy/ResourceLifecycle обоих API, focused DX11 XML и untracked FFT/leak-stack diagnostics не присваивать текущему изменению.
- Test-mutated TransparentPrimitive/OffWhite архивированы перед rebuild/восстановлены; variant whitespace архивируется отдельно. ProjectSettings без фактического diff.
- Historical evictable2490368B NOT REPRODUCED, owner не доказан/not fixed. DX11 NonLocal growth также в Empty; CPU Submit/engine inventories не GPU cost/physical VRAM. Intel/XR unavailable.
- No GenerateScenes, no upstream/historical diagnostic edits, no simultaneous Editors. Runtime change требует full/API, обоих builds/4 smokes; documentation-only не требует rebuild.

В корне8 обязательных Markdown. Не создавать новые status/progress/roadmap документы. Commit checkpoint — git log -1.
