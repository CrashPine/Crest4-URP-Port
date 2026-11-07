# Точка продолжения

Обновлено: **2026-11-06**. Цель **Unity6000.6.5f1 + URP17.6.0**, Windows DX11/DX12 Render Graph. Работать только в `outputs/Crest4-URP-Port`, branch `port/unity-6.6-urp`. Полный порт **не завершён**.

## Текущий этап

P00–P10 DONE в bounded/scoped-области PORT_PLAN. P11 ACTIVE: P11.1/P11.2/P11.3-A/B scoped DONE; **P11.3-C NEXT**. E optional/non-gating. Единственный порядок/критерии — PORT_PLAN, измерения — TEST_RESULTS, приёмка подсистем — FEATURE_MATRIX. Не переоткрывать P09, не создавать новый status/roadmap Markdown.

## Активные операции

Нет. P11-Scenes-batch-operation.json complete:20 итоговых Player states (10/API) проверены. Main DX11 принят отдельным запуском на той же сборке и не повторялся внутри batch. Перед любым новым Unity запуском проверить реальные процессы и timestamp логов.

## Последний checkpoint: P11.3-B

Шесть исходных standalone scenes, Forward/PC/1x/640x360, исходные scene/bake/compute/physics не переписывались. Native hull movement/wetness, wake Off/On/Off, river/whirlpool field controls и source geometry controls Passed на DX11/DX12, complete Player logs errors0. Контролируемые изображения восстанавливаются точно. Это не physical keyboard/GUI/performance/all-feature acceptance.

Три исправления input: BoatAlignNormal, BoatProbes, OceanDebugGUI безопасны без Keyboard, исходная тяга/поворот/GUI maintenance сохранены. Реальный начальный FAILED17 архивирован. Итоговый preflight без клавиатуры даёт force7N/torque.2648703, errors0.

Исправление shader retention: Automatic native lightmap stripping вырезал необходимую Main URP/Lit mixed-light комбинацию до C# strippers. Broad all-mode trial прерван/не принят; SVC-only trial FAILED и удалён из Assets/build integration. Принят Custom профиль Plain/Directional/Subtractive/ShadowMask, unused dynamic map types excluded. Все6 source scenes realtime lightmaps0; существующий LakesAndRivers bake сохранён. Trace expected exact1 до/после C# stripping, оба shader stages/API; Main pink0 и geometry delta67724/67754/restoration0. Никаких shader/compiler-data patches или отключения strict matching.

**Обычные сборки устарели относительно текущего кода.** Их подтверждённый normal checkpoint0f833b4 =full121+actual interactive2/API/Dev+Release/4smokes;12 managed/packed hashes неизменны. После него изменены3 production input files +GraphicsSettings. B проверен целевым скомпилированным Player; **P11.4 full/API+interactive/ValidateAssets/Dev+Release/4smokes ещё обязателен** после группировки следующих runtime исправлений. Старую полную регрессию не выдавать за приёмку новых исходников.

## Точное следующее действие

**P11.3-C:** прочитать UnderwaterVolumeTests, CameraSettingsTests, PlanarReflectionTests и заморозить конечный список переносимых состояний в существующем PORT_PLAN: FullScreen/Portal/Volume outside, FlyThrough outside/inside1x; before/after-transparent geometry overlay1x/4x; reflection Forward/Forward+/Deferred HDR/stencil/resize; существующие Mobile/PC quality. Не строить декартов продукт и не повторять Editor proof. Volume inside исходно не поддерживается — использовать FlyThrough.

Сделать отдельный diagnostic Player transfer с owned serialized dependencies/variants и фактическими descriptors/bindings/causal controls. Позднейшие runtime fixes группировать для P11.4; diagnostic-only изменения проверять адресно. Не перегенерировать demos/не перепекать исходные сцены. D/F/G/H/finalP11.4/P12 остаются в PORT_PLAN.

## Evidence и сохраняемые изменения

- P11-Scenes-summary/files/evidence.zip:20 raw reports/logs/images, source и actual Editor/Player DLLs. Input17/Main-pink/SVC/native trace/interrupted FXC/aborted broad-retention attempts сохранены отдельно, не считать PASSED. Первая trace compile ошибка missing namespace также сохранена.
- Shell recursive deletion ранее отклонён automatic policy; после проверки путей/GUID безопасное обратимое перемещение сохранило оба interrupted fixtures целиком вне Assets в Evidence/P11-Scenes-retained-CrestResourceProbe_*. Rejected SVC source/asset — Evidence/P11-Scenes-rejected-svc. Не stage эти loose backups: нужное уже в raw archives.
- Foreign Ocean.mat (4 serialization fields), original baked LightingData/ReflectionProbe dirs и13 untracked diagnostics не stage/reset/delete. Их байты и4 dirty Buoyancy/ResourceLifecycle/focused DX11 XML проверены по baseline. Known TransparentPrimitive/OffWhite/PlayerSettings unchanged; variant whitespace архивирован/восстановлен.
- P10 GPU-cost/driver-memory/Intel/XR limitations остаются явными. Historical evictable2490368B NOT REPRODUCED/not fixed; старый .015754165 history-restoration failure не объяснён новыми bounded контролями. По smoke не объявлять полную приёмку.

Структурный аудит1413 файлов: missing meta0/duplicate GUID0/missing original0. Пустая generated Resources (creation08:15:50 во время interrupted build) и meta также сохранены обратимым переносом вне Assets, Evidence/P11-Scenes-empty-build-resources.

В корне8 обязательных Markdown. Commit checkpoint — `git log -1`.
