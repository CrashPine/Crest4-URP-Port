# Точка продолжения

Обновлено: **2026-11-05**. Цель Unity **6000.6.5f1 + URP17.6.0**, Windows DX11/DX12 Render Graph. Работать только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап: P10 ACTIVE

P00–P09 DONE в scoped-области PORT_PLAN; P11/P12 TODO. Единственный порядок задач — PORT_PLAN; результаты/ограничения — TEST_RESULTS/FEATURE_MATRIX. Не переоткрывать закрытый P09.

## Последний завершённый checkpoint

P10-Rekey scoped DONE: existing-target convergence терял10 native FFT textures; adaptation при frozen time возвращала старый spectrum output. Retired generators остаются owned до Ocean command submission/full cleanup; fast path требует initialized spectrum. Focused5/API/full115+actual interactive2/API, оба normal builds/4 smokes Passed. Target bytes/RT сохранены, старый consumer восстановлен точно, updated frozen output совпадает со fresh reference;16 queued EditMode layers корректны. Original runtime failure/queued before-control и2 setup failures архивированы отдельно. GUID/compute597, audit1404/rootMD8 pass. Активных операций нет.

Current normal fingerprints — Build-checkpoint.json, P10-Rekey-checkpoint.json; raw/source/5 actual Player+Editor DLLs — -evidence.zip/-files.json. Previous full113 bytes — -before.zip.28 capture frame pairs match, RenderData warnings0. Historical canonical fixture reports restored after verified archive; they are not current acceptance.

## Точное следующее действие

PORT_PLAN «Приёмка P10», пункт3 NEXT — geometry resource inventory. Прочитать OceanBuilder s_Meshes/GenerateMesh/CleanUp и OceanRenderer rebuild/cleanup, проверить existing tests перед новым запуском. Подготовить bounded native Mesh identity control create→geometry-density/LOD rebuild→destroy на обеих API, включая patch meshes без tile assignment, tile/root/proxy ownership. Сначала доказать actual lifetime; не менять geometry algorithms заранее. Не повторять принятые FFT, Calm, active-owner, GPU-counter capability и retention windows.

GPU timing attribution требует отдельного stable controlled window с focus/environment telemetry; наличие counters уже доказано. Driver NonLocal attribution/other configurations открыты. P11 combined Whirlpool damping, UnderwaterLighting fill0, остальные exact supported configurations и final acceptance остаются в плане.

## Сохраняемые изменения и ограничения

- Foreign Ocean.mat (4 serialization fields) и baked LightingData/ReflectionProbe folders BoatWakes/boat/threeboats/Examples/main/whirlpool не stage/reset/delete.
- Pre-existing dirty Buoyancy/ResourceLifecycle обоих API, focused DX11 XML и untracked FFT/leak-stack diagnostics не присваивать текущему изменению.
- Test-mutated TransparentPrimitive/OffWhite архивированы перед rebuild/восстановлены; variant whitespace архивируется отдельно. ProjectSettings без фактического diff.
- Historical evictable2490368B NOT REPRODUCED, owner не доказан/not fixed. DX11 NonLocal growth также в Empty; CPU Submit/engine inventories не GPU cost/physical VRAM. Intel/XR unavailable.
- No GenerateScenes, no upstream/historical diagnostic edits, no simultaneous Editors. Runtime change требует full/API, обоих builds/4 smokes; documentation-only не требует rebuild.

В корне8 обязательных Markdown. Не создавать новые status/progress/roadmap документы. Commit checkpoint — git log -1.
