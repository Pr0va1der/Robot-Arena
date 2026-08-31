using RobotArena.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WinScreen : MonoBehaviour
{
    [Header("Ссылки UI")]
    public GameObject winScreenUI;

    private bool isWin = false;

    public SessionResult? Result { get; private set; }
    public float? BestTime { get; private set; }

    void Start()
    {
        if (winScreenUI != null)
        {
            winScreenUI.SetActive(false);
        }

        if (DesktopArenaUi.Instance != null)
        {
            return;
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void ShowWinScreen()
    {
        if (isWin) return;
        isWin = true;

        Time.timeScale = 0f;
        if (winScreenUI != null)
        {
            winScreenUI.SetActive(true);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void SetResult(SessionResult result, float? bestTime)
    {
        Result = result;
        BestTime = bestTime;
    }

    public void HideWinScreen()
    {
        if (winScreenUI != null)
        {
            winScreenUI.SetActive(false);
        }
        Time.timeScale = 1f;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        isWin = false;
    }

    public void RestartLevel()
    {
        GameMusicRuntime.GetOrCreate().BeginFreshCalm();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitToMenu(string sceneName)
    {
        GameMusicRuntime.GetOrCreate().BeginFreshCalm();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(0);
    }
}
