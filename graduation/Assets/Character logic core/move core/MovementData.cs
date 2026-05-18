using UnityEngine;

[CreateAssetMenu(fileName = "MovementData", menuName = "Data/MovementData")]
public class MovementData : ScriptableObject
{
    [Header("Walk")]
    
    public float WalkSpeed = 3f;
    //走路速度
    [Header("Run")]
    public float RunSpeed = 6f;

    [Header("Dodge")]
    public float DodgeSpeed = 10f;
    public float DodgeDistance = 4f;//躲避移動距離
    public float DodgeDuration = 0.3f;//躲避花的時間
    
    [Header("Jump")]
    public float JumpForce = 8f;
    public float Gravity = -20f;

    [Header("Stop")]
    public float DecelerationRate = 15f;
}