using UnityEngine;
using UnityEngine.AI;
using Mirror;

public class MonsterAI : NetworkBehaviour
{
    [Header("設定")]
    public float attackDistance = 2f;
    public float findPlayerInterval = 1f;
    public float speedSyncInterval = 0.1f;
    public int attackDamage = 10;
    public float attackDelay = 10f;    // 就緒後幾秒才能開始攻擊
    public float attackCooldown = 8f;  // 每次攻擊後的冷卻時間（秒）

    private NavMeshAgent _navMesh;
    private Animator _animator;
    private NetworkIdentity _netIdentity;

    private Transform _target;
    private bool _navMeshReady = false;
    private bool _canAttack = false;
    private float _attackCooldownTimer = 0f;
    private float _findTimer = 0f;
    private float _speedSyncTimer = 0f;
    private float _navMeshRetryTimer = 0f;

    private enum AIState { Idle, Chasing, Attacking }
    private AIState _currentState = AIState.Idle;

    void Awake()
    {
        _navMesh = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _netIdentity = GetComponent<NetworkIdentity>();
    }

    void OnEnable()  => MapGenerator.OnNavMeshReady += OnNavMeshReady;
    void OnDisable() => MapGenerator.OnNavMeshReady -= OnNavMeshReady;

    // Scene 物件：地圖建好收到事件
    private void OnNavMeshReady()
    {
        if (!ShouldRunAI()) return;
        StartCoroutine(WaitAndEnable());
    }

    // Spawn 物件：Server spawn 後 NavMesh 已存在，直接嘗試
    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(WaitAndEnable());
    }

    // Client 上不需要 NavMeshAgent（位置由 NetworkTransform 同步）
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isServer && _navMesh != null)
            _navMesh.enabled = false;
    }

    private System.Collections.IEnumerator WaitAndEnable()
    {
        if (!ShouldRunAI()) yield break;
        if (_navMeshReady) yield break;
        yield return null;
        TryEnableNavMesh();
    }

    private void TryEnableNavMesh()
    {
        if (!ShouldRunAI()) return;
        if (_navMeshReady) return;
        if (_navMesh == null) return;

        if (!_navMesh.enabled)
            _navMesh.enabled = true;

        if (_navMesh.isOnNavMesh)
        {
            SetReady();
            return;
        }

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 50f, NavMesh.AllAreas))
        {
            _navMesh.Warp(hit.position);
            SetReady();
        }
        else
        {
            Debug.LogWarning($"[MonsterAI] {name} 找不到有效的 NavMesh 位置");
        }
    }

    private void SetReady()
    {
        _navMeshReady = true;
        _findTimer = findPlayerInterval; // 下一幀立刻找目標
        Debug.Log($"[MonsterAI] {name} 就緒，{attackDelay} 秒後開始攻擊");
        StartCoroutine(AttackDelayCoroutine());
    }

    private System.Collections.IEnumerator AttackDelayCoroutine()
    {
        yield return new WaitForSeconds(attackDelay);
        _canAttack = true;
        Debug.Log($"[MonsterAI] {name} 開始攻擊");
    }

    private bool ShouldRunAI() => _netIdentity != null ? NetworkServer.active : true;

    void Update()
    {
        if (!ShouldRunAI()) return;

        if (!_navMeshReady)
        {
            _navMeshRetryTimer += Time.deltaTime;
            if (_navMeshRetryTimer >= 0.5f)
            {
                _navMeshRetryTimer = 0f;
                TryEnableNavMesh();
            }
            return;
        }

        if (!_navMesh.isOnNavMesh) return;

        _findTimer += Time.deltaTime;
        if (_findTimer >= findPlayerInterval)
        {
            _findTimer = 0f;
            FindNearestPlayer();
        }

        if (_target == null)
        {
            if (_currentState != AIState.Idle)
            {
                _currentState = AIState.Idle;
                _navMesh.SetDestination(transform.position);
                PlayAnimation("Idle");
                SetSpeed(0f);
            }
            return;
        }

        float dist = Vector3.Distance(transform.position, _target.position);

        // 冷卻計時
        if (_attackCooldownTimer > 0f)
            _attackCooldownTimer -= Time.deltaTime;

        if (dist <= attackDistance && _canAttack && _attackCooldownTimer <= 0f)
        {
            if (_currentState != AIState.Attacking)
            {
                _currentState = AIState.Attacking;
                _attackCooldownTimer = attackCooldown;
                _navMesh.SetDestination(transform.position);
                PlayAnimation("Attack");
                SetSpeed(0f);
            }
        }
        else
        {
            if (_currentState != AIState.Chasing)
                _currentState = AIState.Chasing;

            _navMesh.SetDestination(_target.position);

            _speedSyncTimer += Time.deltaTime;
            if (_speedSyncTimer >= speedSyncInterval)
            {
                _speedSyncTimer = 0f;
                SetSpeed(_navMesh.velocity.magnitude);
            }
        }
    }

    private void FindNearestPlayer()
    {
        float closestDist = Mathf.Infinity;
        Transform closest = null;

        // 優先用 Mirror 的連線列表，確保多人連線時能找到所有玩家
        if (NetworkServer.active)
        {
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn.identity == null) continue;
                var player = conn.identity.GetComponent<PlayCol>();
                if (player == null) continue;

                float dist = Vector3.Distance(transform.position, conn.identity.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = conn.identity.transform;
                }
            }
        }

        // 備用：直接搜尋場景（例如 Host 自己的玩家可能不在 connections 裡）
        if (closest == null)
        {
            foreach (var player in FindObjectsOfType<PlayCol>())
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = player.transform;
                }
            }
        }

        _target = closest;
    }

    private void PlayAnimation(string stateName)
    {
        if (_netIdentity != null) RpcPlayAnimation(stateName);
        else ApplyAnimation(stateName);
    }

    private void SetSpeed(float speed)
    {
        if (_netIdentity != null) RpcSetSpeed(speed);
        else ApplySpeed(speed);
    }

    private void ApplyAnimation(string stateName)
    {
        if (_animator != null) _animator.CrossFadeInFixedTime(stateName, 0.1f);
    }

    private void ApplySpeed(float speed)
    {
        if (_animator != null) _animator.SetFloat("Speed", speed);
    }

    [ClientRpc] private void RpcPlayAnimation(string stateName) => ApplyAnimation(stateName);
    [ClientRpc] private void RpcSetSpeed(float speed) => ApplySpeed(speed);

    // ── Animation Events（由 Attack 動畫呼叫）──────────────────────────

    // 攻擊動畫打到人的那一幀：對範圍內玩家造成傷害
    public void OnAttackHit()
    {
        if (!ShouldRunAI()) return;
        if (_target == null) return;

        float dist = Vector3.Distance(transform.position, _target.position);
        if (dist > attackDistance) return;

        var player = _target.GetComponent<PlayCol>();
        if (player != null)
        {
            Vector3 dir = (_target.position - transform.position).normalized;
            player.TakeDamage(attackDamage, dir);
        }
    }

    // 攻擊動畫結束：重置狀態讓下一次攻擊可以重新觸發
    public void OnActionComplete()
    {
        if (!ShouldRunAI()) return;
        // 強制清除 Attacking 狀態，下一幀 Update 會重新判斷並再次觸發動畫
        if (_currentState == AIState.Attacking)
            _currentState = AIState.Idle;
    }
}
