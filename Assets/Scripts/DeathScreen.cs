using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathScreen : MonoBehaviour
{
    [Header("Ссылки UI")]
    public GameObject deathScreenUI; // Empty Object с картинкой и кнопками

    private bool isDead = false;

    void Start()
    {
        deathScreenUI.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Вызывается при смерти
    public void ShowDeathScreen()
    {
        if (isDead) return;
        isDead = true;

        Time.timeScale = 0f; // Пауза времени
        deathScreenUI.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // Скрыть экран (может быть использовано, если ресет)
    public void HideDeathScreen()
    {
        deathScreenUI.SetActive(false);
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
