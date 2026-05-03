using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BackpackUIController : MonoBehaviour
{
    #region References

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("Slot UI")]
    [SerializeField] private Transform slotGridRoot;
    [SerializeField] private BackpackSlotUI slotPrefab;

    [Header("Detail UI")]
    [SerializeField] private GameObject detailPanelRoot;
    [SerializeField] private Image detailIconImage;
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailStatText;
    [SerializeField] private Button equipButton;


    [Header("Database")]
    [SerializeField] private GameDatabase gameDatabase;

    #endregion

    #region Runtime Data

    private PlayerInventoryNetwork localInventory;
    private StoredPartRuntimeData selectedSlotData;
    private PartData selectedPartData;

    private readonly List<BackpackSlotUI> slotUIs = new List<BackpackSlotUI>();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (equipButton != null)
        {
            equipButton.onClick.AddListener(HandleEquipClicked);
        }

        ClearDetailPanel();
        HideDetailPanel();
    }

    private void OnDestroy()
    {
        if (equipButton != null)
        {
            equipButton.onClick.RemoveListener(HandleEquipClicked);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 綁定本地玩家背包。
    /// </summary>
    public void Bind(PlayerInventoryNetwork inventory)
    {
        localInventory = inventory;
        RefreshUI();
    }

    /// <summary>
    /// 顯示背包。
    /// </summary>
    public void Show()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        RefreshUI();
    }

    /// <summary>
    /// 隱藏背包。
    /// </summary>
    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    /// <summary>
    /// 切換背包顯示狀態。
    /// </summary>
    public void Toggle()
    {
        if (panelRoot == null)
        {
            return;
        }

        if (panelRoot.activeSelf)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    #endregion

    #region UI Refresh

    /// <summary>
    /// 刷新背包 UI。
    /// </summary>
    public void RefreshUI()
    {
        if (localInventory == null || gameDatabase == null)
        {
            return;
        }

        EnsureSlotCount(localInventory.StoredParts.Count);

        for (int i = 0; i < slotUIs.Count; i++)
        {
            if (i >= localInventory.StoredParts.Count)
            {
                slotUIs[i].Clear();
                continue;
            }

            StoredPartRuntimeData slotData = localInventory.StoredParts[i];

            if (slotData.IsEmpty)
            {
                slotUIs[i].Clear();
                continue;
            }

            PartData partData = gameDatabase.GetPartData(slotData.partID);

            if (partData == null)
            {
                slotUIs[i].Clear();
                continue;
            }

            slotUIs[i].Set(slotData,partData,HandleSlotClicked,HandleSlotHovered,HandleSlotHoverExit);
        }
    }

    private void EnsureSlotCount(int count)
    {
        while (slotUIs.Count < count)
        {
            BackpackSlotUI newSlot = Instantiate(slotPrefab, slotGridRoot);
            slotUIs.Add(newSlot);
        }
    }

    #endregion

    #region Detail Panel

    private void HandleSlotClicked(StoredPartRuntimeData slotData, PartData partData)
    {
        selectedSlotData = slotData;
        selectedPartData = partData;

        RefreshDetailPanel();
    }

    private void RefreshDetailPanel()
    {
        if (selectedPartData == null)
        {
            ClearDetailPanel();
            return;
        }

        if (detailIconImage != null)
        {
            detailIconImage.enabled = true;
            detailIconImage.sprite = selectedPartData.icon;
        }

        if (detailNameText != null)
        {
            detailNameText.text = selectedPartData.partName;
        }

        if (detailStatText != null)
        {
            detailStatText.text =
                $"HP +{selectedPartData.hpBonus}\n" +
                $"Attack +{selectedPartData.attackBonus}\n" +
                $"Move Speed +{selectedPartData.moveSpeedBonus}\n" +
                $"Defense +{selectedPartData.defenseBonus}\n" +
                $"Attack Speed +{selectedPartData.attackSpeedBonus}";
        }

        if (equipButton != null)
        {
            equipButton.interactable = true;
        }
    }

    private void HandleSlotHovered(StoredPartRuntimeData slotData, PartData partData)
    {
        selectedSlotData = slotData;
        selectedPartData = partData;

        ShowDetailPanel();
        RefreshDetailPanel();
    }

    private void HandleSlotHoverExit()
    {
        ClearDetailPanel();
        HideDetailPanel();
    }

    private void ShowDetailPanel()
    {
        if (detailPanelRoot != null)
        {
            detailPanelRoot.SetActive(true);
        }
    }

    private void HideDetailPanel()
    {
        if (detailPanelRoot != null)
        {
            detailPanelRoot.SetActive(false);
        }
    }

    private void ClearDetailPanel()
    {
        selectedSlotData = default;
        selectedPartData = null;

        if (detailIconImage != null)
        {
            detailIconImage.enabled = false;
            detailIconImage.sprite = null;
        }

        if (detailNameText != null)
        {
            detailNameText.text = "未選擇部件";
        }

        if (detailStatText != null)
        {
            detailStatText.text = string.Empty;
        }

        if (equipButton != null)
        {
            equipButton.interactable = false;
        }
    }

    #endregion

    #region Button Events

    private void HandleEquipClicked()
    {
        if (selectedPartData == null)
        {
            return;
        }

        Debug.Log($"[BackpackUI] 點擊裝備部件：{selectedPartData.partName}, PartID: {selectedPartData.partID}");

        // 初版先只測試 UI 點擊是否正常。
        // 下一階段再接 PlayerEquipmentController / Mirror Command / Server 裝備同步。
    }

    #endregion
}