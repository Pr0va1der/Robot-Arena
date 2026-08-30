using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Installs the small set of browser-level protections needed by the desktop WebGL shell.
/// Input remains owned by Unity; this only prevents the page from competing with the game.
/// </summary>
public sealed class DesktopBrowserGuards : MonoBehaviour
{
    private static bool installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForDesktopWebGl()
    {
        if (installed || Application.isMobilePlatform)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "Title Screen" && activeScene.name != "SampleScene")
        {
            return;
        }

        GameObject host = new GameObject(nameof(DesktopBrowserGuards));
        DontDestroyOnLoad(host);
        host.AddComponent<DesktopBrowserGuards>();
        installed = true;
    }

    private void Awake()
    {
        InstallBrowserGuards();
    }

    private static void InstallBrowserGuards()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        RobotArenaInstallBrowserGuards();
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void RobotArenaInstallBrowserGuards();
#endif
}
