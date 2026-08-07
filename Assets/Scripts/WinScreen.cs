using UnityEngine;
using UnityEngine.SceneManagement;

public class WinScreen : MonoBehaviour
{
    [Header("—сылки UI")]
    public GameObject winScreenUI;

    private bool isWin = false;

    void Start()
    {
        winScreenUI.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void ShowWinScreen()
    {
        if (isWin) return;
        isWin = true;

        Time.timeScale = 0f;
        winScreenUI.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void HideWinScreen()
    {
        winScreenUI.SetActive(false);
        Time.timeScale = 1f;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        isWin = false;
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitToMenu(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }
}
