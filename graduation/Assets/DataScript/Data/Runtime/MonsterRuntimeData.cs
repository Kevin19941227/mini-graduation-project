using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MonsterRuntimeData
{
    public int runtimeMonsterID;
    public int monsterDataID;

    public int currentHP;
    public Vector3 currentPosition;
    public bool isDead;

    public int currentTargetPlayerRuntimeID = -1;
    public List<ActiveBuffRuntimeData> activeBuffs = new List<ActiveBuffRuntimeData>();
}