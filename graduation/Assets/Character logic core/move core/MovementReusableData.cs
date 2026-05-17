
using UnityEngine;

public class MovementReusableData
{
    // Input
    public Vector2 MoveInput { get; set; }
    public Vector2 LastMoveInput { get; set; }

    // State
    public bool IsRunning { get; set; }
    public bool IsGrounded { get; set; }
    public bool JumpRequested { get; set; }
    public bool IsDodging { get; set; }

    // Runtime 速度（從 SO 讀基準值，Runtime 在這裡改）
    public float CurrentWalkSpeed { get; set; }
    public float CurrentRunSpeed { get; set; }
    public float CurrentDodgeSpeed { get; set; }

    // 給動畫用
    public Vector3 CurrentVelocity { get; set; }
}
