using RobotArena.Session;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ArenaPlayerSettings = RobotArena.Session.PlayerSettings;

public sealed class DesktopArenaUi : MonoBehaviour
{
    private static readonly Color OverlayColor = new Color(0.02f, 0.04f, 0.08f, 0.88f);
    private static readonly Color CardColor = new Color(0.08f, 0.12f, 0.20f, 0.98f);
    private static readonly Color ButtonColor = new Color(0.12f, 0.33f, 0.52f, 1f);
    private static readonly Color AccentColor = new Color(0.30f, 0.82f, 0.95f, 1f);

    private PauseMenu pauseMenu;
    private BotSpawnManager sessionManager;
    private PlayerHP playerHealth;
    private PlayerShooting playerShooting;
    private GameSettingsRuntime settingsRuntime;

    private GameObject desktopRoot;
    private GameObject gameplayPanel;
    private GameObject pausePanel;
    private GameObject tutorialPanel;
    private GameObject resultPanel;
    private TextMeshProUGUI waveText;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI botText;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI healthText;
    private TextMeshProUGUI cooldownText;
    private TextMeshProUGUI controlsHint;
    private TextMeshProUGUI pointerPrompt;
    private TextMeshProUGUI pauseTitleText;
    private TextMeshProUGUI pauseDetailsText;
    private TextMeshProUGUI tutorialTitleText;
    private TextMeshProUGUI tutorialDetailsText;
    private TextMeshProUGUI resultTitleText;
    private TextMeshProUGUI resultDetailsText;
    private DesktopCrosshair crosshair;
    private Button pauseResumeButton;
    private Button tutorialStartButton;
    private Button pauseMenuButton;
    private Button resultRetryButton;
    private Button resultMenuButton;
    private Image healthFill;
    private Sprite healthSprite;
    private bool tutorialVisible;
    private bool resultVisible;

    public static DesktopArenaUi Instance { get; private set; }

    public void Bind(PauseMenu menu)
    {
        pauseMenu = menu;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (pauseMenu == null)
        {
            pauseMenu = GetComponent<PauseMenu>();
        }

        Canvas canvas = GetComponent<Canvas>();
        if (pauseMenu == null || canvas == null)
        {
            enabled = false;
            return;
        }

        settingsRuntime = GameSettingsRuntime.GetOrCreate();
        settingsRuntime.SettingsChanged += OnSettingsChanged;

        DesktopCanvasLayout.Ensure(canvas);
        DesktopUiFactory.EnsureEventSystem();

        sessionManager = FindObjectOfType<BotSpawnManager>();
        playerHealth = FindObjectOfType<PlayerHP>();
        playerShooting = FindObjectOfType<PlayerShooting>();

        CreateUi(canvas.transform);
        pauseMenu.SetResultMode(false);
        bool shouldShowTutorial = !settingsRuntime.Current.HasCompletedTutorial;
        pauseMenu.SetTutorialMode(shouldShowTutorial);
        pauseMenu.RequirePointerLockClick();

        tutorialVisible = shouldShowTutorial;
        tutorialPanel.SetActive(shouldShowTutorial);
        resultPanel.SetActive(false);

        if (sessionManager != null)
        {
            sessionManager.SessionStateChanged += OnSessionStateChanged;
            if (sessionManager.State == SessionState.Won || sessionManager.State == SessionState.Lost)
            {
                ShowResult(sessionManager.State);
            }
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += OnHealthChanged;
        }

        pauseMenu.PauseCoordinator.PauseStateChanged += OnPauseStateChanged;

        RefreshLocalizedText();
        UpdateHud();
        UpdatePresentation();
        DesktopUiFactory.Select(shouldShowTutorial ? tutorialStartButton : null);
    }

    private void Update()
    {
        if (tutorialVisible && GameplayInputActions.Current.SubmitPressed)
        {
            StartTutorial();
        }

        UpdateHud();
    }

    private void OnDestroy()
    {
        if (settingsRuntime != null)
        {
            settingsRuntime.SettingsChanged -= OnSettingsChanged;
        }

        if (sessionManager != null)
        {
            sessionManager.SessionStateChanged -= OnSessionStateChanged;
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= OnHealthChanged;
        }

        if (pauseMenu != null)
        {
            pauseMenu.PauseCoordinator.PauseStateChanged -= OnPauseStateChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }

        if (healthSprite != null)
        {
            Destroy(healthSprite);
        }
    }

    public void StartTutorial()
    {
        if (!tutorialVisible || resultVisible)
        {
            return;
        }

        tutorialVisible = false;
        tutorialPanel.SetActive(false);
        settingsRuntime.MarkTutorialCompleted();
        pauseMenu.SetTutorialMode(false);
        pauseMenu.RequirePointerLockClick();
        pauseMenu.Resume();
        UpdatePresentation();
        DesktopUiFactory.Select(null);
    }

    private void OnSessionStateChanged(SessionState state)
    {
        if (state == SessionState.Won || state == SessionState.Lost)
        {
            ShowResult(state);
        }
    }

    private void ShowResult(SessionState state)
    {
        if (resultVisible || sessionManager == null || !sessionManager.Result.HasValue)
        {
            return;
        }

        resultVisible = true;
        tutorialVisible = false;
        tutorialPanel.SetActive(false);
        resultPanel.SetActive(true);
        pauseMenu.SetTutorialMode(false);
        pauseMenu.SetResultMode(true);
        RefreshResultText(state);
        UpdatePresentation();

        DesktopUiFactory.Select(resultRetryButton);
    }

    private void RefreshResultText(SessionState state)
    {
        if (sessionManager == null || !sessionManager.Result.HasValue || resultTitleText == null)
        {
            return;
        }

        SessionResult result = sessionManager.Result.Value;
        resultTitleText.text = Localize(state == SessionState.Won ? LocalizationKey.Victory : LocalizationKey.Defeat);
        resultTitleText.color = state == SessionState.Won ? AccentColor : Color.white;
        resultDetailsText.text = string.Format(
            "{0}\n{1}\n{2}",
            string.Format(Localize(LocalizationKey.ReachedWave), result.ReachedWave),
            string.Format(Localize(LocalizationKey.ActiveTime), result.ActiveTime),
            string.Format(
                Localize(LocalizationKey.BestTime),
                sessionManager.BestTime.HasValue
                    ? FormatSeconds(sessionManager.BestTime.Value)
                    : "—"));
    }

    private void UpdateHud()
    {
        if (sessionManager == null || gameplayPanel == null)
        {
            return;
        }

        waveText.text = string.Format(
            Localize(LocalizationKey.Wave),
            sessionManager.CurrentWaveNumber,
            sessionManager.TotalWaves);
        statusText.text = string.Format(
            Localize(LocalizationKey.State),
            Localize(GetStateKey(sessionManager.State)));
        botText.text = string.Format(Localize(LocalizationKey.Bots), sessionManager.LiveBotCount);

        if (sessionManager.State == SessionState.Spawning)
        {
            timerText.text = string.Format(
                Localize(LocalizationKey.Spawn),
                sessionManager.SpawnTimeRemaining);
        }
        else if (sessionManager.State == SessionState.Intermission)
        {
            timerText.text = string.Format(
                Localize(LocalizationKey.NextWave),
                sessionManager.IntermissionTimeRemaining);
        }
        else if (sessionManager.State == SessionState.Clearing)
        {
            timerText.text = Localize(LocalizationKey.ClearArena);
        }
        else
        {
            timerText.text = string.Empty;
        }

        if (playerHealth != null)
        {
            healthText.text = string.Format(
                Localize(LocalizationKey.Health),
                playerHealth.CurrentHealth,
                playerHealth.MaxHealth);
            healthFill.fillAmount = playerHealth.MaxHealth <= 0f
                ? 0f
                : playerHealth.CurrentHealth / playerHealth.MaxHealth;
        }

        if (playerShooting != null)
        {
            cooldownText.text = playerShooting.IsUltimateReady
                ? Localize(LocalizationKey.ImpulseReady)
                : string.Format(
                    Localize(LocalizationKey.ImpulseCooldown),
                    playerShooting.UltimateCooldownRemaining);
        }

        pointerPrompt.gameObject.SetActive(ShouldShowPointerPrompt());
    }

    private void UpdatePresentation()
    {
        if (gameplayPanel != null)
        {
            gameplayPanel.SetActive(!tutorialVisible && !resultVisible);
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(
                !tutorialVisible &&
                !resultVisible &&
                pauseMenu.ActivePauseSources.HasFlag(PauseSource.User));
        }

        if (pointerPrompt != null)
        {
            pointerPrompt.gameObject.SetActive(ShouldShowPointerPrompt());
        }

        if (crosshair != null)
        {
            crosshair.SetVisible(ShouldShowCrosshair());
        }
    }

    private bool ShouldShowPointerPrompt()
    {
        return IsGameplayPresentationActive() && pauseMenu.RequiresPointerLockClick;
    }

    private bool ShouldShowCrosshair()
    {
        return IsGameplayPresentationActive() && !pauseMenu.RequiresPointerLockClick;
    }

    private bool IsGameplayPresentationActive()
    {
        return !tutorialVisible && !resultVisible && !pauseMenu.IsPaused;
    }

    private void OnHealthChanged(float current, float max)
    {
        UpdateHud();
    }

    private void OnPauseStateChanged()
    {
        UpdatePresentation();

        if (pauseMenu.IsPaused &&
            !tutorialVisible &&
            !resultVisible &&
            pauseMenu.ActivePauseSources.HasFlag(PauseSource.User))
        {
            DesktopUiFactory.Select(pauseResumeButton);
        }
    }

    private void OnSettingsChanged(ArenaPlayerSettings settings)
    {
        RefreshLocalizedText();
        UpdateHud();
        if (resultVisible && sessionManager != null && sessionManager.Result.HasValue)
        {
            RefreshResultText(sessionManager.State);
        }
    }

    private void RefreshLocalizedText()
    {
        if (waveText == null)
        {
            return;
        }

        pauseTitleText.text = Localize(LocalizationKey.Pause);
        pauseDetailsText.text = Localize(LocalizationKey.GameStopped);
        tutorialTitleText.text = Localize(LocalizationKey.TutorialTitle);
        tutorialDetailsText.text = Localize(LocalizationKey.TutorialDetails);
        controlsHint.text = Localize(LocalizationKey.ControlsHint);
        pointerPrompt.text = Localize(LocalizationKey.PointerPrompt);

        DesktopUiFactory.SetButtonLabel(pauseResumeButton, Localize(LocalizationKey.Resume));
        DesktopUiFactory.SetButtonLabel(pauseMenuButton, Localize(LocalizationKey.MainMenu));
        DesktopUiFactory.SetButtonLabel(tutorialStartButton, Localize(LocalizationKey.StartSession));
        DesktopUiFactory.SetButtonLabel(resultRetryButton, Localize(LocalizationKey.StartSession));
        DesktopUiFactory.SetButtonLabel(resultMenuButton, Localize(LocalizationKey.MainMenu));

        if (resultVisible && sessionManager != null)
        {
            RefreshResultText(sessionManager.State);
        }
    }

    private string Localize(LocalizationKey key)
    {
        return DesktopLocalization.Get(settingsRuntime, key);
    }

    private static LocalizationKey GetStateKey(SessionState state)
    {
        switch (state)
        {
            case SessionState.Spawning:
                return LocalizationKey.Spawning;
            case SessionState.Clearing:
                return LocalizationKey.Clearing;
            case SessionState.Intermission:
                return LocalizationKey.Intermission;
            case SessionState.Won:
                return LocalizationKey.Victory;
            case SessionState.Lost:
                return LocalizationKey.Defeat;
            default:
                return LocalizationKey.Waiting;
        }
    }

    private string FormatSeconds(float seconds)
    {
        return seconds.ToString("0.0") + " " + Localize(LocalizationKey.Seconds);
    }

    private void CreateUi(Transform canvasTransform)
    {
        desktopRoot = DesktopUiFactory.CreateFullScreenRoot("DesktopUI", canvasTransform, false);
        Transform safeRoot = DesktopUiFactory.CreateFullScreenRoot(
            "SafeArea",
            desktopRoot.transform,
            true).transform;

        gameplayPanel = DesktopUiFactory.CreateFullScreenRoot("GameplayUI", safeRoot, false);
        CreateHud(gameplayPanel.transform);
        crosshair = CreateCrosshair(desktopRoot.transform);
        crosshair.Bind(Camera.main);
        pointerPrompt = DesktopUiFactory.CreateText(
            "PointerPrompt",
            safeRoot,
            string.Empty,
            24f,
            AccentColor);
        DesktopUiFactory.SetAnchor(pointerPrompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(700f, 48f));
        pointerPrompt.gameObject.SetActive(false);

        CreatePausePanel(safeRoot);
        CreateTutorialPanel(safeRoot);
        CreateResultPanel(safeRoot);
    }

    private static DesktopCrosshair CreateCrosshair(Transform parent)
    {
        GameObject root = new GameObject("Crosshair", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        DesktopUiFactory.SetCenter(rootRect, Vector2.zero, new Vector2(32f, 32f));

        CreateCrosshairStroke("Left", root.transform, new Vector2(-10f, 0f), new Vector2(8f, 2f));
        CreateCrosshairStroke("Right", root.transform, new Vector2(10f, 0f), new Vector2(8f, 2f));
        CreateCrosshairStroke("Up", root.transform, new Vector2(0f, 10f), new Vector2(2f, 8f));
        CreateCrosshairStroke("Down", root.transform, new Vector2(0f, -10f), new Vector2(2f, 8f));

        return root.AddComponent<DesktopCrosshair>();
    }

    private static void CreateCrosshairStroke(
        string name,
        Transform parent,
        Vector2 position,
        Vector2 size)
    {
        GameObject stroke = DesktopUiFactory.CreatePanel(name, parent, Color.white, false);
        DesktopUiFactory.SetCenter(stroke.GetComponent<RectTransform>(), position, size);

        Outline outline = stroke.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1f, 1f);
        outline.useGraphicAlpha = true;
    }

    private void CreateHud(Transform parent)
    {
        waveText = DesktopUiFactory.CreateText("WaveText", parent, string.Empty, 30f, AccentColor, TextAlignmentOptions.Left);
        DesktopUiFactory.SetAnchor(waveText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -26f), new Vector2(380f, 42f));

        statusText = DesktopUiFactory.CreateText("StatusText", parent, string.Empty, 20f, Color.white, TextAlignmentOptions.Left);
        DesktopUiFactory.SetAnchor(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -70f), new Vector2(380f, 34f));

        botText = DesktopUiFactory.CreateText("BotText", parent, string.Empty, 24f, Color.white, TextAlignmentOptions.Right);
        DesktopUiFactory.SetAnchor(botText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -28f), new Vector2(380f, 42f));

        timerText = DesktopUiFactory.CreateText("TimerText", parent, string.Empty, 22f, Color.white);
        DesktopUiFactory.SetAnchor(timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(460f, 42f));

        GameObject healthBar = DesktopUiFactory.CreatePanel(
            "HealthBar",
            parent,
            new Color(0f, 0f, 0f, 0.55f),
            false);
        RectTransform healthBarRect = healthBar.GetComponent<RectTransform>();
        DesktopUiFactory.SetAnchor(healthBarRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 32f), new Vector2(360f, 24f));

        GameObject fill = DesktopUiFactory.CreatePanel(
            "Fill",
            healthBar.transform,
            new Color(0.17f, 0.78f, 0.46f, 1f),
            false);
        healthFill = fill.GetComponent<Image>();
        healthSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        healthFill.sprite = healthSprite;
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = 0;
        DesktopUiFactory.Stretch(fill.GetComponent<RectTransform>());

        healthText = DesktopUiFactory.CreateText("HealthText", parent, string.Empty, 20f, Color.white, TextAlignmentOptions.Left);
        DesktopUiFactory.SetAnchor(healthText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 58f), new Vector2(360f, 34f));

        cooldownText = DesktopUiFactory.CreateText("CooldownText", parent, string.Empty, 20f, AccentColor, TextAlignmentOptions.Right);
        DesktopUiFactory.SetAnchor(cooldownText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 38f), new Vector2(360f, 34f));

        controlsHint = DesktopUiFactory.CreateText(
            "ControlsHint",
            parent,
            string.Empty,
            16f,
            new Color(1f, 1f, 1f, 0.75f));
        DesktopUiFactory.SetAnchor(controlsHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(1200f, 30f));

    }

    private void CreatePausePanel(Transform parent)
    {
        pausePanel = DesktopUiFactory.CreateOverlay("PauseMenu", parent, OverlayColor);
        GameObject panel = pausePanel;
        GameObject card = DesktopUiFactory.CreateCard("Card", panel.transform, CardColor, new Vector2(560f, 390f));
        pauseTitleText = DesktopUiFactory.CreatePositionedText("Title", card.transform, string.Empty, 42f, AccentColor, new Vector2(0f, 118f), new Vector2(500f, 64f));
        pauseDetailsText = DesktopUiFactory.CreatePositionedText("Details", card.transform, string.Empty, 24f, Color.white, new Vector2(0f, 54f), new Vector2(500f, 48f));
        pauseResumeButton = DesktopUiFactory.CreateButton("ResumeButton", card.transform, string.Empty, new Vector2(420f, 64f), ButtonColor, pauseMenu.Resume);
        DesktopUiFactory.BindPointerDown(pauseResumeButton, () => pauseMenu.ResumeFromPointerGesture());
        DesktopUiFactory.SetCenter(pauseResumeButton.GetComponent<RectTransform>(), new Vector2(0f, -32f), new Vector2(420f, 64f));
        pauseMenuButton = DesktopUiFactory.CreateButton("MenuButton", card.transform, string.Empty, new Vector2(420f, 64f), ButtonColor, pauseMenu.ToTitleScreen);
        DesktopUiFactory.SetCenter(pauseMenuButton.GetComponent<RectTransform>(), new Vector2(0f, -112f), new Vector2(420f, 64f));
        DesktopUiFactory.ConfigureVerticalNavigation(pauseResumeButton, pauseMenuButton);
        panel.SetActive(false);
    }

    private void CreateTutorialPanel(Transform parent)
    {
        tutorialPanel = DesktopUiFactory.CreateOverlay("Tutorial", parent, OverlayColor);
        GameObject card = DesktopUiFactory.CreateCard("Card", tutorialPanel.transform, CardColor, new Vector2(760f, 560f));
        tutorialTitleText = DesktopUiFactory.CreatePositionedText("Title", card.transform, string.Empty, 42f, AccentColor, new Vector2(0f, 230f), new Vector2(700f, 64f));
        tutorialDetailsText = DesktopUiFactory.CreatePositionedText(
            "Details",
            card.transform,
            string.Empty,
            25f,
            Color.white,
            new Vector2(0f, 28f),
            new Vector2(650f, 330f));
        tutorialStartButton = DesktopUiFactory.CreateButton("StartButton", card.transform, string.Empty, new Vector2(430f, 70f), ButtonColor, StartTutorial);
        DesktopUiFactory.SetCenter(tutorialStartButton.GetComponent<RectTransform>(), new Vector2(0f, -225f), new Vector2(430f, 70f));
        DesktopUiFactory.ConfigureVerticalNavigation(tutorialStartButton);
    }

    private void CreateResultPanel(Transform parent)
    {
        resultPanel = DesktopUiFactory.CreateOverlay("Result", parent, OverlayColor);
        GameObject card = DesktopUiFactory.CreateCard("Card", resultPanel.transform, CardColor, new Vector2(700f, 540f));
        resultTitleText = DesktopUiFactory.CreatePositionedText("Title", card.transform, string.Empty, 42f, AccentColor, new Vector2(0f, 176f), new Vector2(640f, 64f));
        resultDetailsText = DesktopUiFactory.CreatePositionedText("Details", card.transform, string.Empty, 26f, Color.white, new Vector2(0f, 55f), new Vector2(600f, 190f));
        resultRetryButton = DesktopUiFactory.CreateButton("RetryButton", card.transform, string.Empty, new Vector2(400f, 64f), ButtonColor, RestartSession);
        DesktopUiFactory.SetCenter(resultRetryButton.GetComponent<RectTransform>(), new Vector2(0f, -112f), new Vector2(400f, 64f));
        resultMenuButton = DesktopUiFactory.CreateButton("MenuButton", card.transform, string.Empty, new Vector2(400f, 64f), ButtonColor, pauseMenu.ToTitleScreen);
        DesktopUiFactory.SetCenter(resultMenuButton.GetComponent<RectTransform>(), new Vector2(0f, -190f), new Vector2(400f, 64f));
        DesktopUiFactory.ConfigureVerticalNavigation(resultRetryButton, resultMenuButton);
        resultPanel.SetActive(false);
    }

    private void RestartSession()
    {
        GameMusicRuntime.GetOrCreate().BeginFreshCalm();
        pauseMenu.SetResultMode(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

}
