using RobotArena.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused { get; private set; }

    [Header("UI Elements")]
    public GameObject pauseMenuUI;
    public GameObject ingameUI;

    private readonly PauseCoordinator pauseCoordinator = new PauseCoordinator();
    private bool focusWasLost;
    private bool applicationWasPaused;

    public PauseCoordinator PauseCoordinator => pauseCoordinator;
    public bool IsPaused => pauseCoordinator.IsPaused;
    public PauseSource ActivePauseSources => pauseCoordinator.ActiveSources;

    private void Awake()
    {
        pauseCoordinator.PauseStateChanged += OnPauseStateChanged;
    }

    private void Start()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }

        if (ingameUI != null)
        {
            ingameUI.SetActive(true);
        }

        ApplyPauseState(pauseCoordinator.IsPaused);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleUserPause();
        }

        if (!pauseCoordinator.IsPaused &&
            Input.GetMouseButtonDown(0) &&
            pauseCoordinator.TryConsumePointerLockRequest())
        {
            LockPointer();
        }
    }

    public void SetPauseSource(PauseSource source, bool isActive)
    {
        pauseCoordinator.SetSource(source, isActive);
    }

    public void Resume()
    {
        SetPauseSource(PauseSource.User, false);
    }

    public void SetPlatformPaused(bool isPaused)
    {
        SetPauseSource(PauseSource.Platform, isPaused);
    }

    public void SetAdvertisementPaused(bool isPaused)
    {
        SetPauseSource(PauseSource.Advertisement, isPaused);
    }

    private void ToggleUserPause()
    {
        SetPauseSource(PauseSource.User, !pauseCoordinator.IsSourceActive(PauseSource.User));
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        UpdateLifecyclePause(PauseSource.Focus, !hasFocus, ref focusWasLost);
    }

    private void OnApplicationPause(bool isPaused)
    {
        UpdateLifecyclePause(PauseSource.Platform, isPaused, ref applicationWasPaused);
    }

    private void OnPauseStateChanged(bool isPaused)
    {
        ApplyPauseState(isPaused);
    }

    private void ApplyPauseState(bool isPaused)
    {
        GameIsPaused = isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        AudioListener.pause = isPaused;

        if (isPaused || pauseCoordinator.RequiresPointerLockClick)
        {
            UnlockPointer();
        }
        else
        {
            LockPointer();
        }

        UpdatePauseUi();
    }

    private void UpdatePauseUi()
    {
        bool userPaused = pauseCoordinator.IsSourceActive(PauseSource.User);
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(userPaused);
        }

        if (ingameUI != null)
        {
            ingameUI.SetActive(!userPaused);
        }
    }

    private void UpdateLifecyclePause(PauseSource source, bool isPaused, ref bool wasPaused)
    {
        if (isPaused)
        {
            if (wasPaused)
            {
                return;
            }

            wasPaused = true;
            SetPauseSource(source, true);
            return;
        }

        if (!wasPaused)
        {
            return;
        }

        wasPaused = false;
        SetPauseSource(source, false);
    }

    private static void LockPointer()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockPointer()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ToTitleScreen()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(0);
    }

    private void OnDestroy()
    {
        pauseCoordinator.PauseStateChanged -= OnPauseStateChanged;
        GameIsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}
