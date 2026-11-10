# Белка: изображения и анимация

Созданы встроенным image_gen с прозрачным фоном. Визуальный референс белки — существующий простой спрайт кролика `rabbit-v2-simple.png`. Пиксели и прозрачность сохранены без обработки.

## Готовые файлы

- `Assets/MonsterSprite/ProjectileArt/Squirrel/squirrel.png` — белка, бой и карточка выбора.
- `Assets/MonsterSprite/ProjectileArt/Squirrel/acorn.png` — посаженный жёлудь.
- `Assets/MonsterSprite/ProjectileArt/Squirrel/vine-binding-sheet.png` — восемь кадров лоз, сетка 4 × 2.

## Промпт белки

Asset: one 2D game projectile/UI sprite of a red squirrel HEAD. The supplied rabbit image is the exact STYLE reference: imitate its extremely simple clean round animal face, thick dark brown outline, very few flat colour shapes, tiny dark dot eyes with a single glint, small nose and subtle mouth, soft friendly expression, clear silhouette. Change the species to a red squirrel: warm russet-orange round head, small upright pointed furry ears with tiny tufts (NOT long rabbit ears), creamy small cheeks and muzzle, subtle small paired buck teeth. One centered front-facing head only, no body or tail, no accessories, no effects, no scenery or shadow. Compact recognizable squirrel silhouette, readable at 32 pixels. Subject fills about 75 percent of square canvas. Match rabbit's simple friendly flat contour style, no detailed realistic fur. True transparent background.

## Промпт жёлудя

Use case: 2D Unity game sprite of a single acorn embedded into an enemy. Create one simple oak acorn viewed from slightly above/front, oriented vertically with little stem pointing UP and pointed seed tip down. Warm amber-brown smooth seed, dark brown textured cap with just 4 or 5 chunky scales, small bent stem, strong thick dark brown contour and simple warm cream highlight. Forest cartoon game art, flat layered colour shapes, extremely readable at 20 pixels. Compact oval shape filling 75 percent of square canvas, perfectly centered. No leaves, no cracks, no effects, no arms or face, no text, no badge, no shadow, no ground. Single isolated acorn on genuinely transparent background.

## Промпт лоз

Create a TRANSPARENT 2D GAME EFFECT SPRITE SHEET, exactly 4 COLUMNS by 2 ROWS, eight equal square cells, no gaps or labels. Reading order left to right then top to bottom. Each cell contains ONLY curling dark olive green forest vines and a few fresh green leaves with thick dark brown outlines, warm simple flat cartoon game art. This is an animated vine-binding overlay placed over a square floating enemy HEAD: EMPTY TRANSPARENT CENTRE in every cell to keep enemy face visible, NO HEAD and NO CHARACTER drawn. Keep vines within the same compact softly rounded square outline in all eight cells. Animation: frame1 two short tendrils at lower corners, frame2 tendrils growing up the sides, frame3 vines curl around upper corners, frame4 tendrils wrap from corners across edges, frame5 complete leafy binding around all four edges, frame6 full binding gently sways, frame7 full binding subtly tightens, frame8 full binding relaxed sway. Binding stays near the perimeter; centre 55 percent area is clear transparency, and no tendrils extend beyond each cell. Consistent fixed position, consistent size and stroke weight across frames. No text, no grid lines, no background, no shadows or glows. Entire sheet truly transparent.

## Механика

Белка имеет размер и прямой урон обычного снаряда. Первое попадание сажает один жёлудь по центру врага. Жёлудь в среднем составляет 80% размера врага, следует за его обычными движениями и плавно меняет оранжевый оттенок, прозрачность и размер. Счётчика ударов нет.

При смерти врага от любого источника жёлудь всегда взрывается: 100 огненного урона в круговом радиусе одной клетки, только в своей комнате. Ранний взрыв также повреждает живого носителя. Каждый взрыв оставляет слабый костёр: 10 огненного урона в радиусе клетки за один ход. Если взрыв убивает другого носителя жёлудя, его жёлудь тоже взрывается при смерти.

Только повторное попадание белочки по живому носителю проверяет шанс раннего взрыва: 5% изначально и 10/15/20% после улучшений. Первое попадание этот шанс не проверяет; другие снаряды, огонь и яд не вызывают ранний взрыв. Новая белка после взрыва может посадить новый жёлудь.

Остальные ветки: количество 1/2/3 и независимый шанс оцепенения каждого врага от взрыва 3/6/9%. Оцепенение пропускает один ход врага, лозы анимируются и исчезают после него.

Белка — восьмой вид, включается в выбор пяти видов на уровень и открывается через боевые улучшения. В общем залпе идёт после бобра; лягушка остаётся непосредственно перед светлячком.

Ульта «Цепная реакция» предлагается после полной прокачки трёх веток. Взрыв подрывает соседние жёлуди даже при выживших врагах и может продолжить цепь в пределах комнаты.

## Обновлённый взрыв

Взрыв использует 16 кадров imagegen, огненную вспышку, скорлупу, искры и дым. Длительность — 0,45 секунды. Общие 100 базового урона разделены на три удара 33/33/34 с промежутком 0,13 секунды; каждый удар заново запускает тряску врага. Пламя костра плавно затухает 3 секунды после последнего хода. Исходные промпты и параметры: [SquirrelExplosionArtPrompts.md](SquirrelExplosionArtPrompts.md).
