# Взрыв жёлудя и продолжительная анимация костра

Создано встроенным image_gen; сохранена исходная прозрачность PNG.

## Готовые файлы

- `Assets/MonsterSprite/ProjectileArt/Squirrel/acorn-explosion-sheet.png` — 16 кадров, сетка 4 × 4.
- `Assets/AnimalsBullet/SquirrelAcorn.prefab` — редактируемый спрайтовый взрыв длительностью 0,45 секунды.
- `Assets/Resources/Effects/Firefly/Bonfire.prefab` — редактируемая система огня: частицы живут 0,9–1,8 секунды, затухание после последнего хода длится 3 секунды.
- `Tools/SquirrelExplosionPreview.png` и `Tools/SquirrelBonfireFadePreview.png` — снимки проверки в Unity.
- `Tools/SquirrelEffectsValidationReport.txt` — проверки трёх ударов, трёх реакций врага, быстрого завершения взрыва и продолжительного костра.

Взрыв наносит общий базовый урон 100 тремя ударами: 33, 33 и 34 с интервалом
0,13 секунды. Каждый удар запускает обычную тряску врага и короткую ударную волну.
Вероятность лоз проверяется один раз на врага за взрыв. Ульта подрывает соседние
жёлуди на первом ударе; каждая ветвь цепи выполняет все три удара до завершения.
Настройки темпа сохранены и в префабе жёлудя артефактов. Продолжительность жизни
искр взрыва — 0,16–0,32 секунды. Списки целей возвращаются в пул после завершения
или удаления взрыва.

Урон и длительность действия слабого костра: 10 огненного урона за один ход. При затухании костёр удалён из расчёта урона; визуальные угли не задерживают следующий ход.

Обновление сохранённых эффектов в редакторе: Forest Animal War / Art / Обновить взрыв жёлудя и костёр.

## Промпт генерации

Use case: stylized-concept.
Asset type: Unity 2D game VFX animation sprite sheet, one square transparent atlas with EXACTLY 4 columns and 4 rows, 16 equal square cells. Each cell is one successive frame of the SAME ACORN FIRE EXPLOSION. Reading order left to right, then top to bottom.
Primary request: an exciting compact magical forest acorn exploding into orange fire, golden sparks and brown acorn shell fragments. This will appear over one enemy and expand to about two game cells across.
Style: polished hand-painted cartoon forest game art, bold simple shapes, crisp dark warm brown contours on shell fragments, warm russet orange, golden yellow and pale cream hot highlights, soft translucent reddish smoke at the end. Readable at small size; fire should have convincing lively curling tongues. No realistic volumetric rendering.
Animation sequence: frames 1-2 bright small compressed ignition flash and splitting acorn shell; frames 3-5 fast dramatic outward bloom with a cream-yellow core and orange flame petals; frames 6-8 peak energetic radial blast with a clear flaming shockwave and several flying curved brown shell fragments; frames 9-12 flames opening into a ragged broken ring, sparks dispersing outward and warm wisps; frames 13-16 dying orange embers and a few very faint smoke curls, progressively disappearing. Final frame almost completely empty. This is ONE evolving burst, not sixteen different designs.
Composition: identical fixed centre at the centre of EVERY cell, equal canvas size and consistent camera. Peak explosion fills about 82 percent of its own cell. All artwork including sparks stays within its own cell with a transparent margin of at least 8 percent; NO cross-cell overlaps. No whole intact acorn obscuring the explosion after frame 2.
Background: genuinely transparent RGBA everywhere outside the isolated effect, including all cell margins and the gaps within the flame ring. NO checkerboard drawn into the image, NO solid or gradient backdrop, no floor, no environment, no ground shadow.
Constraints: square image, seamless regular 4x4 layout, no grid lines, no frame numbers, no captions, no letters, no UI, no character or face, no watermark.

## Уточнение расположения кадров

Use case: precise-object-edit. Edit target: the supplied transparent 4x4 acorn explosion animation atlas.
Keep the exact SIXTEEN frames, reading order, warm orange-yellow fire, wooden acorn shell fragments, evolving flash/bloom/shockwave/embers sequence, painted style and transparent RGBA background.
Change only the framing and separation inside the cells: every frame MUST fit inside the central 70 percent of its equal square cell. Leave an entirely transparent margin at least 15 percent on ALL FOUR SIDES of EVERY cell, including the biggest peak frames in row 2. Every single spark, smoke wisp and glow must respect this clear margin. Frames must not overlap, touch cell borders, or get cut off.
Keep each effect centred on its fixed cell centre and keep the same gradual expansion then dissipation. Remove diffuse noisy glow from the margins. Make flames and ember edges crisp, with intentional smooth alpha rather than speckled noise. Final frame contains only a few very faint embers and is almost empty.
Exact layout: four columns and four rows, equal square cells, square sheet; no grid lines, no frame numbers, no checkerboard painted into the image, no captions, no floor, no scene. Real transparent background.
