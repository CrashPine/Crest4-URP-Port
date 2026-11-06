# Точка продолжения

Обновлено: **2026-11-06**. Цель Unity **6000.6.5f1 + URP17.6.0**, Windows DX11/DX12 Render Graph. Работать только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап: P11 ACTIVE

P00–P10 DONE в bounded/scoped-области PORT_PLAN; P11 ACTIVE/P12 TODO. Единственный порядок задач — PORT_PLAN; результаты/ограничения — TEST_RESULTS/FEATURE_MATRIX. Не переоткрывать закрытый P09.

## Последний завершённый checkpoint

P10-StableGpu bounded DONE with explicit measurement limitations. Two ordinary-camera frozen-Calm surface A–B–A/API: original simulation retained, no screenshot/counter-capability repetition. Focus/render/GPU timestamps+nvidia-smi clock/temp/load/power captured. Both Players exit0/errors0; observed+60 draw/+275716 triangle/+143670 vertex delta restores exactly. Isolated GPU cost REJECTED by unchanged protocol gates: DX11 loses focus/clock spread29.2%; DX12 stays100% focused but baseline drift12.16%/clock spread51.61%. No API/FPS/VRAM or full performance acceptance. Do not repeat without a new causal/environment trigger.

Only CREST_RESOURCE_PROBE source/diagnostic wrapper changed. Normal12 managed/packed hashes match current Build-checkpoint (e4cbf02 full117+interactive2/API, both normal builds/4 smokes). No full-suite/normal rebuild required for diagnostic-only change. Raw/source/3 diagnostic DLLs and gate report — P10-StableGpu-summary/protocol/evidence.zip/files.json. Audit1405/rootMD8; foreign files preserved, variant whitespace archived/restored. No active Unity/Player operation.

## Точное следующее действие

P10 bounded scope closed with explicit limits; P11 ACTIVE, P11.1 first. Read OriginalWhirlpoolTests, RippleGeneratorTests (RipplePulseClock/RippleStepObserver), LodDataMgrPersistent.ClearLodData/BuildCommandBuffer, Whirlpool.UpdateInputs/UpdateMaterials and DynWavesDampenCircle.shader. Build one matched nonzero dynamic-wave-history On/Off causal control preserving original combined displacement/flow/foam/waves. Read actual _Radius/_DampenStrength (shader defaults3/.01; quad scale is not shader radius), both RG channels, independent falloff/outside reference. Verify substep consumption/history initialization before assertions. Do not assume exact restoration while original simulation evolves; old .015754165 failure stays unresolved.

P11.2 UnderwaterLighting fill0, P11.3 exact supported configurations and P11.4 final milestone remain TODO. P12 TODO; full port unfinished. P10 GPU isolation/driver NonLocal/physical Intel/XR are documented limits, not fixed or PASSED. No generic owner/counter/Calm/geometry/FFT reruns.

## Сохраняемые изменения и ограничения

- Foreign Ocean.mat (4 serialization fields) и baked LightingData/ReflectionProbe folders BoatWakes/boat/threeboats/Examples/main/whirlpool не stage/reset/delete.
- Pre-existing dirty Buoyancy/ResourceLifecycle обоих API, focused DX11 XML и untracked FFT/leak-stack diagnostics не присваивать текущему изменению.
- Test-mutated TransparentPrimitive/OffWhite архивированы перед rebuild/восстановлены; variant whitespace архивируется отдельно. ProjectSettings без фактического diff.
- Historical evictable2490368B NOT REPRODUCED, owner не доказан/not fixed. DX11 NonLocal growth также в Empty; CPU Submit/engine inventories не GPU cost/physical VRAM. Intel/XR unavailable.
- No GenerateScenes, no upstream/historical diagnostic edits, no simultaneous Editors. Runtime change требует full/API, обоих builds/4 smokes; documentation-only не требует rebuild.

В корне8 обязательных Markdown. Не создавать новые status/progress/roadmap документы. Commit checkpoint — git log -1.
