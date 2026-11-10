using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent]
public sealed class ForestEyeBlinker : MonoBehaviour
{
    [SerializeField] private LineRenderer leftEye;
    [SerializeField] private LineRenderer rightEye;
    [SerializeField, Min(0.04f)] private float blinkDuration = 0.14f;
    [SerializeField] private Vector2 blinkInterval = new Vector2(1.8f, 4.8f);

    private float wait;
    private float blinkAge = -1f;
    private uint randomState;

    public LineRenderer LeftEye => leftEye;
    public LineRenderer RightEye => rightEye;

    public void Configure(Material material, Color color, int sortingOrder)
    {
        leftEye = EnsureEye("Left Eye", leftEye, material, color, sortingOrder);
        rightEye = EnsureEye("Right Eye", rightEye, material, color, sortingOrder);
        leftEye.transform.localPosition = new Vector3(-0.075f, 0f, 0f);
        rightEye.transform.localPosition = new Vector3(0.075f, 0f, 0f);
        ResetBlink();
    }

    private LineRenderer EnsureEye(string objectName, LineRenderer current,
        Material material, Color color, int sortingOrder)
    {
        if (current == null)
        {
            Transform child = transform.Find(objectName);
            if (child == null)
            {
                GameObject eye = new GameObject(objectName);
                child = eye.transform;
                child.SetParent(transform, false);
            }
            current = child.GetComponent<LineRenderer>();
            if (current == null)
            {
                current = child.gameObject.AddComponent<LineRenderer>();
            }
        }

        current.useWorldSpace = false;
        current.positionCount = 2;
        current.SetPosition(0, new Vector3(-0.022f, 0f, 0f));
        current.SetPosition(1, new Vector3(0.022f, 0f, 0f));
        current.widthMultiplier = 0.055f;
        current.numCapVertices = 5;
        current.numCornerVertices = 2;
        current.textureMode = LineTextureMode.Stretch;
        current.alignment = LineAlignment.View;
        current.startColor = color;
        current.endColor = color;
        current.sharedMaterial = material;
        current.sortingOrder = sortingOrder;
        current.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        current.receiveShadows = false;
        current.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        current.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        current.transform.localRotation = Quaternion.identity;
        current.transform.localScale = Vector3.one;
        return current;
    }

    private void OnEnable() => ResetBlink();

    private void OnDisable()
    {
        Vector3 scale = transform.localScale;
        scale.y = 1f;
        transform.localScale = scale;
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            SetOpenAmount(1f);
            return;
        }

        if (blinkAge < 0f)
        {
            wait -= Time.unscaledDeltaTime;
            if (wait <= 0f)
            {
                blinkAge = 0f;
            }

            return;
        }

        blinkAge += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(blinkAge / blinkDuration);
        // Close and reopen without a hard visibility toggle.
        SetOpenAmount(Mathf.Max(0.06f, Mathf.Abs(progress * 2f - 1f)));
        if (progress >= 1f)
        {
            ResetBlink();
        }
    }

    private void ResetBlink()
    {
        if (randomState == 0)
        {
            randomState = unchecked((uint)(GetInstanceID() * 747796405) + 2891336453u);
        }

        randomState = randomState * 1664525u + 1013904223u;
        float normalized = (randomState & 0x00FFFFFFu) / 16777215f;
        wait = Mathf.Lerp(Mathf.Max(0.2f, blinkInterval.x),
            Mathf.Max(blinkInterval.x, blinkInterval.y), normalized);
        blinkAge = -1f;
        SetOpenAmount(1f);
    }

    private void SetOpenAmount(float amount)
    {
        Vector3 scale = transform.localScale;
        scale.y = amount;
        transform.localScale = scale;
    }
}
