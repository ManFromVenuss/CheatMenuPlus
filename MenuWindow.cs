using BepInEx.Logging;
using Pigeon.Movement;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class MenuMod2Manager
{
    public static List<MenuMod2Menu> allMenus;
    public static MenuMod2Menu currentMenu = null;
}

public class MenuMod2Menu
{
    private const int PanelWidth = 420;
    private const int PanelHeight = 620;
    private const int HeaderHeight = 78;
    private const int FooterHeight = 34;
    private const int ButtonWidth = 390;
    private const int ButtonHeight = 34;
    private const int ButtonSpacing = 2;
    private const int ContentPadding = 15;

    public List<MM2Button> buttons;
    public GameObject menuCanvas;
    public RectTransform contentRoot;
    public string menuName;
    public MenuMod2Menu parrentMenu;
    public MM2Button thisButton = null;
    public List<MenuMod2Menu> subMenus;
    private bool hasMenuCameraLock = false;
    private bool hasMenuRotationLock = false;
    private bool hasMenuInputLock = false;
    private bool hasMenuFireLock = false;
    public MenuMod2Menu(string indetifier, MenuMod2Menu _parrentMenu = null)
    {
        try
        {
            menuName = indetifier;
            if (MenuMod2Manager.allMenus == null)
            {
                MenuMod2Manager.allMenus = new List<MenuMod2Menu>();
            }
            foreach (var menu in MenuMod2Manager.allMenus)
            {
                if (menu.menuName == indetifier)
                {
                    throw new Exception($"Menu with name {indetifier} already exists.");
                }
            }
            MenuMod2Manager.allMenus.Add(this);
            buttons = new List<MM2Button>();
            menuCanvas = new GameObject("menuCanvas");
            GameObject.DontDestroyOnLoad(menuCanvas);

            Canvas canvas = menuCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = menuCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            menuCanvas.AddComponent<GraphicRaycaster>();

            BuildMenuShell();
            menuCanvas.SetActive(false);

            if (_parrentMenu != null)
            {
                parrentMenu = _parrentMenu;
                parrentMenu.subMenus ??= new List<MenuMod2Menu>();
                parrentMenu.subMenus.Add(this);
                thisButton = parrentMenu.addButton(indetifier, () => { this.Open(); });
                this.addButton("Back", () => { parrentMenu.Open(); }).changeColour(MenuStyle.Secondary);
            }
            else if (indetifier == "Main Menu")
            {
                this.addButton("Close", () => { this.Close(); }).changeColour(MenuStyle.Danger);
                parrentMenu = null;
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in MenuMod2Menu constructor: {ex.Message}");
        }
    }
    private void BuildMenuShell()
    {
        GameObject dimmer = new GameObject("MenuBackdrop");
        dimmer.transform.SetParent(menuCanvas.transform, false);
        RectTransform dimmerRect = dimmer.AddComponent<RectTransform>();
        dimmerRect.anchorMin = Vector2.zero;
        dimmerRect.anchorMax = Vector2.one;
        dimmerRect.offsetMin = Vector2.zero;
        dimmerRect.offsetMax = Vector2.zero;
        Image dimmerImage = dimmer.AddComponent<Image>();
        dimmerImage.color = new Color(0f, 0f, 0f, 0f);

        GameObject panel = new GameObject("MenuPanel");
        panel.transform.SetParent(menuCanvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.08f, 0.5f);
        panelRect.anchorMax = new Vector2(0.08f, 0.5f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panelRect.anchoredPosition = Vector2.zero;
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = MenuStyle.Panel;

        GameObject header = new GameObject("Header");
        header.transform.SetParent(panel.transform, false);
        RectTransform headerRect = header.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, HeaderHeight);
        headerRect.anchoredPosition = Vector2.zero;
        Image headerImage = header.AddComponent<Image>();
        headerImage.color = MenuStyle.Header;

        CreateText(header.transform, "Brand", "CHEATMENU+", 24, FontStyle.Bold, MenuStyle.HeaderText, TextAnchor.MiddleCenter,
            new Vector2(0f, -30f), new Vector2(PanelWidth, 34f), new Vector2(0.5f, 1f));
        CreateText(header.transform, "Version", SparrohPlugin.PluginVersion, 12, FontStyle.Bold, MenuStyle.HeaderSubText, TextAnchor.MiddleCenter,
            new Vector2(0f, -58f), new Vector2(PanelWidth, 20f), new Vector2(0.5f, 1f));

        GameObject section = new GameObject("Section");
        section.transform.SetParent(panel.transform, false);
        RectTransform sectionRect = section.AddComponent<RectTransform>();
        sectionRect.anchorMin = new Vector2(0f, 1f);
        sectionRect.anchorMax = new Vector2(1f, 1f);
        sectionRect.pivot = new Vector2(0.5f, 1f);
        sectionRect.sizeDelta = new Vector2(0f, 36f);
        sectionRect.anchoredPosition = new Vector2(0f, -HeaderHeight);
        Image sectionImage = section.AddComponent<Image>();
        sectionImage.color = MenuStyle.Section;
        CreateText(section.transform, "Title", menuName.ToUpperInvariant(), 16, FontStyle.Bold, MenuStyle.Text, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(PanelWidth, 36f), new Vector2(0.5f, 0.5f));

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(panel.transform, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.offsetMin = new Vector2(ContentPadding, FooterHeight + ContentPadding);
        viewportRect.offsetMax = new Vector2(-ContentPadding, -(HeaderHeight + 36f + ContentPadding));
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewport.AddComponent<RectMask2D>();

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        contentRoot = content.AddComponent<RectTransform>();
        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0.5f, 1f);
        contentRoot.anchoredPosition = Vector2.zero;
        contentRoot.sizeDelta = new Vector2(0f, 0f);

        ScrollRect scrollRect = panel.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRoot;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;

        GameObject footer = new GameObject("Footer");
        footer.transform.SetParent(panel.transform, false);
        RectTransform footerRect = footer.AddComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.sizeDelta = new Vector2(0f, FooterHeight);
        footerRect.anchoredPosition = Vector2.zero;
        Image footerImage = footer.AddComponent<Image>();
        footerImage.color = MenuStyle.Footer;
        CreateText(footer.transform, "Hint", "INSERT  Open / Close", 12, FontStyle.Bold, MenuStyle.MutedText, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(PanelWidth, FooterHeight), new Vector2(0.5f, 0.5f));
    }
    private Text CreateText(Transform parent, string objectName, string value, int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
    {
        GameObject textObj = new GameObject(objectName);
        textObj.transform.SetParent(parent, false);
        Text text = textObj.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        return text;
    }
    public void Open()
    {
        try
        {
            SparrohPlugin.Logger.LogDebug($"Opening menu: {menuName ?? "unkown"}");
            if (MenuMod2Manager.currentMenu != null && MenuMod2Manager.currentMenu != this)
            {
                MenuMod2Manager.currentMenu.Close();
            }
            MenuMod2Manager.currentMenu = this;
            menuCanvas.SetActive(true);
            AcquireMenuInputLock();
            UnityEngine.Cursor.visible = true;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            foreach (var button in buttons)
            {
                button.show();
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in Open(): {ex.Message}");
        }
    }
    public void Close()
    {
        try
        {
            if (MenuMod2Manager.currentMenu != this)
            {
                SparrohPlugin.Logger.LogWarning($"Attempted to close menu \"{menuName}\" that wasn't open.  This should not happen");
                return;
            }
            SparrohPlugin.Logger.LogDebug($"Closing menu: {menuName}");
            MenuMod2Manager.currentMenu = null;

            ReleaseMenuInputLock();
            foreach (var button in buttons)
            {
                button.hide();
            }
            menuCanvas.SetActive(false);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in Close(): {ex.Message}");
        }
    }
    private void AcquireMenuCameraLock()
    {
        try
        {
            if (hasMenuCameraLock || Player.LocalPlayer == null || Player.LocalPlayer.PlayerLook == null)
            {
                return;
            }

            Player.LocalPlayer.PlayerLook.EnableMenuCamera += 1;
            hasMenuCameraLock = true;
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in AcquireMenuCameraLock(): {ex.Message}");
        }
    }

    private void AcquireMenuInputLock()
    {
        try
        {
            if (!hasMenuInputLock)
            {
                PlayerInput.EnableMenu();
                hasMenuInputLock = true;
            }

            if (hasMenuRotationLock || Player.LocalPlayer == null || Player.LocalPlayer.PlayerLook == null)
            {
                return;
            }

            Player.LocalPlayer.PlayerLook.RotationLocksX += 1;
            Player.LocalPlayer.PlayerLook.RotationLocksY += 1;
            Player.LocalPlayer.LockFiring(true);
            hasMenuRotationLock = true;
            hasMenuFireLock = true;
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in AcquireMenuInputLock(): {ex.Message}");
        }
    }

    private void ReleaseMenuInputLock()
    {
        try
        {
            if (hasMenuRotationLock && Player.LocalPlayer != null && Player.LocalPlayer.PlayerLook != null)
            {
                Player.LocalPlayer.PlayerLook.RotationLocksX = Math.Max(0, Player.LocalPlayer.PlayerLook.RotationLocksX - 1);
                Player.LocalPlayer.PlayerLook.RotationLocksY = Math.Max(0, Player.LocalPlayer.PlayerLook.RotationLocksY - 1);
            }
            if (hasMenuFireLock && Player.LocalPlayer != null)
            {
                Player.LocalPlayer.LockFiring(false);
            }
            hasMenuRotationLock = false;
            hasMenuFireLock = false;

            if (hasMenuInputLock)
            {
                PlayerInput.DisableMenu();
            }
            hasMenuInputLock = false;
        }
        catch (Exception ex)
        {
            hasMenuInputLock = false;
            hasMenuRotationLock = false;
            hasMenuFireLock = false;
            SparrohPlugin.Logger.LogError($"Exception in ReleaseMenuInputLock(): {ex.Message}");
        }
    }

    private void ReleaseMenuCameraLock()
    {
        try
        {
            if (!hasMenuCameraLock || Player.LocalPlayer == null || Player.LocalPlayer.PlayerLook == null)
            {
                hasMenuCameraLock = false;
                return;
            }

            Player.LocalPlayer.PlayerLook.EnableMenuCamera = Math.Max(0, Player.LocalPlayer.PlayerLook.EnableMenuCamera - 1);
            hasMenuCameraLock = false;

            if (Player.LocalPlayer.PlayerLook.EnableMenuCamera == 0)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in ReleaseMenuCameraLock(): {ex.Message}");
        }
    }
    public MenuMod2Menu hasMenu(string menuName)
    {
        if (this.menuName == menuName)
        {
            return this;
        }
        if (subMenus != null)
        {
            foreach (var subMenu in subMenus)
            {
                if (subMenu.menuName == menuName)
                {
                    return subMenu;
                }
            }
        }
        return null;
    }
    public MM2Button addButtonBackup(string text, UnityAction callback)
    {
        MM2Button button = new MM2Button(this, new Vector2(0, 0), text, callback, contentRoot.gameObject);
        buttons.Add(button);
        arrangeButtons();
        return button;
    }
    public MM2Button addButton(string text, UnityAction callback)
    {
        MM2Button button = new MM2Button(this, new Vector2(0, 0), text, null, contentRoot.gameObject);

        button.createButton();
        button.SetCallback(callback);

        buttons.Add(button);
        arrangeButtons();
        return button;
    }
    public MM2Button addInput(string text, string defaultValue)
    {
        MM2Button button = new MM2Button(this, new Vector2(0, 0), text, null, contentRoot.gameObject);

        button.createInput(defaultValue);

        buttons.Add(button);
        arrangeButtons();
        return button;
    }
    public void destroy()
    {
        try
        {
            var tempMenus = new List<MenuMod2Menu>(MenuMod2Manager.allMenus);
            foreach (var menu in tempMenus)
            {
                if (menu.menuName == this.menuName)
                {
                    MenuMod2Manager.allMenus.Remove(menu);
                }
            }
            if (MenuMod2Manager.currentMenu == this)
            {
                var backButton = this.buttons.FirstOrDefault(b => b.name == "back");
                if (backButton == null)
                {
                    this.Close();
                }
                else
                {
                    backButton.button.onClick.Invoke();
                }
            }
            var tempParrentButtons = new List<MM2Button>(parrentMenu?.buttons ?? new List<MM2Button>());
            foreach (var button in tempParrentButtons)
            {
                if (button.name == this.thisButton.name)
                {
                    parrentMenu.buttons.Remove(button);
                }
            }
            var buttonsToRemove = new List<MM2Button>(buttons);
            foreach (var button in buttonsToRemove)
            {
                GameObject.Destroy(button.buttonObj);
                buttons.Remove(button);
            }
            var subMenusToDestroy = subMenus ?? new List<MenuMod2Menu>();
            foreach (var subMenu in subMenusToDestroy)
            {
                subMenu.destroy();
            }

            GameObject.Destroy(menuCanvas);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in destroy(): {ex.Message}");
        }
    }
    public bool removeButton(string buttonName)
    {
        try
        {
            var buttonToRemove = buttons.FirstOrDefault(b => b.name == buttonName);
            if (buttonToRemove != null)
            {
                buttons.Remove(buttonToRemove);
                GameObject.Destroy(buttonToRemove.buttonObj);
                arrangeButtons();
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in removeButton(): {ex.Message}");
            return false;
        }
    }
    public void arrangeButtons()
    {
        try
        {
            if (contentRoot == null) return;

            int columns = 1;
            float rowHeight = ButtonHeight + ButtonSpacing;
            float totalWidth = (columns * ButtonWidth) + ((columns - 1) * ButtonSpacing);
            int rows = Mathf.CeilToInt(buttons.Count / (float)columns);
            float contentHeight = Mathf.Max(0, rows * rowHeight - ButtonSpacing);
            contentRoot.sizeDelta = new Vector2(0f, contentHeight);

            for (int i = 0; i < buttons.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                float x = (-totalWidth / 2f) + (ButtonWidth / 2f) + column * (ButtonWidth + ButtonSpacing);
                float y = -(ButtonHeight / 2f) - row * rowHeight;
                buttons[i].move(new Vector2(x, y));
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in arrangeButtons(): {ex.Message}");
        }
    }
}
public static class MenuStyle
{
    public static readonly Color Panel = new Color(0.015f, 0.015f, 0.018f, 0.86f);
    public static readonly Color Header = new Color(0.12f, 0.44f, 0.95f, 0.98f);
    public static readonly Color Section = new Color(0.02f, 0.02f, 0.024f, 0.92f);
    public static readonly Color Footer = new Color(0.02f, 0.02f, 0.024f, 0.94f);
    public static readonly Color Button = new Color(0.03f, 0.03f, 0.036f, 0.76f);
    public static readonly Color ButtonHover = new Color(0.12f, 0.44f, 0.95f, 0.92f);
    public static readonly Color Accent = new Color(0.12f, 0.44f, 0.95f, 1f);
    public static readonly Color Secondary = new Color(0.08f, 0.08f, 0.09f, 0.92f);
    public static readonly Color Success = new Color(0.05f, 0.38f, 0.18f, 0.92f);
    public static readonly Color Danger = new Color(0.50f, 0.08f, 0.08f, 0.92f);
    public static readonly Color HeaderText = new Color(1f, 1f, 1f, 1f);
    public static readonly Color HeaderSubText = new Color(0.82f, 0.90f, 1f, 1f);
    public static readonly Color Text = new Color(0.94f, 0.96f, 0.96f, 1f);
    public static readonly Color MutedText = new Color(0.62f, 0.66f, 0.70f, 1f);
}
public class MM2Button
{
    public GameObject buttonObj;
    public MenuMod2Menu menu;
    public string name;
    public string prefix;
    public string suffix;
    public GameObject canvas;
    public Vector2 pos;
    public Button button;
    public InputField inputField;
    public MM2Button(MenuMod2Menu _menu, Vector2 screenPos, string text, UnityAction callback, GameObject menuCanvas)
    {
        menu = _menu;
        name = text;
        pos = screenPos;
        canvas = menuCanvas;
        prefix = string.Empty;
        suffix = string.Empty;
    }
    public MM2Button createButton()
    {
        buttonObj = new GameObject("MenuButton");
        buttonObj.transform.SetParent(canvas.transform, false);
        RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(390, 34);
        rectTransform.anchoredPosition = pos;

        Image image = buttonObj.AddComponent<Image>();
        image.color = MenuStyle.Button;
        button = buttonObj.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = MenuStyle.Button;
        colors.highlightedColor = MenuStyle.ButtonHover;
        colors.pressedColor = MenuStyle.Accent;
        colors.selectedColor = MenuStyle.ButtonHover;
        colors.disabledColor = new Color(0.10f, 0.11f, 0.12f, 0.65f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        GameObject textObj = new GameObject("ButtonText");
        textObj.transform.SetParent(buttonObj.transform, false);
        Text buttonText = textObj.AddComponent<Text>();
        buttonText.text = prefix + name + suffix;
        buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        buttonText.fontSize = 15;
        buttonText.color = MenuStyle.Text;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.horizontalOverflow = HorizontalWrapMode.Wrap;
        buttonText.verticalOverflow = VerticalWrapMode.Truncate;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 0f);
        textRect.offsetMax = new Vector2(-18f, 0f);
        hide();
        return this;
    }
    public MM2Button createInput(string defaultValue)
    {
        buttonObj = new GameObject("MenuInput");
        buttonObj.transform.SetParent(canvas.transform, false);
        RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(390, 34);
        rectTransform.anchoredPosition = pos;

        Image image = buttonObj.AddComponent<Image>();
        image.color = MenuStyle.Button;

        GameObject labelObj = new GameObject("InputLabel");
        labelObj.transform.SetParent(buttonObj.transform, false);
        Text labelText = labelObj.AddComponent<Text>();
        labelText.text = name;
        labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        labelText.fontSize = 14;
        labelText.color = MenuStyle.MutedText;
        labelText.alignment = TextAnchor.MiddleLeft;
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0.48f, 1f);
        labelRect.offsetMin = new Vector2(18f, 0f);
        labelRect.offsetMax = new Vector2(-6f, 0f);

        GameObject inputObj = new GameObject("InputField");
        inputObj.transform.SetParent(buttonObj.transform, false);
        RectTransform inputRect = inputObj.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.50f, 0.16f);
        inputRect.anchorMax = new Vector2(1f, 0.84f);
        inputRect.offsetMin = new Vector2(4f, 0f);
        inputRect.offsetMax = new Vector2(-16f, 0f);
        Image inputImage = inputObj.AddComponent<Image>();
        inputImage.color = new Color(0.015f, 0.015f, 0.018f, 0.95f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(inputObj.transform, false);
        Text inputText = textObj.AddComponent<Text>();
        inputText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        inputText.fontSize = 14;
        inputText.color = MenuStyle.Text;
        inputText.alignment = TextAnchor.MiddleCenter;
        inputText.horizontalOverflow = HorizontalWrapMode.Wrap;
        inputText.verticalOverflow = VerticalWrapMode.Truncate;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);

        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(inputObj.transform, false);
        Text placeholderText = placeholderObj.AddComponent<Text>();
        placeholderText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        placeholderText.fontSize = 14;
        placeholderText.fontStyle = FontStyle.Italic;
        placeholderText.color = MenuStyle.MutedText;
        placeholderText.alignment = TextAnchor.MiddleCenter;
        placeholderText.text = defaultValue;
        RectTransform placeholderRect = placeholderObj.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(8f, 0f);
        placeholderRect.offsetMax = new Vector2(-8f, 0f);

        inputField = inputObj.AddComponent<InputField>();
        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;
        inputField.contentType = InputField.ContentType.DecimalNumber;
        inputField.text = defaultValue;
        inputField.caretColor = MenuStyle.Text;
        inputField.selectionColor = MenuStyle.ButtonHover;

        hide();
        return this;
    }
    public MM2Button hide()
    {
        buttonObj.SetActive(false);
        return this;
    }
    public MM2Button show()
    {
        buttonObj.SetActive(true);
        return this;
    }
    public MM2Button updateText()
    {
        if (inputField != null)
        {
            return this;
        }

        Text buttonText = buttonObj.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.text = prefix + name + suffix;
        }
        else
        {
            SparrohPlugin.Logger.LogWarning("Button text component not found, cannot update text.");
        }
        return this;
    }
    public MM2Button changeColour(Color newColor)
    {
        Image buttonImage = buttonObj.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = newColor;
            if (button != null)
            {
                ColorBlock colors = button.colors;
                Color rowColor = Color.Lerp(MenuStyle.Button, newColor, 0.22f);
                buttonImage.color = rowColor;
                colors.normalColor = rowColor;
                colors.highlightedColor = Color.Lerp(rowColor, Color.white, 0.10f);
                colors.pressedColor = MenuStyle.Accent;
                colors.selectedColor = colors.highlightedColor;
                button.colors = colors;
            }
        }
        else
        {
            SparrohPlugin.Logger.LogWarning("Button image component not found, cannot change color.");
        }
        return this;
    }
    public MM2Button changeName(string newName)
    {
        name = newName;
        updateText();
        return this;
    }
    public MM2Button changePrefix(string newPrefix)
    {
        prefix = newPrefix;
        updateText();
        return this;
    }
    public MM2Button changeSuffix(string newSuffix)
    {
        suffix = newSuffix;
        updateText();
        return this;
    }
    public MM2Button move(Vector2 newPos)
    {
        RectTransform rectTransform = buttonObj.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = newPos;
        return this;
    }
    public void SetCallback(UnityAction callback)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(callback);
    }
    public string getInputText()
    {
        return inputField != null ? inputField.text : string.Empty;
    }
}
