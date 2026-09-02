using RobotArena.Session;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
public sealed class DesktopCanvasLayout : MonoBehaviour
{
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;

    private Camera targetCamera;

    public static DesktopCanvasLayout Ensure(Canvas canvas)
    {
        if (canvas == null)
        {
            return null;
        }

        DesktopCanvasLayout layout = canvas.GetComponent<DesktopCanvasLayout>();
        if (layout == null)
        {
            layout = canvas.gameObject.AddComponent<DesktopCanvasLayout>();
        }

        return layout;
    }

    private void Awake()
    {
        ConfigureCanvas();
    }

    private void LateUpdate()
    {
        ConfigureCanvas();
        ApplyViewport();
    }

    private void ConfigureCanvas()
    {
        // Screen-space canvases must keep a unit transform. The legacy scenes
        // serialized this root at zero scale, which would also hide runtime UI.
        transform.localScale = Vector3.one;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = Screen.width / (float)Screen.height > DesktopViewportLayout.TargetAspectRatio
            ? 1f
            : 0f;
    }

    private void ApplyViewport()
    {
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        NormalizedViewport viewport = DesktopViewportLayout.Calculate(Screen.width, Screen.height);
        targetCamera.rect = new Rect(viewport.X, viewport.Y, viewport.Width, viewport.Height);
    }
}
