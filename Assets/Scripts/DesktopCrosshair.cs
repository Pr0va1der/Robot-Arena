using UnityEngine;

/// <summary>
/// Keeps a screen-space reticle at the center of the active gameplay camera viewport.
/// </summary>
[DefaultExecutionOrder(300)]
[RequireComponent(typeof(RectTransform))]
public sealed class DesktopCrosshair : MonoBehaviour
{
    private RectTransform rectTransform;
    private Camera targetCamera;

    public bool IsVisible => gameObject.activeSelf;

    public void Bind(Camera camera)
    {
        targetCamera = camera;
        RefreshPosition();
    }

    public void SetVisible(bool visible)
    {
        if (visible)
        {
            gameObject.SetActive(true);
            RefreshPosition();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        RefreshPosition();
    }

    private void LateUpdate()
    {
        RefreshPosition();
    }

    private void RefreshPosition()
    {
        if (rectTransform == null)
        {
            return;
        }

        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            targetCamera = Camera.main;
        }

        Rect viewport = targetCamera == null
            ? new Rect(0f, 0f, 1f, 1f)
            : targetCamera.rect;

        Vector2 center = new Vector2(viewport.center.x, viewport.center.y);
        rectTransform.anchorMin = center;
        rectTransform.anchorMax = center;
        rectTransform.anchoredPosition = Vector2.zero;
    }
}
