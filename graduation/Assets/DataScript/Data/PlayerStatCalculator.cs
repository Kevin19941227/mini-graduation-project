using System.Collections.Generic;
using UnityEngine;

public static class PlayerStatCalculator
{
    private static readonly List<StatModifier> ModifierBuffer = new List<StatModifier>();

    /// <summary>
    /// Calculates the current player stats from base data, equipped parts, and active buffs.
    /// </summary>
    public static void RecalculatePlayerStats(
        PlayerBaseData baseData,
        List<PartData> equippedPartDataList,
        List<BuffData> activeBuffDataList,
        PlayerRuntimeData runtimeData)
    {
        if (baseData == null || runtimeData == null)
        {
            Debug.LogWarning("RecalculatePlayerStats failed: baseData or runtimeData is null.");
            return;
        }

        int finalHP = baseData.baseHP;
        int finalAttack = baseData.baseAttack;
        float finalMoveSpeed = baseData.baseMoveSpeed;
        int finalDefense = baseData.baseDefense;
        float finalAttackSpeed = baseData.baseAttackSpeed;

        #region Parts Bonus
        if (equippedPartDataList != null)
        {
            for (int i = 0; i < equippedPartDataList.Count; i++)
            {
                PartData part = equippedPartDataList[i];
                if (part == null) continue;

                ModifierBuffer.Clear();
                part.AppendStatModifiers(ModifierBuffer);
                ApplyModifiers(ModifierBuffer, ref finalHP, ref finalAttack, ref finalMoveSpeed, ref finalDefense, ref finalAttackSpeed);
            }
        }
        #endregion

        #region Buff Bonus
        if (activeBuffDataList != null)
        {
            for (int i = 0; i < activeBuffDataList.Count; i++)
            {
                BuffData buff = activeBuffDataList[i];
                if (buff == null) continue;

                ModifierBuffer.Clear();
                buff.AppendStatModifiers(ModifierBuffer);
                ApplyModifiers(ModifierBuffer, ref finalHP, ref finalAttack, ref finalMoveSpeed, ref finalDefense, ref finalAttackSpeed);
            }
        }
        #endregion

        runtimeData.currentAttack = finalAttack;
        runtimeData.currentMoveSpeed = Mathf.Max(0f, finalMoveSpeed);
        runtimeData.currentDefense = finalDefense;
        runtimeData.currentAttackSpeed = Mathf.Max(0.1f, finalAttackSpeed);

        if (runtimeData.currentHP > finalHP)
        {
            runtimeData.currentHP = finalHP;
        }
    }

    private static void ApplyModifiers(
        List<StatModifier> modifiers,
        ref int finalHP,
        ref int finalAttack,
        ref float finalMoveSpeed,
        ref int finalDefense,
        ref float finalAttackSpeed)
    {
        for (int i = 0; i < modifiers.Count; i++)
        {
            StatModifier modifier = modifiers[i];

            switch (modifier.statType)
            {
                case StatType.MaxHP:
                    finalHP = Mathf.RoundToInt(ApplyModifier(finalHP, modifier));
                    break;

                case StatType.Attack:
                    finalAttack = Mathf.RoundToInt(ApplyModifier(finalAttack, modifier));
                    break;

                case StatType.MoveSpeed:
                    finalMoveSpeed = ApplyModifier(finalMoveSpeed, modifier);
                    break;

                case StatType.Defense:
                    finalDefense = Mathf.RoundToInt(ApplyModifier(finalDefense, modifier));
                    break;

                case StatType.AttackSpeed:
                    finalAttackSpeed = ApplyModifier(finalAttackSpeed, modifier);
                    break;
            }
        }
    }

    private static float ApplyModifier(float currentValue, StatModifier modifier)
    {
        switch (modifier.mode)
        {
            case StatModifierMode.PercentAdd:
                return currentValue + currentValue * modifier.value * 0.01f;

            case StatModifierMode.PercentMultiply:
                return currentValue * (1f + modifier.value * 0.01f);

            default:
                return currentValue + modifier.value;
        }
    }
}
