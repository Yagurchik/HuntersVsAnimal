using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "PlayerVisualProfile",
    menuName = "Forest Animal War/Player Visual Profile")]
public sealed class PlayerVisualProfile : ScriptableObject
{
    [Header("Sprites")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField, Tooltip("AnimationClip played by the character Animator when a volley starts.")]
    private AnimationClip shootingAnimation;
    [SerializeField, Tooltip("Editable pair of fire glows placed over the pumpkin's button eyes.")]
    private GameObject eyeFirePrefab;

    [Header("Placement")]
    [SerializeField, Min(0.1f), Tooltip("Width of the character in world units, independent of the room template scale.")]
    private float worldWidth = 1.15f;
    [SerializeField, Tooltip("Vertical offset from the projectile launch point in world units.")]
    private float verticalOffset = -0.08f;

    [Header("Cardboard relocation")]
    [SerializeField, Range(1, 16), Tooltip("Number of visible position jumps after a projectile lands.")]
    private int relocationSteps = 6;
    [SerializeField, Min(0.01f), Tooltip("Pause between the discrete movement frames.")]
    private float relocationFrameDuration = 0.06f;
    [FormerlySerializedAs("rotationDegreesPerStep")]
    [SerializeField, Range(0f, 15f), Tooltip("Alternating cardboard tilt while shuffling, in degrees.")]
    private float relocationTiltDegrees = 7f;
    [SerializeField, Range(0f, 0.3f), Tooltip("Small visual hop on every other shuffle frame, in world units.")]
    private float relocationHopHeight = 0.09f;
    [SerializeField, Range(0f, 0.25f), Tooltip("Temporary squash of the pumpkin on contact frames.")]
    private float relocationSquash = 0.07f;

    public Sprite IdleSprite => idleSprite;
    public AnimationClip ShootingAnimation => shootingAnimation;
    public GameObject EyeFirePrefab => eyeFirePrefab;
    public float WorldWidth => Mathf.Max(0.1f, worldWidth);
    public float VerticalOffset => verticalOffset;
    public int RelocationSteps => Mathf.Clamp(relocationSteps, 1, 16);
    public float RelocationFrameDuration => Mathf.Max(0.01f, relocationFrameDuration);
    public float RelocationTiltDegrees => Mathf.Clamp(relocationTiltDegrees, 0f, 15f);
    public float RelocationHopHeight => Mathf.Clamp(relocationHopHeight, 0f, 0.3f);
    public float RelocationSquash => Mathf.Clamp(relocationSquash, 0f, 0.25f);
}
