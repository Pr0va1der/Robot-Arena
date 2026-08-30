using Cinemachine;
using UnityEngine;

/// <summary>
/// Bridges the platform-neutral gameplay look action into Cinemachine's axis provider seam.
/// </summary>
public sealed class DesktopCinemachineInput : MonoBehaviour, AxisState.IInputAxisProvider
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForSampleScene()
    {
        if (Application.isMobilePlatform ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SampleScene")
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
        if (!enabled || PauseMenu.GameIsPaused || Cursor.lockState != CursorLockMode.Locked)
        {
            return 0f;
        }

        Vector2 look = GameplayInputActions.Current.Look;
        switch (axis)
        {
            case 0:
                return look.x;
            case 1:
                return look.y;
            default:
                return 0f;
        }
    }
}
