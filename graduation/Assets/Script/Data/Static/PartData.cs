using UnityEngine;

public enum PartType
{
    Head,
    Body,
    Arm,
    Leg,
    Special
}

public enum RarityType
{
    Common,
    Rare,
    Epic,
    Legendary
}

[CreateAssetMenu(fileName = "PartData", menuName = "GameData/Part Data")]
public class PartData : ScriptableObject
{
    [Header("Identity")]
    public int partID;
    public string partName;
    public PartType partType;
    public RarityType rarity;

    [Header("Stat Bonus")]
    public int hpBonus;
    public int attackBonus;
    public float moveSpeedBonus;
    public int defenseBonus;
    public float attackSpeedBonus;

    [Header("Presentation")]
    public Sprite icon;
    public GameObject partPrefab;
}