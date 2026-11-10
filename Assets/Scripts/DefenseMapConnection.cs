using UnityEngine;

[ExecuteAlways]
public class DefenseMapConnection : MonoBehaviour
{
    [SerializeField] private Vector2 from;
    [SerializeField] private Vector2 to;
    private Vector2 lastSize;
    public void Configure(Vector2 start, Vector2 end) { from = start; to = end; Refresh(); }
    private void OnEnable() => Refresh();
    private void LateUpdate()
    {
        RectTransform parent = transform.parent as RectTransform;
        if (parent != null && parent.rect.size != lastSize)
        {
            Refresh();
        }
    }
    private void Refresh()
    {
        RectTransform rect = transform as RectTransform;
        RectTransform parent = transform.parent as RectTransform;
        if (rect == null || parent == null)
        {
            return;
        }

        lastSize = parent.rect.size;
        Vector2 delta = Vector2.Scale(to - from, lastSize);
        rect.anchorMin = rect.anchorMax = (from + to) * 0.5f;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(delta.magnitude, 4f);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }
}
