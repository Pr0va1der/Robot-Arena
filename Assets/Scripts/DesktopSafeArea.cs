using UnityEngine;

public sealed class DesktopSafeArea : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastScreenSafeArea;
    private Rect lastCameraRect;
    private Vector2Int lastScreenSize;
    private Camera targetCamera;

    public static DesktopSafeArea Ensure(RectTransform parent)
    {
        if (parent == null)
        {
            return null;
        }

        DesktopSafeArea safeArea = parent.GetComponent<DesktopSafeArea>();
        if (safeArea == null)
        {
            safeArea = parent.gameObject.AddComponent<DesktopSafeArea>();
        }

        return safeArea;
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void Update()
    {
        Rect cameraRect = GetCameraRect();
        if (lastScreenSafeArea != Screen.safeArea ||
            lastCameraRect != cameraRect ||
            lastScreenSize.x != Screen.width ||
            lastScreenSize.y != Screen.height)
        {
            ApplySafeArea();
        }
    }

    private void ApplySafeArea()
    {
        if (rectTransform == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        Rect safeArea = GetSafeAreaWithinCamera();
        rectTransform.anchorMin = new Vector2(
            safeArea.xMin / Screen.width,
            safeArea.yMin / Screen.height);
        rectTransform.anchorMax = new Vector2(
            safeArea.xMax / Screen.width,
            safeArea.yMax / Screen.height);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        lastScreenSafeArea = Screen.safeArea;
        lastCameraRect = GetCameraRect();
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }

    private Rect GetSafeAreaWithinCamera()
    {
        Rect safeArea = Screen.safeArea;
        Rect cameraRect = GetCameraRect();

        float cameraXMin = cameraRect.x * Screen.width;
        float cameraYMin = cameraRect.y * Screen.height;
        float cameraXMax = cameraRect.xMax * Screen.width;
        float cameraYMax = cameraRect.yMax * Screen.height;

        return Rect.MinMaxRect(
            Mathf.Max(safeArea.xMin, cameraXMin),
            Mathf.Max(safeArea.yMin, cameraYMin),
            Mathf.Min(safeArea.xMax, cameraXMax),
            Mathf.Min(safeArea.yMax, cameraYMax));
    }

    private Rect GetCameraRect()
    {
        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            targetCamera = Camera.main;
        }

        return targetCamera == null
            ? new Rect(0f, 0f, 1f, 1f)
            : targetCamera.rect;
    }
}
