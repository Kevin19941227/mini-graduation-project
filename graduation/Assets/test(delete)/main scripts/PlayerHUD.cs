using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    private const float ChargeFadeSpeed = 10f;
    private const int RuntimeSortingOrder = 10;
    private const int BackpackPanelWidth = 520;
    private const int BackpackPanelHeight = 420;
    private const int BackpackTitleFontSize = 32;
    private const int BackpackHintFontSize = 18;
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    [Header("HP")]
    public Image hpFillImage;
    public Image delayedHpFillImage;

    [Header("Charge")]
    public Image chargeFillImage;
    public CanvasGroup chargeGroup;

    [Header("Game Over")]
    public GameObject gameOverPanel;

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

    /// <summary>Initializes the local player HUD and makes sure the canvas is visible.</summary>
    public void Init()
    {
        if (_canvas != null)
            _canvas.enabled = true;

        EnsureBackpackPanel();
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
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
    }

    /// <summary>Toggles the local backpack panel visibility.</summary>
    public void ToggleBackpack()
    {
        EnsureBackpackPanel();

        if (backpackPanel != null)
            backpackPanel.SetActive(!backpackPanel.activeSelf);
    }

    /// <summary>Sets the local backpack panel visibility.</summary>
    public void SetBackpackVisible(bool isVisible)
    {
        EnsureBackpackPanel();

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

    #region Backpack UI

    private void EnsureBackpackPanel()
    {
        if (backpackPanel != null) return;

        RectTransform parent = transform as RectTransform;
        if (parent == null) return;

        GameObject panel = CreateRect(parent, "Backpack_Panel",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(BackpackPanelWidth, BackpackPanelHeight));

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.04f, 0.05f, 0.06f, 0.92f);
        panelImage.raycastTarget = true;

        CreateText(panel.transform, "Backpack_Title", "Backpack",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -28f),
            new Vector2(0f, 48f),
            BackpackTitleFontSize,
            TextAnchor.MiddleCenter,
            Color.white);

        CreateText(panel.transform, "Backpack_Empty_Text", "No backpack UI is assigned yet.",
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-48f, -120f),
            BackpackHintFontSize,
            TextAnchor.MiddleCenter,
            new Color(0.78f, 0.84f, 0.9f, 1f));

        backpackPanel = panel;
        backpackPanel.SetActive(false);
    }

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

    private static void CreateText(Transform parent, string objectName, string content, Vector2 anchorMin,
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
    }

    #endregion
}
