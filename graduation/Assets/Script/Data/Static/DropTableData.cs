using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DropEntry
{
    public int partID;
    [Range(0f, 1f)] public float dropRate = 0.5f;
    public int minCount = 1;
    public int maxCount = 1;
}

[CreateAssetMenu(fileName = "DropTableData", menuName = "GameData/Drop Table Data")]
public class DropTableData : ScriptableObject
{
    [Header("Identity")]
    public int dropTableID;

    [Header("Entries")]
    public List<DropEntry> dropEntries = new List<DropEntry>();
}