using RobotArena.Session;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ArenaPlayerSettings = RobotArena.Session.PlayerSettings;

public sealed class DesktopTitleUi : MonoBehaviour
{
    private static readonly Color OverlayColor = new Color(0.02f, 0.04f, 0.08f, 0.88f);
    private static readonly Color CardColor = new Color(0.08f, 0.12f, 0.20f, 0.98f);
    private static readonly Color ButtonColor = new Color(0.12f, 0.33f, 0.52f, 1f);
    private static readonly Color AccentColor = new Color(0.30f, 0.82f, 0.95f, 1f);

    private GameSettingsRuntime settingsRuntime;
    private DesktopSettingsUi settingsUi;
    private GameObject desktopRoot;
    private GameObject controlsPanel;
    private GameObject authorsPanel;

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI subtitleText;
    private TextMeshProUGUI hintText;
    private TextMeshProUGUI controlsTitleText;
    private TextMeshProUGUI controlsDetailsText;
    private TextMeshProUGUI authorsTitleText;
    private TextMeshProUGUI authorsDetailsText;
    private Button startButton;
    private Button controlsButton;
    private Button settingsButton;
    private Button authorsButton;
    private Button controlsCloseButton;
    private Button authorsCloseButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= InstallForTitleScene;
        SceneManager.sceneLoaded += InstallForTitleScene;
    }

    private static void InstallForTitleScene(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Title Screen")
        {
            return;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null || canvas.GetComponent<DesktopTitleUi>() != null)
        {
            return;
        }

        canvas.gameObject.AddComponent<DesktopTitleUi>();
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
        CreateUi(canvas.transform);
        RefreshLocalizedText();
        DesktopUiFactory.Select(startButton);
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
            else if (authorsPanel != null && authorsPanel.activeSelf)
            {
                HideAuthors();
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

    public void ShowControls()
    {
        if (settingsUi != null && settingsUi.IsVisible)
        {
            settingsUi.Hide();
        }

        authorsPanel.SetActive(false);
        controlsPanel.SetActive(true);
        DesktopUiFactory.Select(controlsCloseButton);
    }

    public void HideControls()
    {
        controlsPanel.SetActive(false);
        DesktopUiFactory.Select(controlsButton);
    }

    public void ShowAuthors()
    {
        if (settingsUi != null && settingsUi.IsVisible)
        {
            settingsUi.Hide();
        }

        controlsPanel.SetActive(false);
        authorsPanel.SetActive(true);
        DesktopUiFactory.Select(authorsCloseButton);
    }

    public void HideAuthors()
    {
        authorsPanel.SetActive(false);
        DesktopUiFactory.Select(authorsButton);
    }

    public void ShowSettings()
    {
        controlsPanel.SetActive(false);
        authorsPanel.SetActive(false);
        settingsUi.Show();
    }

    public void HideSettings()
    {
        settingsUi.Hide();
        DesktopUiFactory.Select(settingsButton);
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

        GameObject card = DesktopUiFactory.CreateCard("Card", safeRoot, CardColor, new Vector2(720f, 700f));
        titleText = DesktopUiFactory.CreatePositionedText(
            "Title", card.transform, string.Empty, 56f, AccentColor,
            new Vector2(0f, 252f), new Vector2(660f, 86f));
        subtitleText = DesktopUiFactory.CreatePositionedText(
            "Subtitle", card.transform, string.Empty, 23f, Color.white,
            new Vector2(0f, 196f), new Vector2(620f, 42f));

        startButton = DesktopUiFactory.CreateButton(
            "StartButton", card.transform, string.Empty, new Vector2(430f, 70f), ButtonColor, StartGame);
        DesktopUiFactory.SetCenter(startButton.GetComponent<RectTransform>(), new Vector2(0f, 104f), new Vector2(430f, 70f));
        controlsButton = DesktopUiFactory.CreateButton(
            "ControlsButton", card.transform, string.Empty, new Vector2(430f, 70f), ButtonColor, ShowControls);
        DesktopUiFactory.SetCenter(controlsButton.GetComponent<RectTransform>(), new Vector2(0f, 20f), new Vector2(430f, 70f));
        settingsButton = DesktopUiFactory.CreateButton(
            "SettingsButton", card.transform, string.Empty, new Vector2(430f, 70f), ButtonColor, ShowSettings);
        DesktopUiFactory.SetCenter(settingsButton.GetComponent<RectTransform>(), new Vector2(0f, -64f), new Vector2(430f, 70f));
        authorsButton = DesktopUiFactory.CreateButton(
            "AuthorsButton", card.transform, string.Empty, new Vector2(430f, 70f), ButtonColor, ShowAuthors);
        DesktopUiFactory.SetCenter(authorsButton.GetComponent<RectTransform>(), new Vector2(0f, -148f), new Vector2(430f, 70f));
        DesktopUiFactory.ConfigureVerticalNavigation(startButton, controlsButton, settingsButton, authorsButton);

        hintText = DesktopUiFactory.CreatePositionedText(
            "Hint", card.transform, string.Empty, 18f, new Color(1f, 1f, 1f, 0.75f),
            new Vector2(0f, -274f), new Vector2(600f, 36f));

        CreateControlsPanel(safeRoot);
        CreateAuthorsPanel(safeRoot);
        GameObject settingsPanel = DesktopUiFactory.CreateOverlay("Settings", safeRoot, OverlayColor);
        settingsUi = settingsPanel.AddComponent<DesktopSettingsUi>();
        settingsUi.Initialize(settingsRuntime, HideSettings);
        settingsPanel.SetActive(false);
    }

    private void CreateControlsPanel(Transform parent)
    {
        controlsPanel = DesktopUiFactory.CreateOverlay("Controls", parent, OverlayColor);
        GameObject card = DesktopUiFactory.CreateCard("Card", controlsPanel.transform, CardColor, new Vector2(760f, 610f));
        controlsTitleText = DesktopUiFactory.CreatePositionedText(
            "Title", card.transform, string.Empty, 44f, AccentColor,
            new Vector2(0f, 214f), new Vector2(700f, 70f));
        controlsDetailsText = DesktopUiFactory.CreatePositionedText(
            "Details", card.transform, string.Empty, 27f, Color.white,
            new Vector2(0f, 28f), new Vector2(660f, 300f));
        controlsCloseButton = DesktopUiFactory.CreateButton(
            "CloseButton", card.transform, string.Empty, new Vector2(400f, 70f), ButtonColor, HideControls);
        DesktopUiFactory.SetCenter(controlsCloseButton.GetComponent<RectTransform>(), new Vector2(0f, -205f), new Vector2(400f, 70f));
        DesktopUiFactory.ConfigureVerticalNavigation(controlsCloseButton);
        controlsPanel.SetActive(false);
    }

    private void CreateAuthorsPanel(Transform parent)
    {
        authorsPanel = DesktopUiFactory.CreateOverlay("Authors", parent, OverlayColor);
        GameObject card = DesktopUiFactory.CreateCard("Card", authorsPanel.transform, CardColor, new Vector2(620f, 520f));
        authorsTitleText = DesktopUiFactory.CreatePositionedText(
            "Title", card.transform, string.Empty, 44f, AccentColor,
            new Vector2(0f, 176f), new Vector2(560f, 70f));
        authorsDetailsText = DesktopUiFactory.CreatePositionedText(
            "Details", card.transform, string.Empty, 30f, Color.white,
            new Vector2(0f, 20f), new Vector2(500f, 230f));
        authorsCloseButton = DesktopUiFactory.CreateButton(
            "CloseButton", card.transform, string.Empty, new Vector2(400f, 70f), ButtonColor, HideAuthors);
        DesktopUiFactory.SetCenter(authorsCloseButton.GetComponent<RectTransform>(), new Vector2(0f, -170f), new Vector2(400f, 70f));
        DesktopUiFactory.ConfigureVerticalNavigation(authorsCloseButton);
        authorsPanel.SetActive(false);
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
        authorsTitleText.text = Localize(LocalizationKey.Authors);
        authorsDetailsText.text = Localize(LocalizationKey.AuthorsDetails);
        DesktopUiFactory.SetButtonLabel(startButton, Localize(LocalizationKey.StartSession));
        DesktopUiFactory.SetButtonLabel(controlsButton, Localize(LocalizationKey.Controls));
        DesktopUiFactory.SetButtonLabel(settingsButton, Localize(LocalizationKey.Settings));
        DesktopUiFactory.SetButtonLabel(authorsButton, Localize(LocalizationKey.Authors));
        DesktopUiFactory.SetButtonLabel(controlsCloseButton, Localize(LocalizationKey.Back));
        DesktopUiFactory.SetButtonLabel(authorsCloseButton, Localize(LocalizationKey.Back));
        settingsUi.Refresh();
    }

}
