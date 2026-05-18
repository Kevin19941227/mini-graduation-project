using System.Collections.Generic;
using UnityEngine;

public enum BuffType
{
    Poison,
    Slow,
    SpeedUp,
    DefenseUp,
    AttackUp
}

[CreateAssetMenu(fileName = "BuffData", menuName = "GameData/Buff Data")]
public class BuffData : ScriptableObject
{
    [Header("Identity")]
    public int buffID;
    public string buffName;
    public BuffType buffType;

    [Header("Stat Modifiers")]
    public List<StatModifier> statModifiers = new List<StatModifier>();

    [Header("Effect")]
    public float duration = 5f;
    public float value = 1f;
    public bool stackable = false;
    public int maxStack = 1;

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

        AppendLegacyStatModifier(target);
    }

    private void AppendLegacyStatModifier(List<StatModifier> target)
    {
        switch (buffType)
        {
            case BuffType.SpeedUp:
                target.Add(new StatModifier(StatType.MoveSpeed, value));
                break;

            case BuffType.DefenseUp:
                target.Add(new StatModifier(StatType.Defense, value));
                break;

            case BuffType.AttackUp:
                target.Add(new StatModifier(StatType.Attack, value));
                break;

            case BuffType.Slow:
                target.Add(new StatModifier(StatType.MoveSpeed, -value));
                break;
        }
    }
}
