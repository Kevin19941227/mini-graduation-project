using UnityEngine;
using UnityEngine.AI;
using Mirror;

public class MonsterAI : NetworkBehaviour
{
    [Header("設定")]
    public float attackDistance = 2f;    // 多近開始攻擊
    public float findPlayerInterval = 1f; // 幾秒重新找一次最近玩家

    private NavMeshAgent _navMesh;
    private Animator _animator;
    private Transform _target;
    private bool _navMeshReady = false;
    private float _findTimer = 0f;

    void Awake()
    {
        _navMesh = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
    }

    void OnEnable()
    {
     //   MapGenerator.OnNavMeshReady += OnNavMeshReady;
    }

    void OnDisable()
    {
      //  MapGenerator.OnNavMeshReady -= OnNavMeshReady;
    }

    private void OnNavMeshReady()
    {
        _navMeshReady = true;
        Debug.Log("[MonsterAI] NavMesh 就緒，開始追蹤");
    }

    void Update()
    {
        // 只有 Server 控制 AI 邏輯
        if (!isServer) return;
        if (!_navMeshReady) return;
        if (!_navMesh.isOnNavMesh) return;

        // 定時重新找最近玩家
        _findTimer += Time.deltaTime;
        if (_findTimer >= findPlayerInterval)
        {
            _findTimer = 0f;
            FindNearestPlayer();
        }

        if (_target == null) return;

        float dist = Vector3.Distance(transform.position, _target.position);

        if (dist <= attackDistance)
        {
            // 到達攻擊距離，停下來
            _navMesh.SetDestination(transform.position);
            RpcPlayAnimation("Attack");
        }
        else
        {
            // 追蹤中
            _navMesh.SetDestination(_target.position);

            // 根據移動速度判斷播走路還是跑步
            float speed = _navMesh.velocity.magnitude;
            RpcSetSpeed(speed);
        }
    }

    private void FindNearestPlayer()
    {
        float closestDist = Mathf.Infinity;
        Transform closest = null;

        foreach (var player in FindObjectsOfType<PlayCol>())
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = player.transform;
            }
        }

        _target = closest;
    }

    // 播放動畫同步給所有 Client
    [ClientRpc]
    private void RpcPlayAnimation(string stateName)
    {
        if (_animator != null)
            _animator.CrossFadeInFixedTime(stateName, 0.1f);
    }

    // 同步移動速度給 Animator
    [ClientRpc]
    private void RpcSetSpeed(float speed)
    {
        if (_animator != null)
            _animator.SetFloat("Speed", speed);
    }
}