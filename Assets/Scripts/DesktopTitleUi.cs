using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class DesktopTitleUi : MonoBehaviour
{
    private static readonly Color OverlayColor = new Color(0.02f, 0.04f, 0.08f, 0.88f);
    private static readonly Color CardColor = new Color(0.08f, 0.12f, 0.20f, 0.98f);
    private static readonly Color ButtonColor = new Color(0.12f, 0.33f, 0.52f, 1f);
    private static readonly Color AccentColor = new Color(0.30f, 0.82f, 0.95f, 1f);

    private Transform legacyRoot;
    private GameObject desktopRoot;
    private GameObject controlsPanel;

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

        DesktopUiFactory.EnsureEventSystem();
        HideLegacyUi();
        CreateUi(canvas.transform);
        SelectButton(desktopRoot.transform.Find("SafeArea/Card/StartButton")?.GetComponent<Button>());
    }

    private void Update()
    {
        if (controlsPanel != null && controlsPanel.activeSelf && GameplayInputActions.Current.PausePressed)
        {
            HideControls();
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
        controlsPanel.SetActive(true);
        SelectButton(controlsPanel.transform.Find("Card/CloseButton")?.GetComponent<Button>());
    }

    public void HideControls()
    {
        controlsPanel.SetActive(false);
        SelectButton(desktopRoot.transform.Find("SafeArea/Card/StartButton")?.GetComponent<Button>());
    }

    private void CreateUi(Transform canvasTransform)
    {
        desktopRoot = DesktopUiFactory.CreateFullScreenRoot("DesktopTitleUI", canvasTransform, false);
        Transform safeRoot = DesktopUiFactory.CreateFullScreenRoot(
            "SafeArea",
            desktopRoot.transform,
            true).transform;

        GameObject card = CreateCard(safeRoot, "Card", new Vector2(720f, 620f));
        CreateText(card.transform, "Title", "ROBOT ARENA", 56f, new Vector2(0f, 220f), new Vector2(660f, 86f), AccentColor);
        CreateText(card.transform, "Subtitle", "Desktop WebGL prototype", 23f, new Vector2(0f, 164f), new Vector2(620f, 42f), Color.white);

        Button start = DesktopUiFactory.CreateButton("StartButton", card.transform, "Начать сессию", new Vector2(430f, 70f), ButtonColor, StartGame);
        SetCenter(start.GetComponent<RectTransform>(), new Vector2(0f, 78f), new Vector2(430f, 70f));
        Button controls = DesktopUiFactory.CreateButton("ControlsButton", card.transform, "Управление", new Vector2(430f, 70f), ButtonColor, ShowControls);
        SetCenter(controls.GetComponent<RectTransform>(), new Vector2(0f, -10f), new Vector2(430f, 70f));
        Button quit = DesktopUiFactory.CreateButton("QuitButton", card.transform, "Выйти", new Vector2(430f, 70f), ButtonColor, QuitGame);
        SetCenter(quit.GetComponent<RectTransform>(), new Vector2(0f, -98f), new Vector2(430f, 70f));
        ConfigureNavigation(start, controls, quit);

        CreateText(card.transform, "Hint", "Enter / Space — выбрать    Esc — назад", 18f, new Vector2(0f, -205f), new Vector2(600f, 36f), new Color(1f, 1f, 1f, 0.75f));
        CreateControlsPanel(safeRoot);
    }

    private void CreateControlsPanel(Transform parent)
    {
        controlsPanel = CreateOverlay(parent, "Controls");
        GameObject card = CreateCard(controlsPanel.transform, "Card", new Vector2(760f, 610f));
        CreateText(card.transform, "Title", "Управление", 44f, new Vector2(0f, 214f), new Vector2(700f, 70f), AccentColor);
        CreateText(
            card.transform,
            "Details",
            "WASD / стрелки — движение\nМышь — обзор и огонь\nSpace — прыжок\nLeft Shift — торможение\nQ — импульс отдачи\nEsc — пауза",
            27f,
            new Vector2(0f, 28f),
            new Vector2(660f, 300f),
            Color.white);
        Button close = DesktopUiFactory.CreateButton("CloseButton", card.transform, "Назад", new Vector2(400f, 70f), ButtonColor, HideControls);
        SetCenter(close.GetComponent<RectTransform>(), new Vector2(0f, -205f), new Vector2(400f, 70f));
        SetButtonNavigation(close, null, null);
        controlsPanel.SetActive(false);
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

    private static void ConfigureNavigation(Button start, Button controls, Button quit)
    {
        SetButtonNavigation(start, null, controls);
        SetButtonNavigation(controls, start, quit);
        SetButtonNavigation(quit, controls, null);
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
