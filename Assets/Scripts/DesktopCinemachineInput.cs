using Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bridges the platform-neutral gameplay look action into Cinemachine's axis provider seam.
/// </summary>
public sealed class DesktopCinemachineInput : MonoBehaviour, AxisState.IInputAxisProvider
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= InstallForSampleScene;
        SceneManager.sceneLoaded += InstallForSampleScene;
    }

    private static void InstallForSampleScene(Scene scene, LoadSceneMode mode)
    {
        if (Application.isMobilePlatform ||
            scene.name != "SampleScene")
        {
            return;
        }

        CinemachineFreeLook[] cameras = FindObjectsOfType<CinemachineFreeLook>();
        foreach (CinemachineFreeLook camera in cameras)
        {
            DesktopCinemachineInput provider = camera.GetComponent<DesktopCinemachineInput>();
            if (provider == null)
            {
                provider = camera.gameObject.AddComponent<DesktopCinemachineInput>();
            }

            camera.UpdateInputAxisProvider();
        }
    }

    public float GetAxisValue(int axis)
    {
        if (!enabled ||
            PauseMenu.GameIsPaused ||
            PauseMenu.PointerLockGestureConsumed ||
            Cursor.lockState != CursorLockMode.Locked)
        {
            return 0f;
        }

        Vector2 look = GameplayInputActions.Current.Look;
        switch (axis)
        {
            case 0:
                return look.x;
            case 1:
                // Cinemachine's FreeLook vertical axis increases toward the
                // lower camera orbit. Keep mouse-up aligned with a rising view.
                return -look.y;
            default:
                return 0f;
        }
    }
}
