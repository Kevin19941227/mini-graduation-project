using Mirror;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterNavMeshAIController : NetworkBehaviour
{
    #region Inspector

    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private float detectRange = 20f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float targetRefreshInterval = 0.3f;
    [SerializeField] private bool requireReachableTarget = true;
    [SerializeField] private float unreachableRespawnDelay = 12f;
    [SerializeField] private float offNavMeshRespawnDelay = 3f;
    [SerializeField] private float respawnSampleRadius = 5f;

    #endregion

    #region Runtime

    private Transform currentTarget;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float targetRefreshTimer;
    private float unreachableTimer;
    private float offNavMeshTimer;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
    }

    private void Start()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
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

        if (!ValidateNavMeshState())
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

    #region AI Logic

    private void UpdateTarget()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        Transform nearestPlayer = null;
        float nearestSqrDistance = detectRange * detectRange;

        for (int i = 0; i < players.Length; i++)
        {
            Transform playerTransform = players[i].transform;
            float sqrDistance = (playerTransform.position - transform.position).sqrMagnitude;

            if (sqrDistance > nearestSqrDistance)
            {
                continue;
            }

            if (requireReachableTarget && !CanReachTarget(playerTransform.position))
            {
                continue;
            }

            nearestSqrDistance = sqrDistance;
            nearestPlayer = playerTransform;
        }

        currentTarget = nearestPlayer;
        UpdateUnreachableTimer();
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
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(currentTarget.position);
    }

    private bool CanReachTarget(Vector3 targetPosition)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return false;
        }

        NavMeshPath path = new NavMeshPath();

        if (!agent.CalculatePath(targetPosition, path))
        {
            return false;
        }

        return path.status == NavMeshPathStatus.PathComplete;
    }

    private bool ValidateNavMeshState()
    {
        if (agent == null || !agent.enabled)
        {
            return false;
        }

        if (agent.isOnNavMesh)
        {
            offNavMeshTimer = 0f;
            return true;
        }

        offNavMeshTimer += Time.deltaTime;

        if (offNavMeshTimer >= offNavMeshRespawnDelay)
        {
            RespawnAtSpawnPoint();
        }

        return false;
    }

    private void UpdateUnreachableTimer()
    {
        if (currentTarget != null)
        {
            unreachableTimer = 0f;
            return;
        }

        unreachableTimer += targetRefreshInterval;

        if (unreachableTimer >= unreachableRespawnDelay)
        {
            RespawnAtSpawnPoint();
        }
    }

    private void RespawnAtSpawnPoint()
    {
        if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, respawnSampleRadius, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
        else
        {
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        }

        transform.rotation = spawnRotation;
        currentTarget = null;
        targetRefreshTimer = 0f;
        unreachableTimer = 0f;
        offNavMeshTimer = 0f;
        agent.isStopped = true;
        agent.ResetPath();
    }

    #endregion
}
