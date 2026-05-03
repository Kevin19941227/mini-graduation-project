using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BackpackSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    #region References

    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text countText;

    #endregion

    #region Runtime Data

    private StoredPartRuntimeData currentSlotData;
    private PartData currentPartData;

    private System.Action<StoredPartRuntimeData, PartData> clickedCallback;
    private System.Action<StoredPartRuntimeData, PartData> hoveredCallback;
    private System.Action hoverExitCallback;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(HandleClicked);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 設定背包格顯示資料。
    /// </summary>
    public void Set(
        StoredPartRuntimeData slotData,
        PartData partData,
        System.Action<StoredPartRuntimeData, PartData> onClicked,
        System.Action<StoredPartRuntimeData, PartData> onHovered,
        System.Action onHoverExit)
    {
        currentSlotData = slotData;
        currentPartData = partData;

        clickedCallback = onClicked;
        hoveredCallback = onHovered;
        hoverExitCallback = onHoverExit;

        if (partData == null)
        {
            Clear();
            return;
        }

        if (iconImage != null)
        {
            iconImage.enabled = true;
            iconImage.sprite = partData.icon;
        }

        if (countText != null)
        {
            countText.text = slotData.count.ToString();
        }

        if (button != null)
        {
            button.interactable = true;
        }
    }

    /// <summary>
    /// 清空背包格。
    /// </summary>
    public void Clear()
    {
        currentSlotData = default;
        currentPartData = null;

        clickedCallback = null;
        hoveredCallback = null;
        hoverExitCallback = null;

        if (iconImage != null)
        {
            iconImage.enabled = false;
            iconImage.sprite = null;
        }

        if (countText != null)
        {
            countText.text = string.Empty;
        }

        if (button != null)
        {
            button.interactable = false;
        }
    }

    #endregion

    #region Button Events

    private void HandleClicked()
    {
        if (currentPartData == null)
        {
            return;
        }

        clickedCallback?.Invoke(currentSlotData, currentPartData);
    }

    #endregion

    #region Pointer Events

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentPartData == null)
        {
            return;
        }

        hoveredCallback?.Invoke(currentSlotData, currentPartData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hoverExitCallback?.Invoke();
    }

    #endregion
}