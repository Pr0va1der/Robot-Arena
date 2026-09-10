using RobotArena.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused { get; private set; }
    public static bool AudioIsPaused { get; private set; }
    public static PauseSource CurrentActivePauseSources { get; private set; }
    public static bool PointerLockGestureConsumed { get; private set; }

    private readonly PauseCoordinator pauseCoordinator = new PauseCoordinator();
    private bool focusWasLost;
    private bool applicationWasPaused;
    private bool tutorialMode;
    private bool resultMode;

    public PauseCoordinator PauseCoordinator => pauseCoordinator;
    public bool IsPaused => pauseCoordinator.IsPaused;
    public bool IsGameplayPaused => pauseCoordinator.IsGameplayPaused;
    public bool IsAudioPaused => pauseCoordinator.IsAudioPaused;
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

    public void SetTutorialMode(bool isActive)
    {
        tutorialMode = isActive;
        SetPauseSource(PauseSource.Tutorial, isActive);
    }

    public void SetResultMode(bool isActive)
    {
        resultMode = isActive;
        SetPauseSource(PauseSource.Result, isActive);
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
        CurrentActivePauseSources = pauseCoordinator.ActiveSources;
    }

    public void Resume()
    {
        if (!pauseCoordinator.IsPaused && !pauseCoordinator.RequiresPointerLockClick)
        {
            return;
        }

        pauseCoordinator.RequirePointerLockClick();
        SetPauseSource(PauseSource.User, false);
    }

    public bool ResumeFromPointerGesture()
    {
        pauseCoordinator.RequirePointerLockClick();
        SetPauseSource(PauseSource.User, false);
        return TryAcquirePointerLockFromUserGesture();
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

    private void OnPauseStateChanged()
    {
        ApplyPauseState();
    }

    private void ApplyPauseState()
    {
        bool gameplayPaused = pauseCoordinator.IsGameplayPaused;
        GameIsPaused = gameplayPaused;
        AudioIsPaused = pauseCoordinator.IsAudioPaused;
        CurrentActivePauseSources = pauseCoordinator.ActiveSources;
        Time.timeScale = gameplayPaused ? 0f : 1f;
        AudioListener.pause = AudioIsPaused;

        if (gameplayPaused || pauseCoordinator.RequiresPointerLockClick)
        {
            UnlockPointer();
        }
        else
        {
            LockPointer();
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
        GameMusicRuntime.GetOrCreate().BeginFreshCalm();
        SetPauseSource(PauseSource.Result, false);
        SetPauseSource(PauseSource.User, false);
        pauseCoordinator.ClearResumeRequirement();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene("Title Screen");
    }

    private void OnDestroy()
    {
        pauseCoordinator.PauseStateChanged -= OnPauseStateChanged;
        GameIsPaused = false;
        AudioIsPaused = false;
        CurrentActivePauseSources = PauseSource.None;
        PointerLockGestureConsumed = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UnlockPointer();
    }
}
