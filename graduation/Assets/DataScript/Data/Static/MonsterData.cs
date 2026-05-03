using UnityEngine;

public enum MonsterAIType
{
    Melee,
    Ranged,
    Chase,
    Boss
}

[CreateAssetMenu(fileName = "MonsterData", menuName = "GameData/Monster Data")]
public class MonsterData : ScriptableObject
{
    [Header("Identity")]
    public int monsterID;
    public string monsterName;

    [Header("Base Stats")]
    public int maxHP = 50;
    public int attack = 10;
    public float moveSpeed = 2f;
    public int defense = 0;
    public float attackRange = 1.5f;
    public float attackInterval = 1f;

    [Header("AI / Drop")]
    public MonsterAIType aiType;
    public int dropTableID;

    [Header("Presentation")]
    public GameObject monsterPrefab;
}