# Бобёр и плотина

`beaver-projectile-topdown.png` — игровой спрайт бобра. Верхний ракурс,
силуэт, контур и покраска взяты с `../Boar/boar-projectile-topdown.png` как
референса; это отдельное животное с плоским хвостом и двумя передними зубами.
`beaver-dam.png` — рисунок отдельной комнатной преграды.
`beaver-sawdust-shaving.png` — нарисованная ImageGen стружка для разлетающихся
опилок при каждом укусе. Длинного следа у бобра нет.

Бобёр, плотина и стружка созданы встроенным ImageGen с прозрачным фоном. Финальные
запросы:

> Use case: stylized-concept. Asset type: Unity 2D projectile sprite. Use the
> referenced boar sprite ONLY as style, camera angle, silhouette-density and
> rendering reference. Draw a BEAVER instead: strict top-down view of a compact
> round brown beaver head/body, broad flat crosshatched tail visible behind the
> head, small round ears, dark determined eyes and two prominent white front
> teeth. Match the boar image's thick very dark outline, painterly flat
> warm-brown fur layers, polished cartoon game art, high readability at small
> size. The beaver points upward. Exactly one character centered in a square
> image. Genuine transparent background. No boar features, no tusks, no yellow
> streaks, no trail, no dust, no scenery, no text.

> Use case: stylized-concept. Asset type: 2D Unity game obstacle sprite, a
> beaver dam. Wide horizontal pile of interlocked short wooden logs and sticks
> with a readable strong silhouette, warm brown painterly flat cartoon art,
> thick very dark outline and simple layered shading matching polished
> ForestAnimalWar animal projectile art. Side/front view appropriate for a low
> horizontal barricade across the bottom of a top-down combat room. No water,
> no ground, no characters, no text, no effects, no background; genuine
> transparent alpha. Leave safe margins.

> Use case: stylized-concept. Asset type: one small reusable 2D Unity particle
> sprite for a beaver biting through enemies. Draw ONE isolated curled wood
> shaving / sawdust chip: tiny asymmetrical crescent-shaped splinter of pale
> golden wood with a darker warm-brown edge and a subtle carved wood grain,
> clean bold silhouette readable at tiny game size. Match the polished painterly
> cartoon rendering and dark outline of ForestAnimalWar's beaver and boar
> projectile art. Centered in square transparent canvas with safe margins.
> Genuine transparent alpha. No character, no cloud, no pile, no trail, no
> ground, no text, no glow, no shadow, no background.

Настройки импорта: Sprite / Single, прозрачность, без mipmaps. Бобёр —
1200 PPU; плотина — 300 PPU, стружка — 800 PPU.
