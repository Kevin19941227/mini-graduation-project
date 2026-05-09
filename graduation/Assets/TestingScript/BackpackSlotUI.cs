using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BackpackSlotUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
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
    private System.Action<StoredPartRuntimeData, PartData, Vector2> hoveredCallback;
    private System.Action<Vector2> hoverMoveCallback;
    private System.Action hoverExitCallback;
    private System.Action<StoredPartRuntimeData, PartData, Vector2> beginDragCallback;
    private System.Action<Vector2> dragCallback;
    private System.Action<Vector2> endDragCallback;

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

    public void Set(
        StoredPartRuntimeData slotData,
        PartData partData,
        System.Action<StoredPartRuntimeData, PartData> onClicked,
        System.Action<StoredPartRuntimeData, PartData, Vector2> onHovered,
        System.Action<Vector2> onHoverMove,
        System.Action onHoverExit,
        System.Action<StoredPartRuntimeData, PartData, Vector2> onBeginDrag,
        System.Action<Vector2> onDrag,
        System.Action<Vector2> onEndDrag)
    {
        currentSlotData = slotData;
        currentPartData = partData;

        clickedCallback = onClicked;
        hoveredCallback = onHovered;
        hoverMoveCallback = onHoverMove;
        hoverExitCallback = onHoverExit;
        beginDragCallback = onBeginDrag;
        dragCallback = onDrag;
        endDragCallback = onEndDrag;

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

    public void Clear()
    {
        currentSlotData = default;
        currentPartData = null;

        clickedCallback = null;
        hoveredCallback = null;
        hoverMoveCallback = null;
        hoverExitCallback = null;
        beginDragCallback = null;
        dragCallback = null;
        endDragCallback = null;

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

        hoveredCallback?.Invoke(currentSlotData, currentPartData, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (currentPartData == null)
        {
            return;
        }

        hoverMoveCallback?.Invoke(eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hoverExitCallback?.Invoke();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentPartData == null)
        {
            return;
        }

        beginDragCallback?.Invoke(currentSlotData, currentPartData, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (currentPartData == null)
        {
            return;
        }

        dragCallback?.Invoke(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (currentPartData == null)
        {
            return;
        }

        endDragCallback?.Invoke(eventData.position);
    }

    #endregion
}
