using System.Collections.Generic;

[System.Serializable]
public class PlayerSaveData
{
    public string persistentPlayerID;
    public string playerName;

    public List<int> ownedPartIDs = new List<int>();
    public List<EquippedPartRuntimeData> savedEquippedParts = new List<EquippedPartRuntimeData>();

    public int highestStage = 0;
    public int currency = 0;
}