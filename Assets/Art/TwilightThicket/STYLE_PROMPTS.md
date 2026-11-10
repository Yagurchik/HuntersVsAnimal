# Twilight Thicket battlefield art prompts

Generated with the built-in ImageGen workflow. The production assets deliberately
use a restrained, low-detail treatment so enemies, aiming and projectiles remain
readable on a phone.

## Tile atlas

> Exact 4x4 Unity battlefield tile atlas. Sixteen quiet, nearly monochrome dark
> pine-green cardboard floor cells with a thin charcoal inset seam. Minimal flat
> paper grain, no foliage, roots, stones, glowing specks, characters, UI or text.

## Shadow frame

> Transparent portrait battlefield frame made from one simple dark desaturated
> pine-green vine silhouette. Minimal flat paper-cut shapes and four small woodland
> animal cutouts (owl, fox, hare, hedgehog), with a large transparent center. No
> painterly detail, gradients, highlights, mist, bright accents, eyes, UI or text.

## Exterior shadow frame v2

Saved as `BattlefieldShadowFrame_v2.png`, generated with the built-in imagegen tool.
Replaces the stretched overlay with a sliced exterior border; no changes to the floor atlas.

> Use case: stylized-concept. Asset type: production Unity 2D decorative forest frame sprite, transparent PNG, portrait 4:5 aspect. Create one rectangular frame with a LARGE truly transparent rectangular opening and transparent exterior. Simple almost monochromatic dark desaturated pine-green / charcoal palette, dark fairytale cut-paper silhouettes. Thin intertwined tree-root rails on left and right, dimensional through just three stacked flat paper layers and a subtle muted edge highlight. No animals on side rails. At the TOP and BOTTOM borders: compact groups of 5-7 overlapping woodland animal silhouettes (rabbit ears, fox, owl, hedgehog) watching inward toward the empty central playing field. Top group leans down toward center, bottom group seen mainly from behind leaning up toward center. Animals belong entirely to the horizontal border strips, never intrude into the rectangular center. Simplified silhouettes, no fur detail, no bright eyes (animated eyes will be added separately), no orange or gold. Geometry: left/right rails confined to outer 10% of canvas width; top/bottom animal groups confined to outer 14% of canvas height. Entire central rectangle x=11%-89%, y=15%-85% must be COMPLETELY EMPTY ALPHA. Rectangular upright rails parallel to image edges. Thin sides with tangible layered depth, not a thick ornamental picture frame. Big readable animal ear/head silhouettes across top and bottom, minimal leaves only at corners. No battlefield, no tiles, no ground, no scene background, no text, no UI buttons, no checkerboard pixels. Real transparent alpha both inside opening and outside artwork. Crisp sprite edges, restrained and practical for a mobile game.

## Final four defense-line frames

The final master `BattlefieldShadowFrame_v3.png` uses the built-in imagegen edit prompt below.
The generator returned a baked checkerboard; this was removed locally with the user's permission
using `Tools/ExtractForestFrame.ps1`. No CLI generation or API-key workflow was used.
`Tools/BuildForestFrames.ps1` then created four separate transparent PNGs in `Frames/`,
keeping upper/lower animal proportions while adjusting only the vertical root length for
8x10, 7x9, 6x8 and 5x7 fields. The v2 frame is retained as an unused earlier draft.

> undefined

## Breach vine

> Transparent wide horizontal vine, a single flat muted amber-yellow paper-cut
> silhouette with gentle waviness and sparse leaves. No gradients, highlights,
> realistic shading, texture, background, UI or text.
