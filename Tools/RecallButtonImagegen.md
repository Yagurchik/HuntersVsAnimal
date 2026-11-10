# Кнопка сброса снарядов

Создана встроенным imagegen с прозрачным фоном. Исходный результат скопирован без изменения альфа-канала в `Assets/UI/WheelStyle/ForestRecallButton.png`.

Референсы: `Tools/GameplayActionsPortraitPreview.png` (предыдущий игровой интерфейс) и `Assets/UI/WheelStyle/ForestWheelRing.png` (палитра дерева, корней и листьев).

Кнопка сохранена в основной сцене `Assets/Asset.unity`, в `Animal Selection HUD → Gameplay Actions → DropDown`. Дерево, рамка и стрелка объединены в спрайт; надпись «СБРОС» остаётся редактируемым текстом. Четыре исходных обработчика возврата снарядов сохранены.

## Промпт

```text
Use case: style-transfer / precise-object-edit. Asset type: a single final transparent Unity 2D game UI button sprite, square composition. Input image 1 (game screenshot) is a STYLE AND PALETTE REFERENCE only; do not reproduce the screen. Input image 2 (wooden circular ring) is the EDIT TARGET: redesign this ring into one cohesive 'recall projectiles' wooden button for ForestAnimalWar. Keep the hand painted cartoon game style with bold dark brown outlines, warm orange-brown wood, carved rounded shapes and restrained olive moss/leaf accents that match the battlefield frame and animal selection wheel. Replace the thin ornamental ring with a simpler thick braided root rim and a recessed dark walnut wooden center. Add one large, chunky, clearly legible downward arrow ending in a shallow collection tray, carved/inlaid in warm pale amber wood (not a modern flat white download icon). Put this icon in the upper-middle of the medallion and leave a calm empty lower band inside the circle for a separate live UI caption added by Unity. The icon should be readable at 64 pixels. Front view, no perspective, clean circular silhouette, subtle depth/shadow drawn within the sprite, centered and occupying about 93% of the square canvas. Transparent outside the complete wooden medallion, opaque wooden center, clean alpha edges. No text, letters, numbers, watermark, animals, UI mockup or extra panels. This is ONE isolated usable button asset. Preserve the reference's illustrated woodland materials and bold contour, avoid ornate gold filigree, metallic shine, neon colors, modern vector UI appearance and excessive tiny details.
```

## Проверка

`GameplayActionButtonsPreview.RunBatch` в отдельной копии Unity: 17 успешных проверок. Проверены сохранённый спрайт, исходные обработчики, расположение без перекрытий на 540×960 и 960×540 и настоящее нажатие, возвращающее летящий снаряд вниз. Снимки обоих форматов просмотрены.

Отчёт: `Tools/RecallButtonImagegenValidationReport.txt`. Предпросмотр: `Tools/GameplayActionsPortraitPreview.png`, `Tools/GameplayActionsLandscapePreview.png`.
