using UnityEngine;

[CreateAssetMenu(fileName = "PlayerBaseData", menuName = "GameData/Player Base Data")]
public class PlayerBaseData : ScriptableObject
{
    [Header("Identity")]
    public int playerBaseID;

    [Header("Base Stats")]
    public int baseHP = 100;
    public int baseAttack = 10;
    public float baseMoveSpeed = 5f;
    public int baseDefense = 0;
    public float baseAttackSpeed = 1f;

    [Header("Presentation")]
    public GameObject playerPrefab;
}