using System.Collections.Generic;
using UnityEngine;

public static class PlayerStatCalculator
{
    /// <summary>
    /// 根據基礎資料、部件與 Buff 計算玩家當前數值。
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
        for (int i = 0; i < equippedPartDataList.Count; i++)
        {
            PartData part = equippedPartDataList[i];
            if (part == null) continue;

            finalHP += part.hpBonus;
            finalAttack += part.attackBonus;
            finalMoveSpeed += part.moveSpeedBonus;
            finalDefense += part.defenseBonus;
            finalAttackSpeed += part.attackSpeedBonus;
        }
        #endregion

        #region Buff Bonus
        for (int i = 0; i < activeBuffDataList.Count; i++)
        {
            BuffData buff = activeBuffDataList[i];
            if (buff == null) continue;

            switch (buff.buffType)
            {
                case BuffType.SpeedUp:
                    finalMoveSpeed += buff.value;
                    break;

                case BuffType.DefenseUp:
                    finalDefense += Mathf.RoundToInt(buff.value);
                    break;

                case BuffType.AttackUp:
                    finalAttack += Mathf.RoundToInt(buff.value);
                    break;

                case BuffType.Slow:
                    finalMoveSpeed -= buff.value;
                    break;

                case BuffType.Poison:
                    // 毒通常不直接加在靜態數值，可能另外在Tick系統處理
                    break;
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
}