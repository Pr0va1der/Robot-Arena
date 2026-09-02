using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class DesktopUiFactory
{
    public static GameObject CreatePanel(
        string name,
        Transform parent,
        Color color,
        bool blocksRaycasts)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = blocksRaycasts;
        return panel;
    }

    public static GameObject CreateOverlay(
        string name,
        Transform parent,
        Color color)
    {
        GameObject panel = CreateFullScreenRoot(name, parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    public static GameObject CreateCard(
        string name,
        Transform parent,
        Color color,
        Vector2 size)
    {
        GameObject card = CreatePanel(name, parent, color, true);
        SetCenter(card.GetComponent<RectTransform>(), Vector2.zero, size);
        return card;
    }

    public static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string value,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = RobotArenaUiFont.Get();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    public static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 size,
        Color color,
        Action onClick)
    {
        GameObject buttonObject = CreatePanel(name, parent, color, true);
        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = size;

        Button button = buttonObject.AddComponent<Button>();
        Image image = buttonObject.GetComponent<Image>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(color.r, color.g, color.b, 0.45f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TextMeshProUGUI text = CreateText(
            "Label",
            buttonObject.transform,
            label,
            28f,
            Color.white);
        Stretch(text.rectTransform, 18f, 8f, 18f, 8f);

        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }

        return button;
    }

    public static TextMeshProUGUI CreatePositionedText(
        string name,
        Transform parent,
        string value,
        float fontSize,
        Color color,
        Vector2 position,
        Vector2 size,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        TextMeshProUGUI text = CreateText(name, parent, value, fontSize, color, alignment);
        SetCenter(text.rectTransform, position, size);
        return text;
    }

    public static void BindPointerDown(Button button, Action action)
    {
        if (button == null)
        {
            return;
        }

        DesktopPointerDownHandler handler = button.GetComponent<DesktopPointerDownHandler>();
        if (handler == null)
        {
            handler = button.gameObject.AddComponent<DesktopPointerDownHandler>();
        }

        handler.Bind(action);
    }

    public static TextMeshProUGUI GetButtonLabel(Button button)
    {
        return button == null
            ? null
            : button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
    }

    public static void SetButtonLabel(Button button, string label)
    {
        TextMeshProUGUI text = GetButtonLabel(button);
        if (text != null)
        {
            text.text = label;
        }
    }

    public static Slider CreateSlider(
        string name,
        Transform parent,
        Color backgroundColor,
        Color fillColor,
        Color handleColor,
        Action<float> onValueChanged)
    {
        GameObject sliderObject = new GameObject(name, typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.direction = Slider.Direction.LeftToRight;

        GameObject background = CreatePanel("Background", sliderObject.transform, backgroundColor, true);
        Stretch(background.GetComponent<RectTransform>(), 0f, 5f, 0f, 5f);

        GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        Stretch(fillArea.GetComponent<RectTransform>(), 10f, 0f, 10f, 0f);

        GameObject fill = CreatePanel("Fill", fillArea.transform, fillColor, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject handle = CreatePanel("Handle", sliderObject.transform, handleColor, true);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(24f, 34f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();

        if (onValueChanged != null)
        {
            slider.onValueChanged.AddListener(value => onValueChanged(value));
        }

        return slider;
    }

    public static GameObject CreateFullScreenRoot(
        string name,
        Transform parent,
        bool safeArea)
    {
        GameObject root = CreatePanel(name, parent, Color.clear, false);
        RectTransform rectTransform = root.GetComponent<RectTransform>();
        Stretch(rectTransform);

        if (safeArea)
        {
            DesktopSafeArea.Ensure(rectTransform);
        }

        return root;
    }

    public static void Stretch(
        RectTransform rectTransform,
        float left = 0f,
        float bottom = 0f,
        float right = 0f,
        float top = 0f)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, -top);
    }

    public static void SetCenter(RectTransform rectTransform, Vector2 position, Vector2 size)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;
    }

    public static void SetAnchor(
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

    public static void ConfigureVerticalNavigation(params Button[] buttons)
    {
        if (buttons == null)
        {
            return;
        }

        for (int index = 0; index < buttons.Length; index++)
        {
            Button button = buttons[index];
            if (button == null)
            {
                continue;
            }

            SetNavigation(
                button,
                index > 0 ? buttons[index - 1] : null,
                index + 1 < buttons.Length ? buttons[index + 1] : null);
        }
    }

    public static void SetNavigation(
        Selectable selectable,
        Selectable up,
        Selectable down)
    {
        if (selectable == null)
        {
            return;
        }

        Navigation navigation = selectable.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnUp = up;
        navigation.selectOnDown = down;
        selectable.navigation = navigation;
    }

    public static void Select(Selectable selectable)
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(
                selectable == null ? null : selectable.gameObject);
        }
    }

    public static EventSystem EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            if (EventSystem.current.GetComponent<StandaloneInputModule>() == null)
            {
                EventSystem.current.gameObject.AddComponent<StandaloneInputModule>();
            }

            return EventSystem.current;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
        return eventSystem;
    }
}

internal sealed class DesktopPointerDownHandler : MonoBehaviour, IPointerDownHandler
{
    private Action action;

    public void Bind(Action callback)
    {
        action = callback;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        action?.Invoke();
    }
}
