using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TitleScreen : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject titleScreenUI;
    public GameObject settingsMenuUI;


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && settingsMenuUI.activeSelf)
        {
            BackToTitleScreen();
        }
    }


    public void BackToTitleScreen()
    {
        titleScreenUI.SetActive(true);
        settingsMenuUI.SetActive(false);
    }
}
