using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;

    [Header("UI Elements")]
    public GameObject pauseMenuUI;
    public GameObject ingameUI;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameIsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }


    //public void Resume()
    //{
    //    pauseMenuUI.SetActive(false);
    //    ingameUI.SetActive(true);
    //    Time.timeScale = 1f;
    //    GameIsPaused = false;
    //    Debug.Log(GameIsPaused);
    //}


    //void pause()
    //{
    //    pausemenuui.setactive(true);
    //    ingameui.setactive(false);
    //    time.timescale = 0f;
    //    gameispaused = true;
    //    debug.log(gameispaused);
    //}


    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        ingameUI.SetActive(true);

        Time.timeScale = 1f;
        GameIsPaused = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    


    void Pause()
    {
        pauseMenuUI.SetActive(true);
        ingameUI.SetActive(false);

        Time.timeScale = 0f;
        GameIsPaused = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }



    public void ToTitleScreen()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }
}
