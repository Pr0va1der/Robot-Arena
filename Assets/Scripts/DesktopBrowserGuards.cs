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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= InstallForDesktopWebGl;
        SceneManager.sceneLoaded += InstallForDesktopWebGl;
    }

    private static void InstallForDesktopWebGl(Scene activeScene, LoadSceneMode mode)
    {
        if (installed || Application.isMobilePlatform)
        {
            return;
        }

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
