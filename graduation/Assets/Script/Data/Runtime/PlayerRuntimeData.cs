using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerRuntimeData
{
    public int runtimePlayerID;
    public string persistentPlayerID;
    public string playerName;

    public int currentHP;
    public int currentAttack;
    public float currentMoveSpeed;
    public int currentDefense;
    public float currentAttackSpeed;

    public bool isDead;
    public Vector3 currentPosition;

    public List<EquippedPartRuntimeData> equippedParts = new List<EquippedPartRuntimeData>();
    public List<ActiveBuffRuntimeData> activeBuffs = new List<ActiveBuffRuntimeData>();
}