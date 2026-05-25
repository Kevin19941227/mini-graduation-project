using UnityEngine;

/// <summary>
/// 掛在模型子物件（有 Animator 的那層）上。
/// 把 Animation Event 轉發給父物件上的目標腳本。
/// </summary>
public class AnimationEventBridge : MonoBehaviour
{
    private MonsterAI _monsterAI;
    private PlayCol _playCol;

    void Awake()
    {
        // 往父層找
        _monsterAI = GetComponentInParent<MonsterAI>();
        _playCol   = GetComponentInParent<PlayCol>();
    }

    // 怪物攻擊動畫事件
    public void OnAttackHit()    => _monsterAI?.OnAttackHit();
    public void OnActionComplete() => _monsterAI?.OnActionComplete();

    // 玩家攻擊動畫事件（如果玩家的 Animator 也在子物件上）
    public void OnAttackHit_Player()     => _playCol?.OnAttackHit();
    public void OnActionComplete_Player() => _playCol?.OnActionComplete();
}
