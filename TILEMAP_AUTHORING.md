# Battlefield Tilemap Authoring

The gameplay scene uses an ordinary Unity `Grid` + `Tilemap`. Every defense room points to an editable `BattlefieldTilePalette`; the default location theme is **Twilight Thicket**.

## Current assets

- Active atlas: `Assets/Art/TwilightThicket/TwilightThicket_Flat3Atlas.png` (legacy filename; now softened, not three-colour)
- Exterior frames: `Assets/Art/TwilightThicket/Frames/ForestFrame_Line1_8x10.png` through `ForestFrame_Line4_5x7.png`
- Frame profiles: `Assets/Resources/Battlefield/TwilightThicket/Frames`
- Palette: `Assets/Resources/Battlefield/TwilightThicket/TwilightThicketPalette.asset`
- Individual Tiles: `Assets/Resources/Battlefield/TwilightThicket/Tiles`

The current atlas is 1024×1024 and sliced into sixteen equal 256×256 sprites. One tile is exactly one Unity world unit, so terrain, aim dots, enemies, darkness, and projectiles share the same grid. Each cell alternates between two editable colour tints for a restrained checkerboard; the underlying textured artwork stays the same.

## Exterior forest frame

The `BattlefieldForestStyle` component on each Stage owns the editable **Forest Shadow Frame**, **Breach Vine**, and **Forest Eyes 1–3** scene objects. Each of the four defense lines has a separate PNG and `BattlefieldFrameProfile` asset. All frames import as Full Rect / Simple sprites at 128 PPU. The empty opening encloses the entire Tilemap. Roots and animal spectators remain outside all gameplay cells; no tiles or colliders were added.

Open **Frame Profile** on that component to adjust that line's artwork, **Opening Pixels**, **Field Gap**, **Camera Margin**, **Tint**, and **Eye Pixels** independently, then use the Stage's **Apply line size and repaint this room** action. The camera fits the decorative bounds automatically, including the top and bottom animal groups, while preserving HUD space. Animals on the lower edge face inward, away from the viewer; the three blinking eye pairs belong to the forward-facing upper spectators.

The separate PNGs avoid stretching animal heads with 9-slicing. They are produced locally from the cleaned master `BattlefieldShadowFrame_v3.png` with `Tools/BuildForestFrames.ps1`: upper/lower spectators resize uniformly, and only the animal-free vertical root strips change length. `Tools/ExtractForestFrame.ps1` removes the generated light checkerboard from this dark-green master. These tools do not call a generation API. Rebuilding the Unity tilemap preserves existing edits to all four frame profiles.

The layout reference is the inset battlefield and exterior walls in [Dragon BUURRP](https://www.malavida.com/en/soft/dragon-buurrp/android/). The artwork remains original, nearly monochrome Twilight Thicket forest art. The yellow breach vine still marks the exact gameplay defeat boundary.

## Atlas layout

Read left-to-right, top-to-bottom:

| Top-left | Top A | Top B | Top-right |
| --- | --- | --- | --- |
| Left A | Moss floor | Earth floor | Right A |
| Left B | Grass floor | Worn floor | Right B |
| Bottom-left | Bottom A | Bottom B | Bottom-right |

The four floor variants are placed deterministically across the complete playable grid. The Tilemap has exactly the same dimensions as gameplay (8×10, 7×9, 6×8, or 5×7), with no extra outer ring. Adjacent cells use the Stage's **Light Cell Tint** and **Dark Cell Tint**; change these in the Inspector and repaint the room. Corner and edge sprites remain in the palette for optional decorative layers, but they do not enlarge or replace playable cells.

## Editing in Unity

1. Select any `Stage` object in `Assets/Asset.unity`.
2. Assign or edit its **Battlefield Tile Palette** in the Inspector.
3. Press **Apply line size and repaint this room** to preview that room.
4. Use **Forest Animal War → Art → Rebuild Twilight Thicket tilemap** to reslice the atlas, update the sixteen Tile assets, and repaint every authored room.

Replacing a sprite inside the palette does not require gameplay-code changes. A future location can use another `BattlefieldTilePalette` while keeping the same room geometry and combat systems.

## Image generation prompt

The atlas was generated with the built-in image-generation tool from the previous forest board as a palette/style reference:

> Production-ready Unity 2D orthographic top-down tile atlas for “Twilight Thicket”. Create an exact 4×4 grid of sixteen isolated square tiles: top corners and two top edges; two left edges, four playable moss/earth/grass floor variants, two right edges; bottom corners and two bottom edges. Polished hand-painted fantasy mobile-game art with subtle pixel crispness, deep emerald and teal foliage, muted brown soil, cool cyan bioluminescent flecks, and restrained warm leaves. Dense forest and roots only along the outside boundary; open readable gameplay floor inside. No gutters, margins, perspective, characters, animals, monsters, projectiles, weapons, UI, text, watermark, buildings, or large props. Details must not cross tile boundaries.
