using System;
using UnityEngine;

public enum StatType
{
    MaxHP,
    Attack,
    MoveSpeed,
    Defense,
    AttackSpeed
}

public enum StatModifierMode
{
    Add,
    PercentAdd,
    PercentMultiply
}

[Serializable]
public struct StatModifier
{
    public StatType statType;
    public StatModifierMode mode;
    public float value;

    public StatModifier(StatType statType, float value, StatModifierMode mode = StatModifierMode.Add)
    {
        this.statType = statType;
        this.value = value;
        this.mode = mode;
    }

    public string GetDisplayText()
    {
        string sign = value >= 0f ? "+" : string.Empty;
        string suffix = mode == StatModifierMode.Add ? string.Empty : "%";
        return $"{GetStatName()} {sign}{value}{suffix}";
    }

    private string GetStatName()
    {
        switch (statType)
        {
            case StatType.MaxHP:
                return "HP";
            case StatType.Attack:
                return "Attack";
            case StatType.MoveSpeed:
                return "Move Speed";
            case StatType.Defense:
                return "Defense";
            case StatType.AttackSpeed:
                return "Attack Speed";
            default:
                return statType.ToString();
        }
    }
}
