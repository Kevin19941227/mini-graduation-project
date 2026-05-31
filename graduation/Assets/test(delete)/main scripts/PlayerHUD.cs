using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    private const float ChargeFadeSpeed = 10f;
    private const int RuntimeSortingOrder = 10;
    private const int BackpackPanelWidth = 520;
    private const int BackpackPanelHeight = 420;
    private const int BackpackTitleFontSize = 32;
    private const int BackpackHintFontSize = 18;
    private const int ResultTitleFontSize = 72;
    private const int ResultButtonFontSize = 28;
    private const int ResultButtonWidth = 260;
    private const int ResultButtonHeight = 64;
    private const int ResultButtonRowWidth = 280;
    private const int ResultButtonRowHeight = 72;
    private const string HudLogPrefix = "[PlayerHUD]";
    private const string ReturnLobbyButtonText = "返回大廳";
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    [Header("HP")]
    public Image hpFillImage;
    public Image delayedHpFillImage;

    [Header("Charge")]
    public Image chargeFillImage;
    public CanvasGroup chargeGroup;

    [Header("Game Over")]
    public GameObject gameOverPanel;

    private Text resultText;
    private Button returnLobbyButton;

    [Header("Backpack")]
    public GameObject backpackPanel;

    private Canvas _canvas;

    #region Unity Lifecycle

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        ResetHud();
    }

    #endregion

    #region Public API

    /// <summary>Finds the local runtime HUD, or creates one when the scene has no PlayerHUD component.</summary>
    public static PlayerHUD GetOrCreateRuntimeHud()
    {
        PlayerHUD existingHud = FindObjectOfType<PlayerHUD>();
        if (existingHud != null)
        {
            existingHud.Init();
            return existingHud;
        }

        GameObject hudObject = new GameObject("Runtime_PlayerHUDCanvas");
        Canvas canvas = hudObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = RuntimeSortingOrder;

        CanvasScaler scaler = hudObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;

        hudObject.AddComponent<GraphicRaycaster>();

        PlayerHUD runtimeHud = hudObject.AddComponent<PlayerHUD>();
        runtimeHud.Init();
        Debug.Log($"{HudLogPrefix} Created runtime HUD fallback.");
        return runtimeHud;
    }

    /// <summary>Initializes the local player HUD and makes sure the canvas is visible.</summary>
    public void Init()
    {
        if (_canvas == null)
            _canvas = GetComponent<Canvas>();

        if (_canvas != null)
        {
            _canvas.enabled = true;
            _canvas.sortingOrder = Mathf.Max(_canvas.sortingOrder, RuntimeSortingOrder);
        }

        ResetHud();
    }

    /// <summary>Updates the screen-space HP bar for the local player.</summary>
    public void UpdateHP(int current, int max)
    {
        float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

        if (hpFillImage != null)
            hpFillImage.fillAmount = ratio;

        if (delayedHpFillImage != null)
            delayedHpFillImage.fillAmount = ratio;
    }

    /// <summary>Updates the charge attack bar visibility and fill amount.</summary>
    public void UpdateCharge(float ratio, bool isCharging)
    {
        if (chargeGroup != null)
            chargeGroup.alpha = Mathf.Lerp(chargeGroup.alpha, isCharging ? 1f : 0f, Time.deltaTime * ChargeFadeSpeed);

        if (chargeFillImage != null)
            chargeFillImage.fillAmount = Mathf.Clamp01(ratio);
    }

    /// <summary>Shows the local player game over panel.</summary>
    public void ShowGameOver()
    {
        ShowResult(false);
    }

    /// <summary>Shows the local player victory panel.</summary>
    public void ShowVictory()
    {
        ShowResult(true);
    }

    /// <summary>Shows the local player match result.</summary>
    public void ShowResult(bool isWinner)
    {
        ShowResult(isWinner, null, false);
    }

    /// <summary>Shows the local player match result with optional match control buttons.</summary>
    public void ShowResult(bool isWinner, UnityAction onReturnLobbyClicked, bool controlsInteractable)
    {
        EnsureResultPanel();
        Debug.Log($"{HudLogPrefix} ShowResult isWinner={isWinner}.");

        if (resultText != null)
        {
            resultText.text = isWinner ? "Victory" : "Game Over";
            resultText.color = isWinner
                ? new Color(0.45f, 1f, 0.65f, 1f)
                : new Color(1f, 0.42f, 0.42f, 1f);
        }

        ConfigureResultButtons(onReturnLobbyClicked, controlsInteractable);
        UnlockCursorForResult();

        if (gameOverPanel != null)
        {
            gameOverPanel.transform.SetAsLastSibling();
            gameOverPanel.SetActive(true);
        }
    }

    /// <summary>Toggles the local backpack panel visibility.</summary>
    public void ToggleBackpack()
    {
        if (backpackPanel != null)
            backpackPanel.SetActive(!backpackPanel.activeSelf);
    }

    /// <summary>Sets the local backpack panel visibility.</summary>
    public void SetBackpackVisible(bool isVisible)
    {
        if (backpackPanel != null)
            backpackPanel.SetActive(isVisible);
    }

    #endregion

    #region HUD State

    private void ResetHud()
    {
        UpdateHP(1, 1);

        if (chargeFillImage != null)
            chargeFillImage.fillAmount = 0f;

        if (chargeGroup != null)
            chargeGroup.alpha = 0f;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (backpackPanel != null)
            backpackPanel.SetActive(false);
    }

    #endregion

    #region Result UI

    private void EnsureResultPanel()
    {
        if (gameOverPanel != null)
        {
            if (resultText == null)
                resultText = gameOverPanel.GetComponentInChildren<Text>(true);

            if (resultText == null)
                resultText = CreateText(gameOverPanel.transform, "Result_Text", "Game Over",
                    new Vector2(0f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    Vector2.zero,
                    ResultTitleFontSize,
                    TextAnchor.MiddleCenter,
                    Color.white);

            EnsureResultButtons(gameOverPanel.transform);
            return;
        }

        RectTransform parent = transform as RectTransform;
        if (parent == null) return;

        GameObject panel = CreateRect(parent, "Result_Panel",
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);
        panelImage.raycastTarget = true;

        resultText = CreateText(panel.transform, "Result_Text", "Game Over",
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            ResultTitleFontSize,
            TextAnchor.MiddleCenter,
            Color.white);

        gameOverPanel = panel;
        EnsureResultButtons(gameOverPanel.transform);
        gameOverPanel.SetActive(false);
    }

    private void EnsureResultButtons(Transform panelTransform)
    {
        if (panelTransform == null) return;
        if (returnLobbyButton != null) return;

        Transform existingRow = panelTransform.Find("Result_Button_Row");
        GameObject row = existingRow != null
            ? existingRow.gameObject
            : CreateRect(panelTransform, "Result_Button_Row",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -120f),
                new Vector2(ResultButtonRowWidth, ResultButtonRowHeight));

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = row.AddComponent<HorizontalLayoutGroup>();

        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Transform oldRematchButton = row.transform.Find("Rematch_Button");
        if (oldRematchButton != null)
            oldRematchButton.gameObject.SetActive(false);

        returnLobbyButton = row.transform.Find("Return_Lobby_Button")?.GetComponent<Button>();
        if (returnLobbyButton == null)
            returnLobbyButton = CreateButton(row.transform, "Return_Lobby_Button", ReturnLobbyButtonText, new Color(0.92f, 0.92f, 0.92f, 1f));
    }

    private void ConfigureResultButtons(UnityAction onReturnLobbyClicked, bool controlsInteractable)
    {
        if (returnLobbyButton == null) return;

        bool shouldShowButton = onReturnLobbyClicked != null;
        returnLobbyButton.gameObject.SetActive(shouldShowButton);
        BindButton(returnLobbyButton, onReturnLobbyClicked, controlsInteractable);
    }

    private static void BindButton(Button button, UnityAction callback, bool controlsInteractable)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.interactable = controlsInteractable && callback != null;

        if (callback != null)
            button.onClick.AddListener(callback);
    }

    private static void UnlockCursorForResult()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    #endregion

    #region Backpack UI

    private static GameObject CreateRect(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject rectObject = new GameObject(objectName);
        rectObject.transform.SetParent(parent, false);

        RectTransform rectTransform = rectObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;

        return rectObject;
    }

    private static Button CreateButton(Transform parent, string objectName, string label, Color imageColor)
    {
        GameObject buttonObject = CreateRect(parent, objectName,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(ResultButtonWidth, ResultButtonHeight));

        Image image = buttonObject.AddComponent<Image>();
        image.color = imageColor;
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();

        CreateText(buttonObject.transform, "Label", label,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            ResultButtonFontSize,
            TextAnchor.MiddleCenter,
            Color.black);

        return button;
    }

    private static Text CreateText(Transform parent, string objectName, string content, Vector2 anchorMin,
        Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, int fontSize,
        TextAnchor alignment, Color color)
    {
        GameObject textObject = CreateRect(parent, objectName, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        Text text = textObject.AddComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return text;
    }

    #endregion
}