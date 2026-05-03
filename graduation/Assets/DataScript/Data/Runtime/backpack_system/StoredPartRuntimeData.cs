[System.Serializable]
public struct StoredPartRuntimeData
{
    public int slotIndex;
    public int partID;
    public int count;

    public bool IsEmpty => partID <= 0 || count <= 0;

    public StoredPartRuntimeData(int slotIndex, int partID, int count)
    {
        this.slotIndex = slotIndex;
        this.partID = partID;
        this.count = count;
    }
}