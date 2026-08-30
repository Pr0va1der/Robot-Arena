using UnityEngine;
using RobotArena.Session;
using UnityEngine.SceneManagement;

public class DeathScreen : MonoBehaviour
{
    [Header("Ссылки UI")]
    public GameObject deathScreenUI; // Empty Object с картинкой и кнопками

    private bool isDead = false;

    public SessionResult? Result { get; private set; }
    public float? BestTime { get; private set; }

    void Start()
    {
        if (deathScreenUI != null)
        {
            deathScreenUI.SetActive(false);
        }

        if (DesktopArenaUi.Instance != null)
        {
            return;
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Вызывается при смерти
    public void ShowDeathScreen()
    {
        if (isDead) return;
        isDead = true;

        Time.timeScale = 0f; // Пауза времени
        if (deathScreenUI != null)
        {
            deathScreenUI.SetActive(true);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void SetResult(SessionResult result, float? bestTime)
    {
        Result = result;
        BestTime = bestTime;
    }

    // Скрыть экран (может быть использовано, если ресет)
    public void HideDeathScreen()
    {
        if (deathScreenUI != null)
        {
            deathScreenUI.SetActive(false);
        }
        Time.timeScale = 1f;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        isDead = false;
    }

    // Кнопка рестарта уровня
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Кнопка выхода в главное меню
    public void QuitToMenu(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }
}
