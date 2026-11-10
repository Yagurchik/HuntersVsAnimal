using UnityEngine;
using UnityEngine.Tilemaps;

// Authored on each Stage. Changes only scenery; enemy visibility and turns are untouched.
[DisallowMultipleComponent]
public sealed class DrownedBogVisuals : MonoBehaviour
{
    [SerializeField] private SpriteRenderer fogArtwork;
    [SerializeField] private SpriteRenderer gloomyHollowArtwork;
    [SerializeField] private Tilemap gloomyHollowTiles;
    [SerializeField] private SpriteRenderer drownedBogArtwork;
    [SerializeField] private Tilemap drownedBogTiles;
    [SerializeField] private SpriteRenderer forgottenBayArtwork;
    [SerializeField] private Tilemap forgottenBayTiles;
    [SerializeField] private SpriteRenderer snowyHazeArtwork;
    [SerializeField] private Tilemap snowyHazeTiles;
    [SerializeField] private SpriteRenderer fieryGehennaArtwork;
    [SerializeField] private Tilemap fieryGehennaTiles;
    [SerializeField] private Color swampTerrainTint = new Color(0.43f, 0.51f, 0.34f, 1f);
    [SerializeField] private Color gloomyTerrainTint = Color.white;
    [SerializeField] private Color bogTerrainTint = Color.white;
    [SerializeField] private Color bayTerrainTint = Color.white;
    [SerializeField] private Color snowTerrainTint = Color.white;
    [SerializeField] private Color gehennaTerrainTint = Color.white;
    [SerializeField] private Color ordinaryTerrainTint = Color.white;
    [SerializeField] private Color[] locationFrameTints = new Color[6];

    private void OnEnable() => Refresh();
    private void Start() => Refresh();

    public void Refresh()
    {
        bool campaign = LevelProgression.Instance != null
            && GameManager.Instance != null && !GameManager.Instance.IsSurvivalMode;
        bool swamp = campaign && LevelProgression.Instance.CurrentLocationIndex == 3;
        bool gloomy = campaign && LevelProgression.Instance.CurrentLocationIndex == 2;
        bool bay = campaign && LevelProgression.Instance.CurrentLocationIndex == 4;
        bool snow = campaign && LevelProgression.Instance.CurrentLocationIndex == 5;
        bool gehenna = campaign && LevelProgression.Instance.CurrentLocationIndex == 6;
        int location = campaign ? LevelProgression.Instance.CurrentLocationIndex : 1;
        BattlefieldForestStyle frame = GetComponent<BattlefieldForestStyle>();
        if (frame != null && frame.FrameRenderer != null
            && locationFrameTints != null && locationFrameTints.Length >= location
            && locationFrameTints[location - 1].a > 0f)
        {
            Color tint = locationFrameTints[location - 1];
            tint.a = 1f;
            frame.FrameRenderer.color = tint;
        }
        if (fogArtwork != null)
        {
            fogArtwork.sortingOrder = 20;
            fogArtwork.gameObject.SetActive(swamp);
        }
        if (gloomyHollowArtwork != null)
        {
            gloomyHollowArtwork.sortingOrder = -21;
            gloomyHollowArtwork.gameObject.SetActive(gloomy);
        }
        if (drownedBogArtwork != null)
        {
            drownedBogArtwork.sortingOrder = -21;
            drownedBogArtwork.gameObject.SetActive(swamp);
        }
        if (forgottenBayArtwork != null)
        {
            forgottenBayArtwork.sortingOrder = -21;
            forgottenBayArtwork.gameObject.SetActive(bay);
        }
        if (snowyHazeArtwork != null)
        {
            snowyHazeArtwork.sortingOrder = -21;
            snowyHazeArtwork.gameObject.SetActive(snow);
        }
        if (fieryGehennaArtwork != null)
        {
            fieryGehennaArtwork.sortingOrder = -21;
            fieryGehennaArtwork.gameObject.SetActive(gehenna);
        }
        Tilemap visibleFloor = null;
        foreach (Tilemap tiles in GetComponentsInChildren<Tilemap>(true))
        {
            bool bayTilemap = tiles == forgottenBayTiles;
            bool snowTilemap = tiles == snowyHazeTiles;
            bool gehennaTilemap = tiles == fieryGehennaTiles;
            bool gloomyTilemap = tiles == gloomyHollowTiles;
            bool bogTilemap = tiles == drownedBogTiles;
            tiles.color = bayTilemap ? bayTerrainTint : snowTilemap ? snowTerrainTint
                : gehennaTilemap ? gehennaTerrainTint
                : gloomyTilemap ? gloomyTerrainTint : bogTilemap ? bogTerrainTint
                : swamp ? swampTerrainTint : ordinaryTerrainTint;
            bool visible = bayTilemap ? bay : snowTilemap ? snow
                : gehennaTilemap ? gehenna : gloomyTilemap ? gloomy
                : bogTilemap ? swamp : !bay && !snow && !gehenna && !gloomy && !swamp;
            tiles.gameObject.SetActive(visible);
            if (visible)
            {
                visibleFloor = tiles;
            }
        }
        frame?.RefreshLowerField(GetComponent<Stage>(), visibleFloor);
    }
}
