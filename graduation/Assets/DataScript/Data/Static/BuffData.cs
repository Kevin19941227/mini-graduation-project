using System.Collections.Generic;
using UnityEngine;

public enum BuffType
{
    Poison,
    Slow,
    SpeedUp,
    DefenseUp,
    AttackUp,
    AttackRangeUp,
    AttackSpeedUp,
    SizeUp,
    DamageResistanceUp,
    NegativeEffectResistanceUp,
    ZoneDamageResistanceUp,
    PoisonTrail,
    Fear,
    DodgeDistanceUp,
    DodgeInvincibleFrame,
    FearMark,
    SelfDamageTrueDamage,
    DamageToAttackConversion
}

public enum BuffTriggerType
{
    Passive,
    OnAttack,
    OnHit,
    OnDamageTaken,
    OnDodge,
    OnZoneDamage,
    LinkSet
}

public enum BuffScalingSource
{
    None,
    GeneralPartCount,
    HeadPartCount,
    MaskCount,
    RelatedPartCount,
    SelfMissingHP,
    SoloPlay
}

[CreateAssetMenu(fileName = "BuffData", menuName = "GameData/Buff Data")]
public class BuffData : ScriptableObject
{
    [Header("Identity")]
    public int buffID;
    public string buffName;
    public BuffType buffType;
    [TextArea]
    public string description;

    [Header("Stat Modifiers")]
    public List<StatModifier> statModifiers = new List<StatModifier>();

    [Header("Effect")]
    public BuffTriggerType triggerType = BuffTriggerType.Passive;
    public float duration = 5f;
    public float value = 1f;
    public float secondaryValue;
    public bool stackable = false;
    public int maxStack = 1;

    [Header("Scaling")]
    public BuffScalingSource scalingSource = BuffScalingSource.None;
    public float valuePerStack;
    public int requiredPartCount;
    public bool requiresArmAndLeg;

    /// <summary>
    /// Adds this buff's stat modifiers to the given target list.
    /// </summary>
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

            case BuffType.AttackSpeedUp:
                target.Add(new StatModifier(StatType.AttackSpeed, value));
                break;

            case BuffType.Slow:
                target.Add(new StatModifier(StatType.MoveSpeed, -value));
                break;
        }
    }
}
