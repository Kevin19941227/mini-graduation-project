using UnityEngine;

[System.Serializable]
public class LootRuntimeData
{
    public int lootRuntimeID;
    public int partID;

    public Vector3 position;
    public Vector3 eulerAngles;
    public bool isPicked;

    public int sourceMonsterRuntimeID;
    public float spawnTime;
    public int count = 1;
}