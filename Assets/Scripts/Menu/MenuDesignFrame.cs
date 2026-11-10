using UnityEngine;

// Keeps editable design coordinates consistent on narrow phones and landscape previews.
[ExecuteAlways, RequireComponent(typeof(RectTransform))]
public class MenuDesignFrame : MonoBehaviour
{
    public Vector2 designSize = new Vector2(1080, 1920);

    private void LateUpdate()
    {
        var rect = (RectTransform)transform;
        var parent = rect.parent as RectTransform;
        if (parent == null || designSize.x <= 0 || designSize.y <= 0)
        {
            return;
        }

        float scale = Mathf.Min(parent.rect.width / designSize.x, parent.rect.height / designSize.y);
        if (scale <= 0)
        {
            return;
        }

        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = designSize;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = new Vector3(scale, scale, 1);
    }
}
