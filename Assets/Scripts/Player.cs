using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class Player : MonoBehaviour
{
    public GameObject bullet;
    public UnityEvent OnShootEvent;
    [FormerlySerializedAs("projectileForce")]
    [SerializeField, Min(0.1f)] private float projectileSpeed = 20f;
    [SerializeField, Range(0f, 89f), Tooltip("Minimum launch angle above the horizontal axis.")]
    private float minimumLaunchAngle = 14f;

    [Header("Character visuals")]
    [SerializeField, Tooltip("Optional room-specific override. Uses Resources/PlayerVisualProfile when empty.")]
    private PlayerVisualProfile visualProfile;

    private Magazine magazine;
    public int magazineBullet;
    private Coroutine coroutine;
    private bool isShooting;
    private int comboVolleyId;
    private static int currentCombatVolleyId;
    public static int CurrentVolleyId => HasActiveProjectiles ? currentCombatVolleyId : 0;
    public static int LastCombatVolleyId => currentCombatVolleyId;
    private static Player volleyOwner;
    private int volleyAnimalIndex = -1;
    private bool queueVolleyAwaitingResolution;
    private bool combinedVolleyActive;
    private bool recallRequested;
    private float recallDeadline;
    private bool isAiming;
    private int aimingPointerId;
    private PointerEventData aimPointerEvent;
    private readonly List<RaycastResult> aimUIHits = new List<RaycastResult>();
    private bool landingPositionCommitted;
    private bool landingCancelled;
    private bool hasPendingLanding;
    private float pendingLandingX;
    private SpriteRenderer sourceRenderer;
    private SpriteRenderer characterRenderer;
    private Animator characterAnimator;
    private PlayableGraph shootingAnimationGraph;
    private Transform characterVisual;
    private Coroutine shootingVisualCoroutine;
    private Coroutine relocationCoroutine;
    private bool shootingVisualActive;
    private bool relocationActive;
    private Vector3 relocationTarget;

    public static List<Animal> animalsCollect = new List<Animal>();
    public static bool FirstAnimalOut = false;
    private static bool shootingPlayer = true;
    private static bool waitForFreshTouch;

    private static int activeVolleys;
    private static int activePresentationAnimations;
    private static PlayerVisualProfile sharedVisualProfile;
    public static event System.Action<int> VolleyStarted;
    public static bool HasProjectileBodies
    {
        get
        {
            for (int i = 0; i < animalsCollect.Count; i++)
            {
                if (animalsCollect[i] != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
    public static bool HasActiveProjectiles => HasProjectileBodies
        || FrogRider.HasActiveJumps || SquirrelAcorn.HasActiveBlasts
        || activeVolleys > 0
        || activePresentationAnimations > 0;
    public static bool HasProjectileVolley => HasProjectileBodies || activeVolleys > 0;

    public static void BlockInputUntilReleased()
    {
        waitForFreshTouch = true;
    }

    public static void AllowTutorialVolley()
    {
        // Unlock only the input gate. The tutorial has already granted and
        // loaded exactly one hedgehog volley, so other animals stay spent.
        shootingPlayer = true;
        waitForFreshTouch = true;
    }

    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private AnimalProjectileQueue animalQueue;

    [SerializeField] private Collider2D touchAreaCollider;
    private LevelGenerator currentRoom;
    public float MinimumLaunchAngle => Mathf.Clamp(minimumLaunchAngle, 0f, 89f);
    public bool IsAimingWithFirefly
    {
        get
        {
            GameObject selected = GetSelectedProjectile();
            return selected != null && selected.GetComponent<FireflyImpactEffect>() != null;
        }
    }
    public const float BoarUltimateSpreadAngle = 4f;
    public bool IsAimingWithBoarUltimate
    {
        get
        {
            GameObject selected = GetSelectedProjectile();
            int selectedIndex = animalQueue != null ? animalQueue.SelectedIndex : -1;
            return selected != null
                && selected.GetComponent<BoarProjectile>() != null
                && animalQueue != null
                && animalQueue.HasBoarUltimate(selectedIndex);
        }
    }
    public PlayerVisualProfile VisualProfile => ResolveVisualProfile();
    public SpriteRenderer CharacterRenderer => characterRenderer;
    public float VisualHalfWidth => GetHorizontalHalfSize();
    public GameObject SelectedProjectilePrefab => GetSelectedProjectile();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPresentationState()
    {
        currentCombatVolleyId = 0;
        activePresentationAnimations = 0;
        sharedVisualProfile = null;
        VolleyStarted = null;
    }

    public Vector2 ConstrainLaunchDirection(Vector2 direction)
    {
        return ClampUpwardLaunchDirection(direction, MinimumLaunchAngle);
    }

    public static Vector2 ClampUpwardLaunchDirection(Vector2 direction, float minimumAngleDegrees)
    {
        if (direction.sqrMagnitude < 0.000001f)
        {
            return Vector2.up;
        }

        direction.Normalize();
        float angle = Mathf.Clamp(minimumAngleDegrees, 0f, 89f) * Mathf.Deg2Rad;
        float minimumY = Mathf.Sin(angle);
        if (direction.y >= minimumY)
        {
            return direction;
        }

        // A perfectly vertical downward drag has no meaningful left/right intent.
        if (Mathf.Abs(direction.x) < 0.0001f)
        {
            return Vector2.up;
        }

        float horizontalSign = Mathf.Sign(direction.x);
        return new Vector2(horizontalSign * Mathf.Cos(angle), minimumY).normalized;
    }

    public void Awake()
    {
        magazine = new Magazine(magazineBullet);
        EnsureCharacterVisual();
        ResolveRoomReferences();
        if (animalQueue == null)
        {
            animalQueue = FindObjectOfType<AnimalProjectileQueue>();
        }
    }

    private void OnEnable()
    {
        EnsureCharacterVisual();
        ShowIdleVisual();
        ResolveRoomReferences();
        if (ballSpawner != null)
        {
            // Legacy room templates have this child disabled independently of Player.
            ballSpawner.gameObject.SetActive(true);
            ballSpawner.enabled = true;
            ballSpawner.HideAim(true);
        }
        isAiming = false;
    }

    private void OnDisable()
    {
        bool finishRelocation = relocationActive;
        Vector3 finalPosition = relocationTarget;
        StopAllCoroutines();
        CancelPresentationAnimations();
        if (finishRelocation)
        {
            transform.position = finalPosition;
        }
        if (isShooting)
        {
            landingCancelled = true;
            ReleaseVolley();
        }
        isAiming = false;
        if (ballSpawner != null)
        {
            ballSpawner.HideAim(true);
        }
        ShowIdleVisual();
    }

    private void Update()
    {
        StopVolleyAfterRecallTimeout();

        if (waitForFreshTouch)
        {
            UpdateFreshTouchWait();
            return;
        }

        if (IsAimingBlocked())
        {
            SetAimVisible(false);
            return;
        }

        if (GameManager.Instance.stepPlayer)
        {
            BeginPlayerTurn();
        }

        if (!HasShootableRoom())
        {
            SetAimVisible(false);
            LeaveRoomWithoutTargets();
            return;
        }

        UpdateAim();
    }

    private void StopVolleyAfterRecallTimeout()
    {
        if (!isShooting || !recallRequested || Time.time < recallDeadline)
        {
            return;
        }

        // A large magazine must not keep emitting bullets after the recall timeout.
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }

        if (animalQueue != null && volleyAnimalIndex >= 0)
        {
            animalQueue.ClearAmmo(volleyAnimalIndex);
        }
        else if (magazine != null)
        {
            magazine.currentAmmo = 0;
        }

        shootingPlayer = animalQueue != null && animalQueue.HasRemainingAmmo;
        ReleaseVolley();
        GameManager.Instance?.TryCompletePlayerTurn();
    }

    private void UpdateFreshTouchWait()
    {
        SetAimVisible(false);
        if (Input.touchCount == 0 && !Input.GetMouseButton(0))
        {
            waitForFreshTouch = false;
        }
    }

    private bool IsAimingBlocked()
    {
        GameManager game = GameManager.Instance;
        return game == null
            || game.isPaused
            || game.GameEnded
            || game.IsFrontMapOpen
            || game.stepGame
            || game.IsChoosingUpgrade
            || HasActiveProjectiles;
    }

    private void BeginPlayerTurn()
    {
        if (animalQueue != null)
        {
            animalQueue.ReloadAll();
        }
        else
        {
            magazine.Reload();
        }

        FirstAnimalOut = false;
        shootingPlayer = true;
        GameManager.Instance.stepPlayer = false;
        Debug.Log("Ход игрока");
    }

    private bool HasShootableRoom()
    {
        return currentRoom != null && !currentRoom.IsLost && currentRoom.HasLivingEnemies;
    }

    private void LeaveRoomWithoutTargets()
    {
        // This also moves the camera to another room with a shootable target.
        // Zombie graves remain shootable, so this only runs when the
        // current room has neither living enemies nor breakable graves.
        // Every authored room owns a Player component, so only the Player in
        // the camera's current room may initiate this routing decision.
        if (currentRoom != null && !currentRoom.IsLost
            && GameManager.Instance.Front != null
            && GameManager.Instance.Front.CurrentRoomId == currentRoom.RoomId)
        {
            GameManager.Instance.TryCompletePlayerTurn();
        }
    }

    private void UpdateAim()
    {
        if (GetSelectedProjectile() == null || !shootingPlayer || isShooting
            || !TryGetPointer(out Vector2 screenPosition, out TouchPhase phase, out int pointerId))
        {
            SetAimVisible(false);
            return;
        }

        Camera aimCamera = Camera.main;
        if (aimCamera == null)
        {
            SetAimVisible(false);
            return;
        }

        Vector2 worldTarget = aimCamera.ScreenToWorldPoint(screenPosition);
        bool insideRoom = IsInsideTouchArea(worldTarget);
        bool overUI = IsOverInterface(screenPosition);
        bool canTarget = insideRoom && !overUI;

        if (phase == TouchPhase.Began)
        {
            isAiming = canTarget;
            aimingPointerId = pointerId;
        }

        if (!isAiming || aimingPointerId != pointerId || phase == TouchPhase.Canceled)
        {
            SetAimVisible(false);
            return;
        }

        if (phase == TouchPhase.Ended)
        {
            SetAimVisible(false);
            if (canTarget)
            {
                Shoot(worldTarget);
            }
        }
        else if (canTarget)
        {
            ballSpawner?.ShowAim(worldTarget);
        }
        else
        {
            ballSpawner?.HideAim();
        }
    }

    private static bool TryGetPointer(out Vector2 position, out TouchPhase phase, out int pointerId)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            position = touch.position;
            phase = touch.phase;
            pointerId = touch.fingerId;
            return true;
        }

        position = Input.mousePosition;
        pointerId = -1;
        phase = Input.GetMouseButtonDown(0) ? TouchPhase.Began
            : Input.GetMouseButtonUp(0) ? TouchPhase.Ended : TouchPhase.Moved;
        return (!Application.isMobilePlatform || Application.isEditor)
            && (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0));
    }

    private bool IsOverInterface(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (aimPointerEvent == null)
        {
            aimPointerEvent = new PointerEventData(EventSystem.current);
        }
        aimPointerEvent.Reset();
        aimPointerEvent.position = screenPosition;
        aimUIHits.Clear();
        EventSystem.current.RaycastAll(aimPointerEvent, aimUIHits);
        for (int i = 0; i < aimUIHits.Count; i++)
        {
            Canvas canvas = aimUIHits[i].gameObject.GetComponentInParent<Canvas>();
            // World-space enemy health bars are valid aiming targets.
            if (canvas != null && canvas.rootCanvas.renderMode != RenderMode.WorldSpace)
            {
                return true;
            }
        }
        return false;
    }

    private void ResolveRoomReferences()
    {
        if (ballSpawner == null)
        {
            ballSpawner = GetComponentInChildren<BallSpawner>(true);
        }

        Stage room = GetComponentInParent<Stage>();
        if (room == null)
        {
            return;
        }
        currentRoom = room.GetComponentInChildren<LevelGenerator>(true);

        if (touchAreaCollider != null
            && touchAreaCollider.transform.IsChildOf(room.transform))
        {
            return;
        }

        Collider2D[] roomColliders = room.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < roomColliders.Length; i++)
        {
            if (roomColliders[i].gameObject.name.StartsWith("CollisionTouch"))
            {
                touchAreaCollider = roomColliders[i];
                break;
            }
        }
    }

    private bool IsInsideTouchArea(Vector2 worldPosition)
    {
        if (touchAreaCollider == null)
        {
            ResolveRoomReferences();
        }

        return touchAreaCollider != null && touchAreaCollider.OverlapPoint(worldPosition);
    }

    private void SetAimVisible(bool visible)
    {
        if (!visible)
        {
            isAiming = false;
        }
        if (ballSpawner != null)
        {
            if (!visible)
            {
                ballSpawner.HideAim();
            }
        }
    }
    private void Shoot(Vector2 worldTarget)
    {
        if (isShooting || HasActiveProjectiles || currentRoom == null
            || currentRoom.IsLost || !currentRoom.HasLivingEnemies || GameManager.Instance.GameEnded
            || GameManager.Instance.IsFrontMapOpen || GameManager.Instance.IsChoosingUpgrade)
        {
            return;
        }

        if (animalQueue != null)
        {
            Vector2 direction = ConstrainLaunchDirection(worldTarget - (Vector2)transform.position);
            if (animalQueue.AllVolleyEnabled && animalQueue.HasRemainingAmmo)
            {
                PlayShootingVisual();
                coroutine = StartCoroutine(ShootAllQueueCoroutine(direction));
                OnShootEvent?.Invoke();
                return;
            }

            int selectedAnimal = animalQueue.SelectedIndex;
            if (animalQueue.HasAmmo(selectedAnimal))
            {
                PlayShootingVisual();
                coroutine = StartCoroutine(ShootQueueCoroutine(direction, selectedAnimal));
                OnShootEvent?.Invoke();
                return;
            }
        }

        GameObject selectedProjectile = GetSelectedProjectile();
        if (animalQueue == null && magazine.HasAmmo() && selectedProjectile != null)
        {
            Vector2 direction = ConstrainLaunchDirection(worldTarget - (Vector2)transform.position);
            PlayShootingVisual();
            coroutine = StartCoroutine(ShootLegacyCoroutine(direction, selectedProjectile));
            OnShootEvent?.Invoke();
            return;
        }

        Debug.Log("Нет патронов!");
    }

    private IEnumerator ShootAllQueueCoroutine(Vector2 direction)
    {
        combinedVolleyActive = true;
        BeginCombinedVolley();
        bool launchedAnyProjectile = false;

        for (int order = 0; ; order++)
        {
            int animalIndex = AnimalProjectileQueue.GetAllVolleyAnimalIndex(order);
            if (animalIndex < 0)
            {
                break;
            }

            while (animalQueue != null && animalQueue.HasAmmo(animalIndex))
            {
                volleyAnimalIndex = animalIndex;
                VolleyStarted?.Invoke(animalIndex);
                float volleyDamage = animalQueue.GetManualVolleyDamageMultiplier(animalIndex);
                bool launchedThisVolley = false;

                while (animalQueue.TryConsume(animalIndex, out GameObject projectilePrefab))
                {
                    launchedAnyProjectile = true;
                    launchedThisVolley = true;
                    queueVolleyAwaitingResolution = true;
                    LaunchProjectile(direction, projectilePrefab, animalIndex, volleyDamage);
                    yield return new WaitForSeconds(0.1f);
                }

                if (!launchedThisVolley)
                {
                    break;
                }

                animalQueue.CompleteVolley(animalIndex);
            }
        }

        volleyAnimalIndex = -1;
        if (!launchedAnyProjectile)
        {
            queueVolleyAwaitingResolution = false;
        }

        ReleaseVolley();
        GameManager.Instance?.TryCompletePlayerTurn();
    }

    private IEnumerator ShootQueueCoroutine(Vector2 direction, int selectedAnimal)
    {
        BeginVolley(selectedAnimal);
        float volleyDamage = animalQueue != null
            ? animalQueue.GetManualVolleyDamageMultiplier(selectedAnimal)
            : 1f;
        while (animalQueue != null && animalQueue.TryConsume(selectedAnimal, out GameObject projectilePrefab))
        {
            queueVolleyAwaitingResolution = true;
            LaunchProjectile(direction, projectilePrefab, selectedAnimal, volleyDamage);
            yield return new WaitForSeconds(0.1f);
        }

        ReleaseVolley();
        GameManager.Instance?.TryCompletePlayerTurn();
    }

    private IEnumerator ShootLegacyCoroutine(Vector2 direction, GameObject selectedProjectile)
    {
        BeginVolley(-1);

        while (magazine.HasAmmo())
        {
            LaunchProjectile(direction, selectedProjectile, -1, 1f);
            magazine.UseAmmo();
            yield return new WaitForSeconds(0.1f);
        }

        ReleaseVolley();
        GameManager.Instance?.TryCompletePlayerTurn();
    }

    private void BeginVolley(int animalIndex)
    {
        BeginVolleyState(animalIndex);
        VolleyStarted?.Invoke(animalIndex);
    }

    private void BeginCombinedVolley()
    {
        BeginVolleyState(-1);
    }

    private void BeginVolleyState(int animalIndex)
    {
        isShooting = true;
        landingPositionCommitted = false;
        landingCancelled = false;
        hasPendingLanding = false;
        activeVolleys++;
        volleyOwner = this;
        volleyAnimalIndex = animalIndex;
        // A volley only counts once at least one round was actually consumed.
        queueVolleyAwaitingResolution = false;
        recallRequested = false;
        comboVolleyId = ProjectileComboUI.Ensure().BeginVolley();
        currentCombatVolleyId = comboVolleyId;
        FrogRider.BeginVolley(comboVolleyId);
    }

    private void ReleaseVolley()
    {
        isShooting = false;
        activeVolleys = Mathf.Max(0, activeVolleys - 1);
        coroutine = null;
        TryCommitPendingLanding();
        TryFinalizeQueueVolley();
        if (!queueVolleyAwaitingResolution && volleyOwner == this)
        {
            volleyOwner = null;
        }
    }

    public bool TryCommitProjectileLanding(float requestedWorldX)
    {
        if (landingPositionCommitted || landingCancelled)
        {
            return false;
        }

        Stage room = GetComponentInParent<Stage>();
        if (room == null)
        {
            return false;
        }

        landingPositionCommitted = true;
        FirstAnimalOut = true;
        Vector3 position = transform.position;
        position.x = room.ClampPlayerX(requestedWorldX, GetHorizontalHalfSize());
        BeginCartoonRelocation(position);
        return true;
    }

    public void NotifyProjectileDestroyed(float worldX)
    {
        if (landingPositionCommitted || landingCancelled)
        {
            return;
        }

        pendingLandingX = worldX;
        hasPendingLanding = true;
        TryCommitPendingLanding();
    }

    public void NotifyProjectileResolved()
    {
        TryCommitPendingLanding();
        TryFinalizeQueueVolley();
    }

    private void TryFinalizeQueueVolley()
    {
        if (!queueVolleyAwaitingResolution || isShooting || HasProjectileOwnedByThisPlayer())
        {
            return;
        }

        queueVolleyAwaitingResolution = false;
        if (animalQueue != null)
        {
            if (volleyAnimalIndex >= 0)
            {
                // A completed combined sequence already advances each volley while
                // emitting. A recalled sequence advances only the interrupted one.
                if (!combinedVolleyActive || recallRequested)
                {
                    animalQueue.CompleteVolley(volleyAnimalIndex);
                }
            }
            shootingPlayer = animalQueue.HasRemainingAmmo;
        }
        combinedVolleyActive = false;
        volleyAnimalIndex = -1;
        if (volleyOwner == this)
        {
            volleyOwner = null;
        }
    }

    private bool HasProjectileOwnedByThisPlayer()
    {
        for (int i = 0; i < animalsCollect.Count; i++)
        {
            Animal animal = animalsCollect[i];
            if (animal != null && animal.ReturnPlayer == this)
            {
                return true;
            }
        }
        return false;
    }

    public void CancelProjectileLandingForRecall()
    {
        landingCancelled = true;
        hasPendingLanding = false;
    }

    private void TryCommitPendingLanding()
    {
        if (!hasPendingLanding || isShooting || landingPositionCommitted || landingCancelled
            || HasNormalProjectileInFlight())
        {
            return;
        }

        TryCommitProjectileLanding(pendingLandingX);
        hasPendingLanding = false;
    }

    private bool HasNormalProjectileInFlight()
    {
        for (int i = 0; i < animalsCollect.Count; i++)
        {
            Animal animal = animalsCollect[i];
            if (animal != null && !animal.IsReturning && animal.ReturnPlayer == this)
            {
                return true;
            }
        }
        return false;
    }

    private float GetHorizontalHalfSize()
    {
        PlayerVisualProfile profile = ResolveVisualProfile();
        if (profile != null && profile.IdleSprite != null)
        {
            return Mathf.Max(0.05f, profile.WorldWidth * 0.5f);
        }

        SpriteRenderer playerRenderer = characterRenderer != null
            ? characterRenderer
            : GetComponent<SpriteRenderer>();
        if (playerRenderer != null && playerRenderer.sprite != null)
        {
            return Mathf.Max(0.05f, playerRenderer.bounds.extents.x);
        }

        return 0.25f;
    }

    private PlayerVisualProfile ResolveVisualProfile()
    {
        if (visualProfile != null)
        {
            return visualProfile;
        }

        if (sharedVisualProfile == null)
        {
            sharedVisualProfile = Resources.Load<PlayerVisualProfile>("PlayerVisualProfile");
        }

        return sharedVisualProfile;
    }

    private void EnsureCharacterVisual()
    {
        PlayerVisualProfile profile = ResolveVisualProfile();
        if (profile == null || profile.IdleSprite == null)
        {
            return;
        }

        if (sourceRenderer == null)
        {
            sourceRenderer = GetComponent<SpriteRenderer>();
        }

        if (characterVisual == null)
        {
            characterVisual = transform.Find("Character Visual");
        }

        if (characterVisual == null)
        {
            GameObject visualObject = new GameObject("Character Visual");
            visualObject.layer = gameObject.layer;
            characterVisual = visualObject.transform;
            characterVisual.SetParent(transform, false);
        }

        if (characterRenderer == null)
        {
            characterRenderer = characterVisual.GetComponent<SpriteRenderer>();
            if (characterRenderer == null)
            {
                characterRenderer = characterVisual.gameObject.AddComponent<SpriteRenderer>();
            }
        }

        if (characterAnimator == null)
        {
            characterAnimator = characterVisual.GetComponent<Animator>();
            if (characterAnimator == null)
            {
                characterAnimator = characterVisual.gameObject.AddComponent<Animator>();
            }
            characterAnimator.applyRootMotion = false;
            characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        if (sourceRenderer != null && sourceRenderer != characterRenderer)
        {
            characterRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            characterRenderer.color = sourceRenderer.color;
            characterRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            characterRenderer.sortingOrder = sourceRenderer.sortingOrder;
            characterRenderer.maskInteraction = sourceRenderer.maskInteraction;
            characterRenderer.flipX = sourceRenderer.flipX;
            characterRenderer.flipY = sourceRenderer.flipY;
            sourceRenderer.enabled = false;
        }

        if (!shootingVisualActive)
        {
            characterRenderer.sprite = profile.IdleSprite;
        }

        characterRenderer.enabled = true;
        float spriteWidth = Mathf.Max(0.001f, profile.IdleSprite.bounds.size.x);
        float worldScale = profile.WorldWidth / spriteWidth;
        Vector3 rootScale = transform.lossyScale;
        float rootX = Mathf.Max(0.0001f, Mathf.Abs(rootScale.x));
        float rootY = Mathf.Max(0.0001f, Mathf.Abs(rootScale.y));
        characterVisual.localScale = new Vector3(worldScale / rootX, worldScale / rootY, 1f);
        characterVisual.localPosition = new Vector3(0f, profile.VerticalOffset / rootY, 0f);
        if (Application.isPlaying && profile.EyeFirePrefab != null)
        {
            Transform eyes = characterVisual.Find("Pumpkin Eye Fire");
            if (eyes == null)
            {
                eyes = Instantiate(profile.EyeFirePrefab, characterVisual).transform;
                eyes.name = "Pumpkin Eye Fire";
            }
            eyes.GetComponent<PumpkinEyeFire>()?.MatchSorting(characterRenderer);
        }
    }

    private void PlayShootingVisual()
    {
        EnsureCharacterVisual();
        PlayerVisualProfile profile = ResolveVisualProfile();
        if (characterRenderer == null || profile == null
            || profile.ShootingAnimation == null)
        {
            return;
        }

        if (shootingVisualCoroutine != null)
        {
            StopCoroutine(shootingVisualCoroutine);
            FinishShootingVisual(false);
        }

        shootingVisualActive = true;
        activePresentationAnimations++;
        PlayShootingAnimationClip(profile.ShootingAnimation);
        shootingVisualCoroutine = StartCoroutine(PlayShootingVisualRoutine(profile));
    }

    private IEnumerator PlayShootingVisualRoutine(PlayerVisualProfile profile)
    {
        double duration = Mathf.Max(0.01f, profile.ShootingAnimation.length);
        // Follow the clip's clock, including pauses, instead of a second timer.
        while (shootingAnimationGraph.IsValid()
            && shootingAnimationGraph.GetRootPlayable(0).GetTime() < duration)
        {
            yield return null;
        }

        FinishShootingVisual(true);
    }

    private void PlayShootingAnimationClip(AnimationClip clip)
    {
        StopShootingAnimationClip();
        shootingAnimationGraph = PlayableGraph.Create("Player shooting clip");
        shootingAnimationGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        AnimationClipPlayable playable = AnimationClipPlayable.Create(shootingAnimationGraph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(
            shootingAnimationGraph, "Character", characterAnimator);
        output.SetSourcePlayable(playable);
        shootingAnimationGraph.Play();
        shootingAnimationGraph.Evaluate(0f);
    }

    private void StopShootingAnimationClip()
    {
        if (shootingAnimationGraph.IsValid())
        {
            shootingAnimationGraph.Destroy();
        }
    }

    private void FinishShootingVisual(bool notifyTurnManager)
    {
        StopShootingAnimationClip();
        shootingVisualCoroutine = null;
        if (shootingVisualActive)
        {
            shootingVisualActive = false;
            activePresentationAnimations = Mathf.Max(0, activePresentationAnimations - 1);
        }
        ShowIdleVisual();
        if (notifyTurnManager)
        {
            GameManager.Instance?.TryCompletePlayerTurn();
        }
    }

    private void BeginCartoonRelocation(Vector3 destination)
    {
        EnsureCharacterVisual();
        PlayerVisualProfile profile = ResolveVisualProfile();
        if (relocationCoroutine != null)
        {
            StopCoroutine(relocationCoroutine);
            FinishRelocation(false);
        }
        relocationTarget = destination;
        if (profile == null || Mathf.Abs(destination.x - transform.position.x) < 0.001f)
        {
            transform.position = destination;
            ShowIdleVisual();
            return;
        }

        relocationActive = true;
        activePresentationAnimations++;
        relocationCoroutine = StartCoroutine(CartoonRelocationRoutine(destination, profile));
    }

    private IEnumerator CartoonRelocationRoutine(Vector3 destination, PlayerVisualProfile profile)
    {
        Vector3 origin = transform.position;
        float direction = destination.x >= origin.x ? 1f : -1f;
        Vector3 visualPosition = characterVisual != null ? characterVisual.localPosition : Vector3.zero;
        Vector3 visualScale = characterVisual != null ? characterVisual.localScale : Vector3.one;
        float rootY = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        int steps = profile.RelocationSteps;
        for (int step = 1; step <= steps; step++)
        {
            // Hold each pose for one frame: a small cutout shuffle, not a spin.
            transform.position = Vector3.Lerp(origin, destination, step / (float)steps);
            if (characterVisual != null)
            {
                bool lifted = step % 2 == 1 && step < steps;
                float squash = lifted ? 0f : profile.RelocationSquash;
                characterVisual.localPosition = visualPosition
                    + Vector3.up * (lifted ? profile.RelocationHopHeight / rootY : 0f);
                characterVisual.localScale = new Vector3(
                    visualScale.x * (1f + squash),
                    visualScale.y * (1f - squash), visualScale.z);
                characterVisual.localRotation = Quaternion.Euler(0f, 0f,
                    (lifted ? -direction : direction) * profile.RelocationTiltDegrees);
            }
            yield return new WaitForSeconds(profile.RelocationFrameDuration);
        }

        FinishRelocation(true);
    }

    private void FinishRelocation(bool notifyTurnManager)
    {
        relocationCoroutine = null;
        if (relocationActive)
        {
            relocationActive = false;
            activePresentationAnimations = Mathf.Max(0, activePresentationAnimations - 1);
        }
        transform.position = relocationTarget;
        if (characterVisual != null)
        {
            EnsureCharacterVisual();
            characterVisual.localRotation = Quaternion.identity;
        }
        if (notifyTurnManager)
        {
            GameManager.Instance?.TryCompletePlayerTurn();
        }
    }

    private void CancelPresentationAnimations()
    {
        StopShootingAnimationClip();
        shootingVisualCoroutine = null;
        relocationCoroutine = null;
        if (shootingVisualActive)
        {
            shootingVisualActive = false;
            activePresentationAnimations = Mathf.Max(0, activePresentationAnimations - 1);
        }
        if (relocationActive)
        {
            relocationActive = false;
            activePresentationAnimations = Mathf.Max(0, activePresentationAnimations - 1);
        }
    }

    private void ShowIdleVisual()
    {
        PlayerVisualProfile profile = ResolveVisualProfile();
        if (characterRenderer != null && profile != null && profile.IdleSprite != null)
        {
            characterRenderer.sprite = profile.IdleSprite;
        }
        if (characterVisual != null && !relocationActive)
        {
            characterVisual.localRotation = Quaternion.identity;
        }
    }

    private void LaunchProjectile(Vector2 direction, GameObject projectilePrefab, int animalIndex,
        float shotDamageMultiplier = 1f)
    {
        // Keep direct/scripted launches under the same rule as pointer aiming.
        direction = ConstrainLaunchDirection(direction);
        bool launchBoarUltimate = animalQueue != null
            && animalIndex >= 0
            && projectilePrefab != null
            && projectilePrefab.GetComponent<BoarProjectile>() != null
            && animalQueue.HasBoarUltimate(animalIndex);
        if (launchBoarUltimate)
        {
            LaunchProjectileSingle(RotateDirection(direction, -BoarUltimateSpreadAngle),
                projectilePrefab, animalIndex, shotDamageMultiplier);
            LaunchProjectileSingle(direction, projectilePrefab, animalIndex, shotDamageMultiplier);
            LaunchProjectileSingle(RotateDirection(direction, BoarUltimateSpreadAngle),
                projectilePrefab, animalIndex, shotDamageMultiplier);
            return;
        }

        LaunchProjectileSingle(direction, projectilePrefab, animalIndex, shotDamageMultiplier);
    }

    private void LaunchProjectileSingle(Vector2 direction, GameObject projectilePrefab,
        int animalIndex, float shotDamageMultiplier)
    {
        bool useAreaExplosion = animalQueue != null
            && animalIndex >= 0
            && animalQueue.IsAreaExplosion(animalIndex);
        bool useReplication = animalQueue != null
            && animalIndex >= 0
            && animalQueue.ReplicatesOnEnemyHits(animalIndex);

        GameObject projectile = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        Animal animal = projectile.GetComponent<Animal>();
        if (animal != null)
        {
            animal.SetReturnTarget(gameObject);
            animal.SetComboVolley(comboVolleyId);
            animal.SetCombatSource(CombatDamagePalette.FromAnimalIndex(animalIndex));
        }
        if (animal != null && animalIndex >= 0)
        {
            animal.ApplyPermanentHealthMultiplier(MetaProgression.PumpkinProjectileHealthMultiplier);
            animal.SetRuntimeDamage(animal.dmg * Mathf.Max(0f, shotDamageMultiplier)
                + ArtifactEquipmentBonuses.Get(ArtifactStat.FlatDamage));
        }

        FireflyImpactEffect firefly = projectile.GetComponent<FireflyImpactEffect>();
        if (firefly != null && animalQueue != null && animalIndex >= 0)
        {
            firefly.ConfigureBurnDamage(
                animalQueue.GetBurnDamage(animalIndex),
                animalQueue.HasFireflyUltimate(animalIndex),
                animalQueue.GetBurnUpgradeLevel(animalIndex));
        }

        HoneyBadgerProjectile honeyBadger = projectile.GetComponent<HoneyBadgerProjectile>();
        if (honeyBadger != null && animalQueue != null && animalIndex >= 0)
        {
            honeyBadger.ConfigureRunUpgrades(
                animalQueue.GetHoneyBadgerAttackRadius(animalIndex),
                animalQueue.GetHoneyBadgerMaximumAttacks(animalIndex),
                animalQueue.GetHoneyBadgerConsecutiveAttacks(animalIndex),
                animalQueue.HasHoneyBadgerUltimate(animalIndex));
        }

        if (useAreaExplosion)
        {
            HedgehogExplosion explosion = projectile.GetComponent<HedgehogExplosion>();
            if (explosion == null)
            {
                explosion = projectile.AddComponent<HedgehogExplosion>();
            }

            explosion.Configure(
                animal != null ? animal.dmg : 0f,
                animalQueue.GetExplosionRadius(animalIndex),
                animalQueue.GetExplosionDuration(animalIndex),
                animalQueue.GetExplosionDamageMultiplier(animalIndex),
                animalQueue.GetHedgehogQuillDamageGrowthPerHit(animalIndex),
                animalQueue.HasHedgehogUltimate(animalIndex),
                0.09f);
        }

        if (useReplication)
        {
            RabbitReplication replication = projectile.GetComponent<RabbitReplication>();
            if (replication == null)
            {
                replication = projectile.AddComponent<RabbitReplication>();
            }

            replication.Configure(
                projectilePrefab,
                animalQueue.GetRabbitInitialReplicationHits(animalIndex),
                projectileSpeed * ArtifactEquipmentBonuses.Multiplier(ArtifactStat.ProjectileSpeedPct),
                animalQueue.GetRabbitPackDamagePerRabbit(animalIndex),
                animalQueue.GetRabbitMaximumReplicationHits(animalIndex),
                animalQueue.HasRabbitUltimate(animalIndex));
        }

        FrogProjectile frog = projectile.GetComponent<FrogProjectile>();
        if (frog != null && animalQueue != null && animalIndex >= 0)
        {
            frog.ConfigureRunUpgrades(animalQueue.GetFrogJumpRadius(animalIndex),
                animalQueue.GetFrogChorusRank(animalIndex), animalQueue.HasFrogUltimate(animalIndex));
        }

        SquirrelProjectile squirrel = projectile.GetComponent<SquirrelProjectile>();
        if (squirrel != null && animalQueue != null && animalIndex >= 0)
        {
            squirrel.ConfigureRunUpgrades(animalQueue.GetSquirrelEarlyExplosionChance(animalIndex),
                animalQueue.GetSquirrelBindingRank(animalIndex), animalQueue.HasSquirrelUltimate(animalIndex));
        }

        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            ProjectilePhysics2D projectilePhysics = projectile.GetComponent<ProjectilePhysics2D>();
            if (projectilePhysics == null)
            {
                projectilePhysics = projectile.AddComponent<ProjectilePhysics2D>();
            }
            BoarProjectile boar = projectile.GetComponent<BoarProjectile>();
            BeaverProjectile beaver = projectile.GetComponent<BeaverProjectile>();
            float launchSpeed = projectileSpeed * ArtifactEquipmentBonuses.Multiplier(ArtifactStat.ProjectileSpeedPct) * (boar != null ? boar.LaunchSpeedMultiplier : 1f);
            if (boar != null)
            {
                if (animalQueue != null && animalIndex >= 0)
                {
                    boar.ConfigureRunUpgrades(
                        animalQueue.GetBoarMaximumBounces(animalIndex),
                        animalQueue.GetBoarFirstHitDamageMultiplier(animalIndex),
                        animalQueue.HasBoarUltimate(animalIndex));
                }
                boar.Launch(direction, launchSpeed);
            }
            else if (beaver != null)
            {
                if (animalQueue != null && animalIndex >= 0)
                {
                    beaver.ConfigureRunUpgrades(
                        animalQueue.GetBeaverDefenseHealthUpgradeLevel(animalIndex),
                        animalQueue.GetBeaverBonusDamage(animalIndex),
                        animalQueue.HasBeaverUltimate(animalIndex));
                }

                beaver.Launch(direction, launchSpeed);
            }
            else
            {
                projectilePhysics.Launch(direction, launchSpeed);
            }
        }

        // Shots still being emitted share the original deadline, not a fresh two seconds.
        if (recallRequested && animal != null)
        {
            animal.DropDown(recallDeadline);
        }
    }

    private static Vector2 RotateDirection(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(direction.x * cosine - direction.y * sine,
            direction.x * sine + direction.y * cosine).normalized;
    }

    private GameObject GetSelectedProjectile()
    {
        if (animalQueue == null)
        {
            animalQueue = AnimalProjectileQueue.Instance;
        }

        return animalQueue != null ? animalQueue.SelectedProjectile : bullet;
    }
    public void DropDownBalls()
    {
        if (!HasActiveProjectiles)
        {
            return;
        }

        BlockInputUntilReleased();
        if (volleyOwner != null && !volleyOwner.recallRequested)
        {
            volleyOwner.recallRequested = true;
            volleyOwner.recallDeadline = Time.time + Animal.RecallTimeout;
        }
        float deadline = volleyOwner != null ? volleyOwner.recallDeadline : Time.time + Animal.RecallTimeout;
        for(int i = 0; i < animalsCollect.Count; i++)
        {
            if (animalsCollect[i] != null)
            {
                animalsCollect[i].DropDown(deadline);
            }
        }
    }
}
