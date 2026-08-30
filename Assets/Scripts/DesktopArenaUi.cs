using RobotArena.Session;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    private GameObject desktopRoot;
    private GameObject gameplayPanel;
    private GameObject tutorialPanel;
    private GameObject resultPanel;
    private TextMeshProUGUI waveText;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI botText;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI healthText;
    private TextMeshProUGUI cooldownText;
    private TextMeshProUGUI pointerPrompt;
    private Button pauseResumeButton;
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

        DesktopCanvasLayout.Ensure(canvas);
        DesktopUiFactory.EnsureEventSystem();

        sessionManager = FindObjectOfType<BotSpawnManager>();
        playerHealth = FindObjectOfType<PlayerHP>();
        playerShooting = FindObjectOfType<PlayerShooting>();

        GameObject legacyPauseUi = pauseMenu.pauseMenuUI;
        GameObject legacyGameplayUi = pauseMenu.ingameUI;
        HideLegacyScreens(legacyPauseUi, legacyGameplayUi);

        CreateUi(canvas.transform);
        pauseMenu.ConfigureUi(
            FindChildPanel("PauseMenu").gameObject,
            gameplayPanel);
        pauseMenu.SetResultMode(false);
        pauseMenu.SetTutorialMode(true);
        pauseMenu.RequirePointerLockClick();
        pauseMenu.SetPauseSource(PauseSource.User, true);

        tutorialVisible = true;
        tutorialPanel.SetActive(true);
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

        UpdateHud();
        SelectButton(tutorialPanel.transform.Find("Card/StartButton")?.GetComponent<Button>());
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
        pauseMenu.SetTutorialMode(false);
        pauseMenu.RequirePointerLockClick();
        pauseMenu.Resume();
        SelectButton(null);
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
        pauseMenu.SetPauseSource(PauseSource.User, true);

        SessionResult result = sessionManager.Result.Value;
        TextMeshProUGUI title = resultPanel.transform.Find("Card/Title").GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI details = resultPanel.transform.Find("Card/Details").GetComponent<TextMeshProUGUI>();
        title.text = state == SessionState.Won ? "Победа" : "Сессия завершена";
        title.color = state == SessionState.Won ? AccentColor : Color.white;
        details.text = string.Format(
            "Достигнута волна: {0}\nАктивное время: {1:0.0} с\nЛучшее время: {2}",
            result.ReachedWave,
            result.ActiveTime,
            sessionManager.BestTime.HasValue ? sessionManager.BestTime.Value.ToString("0.0") + " с" : "—");

        SelectButton(resultPanel.transform.Find("Card/RetryButton")?.GetComponent<Button>());
    }

    private void UpdateHud()
    {
        if (sessionManager == null || gameplayPanel == null)
        {
            return;
        }

        waveText.text = string.Format(
            "ВОЛНА {0}/{1}",
            sessionManager.CurrentWaveNumber,
            sessionManager.TotalWaves);
        statusText.text = "СОСТОЯНИЕ: " + GetStateLabel(sessionManager.State);
        botText.text = "БОТОВ: " + sessionManager.LiveBotCount;

        if (sessionManager.State == SessionState.Spawning)
        {
            timerText.text = string.Format("ПОЯВЛЕНИЕ: {0:0}", sessionManager.SpawnTimeRemaining);
        }
        else if (sessionManager.State == SessionState.Intermission)
        {
            timerText.text = string.Format("СЛЕДУЮЩАЯ ВОЛНА: {0:0}", sessionManager.IntermissionTimeRemaining);
        }
        else if (sessionManager.State == SessionState.Clearing)
        {
            timerText.text = "ЗАЧИСТИТЕ АРЕНУ";
        }
        else
        {
            timerText.text = string.Empty;
        }

        if (playerHealth != null)
        {
            healthText.text = string.Format(
                "ЗДОРОВЬЕ {0:0}/{1:0}",
                playerHealth.CurrentHealth,
                playerHealth.MaxHealth);
            healthFill.fillAmount = playerHealth.MaxHealth <= 0f
                ? 0f
                : playerHealth.CurrentHealth / playerHealth.MaxHealth;
        }

        if (playerShooting != null)
        {
            cooldownText.text = playerShooting.IsUltimateReady
                ? "ИМПУЛЬС: ГОТОВ"
                : string.Format("ИМПУЛЬС: {0:0.0} С", playerShooting.UltimateCooldownRemaining);
        }

        bool showPointerPrompt = !tutorialVisible &&
                                  !resultVisible &&
                                  !pauseMenu.IsPaused &&
                                  pauseMenu.RequiresPointerLockClick;
        pointerPrompt.gameObject.SetActive(showPointerPrompt);
    }

    private static string GetStateLabel(SessionState state)
    {
        switch (state)
        {
            case SessionState.Spawning:
                return "ПОЯВЛЕНИЕ";
            case SessionState.Clearing:
                return "ЗАЧИСТКА";
            case SessionState.Intermission:
                return "ПЕРЕРЫВ";
            case SessionState.Won:
                return "ПОБЕДА";
            case SessionState.Lost:
                return "ПОРАЖЕНИЕ";
            default:
                return "ОЖИДАНИЕ";
        }
    }

    private void OnHealthChanged(float current, float max)
    {
        UpdateHud();
    }

    private void OnPauseStateChanged(bool isPaused)
    {
        if (isPaused &&
            !tutorialVisible &&
            !resultVisible &&
            pauseMenu.ActivePauseSources.HasFlag(PauseSource.User))
        {
            SelectButton(pauseResumeButton);
        }
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
        pointerPrompt = DesktopUiFactory.CreateText(
            "PointerPrompt",
            safeRoot,
            "КЛИКНИТЕ ПО АРЕНЕ, ЧТОБЫ ПРОДОЛЖИТЬ",
            24f,
            AccentColor);
        SetAnchor(pointerPrompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(700f, 48f));
        pointerPrompt.gameObject.SetActive(false);

        CreatePausePanel(safeRoot);
        CreateTutorialPanel(safeRoot);
        CreateResultPanel(safeRoot);
    }

    private void CreateHud(Transform parent)
    {
        waveText = DesktopUiFactory.CreateText("WaveText", parent, string.Empty, 30f, AccentColor, TextAlignmentOptions.Left);
        SetAnchor(waveText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -26f), new Vector2(380f, 42f));

        statusText = DesktopUiFactory.CreateText("StatusText", parent, string.Empty, 20f, Color.white, TextAlignmentOptions.Left);
        SetAnchor(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -70f), new Vector2(380f, 34f));

        botText = DesktopUiFactory.CreateText("BotText", parent, string.Empty, 24f, Color.white, TextAlignmentOptions.Right);
        SetAnchor(botText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -28f), new Vector2(380f, 42f));

        timerText = DesktopUiFactory.CreateText("TimerText", parent, string.Empty, 22f, Color.white);
        SetAnchor(timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(460f, 42f));

        GameObject healthBar = DesktopUiFactory.CreatePanel(
            "HealthBar",
            parent,
            new Color(0f, 0f, 0f, 0.55f),
            false);
        RectTransform healthBarRect = healthBar.GetComponent<RectTransform>();
        SetAnchor(healthBarRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 32f), new Vector2(360f, 24f));

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
        SetAnchor(healthText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 58f), new Vector2(360f, 34f));

        cooldownText = DesktopUiFactory.CreateText("CooldownText", parent, string.Empty, 20f, AccentColor, TextAlignmentOptions.Right);
        SetAnchor(cooldownText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 38f), new Vector2(360f, 34f));

        TextMeshProUGUI controls = DesktopUiFactory.CreateText(
            "ControlsHint",
            parent,
            "WASD / стрелки — движение    Мышь — обзор и огонь    Space — прыжок    Shift — торможение    Q — импульс    Esc — пауза",
            16f,
            new Color(1f, 1f, 1f, 0.75f));
        SetAnchor(controls.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(1200f, 30f));

        TextMeshProUGUI crosshair = DesktopUiFactory.CreateText("Crosshair", parent, "+", 30f, Color.white);
        SetAnchor(crosshair.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
    }

    private void CreatePausePanel(Transform parent)
    {
        GameObject panel = CreateOverlay("PauseMenu", parent);
        GameObject card = CreateCard(panel.transform, "Card", new Vector2(560f, 390f));
        CreateCardText(card.transform, "Title", "Пауза", 42f, new Vector2(0f, 118f), new Vector2(500f, 64f), AccentColor);
        CreateCardText(card.transform, "Details", "Игра остановлена", 24f, new Vector2(0f, 54f), new Vector2(500f, 48f), Color.white);
        pauseResumeButton = DesktopUiFactory.CreateButton("ResumeButton", card.transform, "Продолжить", new Vector2(420f, 64f), ButtonColor, pauseMenu.Resume);
        SetCenter(pauseResumeButton.GetComponent<RectTransform>(), new Vector2(0f, -32f), new Vector2(420f, 64f));
        Button menu = DesktopUiFactory.CreateButton("MenuButton", card.transform, "В главное меню", new Vector2(420f, 64f), ButtonColor, pauseMenu.ToTitleScreen);
        SetCenter(menu.GetComponent<RectTransform>(), new Vector2(0f, -112f), new Vector2(420f, 64f));
        ConfigureVerticalNavigation(pauseResumeButton, menu);
        panel.SetActive(false);
    }

    private void CreateTutorialPanel(Transform parent)
    {
        tutorialPanel = CreateOverlay("Tutorial", parent);
        GameObject card = CreateCard(tutorialPanel.transform, "Card", new Vector2(760f, 560f));
        CreateCardText(card.transform, "Title", "Управление", 42f, new Vector2(0f, 230f), new Vector2(700f, 64f), AccentColor);
        CreateCardText(
            card.transform,
            "Details",
            "WASD / стрелки — движение\nМышь — обзор и огонь\nSpace — прыжок\nLeft Shift — торможение\nQ — импульс отдачи\nEsc — пауза\n\nУничтожьте всех ботов в каждой волне.",
            25f,
            new Vector2(0f, 28f),
            new Vector2(650f, 330f),
            Color.white);
        Button start = DesktopUiFactory.CreateButton("StartButton", card.transform, "Начать сессию", new Vector2(430f, 70f), ButtonColor, StartTutorial);
        SetCenter(start.GetComponent<RectTransform>(), new Vector2(0f, -225f), new Vector2(430f, 70f));
        SetButtonNavigation(start, null, null);
    }

    private void CreateResultPanel(Transform parent)
    {
        resultPanel = CreateOverlay("Result", parent);
        GameObject card = CreateCard(resultPanel.transform, "Card", new Vector2(700f, 540f));
        CreateCardText(card.transform, "Title", "Сессия завершена", 42f, new Vector2(0f, 176f), new Vector2(640f, 64f), AccentColor);
        CreateCardText(card.transform, "Details", string.Empty, 26f, new Vector2(0f, 55f), new Vector2(600f, 190f), Color.white);
        Button retry = DesktopUiFactory.CreateButton("RetryButton", card.transform, "Повторить", new Vector2(400f, 64f), ButtonColor, RestartSession);
        SetCenter(retry.GetComponent<RectTransform>(), new Vector2(0f, -112f), new Vector2(400f, 64f));
        Button menu = DesktopUiFactory.CreateButton("MenuButton", card.transform, "В главное меню", new Vector2(400f, 64f), ButtonColor, pauseMenu.ToTitleScreen);
        SetCenter(menu.GetComponent<RectTransform>(), new Vector2(0f, -190f), new Vector2(400f, 64f));
        ConfigureVerticalNavigation(retry, menu);
        resultPanel.SetActive(false);
    }

    private void RestartSession()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private static GameObject CreateOverlay(string name, Transform parent)
    {
        GameObject panel = DesktopUiFactory.CreateFullScreenRoot(name, parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = OverlayColor;
        image.raycastTarget = true;
        return panel;
    }

    private static GameObject CreateCard(Transform parent, string name, Vector2 size)
    {
        GameObject card = DesktopUiFactory.CreatePanel(name, parent, CardColor, true);
        SetCenter(card.GetComponent<RectTransform>(), Vector2.zero, size);
        return card;
    }

    private static TextMeshProUGUI CreateCardText(
        Transform parent,
        string name,
        string value,
        float fontSize,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        TextMeshProUGUI text = DesktopUiFactory.CreateText(name, parent, value, fontSize, color);
        SetCenter(text.rectTransform, position, size);
        return text;
    }

    private static void SetAnchor(
        RectTransform rectTransform,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 position,
        Vector2 size)
    {
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;
    }

    private static void SetCenter(RectTransform rectTransform, Vector2 position, Vector2 size)
    {
        DesktopUiFactory.SetCenter(rectTransform, position, size);
    }

    private static void SetButtonNavigation(Button button, Selectable up, Selectable down)
    {
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnUp = up;
        navigation.selectOnDown = down;
        button.navigation = navigation;
    }

    private static void ConfigureVerticalNavigation(Button first, Button second)
    {
        SetButtonNavigation(first, null, second);
        SetButtonNavigation(second, first, null);
    }

    private static void SelectButton(Button button)
    {
        if (EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(button == null ? null : button.gameObject);
    }

    private Transform FindChildPanel(string name)
    {
        return desktopRoot.transform.Find("SafeArea/" + name);
    }

    private void HideLegacyScreens(GameObject legacyPauseUi, GameObject legacyGameplayUi)
    {
        if (legacyPauseUi != null)
        {
            legacyPauseUi.SetActive(false);
        }

        if (legacyGameplayUi != null)
        {
            legacyGameplayUi.SetActive(false);
        }

        DeathScreen deathScreen = FindObjectOfType<DeathScreen>();
        if (deathScreen != null && deathScreen.deathScreenUI != null)
        {
            deathScreen.deathScreenUI.SetActive(false);
        }

        WinScreen winScreen = FindObjectOfType<WinScreen>();
        if (winScreen != null && winScreen.winScreenUI != null)
        {
            winScreen.winScreenUI.SetActive(false);
        }
    }
}
