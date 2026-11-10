# Practical projectile sprites — v2

Generated with the built-in image generation tool on 2026-09-12. Each v1 image was supplied as its own species/palette reference. The user requested simpler in-game sprites with the same base colors and separate gameplay effects. The existing approved local alpha-removal workflow was reused; final images were downsampled to 256 × 256 RGBA.

## Final assets

- `hedgehog-v2-simple.png` — chestnut/tan/ivory, compact quills; pivot (0.5, 0.5).
- `firefly-v2-simple.png` — olive and yellow-green, no baked fire; pivot (0.5, 0.44).
- `rabbit-v2-simple.png` — warm cream and dusty rose ears; pivot (0.5, 0.36).

All three use 128 pixels/unit, Single sprite, Full Rect, Bilinear filtering, Clamp and no mipmaps. The v1 concept illustrations are preserved. New v2 sprites are assigned to the projectile prefabs and menu portraits. All projectiles rotate only a visual child. Hedgehog impact damage/radius/lifetime are unchanged; a pooled 16-quill mesh replaces the turquoise disk. No separate physical projectiles or additional damage ticks were added by this visual effect.

## hedgehog

```text
Use case: precise-object-edit.
Asset type: practical tiny 2D spinning projectile sprite for an existing Unity game, NOT character concept art or a portrait.
Input image 1 is the previous detailed projectile. Redesign it into a drastically simpler production sprite while preserving ONLY its species, basic palette and dark woodland/cardboard cartoon family.
Style: bold clean near-black hand-inked silhouette, FLAT color fills, one broad cel-shadow, a tiny highlight. Maximum 4 large color regions. No fur strokes, no paper grain, no crosshatching, no ornamental seams or patches, no legs or paws, no costume. Two small simple dark eyes, tiny muzzle only if essential. The whole image should remain instantly readable when shrunk to 32x32 pixels; aim for the simplicity of an actual game ammo icon, NOT a detailed cuddly toy illustration.
Square canvas, true transparent alpha background. No drawn checkerboard, no white background, no shadow, no text, no UI. Single centered sprite with generous empty margins; no loose effects outside the body.
Subject: one compact round brown hedgehog BALL with about 10-12 SHORT, CHUNKY triangular spines around the circular outline. Round central tan face, two tiny dark eyes, small brown triangular nose. Palette: chestnut brown #87502a, warm tan #d3a15c, a few ivory quill tips #f1d29d, ink outline #281a13. The body is a simple circle, no full animal torso, no paws, no ears or tiny details. Spines attached at rest; no exploding needles, motion trails or colored shockwave baked in. Body center exactly at canvas center; entire silhouette fits within middle 70% of canvas width/height.
```

## firefly

```text
Use case: precise-object-edit.
Asset type: practical tiny 2D spinning projectile sprite for an existing Unity game, NOT character concept art or a portrait.
Input image 1 is the previous detailed projectile. Redesign it into a drastically simpler production sprite while preserving ONLY its species, basic palette and dark woodland/cardboard cartoon family.
Style: bold clean near-black hand-inked silhouette, FLAT color fills, one broad cel-shadow, a tiny highlight. Maximum 4 large color regions. No fur strokes, no paper grain, no crosshatching, no ornamental seams or patches, no legs or paws, no costume. Two small simple dark eyes, tiny muzzle only if essential. The whole image should remain instantly readable when shrunk to 32x32 pixels; aim for the simplicity of an actual game ammo icon, NOT a detailed cuddly toy illustration.
Square canvas, true transparent alpha background. No drawn checkerboard, no white background, no shadow, no text, no UI. Single centered sprite with generous empty margins; no loose effects outside the body.
Subject: one compact spherical FIREFLY BALL, muted olive-brown upper shell, bright yellow-green lower belly; two tiny dark eyes and two very short simple antennae. Small simple wing lobes can be attached to each side but no wing veins. Palette preserved: olive #80713a, dark brown #50371e, luminous yellow-green #eced48, warm yellow #ffd665, near-black outline. Absolutely NO flame, NO fiery ring, NO aura, NO bloom, NO floating particles, NO external glow; the game already supplies the fire effect separately. The glow is conveyed solely by the bright flat patch INSIDE the round belly. Body center exactly at canvas center; entire silhouette fits middle 70% of square.
```

## rabbit

```text
Use case: precise-object-edit.
Asset type: practical tiny 2D spinning projectile sprite for an existing Unity game, NOT character concept art or a portrait.
Input image 1 is the previous detailed projectile. Redesign it into a drastically simpler production sprite while preserving ONLY its species, basic palette and dark woodland/cardboard cartoon family.
Style: bold clean near-black hand-inked silhouette, FLAT color fills, one broad cel-shadow, a tiny highlight. Maximum 4 large color regions. No fur strokes, no paper grain, no crosshatching, no ornamental seams or patches, no legs or paws, no costume. Two small simple dark eyes, tiny muzzle only if essential. The whole image should remain instantly readable when shrunk to 32x32 pixels; aim for the simplicity of an actual game ammo icon, NOT a detailed cuddly toy illustration.
Square canvas, true transparent alpha background. No drawn checkerboard, no white background, no shadow, no text, no UI. Single centered sprite with generous empty margins; no loose effects outside the body.
Subject: one warm ivory round RABBIT BALL with two clearly sticking-out SHORT rabbit ears, one slightly bent. No body below head, no paws, no full-body sitting pose. Palette preserved: warm cream #f0d2a5, tan shadow #b88e63, dusty rose ear centers/nose #b67563, dark brown outline #302219. Two small dark eyes and tiny triangular nose, no button sewing, no whiskers. Smooth circular outline apart from two ears, at most two tiny fur notches. Round ball body center EXACTLY at canvas center (50%,50%), body DIAMETER 46% of canvas; ears rise above it but stop at 10% top margin. Ears about one-third body diameter long. This pivot matters because the projectile spins around its spherical body, not around its ears. Entire silhouette fits middle 70% of square.
```
