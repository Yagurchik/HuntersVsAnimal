using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class MenuSafeArea : MonoBehaviour
{
    private Rect lastArea;
    private Vector2 lastSize;

    private void Update()
    {
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        Rect area = Screen.safeArea;
        Vector2 size = new Vector2(Screen.width, Screen.height);
        if (area == lastArea && size == lastSize)
        {
            return;
        }

        lastArea = area;
        lastSize = size;
        RectTransform rect = (RectTransform)transform;
        rect.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
        rect.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
