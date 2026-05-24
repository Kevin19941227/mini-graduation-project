using System;
using Mirror;
using UnityEngine;

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

    public override void OnStartClient()
    {
        base.OnStartClient();

        StoredParts.Callback += HandleStoredPartsChanged;
        EquippedParts.Callback += HandleEquippedPartsChanged;

        OnInventoryChanged?.Invoke();
    }

    public override void OnStopClient()
    {
        StoredParts.Callback -= HandleStoredPartsChanged;
        EquippedParts.Callback -= HandleEquippedPartsChanged;

        base.OnStopClient();
    }

    #endregion

    #region Client Helpers

    public bool ClientHasPart(int partID, int count)
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

    [Command]
    public void CmdInstallPartFromBackpack(
        int partID,
        string attachPointID,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        ServerInstallPartFromBackpack(partID, attachPointID, localPosition, localEulerAngles, localScale);
    }

    [Command]
    public void CmdReturnInstalledPartToBackpack(int partID)
    {
        ServerReturnInstalledPartToBackpack(partID);
    }

    [Command]
    public void CmdReturnAllInstalledPartsToBackpack()
    {
        ServerReturnAllInstalledPartsToBackpack();
    }

    #endregion

    #region Server Methods

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

    [Server]
    public bool ServerInstallPartFromBackpack(
        int partID,
        string attachPointID,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        if (!ServerRemovePart(partID, 1))
        {
            return false;
        }

        EquippedPartRuntimeData equippedPart = new EquippedPartRuntimeData(
            EquippedParts.Count,
            partID,
            attachPointID,
            localPosition,
            localEulerAngles,
            localScale
        );

        EquippedParts.Add(equippedPart);
        return true;
    }

    [Server]
    public bool ServerReturnInstalledPartToBackpack(int partID)
    {
        if (partID <= 0)
        {
            return false;
        }

        int equippedIndex = FindEquippedPartIndex(partID);

        if (equippedIndex < 0)
        {
            return false;
        }

        EquippedParts.RemoveAt(equippedIndex);
        RebuildEquippedIndexes();
        ServerAddPart(partID, 1);
        return true;
    }

    [Server]
    public int ServerReturnAllInstalledPartsToBackpack()
    {
        int returnedCount = 0;

        for (int i = EquippedParts.Count - 1; i >= 0; i--)
        {
            int partID = EquippedParts[i].partID;

            if (partID <= 0)
            {
                continue;
            }

            EquippedParts.RemoveAt(i);
            ServerAddPart(partID, 1);
            returnedCount++;
        }

        RebuildEquippedIndexes();
        return returnedCount;
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

    [Server]
    private int FindEquippedPartIndex(int partID)
    {
        for (int i = 0; i < EquippedParts.Count; i++)
        {
            if (EquippedParts[i].partID == partID)
            {
                return i;
            }
        }

        return -1;
    }

    [Server]
    private void RebuildEquippedIndexes()
    {
        for (int i = 0; i < EquippedParts.Count; i++)
        {
            EquippedPartRuntimeData equippedPart = EquippedParts[i];
            equippedPart.equipIndex = i;
            EquippedParts[i] = equippedPart;
        }
    }

    #endregion
}
