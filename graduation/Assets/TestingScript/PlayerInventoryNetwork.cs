using System;
using Mirror;

public class PlayerInventoryNetwork : NetworkBehaviour
{
    #region SyncLists

    public readonly SyncList<StoredPartRuntimeData> StoredParts = new SyncList<StoredPartRuntimeData>();
    public readonly SyncList<EquippedPartRuntimeData> EquippedParts = new SyncList<EquippedPartRuntimeData>();

    #endregion

    #region Events

    public event Action OnInventoryChanged;

    #endregion

    #region Mirror Callbacks

    /// <summary>
    /// Client 端初始化背包同步事件。
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();

        StoredParts.Callback += HandleStoredPartsChanged;
        EquippedParts.Callback += HandleEquippedPartsChanged;

        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Client 端停止時取消背包同步事件。
    /// </summary>
    public override void OnStopClient()
    {
        StoredParts.Callback -= HandleStoredPartsChanged;
        EquippedParts.Callback -= HandleEquippedPartsChanged;

        base.OnStopClient();
    }

    #endregion

    #region Server Methods

    /// <summary>
    /// Server 將部件加入玩家背包。
    /// </summary>
    [Server]
    public bool ServerAddPart(int partID, int count)
    {
        if (partID <= 0 || count <= 0)
        {
            return false;
        }

        for (int i = 0; i < StoredParts.Count; i++)
        {
            StoredPartRuntimeData slot = StoredParts[i];

            if (slot.partID != partID)
            {
                continue;
            }

            slot.count += count;
            StoredParts[i] = slot;
            return true;
        }

        StoredPartRuntimeData newSlot = new StoredPartRuntimeData(
            StoredParts.Count,
            partID,
            count
        );

        StoredParts.Add(newSlot);
        return true;
    }

    /// <summary>
    /// Server 從玩家背包移除部件。
    /// </summary>
    [Server]
    public bool ServerRemovePart(int partID, int count)
    {
        if (partID <= 0 || count <= 0)
        {
            return false;
        }

        for (int i = 0; i < StoredParts.Count; i++)
        {
            StoredPartRuntimeData slot = StoredParts[i];

            if (slot.partID != partID)
            {
                continue;
            }

            if (slot.count < count)
            {
                return false;
            }

            slot.count -= count;

            if (slot.count <= 0)
            {
                StoredParts.RemoveAt(i);
                RebuildSlotIndexes();
            }
            else
            {
                StoredParts[i] = slot;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Server 檢查玩家是否持有指定部件。
    /// </summary>
    [Server]
    public bool ServerHasPart(int partID, int count)
    {
        if (partID <= 0 || count <= 0)
        {
            return false;
        }

        for (int i = 0; i < StoredParts.Count; i++)
        {
            StoredPartRuntimeData slot = StoredParts[i];

            if (slot.partID == partID && slot.count >= count)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region SyncList Events

    private void HandleStoredPartsChanged(
        SyncList<StoredPartRuntimeData>.Operation operation,
        int index,
        StoredPartRuntimeData oldItem,
        StoredPartRuntimeData newItem)
    {
        OnInventoryChanged?.Invoke();
    }

    private void HandleEquippedPartsChanged(
        SyncList<EquippedPartRuntimeData>.Operation operation,
        int index,
        EquippedPartRuntimeData oldItem,
        EquippedPartRuntimeData newItem)
    {
        OnInventoryChanged?.Invoke();
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// 重新整理背包格索引。
    /// </summary>
    [Server]
    private void RebuildSlotIndexes()
    {
        for (int i = 0; i < StoredParts.Count; i++)
        {
            StoredPartRuntimeData slot = StoredParts[i];
            slot.slotIndex = i;
            StoredParts[i] = slot;
        }
    }

    #endregion
}