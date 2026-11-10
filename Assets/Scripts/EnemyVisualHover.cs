using UnityEngine;

// Moves only the artwork. The enemy's grid position, collider and health bar stay put.
[DisallowMultipleComponent]
[DefaultExecutionOrder(750)]
public sealed class EnemyVisualHover : MonoBehaviour
{
    [SerializeField, Min(0f)] private float floatHeight = 0.055f;
    [SerializeField, Min(0f)] private float floatCyclesPerSecond = 0.7f;
    [SerializeField, Range(0f, 30f)] private float swayDegrees = 3f;

    private Vector3 restPosition;
    private Quaternion restRotation;
    private float phaseOffset;

    private void Awake()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        phaseOffset = Random.Range(0f, 2f * Mathf.PI);
    }

    private void LateUpdate()
    {
        float phase = Time.time * floatCyclesPerSecond * 2f * Mathf.PI + phaseOffset;
        transform.localPosition = restPosition + Vector3.up * (Mathf.Sin(phase) * floatHeight);
        transform.localRotation = restRotation *
            Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.6f) * swayDegrees);
    }

    private void OnDisable()
    {
        transform.localPosition = restPosition;
        transform.localRotation = restRotation;
    }
}
