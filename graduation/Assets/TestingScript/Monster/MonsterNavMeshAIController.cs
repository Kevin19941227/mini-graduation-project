using Mirror;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterNavMeshAIController : NetworkBehaviour
{
    #region Inspector 設定

    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private float detectRange = 20f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float targetRefreshInterval = 0.3f;

    #endregion

    #region Runtime

    private Transform currentTarget;
    private float targetRefreshTimer;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isServer && agent != null)
        {
            agent.enabled = false;
        }
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        targetRefreshTimer -= Time.deltaTime;

        if (targetRefreshTimer <= 0f)
        {
            targetRefreshTimer = targetRefreshInterval;
            UpdateTarget();
        }

        UpdateMovement();
    }

    #endregion

    #region AI 控制

    private void UpdateTarget()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        Transform nearestPlayer = null;
        float nearestSqrDistance = detectRange * detectRange;

        for (int i = 0; i < players.Length; i++)
        {
            float sqrDistance = (players[i].transform.position - transform.position).sqrMagnitude;

            if (sqrDistance <= nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearestPlayer = players[i].transform;
            }
        }

        currentTarget = nearestPlayer;
    }

    private void UpdateMovement()
    {
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        if (currentTarget == null)
        {
            agent.isStopped = true;
            return;
        }

        float sqrDistance = (currentTarget.position - transform.position).sqrMagnitude;

        if (sqrDistance <= attackRange * attackRange)
        {
            agent.isStopped = true;
            // 之後這裡接 Attack State / 動畫
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(currentTarget.position);
    }

    #endregion
}