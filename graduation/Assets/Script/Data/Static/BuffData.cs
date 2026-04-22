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

    [Header("Effect")]
    public float duration = 5f;
    public float value = 1f;
    public bool stackable = false;
    public int maxStack = 1;
}