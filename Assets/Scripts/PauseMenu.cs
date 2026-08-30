using RobotArena.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused { get; private set; }
    public static bool PointerLockGestureConsumed { get; private set; }

    [Header("UI Elements")]
    public GameObject pauseMenuUI;
    public GameObject ingameUI;

    private readonly PauseCoordinator pauseCoordinator = new PauseCoordinator();
    private bool focusWasLost;
    private bool applicationWasPaused;
    private bool tutorialMode;
    private bool resultMode;

    public PauseCoordinator PauseCoordinator => pauseCoordinator;
    public bool IsPaused => pauseCoordinator.IsPaused;
    public PauseSource ActivePauseSources => pauseCoordinator.ActiveSources;
    public bool RequiresPointerLockClick => pauseCoordinator.RequiresPointerLockClick;
    public bool IsTutorialMode => tutorialMode;
    public bool IsResultMode => resultMode;

    private void Awake()
    {
        pauseCoordinator.PauseStateChanged += OnPauseStateChanged;
        pauseCoordinator.RequirePointerLockClick();

        Canvas canvas = GetComponent<Canvas>();
        DesktopCanvasLayout.Ensure(canvas);

        DesktopArenaUi desktopUi = GetComponent<DesktopArenaUi>();
        if (desktopUi == null)
        {
            desktopUi = gameObject.AddComponent<DesktopArenaUi>();
        }

        desktopUi.Bind(this);
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
        PointerLockGestureConsumed = false;

        if (tutorialMode || resultMode)
        {
            return;
        }

        IGameplayInputActions inputActions = GameplayInputActions.Current;

        if (inputActions.PausePressed)
        {
            ToggleUserPause();
        }

        if (!pauseCoordinator.IsPaused && inputActions.PointerGesturePressed)
        {
            TryAcquirePointerLockFromUserGesture();
        }
    }

    public void ConfigureUi(GameObject pauseUi, GameObject gameplayUi)
    {
        pauseMenuUI = pauseUi;
        ingameUI = gameplayUi;
        UpdatePauseUi();
    }

    public void SetTutorialMode(bool isActive)
    {
        tutorialMode = isActive;
        UpdatePauseUi();
    }

    public void SetResultMode(bool isActive)
    {
        resultMode = isActive;
        UpdatePauseUi();
    }

    public void RequirePointerLockClick()
    {
        pauseCoordinator.RequirePointerLockClick();
        if (!pauseCoordinator.IsPaused)
        {
            UnlockPointer();
        }
    }

    public bool TryAcquirePointerLockFromUserGesture()
    {
        if (!pauseCoordinator.TryConsumePointerLockRequest())
        {
            return false;
        }

        LockPointer();
        PointerLockGestureConsumed = true;
        return true;
    }

    public void SetPauseSource(PauseSource source, bool isActive)
    {
        pauseCoordinator.SetSource(source, isActive);
    }

    public void Resume()
    {
        pauseCoordinator.RequirePointerLockClick();
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
        if (pauseCoordinator.IsSourceActive(PauseSource.User))
        {
            Resume();
            return;
        }

        SetPauseSource(PauseSource.User, true);
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
            pauseMenuUI.SetActive(userPaused && !tutorialMode && !resultMode);
        }

        if (ingameUI != null)
        {
            ingameUI.SetActive(!userPaused && !tutorialMode && !resultMode);
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
        SceneManager.LoadScene("Title Screen");
    }

    private void OnDestroy()
    {
        pauseCoordinator.PauseStateChanged -= OnPauseStateChanged;
        GameIsPaused = false;
        PointerLockGestureConsumed = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UnlockPointer();
    }
}
