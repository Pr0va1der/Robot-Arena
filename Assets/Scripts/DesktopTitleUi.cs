using RobotArena.Session;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ArenaPlayerSettings = RobotArena.Session.PlayerSettings;

public sealed class DesktopTitleUi : MonoBehaviour
{
    private static readonly Color OverlayColor = new Color(0.02f, 0.04f, 0.08f, 0.88f);
    private static readonly Color CardColor = new Color(0.08f, 0.12f, 0.20f, 0.98f);
    private static readonly Color ButtonColor = new Color(0.12f, 0.33f, 0.52f, 1f);
    private static readonly Color AccentColor = new Color(0.30f, 0.82f, 0.95f, 1f);

    private Transform legacyRoot;
    private GameSettingsRuntime settingsRuntime;
    private DesktopSettingsUi settingsUi;
    private GameObject desktopRoot;
    private GameObject controlsPanel;

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI subtitleText;
    private TextMeshProUGUI hintText;
    private TextMeshProUGUI controlsTitleText;
    private TextMeshProUGUI controlsDetailsText;
    private Button startButton;
    private Button controlsButton;
    private Button settingsButton;
    private Button quitButton;
    private Button controlsCloseButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForTitleScene()
    {
        if (SceneManager.GetActiveScene().name != "Title Screen")
        {
            return;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null || canvas.GetComponent<DesktopTitleUi>() != null)
        {
            return;
        }

        DesktopTitleUi titleUi = canvas.gameObject.AddComponent<DesktopTitleUi>();
        MainMenu mainMenu = FindObjectOfType<MainMenu>();
        titleUi.legacyRoot = mainMenu == null ? null : mainMenu.transform;
    }

    private void Awake()
    {
        DesktopCanvasLayout.Ensure(GetComponent<Canvas>());
    }

    private void Start()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            enabled = false;
            return;
        }

        settingsRuntime = GameSettingsRuntime.GetOrCreate();
        settingsRuntime.SettingsChanged += OnSettingsChanged;

        DesktopUiFactory.EnsureEventSystem();
        HideLegacyUi();
        CreateUi(canvas.transform);
        RefreshLocalizedText();
        SelectButton(startButton);
    }

    private void Update()
    {
        if (GameplayInputActions.Current.PausePressed)
        {
            if (settingsUi != null && settingsUi.IsVisible)
            {
                HideSettings();
            }
            else if (controlsPanel != null && controlsPanel.activeSelf)
            {
                HideControls();
            }
        }
    }

    private void OnDestroy()
    {
        if (settingsRuntime != null)
        {
            settingsRuntime.SettingsChanged -= OnSettingsChanged;
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene("SampleScene");
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void ShowControls()
    {
        if (settingsUi != null && settingsUi.IsVisible)
        {
            settingsUi.Hide();
        }

        controlsPanel.SetActive(true);
        SelectButton(controlsCloseButton);
    }

    public void HideControls()
    {
        controlsPanel.SetActive(false);
        SelectButton(settingsButton);
    }

    public void ShowSettings()
    {
        controlsPanel.SetActive(false);
        settingsUi.Show();
    }

    public void HideSettings()
    {
        settingsUi.Hide();
        SelectButton(settingsButton);
    }

    private void OnSettingsChanged(ArenaPlayerSettings settings)
    {
        RefreshLocalizedText();
    }

    private void CreateUi(Transform canvasTransform)
    {
        desktopRoot = DesktopUiFactory.CreateFullScreenRoot("DesktopTitleUI", canvasTransform, false);
        Transform safeRoot = DesktopUiFactory.CreateFullScreenRoot(
            "SafeArea",
            desktopRoot.transform,
            true).transform;

        GameObject card = CreateCard(safeRoot, "Card", new Vector2(720f, 700f));
        titleText = CreateText(
            card.transform,
            "Title",
            string.Empty,
            56f,
            new Vector2(0f, 252f),
            new Vector2(660f, 86f),
            AccentColor);
        subtitleText = CreateText(
            card.transform,
            "Subtitle",
            string.Empty,
            23f,
            new Vector2(0f, 196f),
            new Vector2(620f, 42f),
            Color.white);

        startButton = DesktopUiFactory.CreateButton(
            "StartButton",
            card.transform,
            string.Empty,
            new Vector2(430f, 70f),
            ButtonColor,
            StartGame);
        SetCenter(startButton.GetComponent<RectTransform>(), new Vector2(0f, 104f), new Vector2(430f, 70f));
        controlsButton = DesktopUiFactory.CreateButton(
            "ControlsButton",
            card.transform,
            string.Empty,
            new Vector2(430f, 70f),
            ButtonColor,
            ShowControls);
        SetCenter(controlsButton.GetComponent<RectTransform>(), new Vector2(0f, 20f), new Vector2(430f, 70f));
        settingsButton = DesktopUiFactory.CreateButton(
            "SettingsButton",
            card.transform,
            string.Empty,
            new Vector2(430f, 70f),
            ButtonColor,
            ShowSettings);
        SetCenter(settingsButton.GetComponent<RectTransform>(), new Vector2(0f, -64f), new Vector2(430f, 70f));
        quitButton = DesktopUiFactory.CreateButton(
            "QuitButton",
            card.transform,
            string.Empty,
            new Vector2(430f, 70f),
            ButtonColor,
            QuitGame);
        SetCenter(quitButton.GetComponent<RectTransform>(), new Vector2(0f, -148f), new Vector2(430f, 70f));
        ConfigureNavigation(startButton, controlsButton, settingsButton, quitButton);

        hintText = CreateText(
            card.transform,
            "Hint",
            string.Empty,
            18f,
            new Vector2(0f, -274f),
            new Vector2(600f, 36f),
            new Color(1f, 1f, 1f, 0.75f));

        CreateControlsPanel(safeRoot);
        GameObject settingsPanel = CreateOverlay(safeRoot, "Settings");
        settingsUi = settingsPanel.AddComponent<DesktopSettingsUi>();
        settingsUi.Initialize(settingsRuntime, HideSettings);
        settingsPanel.SetActive(false);
    }

    private void CreateControlsPanel(Transform parent)
    {
        controlsPanel = CreateOverlay(parent, "Controls");
        GameObject card = CreateCard(controlsPanel.transform, "Card", new Vector2(760f, 610f));
        controlsTitleText = CreateText(
            card.transform,
            "Title",
            string.Empty,
            44f,
            new Vector2(0f, 214f),
            new Vector2(700f, 70f),
            AccentColor);
        controlsDetailsText = CreateText(
            card.transform,
            "Details",
            string.Empty,
            27f,
            new Vector2(0f, 28f),
            new Vector2(660f, 300f),
            Color.white);
        controlsCloseButton = DesktopUiFactory.CreateButton(
            "CloseButton",
            card.transform,
            string.Empty,
            new Vector2(400f, 70f),
            ButtonColor,
            HideControls);
        SetCenter(controlsCloseButton.GetComponent<RectTransform>(), new Vector2(0f, -205f), new Vector2(400f, 70f));
        SetButtonNavigation(controlsCloseButton, null, null);
        controlsPanel.SetActive(false);
    }

    private string Localize(LocalizationKey key)
    {
        return DesktopLocalization.Get(settingsRuntime, key);
    }

    private void RefreshLocalizedText()
    {
        if (titleText == null)
        {
            return;
        }

        titleText.text = Localize(LocalizationKey.GameTitle);
        subtitleText.text = Localize(LocalizationKey.Subtitle);
        hintText.text = Localize(LocalizationKey.MenuHint);
        controlsTitleText.text = Localize(LocalizationKey.Controls);
        controlsDetailsText.text = Localize(LocalizationKey.ControlsDetails);
        DesktopUiFactory.SetButtonLabel(startButton, Localize(LocalizationKey.StartSession));
        DesktopUiFactory.SetButtonLabel(controlsButton, Localize(LocalizationKey.Controls));
        DesktopUiFactory.SetButtonLabel(settingsButton, Localize(LocalizationKey.Settings));
        DesktopUiFactory.SetButtonLabel(quitButton, Localize(LocalizationKey.Quit));
        DesktopUiFactory.SetButtonLabel(controlsCloseButton, Localize(LocalizationKey.Back));
        settingsUi.Refresh();
    }

    private static GameObject CreateOverlay(Transform parent, string name)
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

    private static TextMeshProUGUI CreateText(
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

    private static void ConfigureNavigation(Button start, Button controls, Button settings, Button quit)
    {
        SetButtonNavigation(start, null, controls);
        SetButtonNavigation(controls, start, settings);
        SetButtonNavigation(settings, controls, quit);
        SetButtonNavigation(quit, settings, null);
    }

    private static void SelectButton(Button button)
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(button == null ? null : button.gameObject);
        }
    }

    private void HideLegacyUi()
    {
        if (legacyRoot == null)
        {
            return;
        }

        for (int index = 0; index < legacyRoot.childCount; index++)
        {
            legacyRoot.GetChild(index).gameObject.SetActive(false);
        }
    }
}
