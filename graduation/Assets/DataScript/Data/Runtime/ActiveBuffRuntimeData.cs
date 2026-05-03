[System.Serializable]
public struct ActiveBuffRuntimeData
{
    public int buffID;
    public float remainingTime;
    public int stackCount;

    public ActiveBuffRuntimeData(int buffID, float remainingTime, int stackCount)
    {
        this.buffID = buffID;
        this.remainingTime = remainingTime;
        this.stackCount = stackCount;
    }
}