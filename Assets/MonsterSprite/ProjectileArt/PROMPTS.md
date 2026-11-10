# Projectile artwork — generation prompts

Generated with the built-in image generation tool on 2026-09-12.

Style reference: `Assets/MonsterSprite/ChatGPT Image 7 сент. 2026 г., 08_39_57.png` (original pumpkin player).

These are separate new artwork files; existing projectile prefabs and gameplay have not been changed. The generator returned 1254 × 1254 RGB images with a baked checkerboard. After the user explicitly approved local image processing, the background was removed using `Tools/PrepareProjectileArt.ps1`. Final files are RGBA PNGs with real transparency. Edge appearance was checked over a dark background. Original generated images remain in the image generator's output folder.

Chosen output filenames: `hedgehog-v1.png`, `firefly-v1.png`, `rabbit-v1.png`.

## hedgehog

```text
Use case: stylized-concept.
Asset type: production-ready 2D Unity game projectile sprite, one isolated character, square 1024x1024 RGBA PNG with actual transparent background.
Input image 1 is STYLE REFERENCE ONLY: the pumpkin player from our game. Do not reproduce the pumpkin or the sprite sheet. Match its hand-inked dark woodland storybook / cardboard puppet look: expressive slightly ragged near-black outlines, warm muted natural colors, modest crosshatching, fabric seams and small stitched details, flat painterly shading, black button eyes with beige cross stitches. Same world and same artist; not glossy 3D, not pixel art, not a sticker with white outline.
Primary request: a HEDGEHOG animal used as a spinning ball projectile. One compact spherical curled-up hedgehog, front-facing readable little tan muzzle and tiny dark nose, mischievous determined button eyes, a brown fabric/fur round body ringed with clearly readable sharp ivory-and-dark-brown quills pointing radially outward. Its special attack shoots quills outward upon impact: convey a spring-loaded spiky silhouette and visible quill shapes, but draw the resting flying projectile with quills attached, no detached extra objects or explosion cloud baked into this base sprite.
Composition: single body centered EXACTLY at image center (512,512); body approximately 530 pixels diameter; attached radial quills extend to at most radius 400. Symmetric balanced mass, small tucked paws, all extremities entirely within a centered circular safe area, at least 10% transparent outer margin. The whole sprite will rotate 360 degrees around the center during flight, so no ground plane, horizon, directional motion trail, cast ground shadow or standing pose.
Constraints: true alpha transparency, crisp clean alpha edges, strong readable silhouette at small mobile game scale, one character only, no sheet, no text, numbers, UI, border, background, watermark, pumpkins or accessories.
```

## firefly

```text
Use case: stylized-concept.
Asset type: production-ready 2D Unity game projectile sprite, one isolated character, square 1024x1024 RGBA PNG with actual transparent background.
Input image 1 is STYLE REFERENCE ONLY: the pumpkin player from our game. Do not reproduce the pumpkin or sprite sheet. Match the same hand-inked dark woodland storybook / cardboard puppet look: slightly ragged near-black ink contours, fabric seams, subtle crosshatching, hand-painted flat shading, black button eyes with beige cross stitches. Warm muted olive and charcoal browns with vivid magic highlights like the pumpkin's summoning spell. Not glossy 3D, not pixel art, not a white-bordered sticker.
Primary request: a FIREFLY animal as a spinning round projectile surrounded by a small FIELD OF FIRE. Design a compact round olive-brown firefly body with a luminous yellow-green abdomen, two expressive button eyes, very short curled antennae, small tucked legs and two restrained translucent leaf-like wings fitted close to its round silhouette. Surround it on ALL sides with an integral compact circular orange-and-gold flame aura, clear individual hand-drawn flickering tongues of fire, warm amber ember highlights, yellow-green glow at the abdomen. Fire must frame the face without obscuring it and taper naturally to genuine transparency. Keep the organic inked style, not a photographic explosion or lens flare.
Composition: body centered EXACTLY at image center (512,512), body approximately 470-530 pixels diameter, circular fire halo contained within radius 400 with at least 10% transparent outer margin. Balanced 360-degree readable spherical projectile for full rotation around its center. No long tail, grounded pose or floor shadow.
Constraints: actual alpha background, no checkerboard painted into image, no dark or white rectangle, no scenery, no separate characters, no sprite sheet, no UI, text, numbers, watermark, badge, border or pumpkin.
```

## rabbit

```text
Use case: stylized-concept.
Asset type: production-ready 2D Unity game projectile sprite, one isolated character, square 1024x1024 RGBA PNG with actual transparent background.
Input image 1 is STYLE REFERENCE ONLY: the pumpkin player from our game. Do not reproduce the pumpkin or sprite sheet. Match the exact family of hand-inked dark woodland storybook / cardboard puppet art: bold imperfect near-black outlines, subtle crosshatching and distressed cloth texture, stitched seams, little black button eyes with beige cross stitches, flat painterly shadows. Palette: warm oatmeal/ivory fur, muted gray-brown shadows and dusty rose inner ears. Not glossy 3D, not pixel art, not a white-bordered sticker.
Primary request: a RABBIT animal as a round spinning ball projectile, a compact plump rabbit curled into a sphere, tiny tucked paws and a small snout, charming determined expression, two clearly visible LONG PROTRUDING RABBIT EARS with soft pink interiors, one straight and one slightly bent at the tip. Must unmistakably read as rabbit at small size. Small stitched patches and short fur tufts in the same handmade storybook style as the pumpkin, but no clothing or extra props.
Composition: the spherical BODY's rotational center is EXACTLY at image center (512,512), body approximately 490 pixels diameter; ears rise from the upper body and stop no higher than y=100; body bottom approximately y=780, leaving more transparent space below to keep the body pivot at the image center. All ear tips and tufts fit within a circular safe area radius 420 with generous transparent padding. Full front-facing character visible. One static sprite that will be rotated through 360 degrees in the game; no motion blur, directional trail or standing pose.
Constraints: true RGBA transparent background, crisp alpha, one character only, no floor or ground shadow, no scene, no text, no UI, no border, no sprite sheet, no numbers, no watermark, no pumpkin.
```

## Background extraction prompt

```text
Use case: background-extraction. Input image 1 is the EDIT TARGET. This is a Unity 2D game sprite. Remove the entire gray/white checkerboard background and replace it with genuine transparency (PNG alpha channel, alpha=0 outside the character). The checkerboard in the source is painted in the RGB pixels; it is NOT transparency and must be completely removed. Preserve the character artwork exactly: same outline, color, face, quills/ears/wings/flame details, texture and proportions, same square canvas. Keep holes between all extremities and flame tongues transparent as well. Do not draw a new checkerboard, do not draw a solid or gradient background, do not add ground shadows, do not flatten onto any backdrop. Deliver the isolated cutout as an actual RGBA PNG with zero opacity outside the character. No redesign, no text, no extra elements.
```
