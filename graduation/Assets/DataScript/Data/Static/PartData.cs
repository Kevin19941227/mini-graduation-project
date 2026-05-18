using System.Collections.Generic;
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

    [Header("Stat Modifiers")]
    public List<StatModifier> statModifiers = new List<StatModifier>();

    [Header("Legacy Stat Bonus")]
    public int hpBonus;
    public int attackBonus;
    public float moveSpeedBonus;
    public int defenseBonus;
    public float attackSpeedBonus;

    [Header("Passive Effects")]
    public List<BuffData> passiveBuffs = new List<BuffData>();

    [Header("Presentation")]
    public Sprite icon;
    public GameObject partPrefab;

    [Header("Inventory")]
    public int maxStack = 99;

    public void AppendStatModifiers(List<StatModifier> target)
    {
        if (target == null)
        {
            return;
        }

        if (statModifiers != null && statModifiers.Count > 0)
        {
            target.AddRange(statModifiers);
            return;
        }

        AppendLegacyStatModifiers(target);
    }

    private void AppendLegacyStatModifiers(List<StatModifier> target)
    {
        if (hpBonus != 0)
        {
            target.Add(new StatModifier(StatType.MaxHP, hpBonus));
        }

        if (attackBonus != 0)
        {
            target.Add(new StatModifier(StatType.Attack, attackBonus));
        }

        if (!Mathf.Approximately(moveSpeedBonus, 0f))
        {
            target.Add(new StatModifier(StatType.MoveSpeed, moveSpeedBonus));
        }

        if (defenseBonus != 0)
        {
            target.Add(new StatModifier(StatType.Defense, defenseBonus));
        }

        if (!Mathf.Approximately(attackSpeedBonus, 0f))
        {
            target.Add(new StatModifier(StatType.AttackSpeed, attackSpeedBonus));
        }
    }
}
