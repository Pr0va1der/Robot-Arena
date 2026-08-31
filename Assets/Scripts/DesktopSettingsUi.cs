using System;
using RobotArena.Session;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ArenaPlayerSettings = RobotArena.Session.PlayerSettings;

public sealed class DesktopSettingsUi : MonoBehaviour
{
    private static readonly Color CardColor = new Color(0.08f, 0.12f, 0.20f, 0.98f);
    private static readonly Color ButtonColor = new Color(0.12f, 0.33f, 0.52f, 1f);
    private static readonly Color SelectedButtonColor = new Color(0.18f, 0.57f, 0.68f, 1f);
    private static readonly Color AccentColor = new Color(0.30f, 0.82f, 0.95f, 1f);
    private static readonly Color SliderBackgroundColor = new Color(0.02f, 0.04f, 0.08f, 0.9f);
    private static readonly Color SliderFillColor = new Color(0.30f, 0.82f, 0.95f, 1f);

    private GameSettingsRuntime settingsRuntime;
    private Action closeAction;
    private bool isInitialized;

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI languageText;
    private TextMeshProUGUI graphicsText;
    private TextMeshProUGUI masterVolumeText;
    private TextMeshProUGUI musicVolumeText;
    private TextMeshProUGUI sfxVolumeText;
    private Slider masterVolumeSlider;
    private Slider musicVolumeSlider;
    private Slider sfxVolumeSlider;
    private Button russianButton;
    private Button englishButton;
    private Button muteButton;
    private Button performanceButton;
    private Button qualityButton;
    private Button closeButton;

    public bool IsVisible => gameObject.activeSelf;

    public void Initialize(GameSettingsRuntime runtime, Action onClose)
    {
        if (isInitialized)
        {
            return;
        }

        settingsRuntime = runtime ?? GameSettingsRuntime.GetOrCreate();
        closeAction = onClose;
        settingsRuntime.SettingsChanged += OnSettingsChanged;
        isInitialized = true;
        CreateUi();
        Refresh();
    }

    public void Show()
    {
        if (!isInitialized)
        {
            return;
        }

        gameObject.SetActive(true);
        Refresh();
        SelectButton(masterVolumeSlider);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Refresh()
    {
        if (!isInitialized || settingsRuntime == null || masterVolumeSlider == null)
        {
            return;
        }

        ArenaPlayerSettings settings = settingsRuntime.Current;

        titleText.text = Localize(LocalizationKey.SettingsTitle);
        languageText.text = Localize(LocalizationKey.Language);
        graphicsText.text = Localize(LocalizationKey.GraphicsProfile);
        masterVolumeText.text = Localize(LocalizationKey.MasterVolume);
        musicVolumeText.text = Localize(LocalizationKey.MusicVolume);
        sfxVolumeText.text = Localize(LocalizationKey.SfxVolume);

        masterVolumeSlider.SetValueWithoutNotify(settings.MasterVolume);
        musicVolumeSlider.SetValueWithoutNotify(settings.MusicVolume);
        sfxVolumeSlider.SetValueWithoutNotify(settings.SfxVolume);

        DesktopUiFactory.SetButtonLabel(russianButton, Localize(LocalizationKey.Russian));
        DesktopUiFactory.SetButtonLabel(englishButton, Localize(LocalizationKey.English));
        DesktopUiFactory.SetButtonLabel(
            muteButton,
            string.Format(
                Localize(LocalizationKey.Mute),
                Localize(settings.IsMuted ? LocalizationKey.On : LocalizationKey.Off)));
        DesktopUiFactory.SetButtonLabel(performanceButton, Localize(LocalizationKey.Performance));
        DesktopUiFactory.SetButtonLabel(qualityButton, Localize(LocalizationKey.Quality));
        DesktopUiFactory.SetButtonLabel(closeButton, Localize(LocalizationKey.Back));

        SetButtonColor(russianButton, settings.Language == GameLanguage.Russian);
        SetButtonColor(englishButton, settings.Language == GameLanguage.English);
        SetButtonColor(
            performanceButton,
            settings.GraphicsProfile == GraphicsQualityProfile.Performance);
        SetButtonColor(qualityButton, settings.GraphicsProfile == GraphicsQualityProfile.Quality);
    }

    private void OnDestroy()
    {
        if (settingsRuntime != null)
        {
            settingsRuntime.SettingsChanged -= OnSettingsChanged;
        }
    }

    private void OnSettingsChanged(ArenaPlayerSettings settings)
    {
        Refresh();
    }

    private void SetRussian()
    {
        settingsRuntime.SetLanguage(GameLanguage.Russian);
    }

    private void SetEnglish()
    {
        settingsRuntime.SetLanguage(GameLanguage.English);
    }

    private void ToggleMute()
    {
        settingsRuntime.SetMuted(!settingsRuntime.Current.IsMuted);
    }

    private void SetPerformanceProfile()
    {
        settingsRuntime.SetGraphicsProfile(GraphicsQualityProfile.Performance);
    }

    private void SetQualityProfile()
    {
        settingsRuntime.SetGraphicsProfile(GraphicsQualityProfile.Quality);
    }

    private void Close()
    {
        closeAction?.Invoke();
    }

    private void CreateUi()
    {
        GameObject card = DesktopUiFactory.CreatePanel("Card", transform, CardColor, true);
        SetCenter(card.GetComponent<RectTransform>(), Vector2.zero, new Vector2(860f, 760f));

        titleText = CreateText(
            card.transform,
            "Title",
            string.Empty,
            46f,
            new Vector2(0f, 300f),
            new Vector2(780f, 70f),
            AccentColor);

        CreateAudioRow(
            card.transform,
            "MasterVolume",
            out masterVolumeText,
            out masterVolumeSlider,
            205f,
            settingsRuntime.SetMasterVolume);
        CreateAudioRow(
            card.transform,
            "MusicVolume",
            out musicVolumeText,
            out musicVolumeSlider,
            145f,
            settingsRuntime.SetMusicVolume);
        CreateAudioRow(
            card.transform,
            "SfxVolume",
            out sfxVolumeText,
            out sfxVolumeSlider,
            85f,
            settingsRuntime.SetSfxVolume);

        muteButton = DesktopUiFactory.CreateButton(
            "MuteButton",
            card.transform,
            string.Empty,
            new Vector2(360f, 54f),
            ButtonColor,
            ToggleMute);
        SetCenter(muteButton.GetComponent<RectTransform>(), new Vector2(0f, 22f), new Vector2(360f, 54f));

        languageText = CreateText(
            card.transform,
            "LanguageLabel",
            string.Empty,
            24f,
            new Vector2(-245f, -46f),
            new Vector2(220f, 42f),
            Color.white,
            TextAlignmentOptions.Left);
        russianButton = DesktopUiFactory.CreateButton(
            "RussianButton",
            card.transform,
            string.Empty,
            new Vector2(210f, 54f),
            ButtonColor,
            SetRussian);
        SetCenter(russianButton.GetComponent<RectTransform>(), new Vector2(-105f, -96f), new Vector2(210f, 54f));
        englishButton = DesktopUiFactory.CreateButton(
            "EnglishButton",
            card.transform,
            string.Empty,
            new Vector2(210f, 54f),
            ButtonColor,
            SetEnglish);
        SetCenter(englishButton.GetComponent<RectTransform>(), new Vector2(105f, -96f), new Vector2(210f, 54f));

        graphicsText = CreateText(
            card.transform,
            "GraphicsLabel",
            string.Empty,
            24f,
            new Vector2(-245f, -166f),
            new Vector2(300f, 42f),
            Color.white,
            TextAlignmentOptions.Left);
        performanceButton = DesktopUiFactory.CreateButton(
            "PerformanceButton",
            card.transform,
            string.Empty,
            new Vector2(250f, 54f),
            ButtonColor,
            SetPerformanceProfile);
        SetCenter(performanceButton.GetComponent<RectTransform>(), new Vector2(-130f, -216f), new Vector2(250f, 54f));
        qualityButton = DesktopUiFactory.CreateButton(
            "QualityButton",
            card.transform,
            string.Empty,
            new Vector2(250f, 54f),
            ButtonColor,
            SetQualityProfile);
        SetCenter(qualityButton.GetComponent<RectTransform>(), new Vector2(130f, -216f), new Vector2(250f, 54f));

        closeButton = DesktopUiFactory.CreateButton(
            "CloseButton",
            card.transform,
            string.Empty,
            new Vector2(360f, 58f),
            ButtonColor,
            Close);
        SetCenter(closeButton.GetComponent<RectTransform>(), new Vector2(0f, -310f), new Vector2(360f, 58f));
        ConfigureNavigation();
    }

    private void CreateAudioRow(
        Transform parent,
        string name,
        out TextMeshProUGUI label,
        out Slider slider,
        float y,
        Action<float> onValueChanged)
    {
        label = CreateText(
            parent,
            name + "Label",
            string.Empty,
            22f,
            new Vector2(-245f, y),
            new Vector2(300f, 38f),
            Color.white,
            TextAlignmentOptions.Left);
        slider = DesktopUiFactory.CreateSlider(
            name + "Slider",
            parent,
            SliderBackgroundColor,
            SliderFillColor,
            AccentColor,
            onValueChanged);
        SetCenter(slider.GetComponent<RectTransform>(), new Vector2(145f, y), new Vector2(430f, 32f));
    }

    private void ConfigureNavigation()
    {
        SetSelectableNavigation(masterVolumeSlider, null, musicVolumeSlider);
        SetSelectableNavigation(musicVolumeSlider, masterVolumeSlider, sfxVolumeSlider);
        SetSelectableNavigation(sfxVolumeSlider, musicVolumeSlider, russianButton);
        SetButtonNavigation(russianButton, null, englishButton);
        SetButtonNavigation(englishButton, russianButton, muteButton);
        SetButtonNavigation(muteButton, englishButton, performanceButton);
        SetButtonNavigation(performanceButton, muteButton, qualityButton);
        SetButtonNavigation(qualityButton, performanceButton, closeButton);
        SetButtonNavigation(closeButton, qualityButton, null);
    }

    private string Localize(LocalizationKey key)
    {
        return DesktopLocalization.Get(settingsRuntime, key);
    }

    private static void SetButtonColor(Button button, bool selected)
    {
        Color color = selected ? SelectedButtonColor : ButtonColor;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(color.r, color.g, color.b, 0.45f);
        button.colors = colors;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string value,
        float fontSize,
        Vector2 position,
        Vector2 size,
        Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        TextMeshProUGUI text = DesktopUiFactory.CreateText(name, parent, value, fontSize, color, alignment);
        SetCenter(text.rectTransform, position, size);
        return text;
    }

    private static void SetCenter(RectTransform rectTransform, Vector2 position, Vector2 size)
    {
        DesktopUiFactory.SetCenter(rectTransform, position, size);
    }

    private static void SetButtonNavigation(Button button, Selectable up, Selectable down)
    {
        SetSelectableNavigation(button, up, down);
    }

    private static void SetSelectableNavigation(Selectable selectable, Selectable up, Selectable down)
    {
        Navigation navigation = selectable.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnUp = up;
        navigation.selectOnDown = down;
        selectable.navigation = navigation;
    }

    private static void SelectButton(Selectable selectable)
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(selectable == null ? null : selectable.gameObject);
        }
    }
}
