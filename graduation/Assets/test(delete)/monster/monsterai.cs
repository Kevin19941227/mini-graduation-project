using Mirror;
using UnityEngine;
using UnityEngine.AI;

public class MonsterAI : NetworkBehaviour
{
    private const int BaseLayerIndex = 0;
    private const float NavMeshRetryInterval = 0.5f;
    private const float AnimationFadeTime = 0.1f;
    private const float NetworkTransformSyncInterval = 0.1f;

    private static class AnimatorIds
    {
        public static readonly int IdleState = Animator.StringToHash("Idle");
        public static readonly int AttackState = Animator.StringToHash("Attack");
        public static readonly int SpeedParameter = Animator.StringToHash("Speed");
    }

    private enum MonsterState
    {
        Idle,
        Chasing,
        Attacking,
        Hurt
    }

    #region Inspector Settings
    [Header("音效設定")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _attackSound;
    [SerializeField] private AudioClip _hurtSound;
    [SerializeField] private AudioClip _dieSound;
    [Header("Combat")]
    [SerializeField, Min(1)] private int maxHealth = 50;
    [SerializeField] private MonsterData monsterData;
    [SerializeField] private GameDatabase gameDatabase;
    [SerializeField] private DropTableData dropTableOverride;
    public float attackDistance = 2f;
    public float findPlayerInterval = 1f;
    public float speedSyncInterval = 0.1f;
    public int attackDamage = 10;
    public float attackDelay = 10f;
    public float attackCooldown = 8f;
    [SerializeField] private string hurtAnimationStateName = "hurt";
    [SerializeField] private AnimationClip hurtAnimationClip;
    [SerializeField, Min(0f)] private float hurtLockDuration = 0.8f;
    [SerializeField, Min(0.1f)] private float runtimeHitboxRadius = 0.75f;
    [SerializeField, Min(0.1f)] private float runtimeHitboxHeight = 2f;
    [SerializeField] private Vector3 runtimeHitboxCenter = new Vector3(0f, 1f, 0f);

    [Header("NavMesh")]
    [SerializeField, Min(0.1f)] private float navMeshSpawnSampleDistance = 50f;
    [SerializeField, Min(0.1f)] private float targetNavMeshSampleDistance = 10f;
    [SerializeField, Min(0.05f)] private float destinationUpdateInterval = 0.25f;
    [SerializeField, Min(0.01f)] private float destinationUpdateDistance = 0.5f;
    [SerializeField, Min(0.01f)] private float speedSyncThreshold = 0.1f;

    [Header("頭上血條")]
    [SerializeField] private WorldHealthBar _worldHealthBar;

    #endregion

    #region Runtime State

    [SyncVar(hook = nameof(OnHealthChanged))]
    private int _currentHealth;

    private NavMeshAgent _agent;
    private Animator _animator;
    private NetworkIdentity _networkIdentity;

    private Transform _target;
    private MonsterState _state = MonsterState.Idle;

    private bool _navMeshReady;
    private bool _canAttack;
    private float _attackCooldownTimer;
    private float _findTargetTimer;
    private float _speedSyncTimer;
    private float _navMeshRetryTimer;
    private float _destinationUpdateTimer;
    private float _hurtTimer;
    private float _lastSyncedSpeed = -1f;
    private Vector3 _lastDestination;
    private int _hurtStateHash;
    private bool _warnedMissingHurtState;
    private PlayCol _lastDamageDealer;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _audioSource = GetComponentInChildren<AudioSource>();
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _networkIdentity = GetComponent<NetworkIdentity>();
        _hurtStateHash = Animator.StringToHash(hurtAnimationStateName);
        EnsureRuntimeHitbox();

        if (_agent != null)
            _agent.enabled = false;

        ConfigureNetworkSyncComponents();
    }
    private void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (_audioSource == null || clip == null) return;
        _audioSource.PlayOneShot(clip, volume);
    }
    private void EnsureRuntimeHitbox()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                return;
        }

        CapsuleCollider hitbox = gameObject.AddComponent<CapsuleCollider>();
        hitbox.isTrigger = true;
        hitbox.radius = runtimeHitboxRadius;
        hitbox.height = runtimeHitboxHeight;
        hitbox.center = runtimeHitboxCenter;

        Debug.Log($"[MonsterAI] {name} had no Collider, added runtime trigger hitbox.");
    }

    private void ConfigureNetworkSyncComponents()
    {
        NetworkBehaviour[] networkBehaviours = GetComponents<NetworkBehaviour>();
        for (int i = 0; i < networkBehaviours.Length; i++)
        {
            NetworkBehaviour behaviour = networkBehaviours[i];
            if (behaviour == null || behaviour == this) continue;

            string behaviourName = behaviour.GetType().Name;
            if (behaviourName.Contains("NetworkTransform"))
                behaviour.syncInterval = NetworkTransformSyncInterval;
            else if (behaviourName.Contains("NetworkAnimator"))
                behaviour.enabled = false;
        }
    }

    private void OnEnable()
    {
        MapGenerator.OnNavMeshReady += OnNavMeshReady;
    }

    private void OnDisable()
    {
        MapGenerator.OnNavMeshReady -= OnNavMeshReady;
    }

    private void Update()
    {
        if (!ShouldRunServerAI()) return;

        if (!EnsureNavMeshReady())
            return;

        UpdateTimers();
        UpdateTarget();
        UpdateState();
    }

    #endregion

    #region Mirror Callbacks

    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentHealth = maxHealth;

        if (_worldHealthBar != null)
            _worldHealthBar.Init(transform);

        StartCoroutine(EnableWhenNavMeshReady());
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isServer && _agent != null)
            _agent.enabled = false;

        if (_animator != null)
        {
            _animator.CrossFadeInFixedTime(AnimatorIds.IdleState, 0f);
            _animator.SetFloat(AnimatorIds.SpeedParameter, 0f);
        }

        // 初始化頭上血條
        if (_worldHealthBar != null)
        {
            _worldHealthBar.Init(transform);
            _worldHealthBar.UpdateHP(_currentHealth, maxHealth);
        }
    }

    #endregion

    #region NavMesh Setup

    private void OnNavMeshReady()
    {
        if (!ShouldRunServerAI()) return;
        StartCoroutine(EnableWhenNavMeshReady());
    }

    private System.Collections.IEnumerator EnableWhenNavMeshReady()
    {
        while (!MapGenerator.IsNavMeshReady)
            yield return null;

        yield return null;
        TryEnableNavMeshAgent();
    }

    private bool EnsureNavMeshReady()
    {
        if (_navMeshReady)
            return _agent != null && _agent.enabled && _agent.isOnNavMesh;

        _navMeshRetryTimer += Time.deltaTime;
        if (_navMeshRetryTimer < NavMeshRetryInterval)
            return false;

        _navMeshRetryTimer = 0f;
        TryEnableNavMeshAgent();
        return _navMeshReady;
    }

    private void TryEnableNavMeshAgent()
    {
        if (!ShouldRunServerAI() || _navMeshReady || _agent == null) return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSpawnSampleDistance, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            _agent.enabled = true;

            if (_agent.isOnNavMesh || _agent.Warp(hit.position))
                MarkNavMeshReady();

            return;
        }

        Debug.LogWarning($"[MonsterAI] {name} cannot find a valid NavMesh position.");
    }

    private void MarkNavMeshReady()
    {
        _navMeshReady = true;
        _findTargetTimer = findPlayerInterval;
        _state = MonsterState.Idle;
        PlayAnimation(AnimatorIds.IdleState);
        SetSpeed(0f);
        StartCoroutine(EnableAttackAfterDelay());
    }

    private System.Collections.IEnumerator EnableAttackAfterDelay()
    {
        _canAttack = false;
        yield return new WaitForSeconds(attackDelay);
        _canAttack = true;
    }

    #endregion

    #region Server AI

    private bool ShouldRunServerAI()
    {
        return _networkIdentity == null || NetworkServer.active;
    }

    private void UpdateTimers()
    {
        if (_attackCooldownTimer > 0f)
            _attackCooldownTimer -= Time.deltaTime;

        _findTargetTimer += Time.deltaTime;
        _speedSyncTimer += Time.deltaTime;
        _destinationUpdateTimer += Time.deltaTime;

        if (_hurtTimer > 0f)
        {
            _hurtTimer -= Time.deltaTime;
            if (_hurtTimer <= 0f && _state == MonsterState.Hurt)
                FinishHurt();
        }
    }

    private void UpdateTarget()
    {
        if (_findTargetTimer < findPlayerInterval) return;

        _findTargetTimer = 0f;
        _target = FindNearestPlayer();
    }

    private void UpdateState()
    {
        if (_hurtTimer > 0f) return;

        if (_target == null)
        {
            SetIdle();
            return;
        }

        float sqrDistanceToTarget = (_target.position - transform.position).sqrMagnitude;
        float sqrAttackDistance = attackDistance * attackDistance;

        if (sqrDistanceToTarget <= sqrAttackDistance && _canAttack && _attackCooldownTimer <= 0f)
        {
            StartAttack();
            return;
        }

        ChaseTarget();
    }

    private Transform FindNearestPlayer()
    {
        Transform nearest = null;
        float nearestSqrDistance = Mathf.Infinity;

        if (NetworkServer.active)
        {
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                if (connection.identity == null) continue;
                PlayCol player = connection.identity.GetComponent<PlayCol>();
                if (player == null) continue;
                CheckNearestPlayer(connection.identity.transform, ref nearest, ref nearestSqrDistance);
            }
        }

        if (nearest != null) return nearest;

        PlayCol[] players = FindObjectsOfType<PlayCol>();
        for (int i = 0; i < players.Length; i++)
            CheckNearestPlayer(players[i].transform, ref nearest, ref nearestSqrDistance);

        return nearest;
    }

    private void CheckNearestPlayer(Transform candidate, ref Transform nearest, ref float nearestSqrDistance)
    {
        float sqrDistance = (transform.position - candidate.position).sqrMagnitude;
        if (sqrDistance >= nearestSqrDistance) return;

        nearestSqrDistance = sqrDistance;
        nearest = candidate;
    }

    private void SetIdle()
    {
        if (_state == MonsterState.Idle) return;

        _state = MonsterState.Idle;
        StopAgent();
        PlayAnimation(AnimatorIds.IdleState);
        SetSpeed(0f);
    }

    private void ChaseTarget()
    {
        _state = MonsterState.Chasing;

        ResumeAgent();
        TrySetDestinationToTarget();

        if (!_agent.hasPath && !_agent.pathPending)
        {
            SetSpeed(0f);
            return;
        }

        if (_speedSyncTimer < speedSyncInterval) return;

        _speedSyncTimer = 0f;
        float currentSpeed = _agent.velocity.magnitude;
        if (Mathf.Abs(currentSpeed - _lastSyncedSpeed) < speedSyncThreshold) return;

        _lastSyncedSpeed = currentSpeed;
        SetSpeed(currentSpeed);
    }

    private void StartAttack()
    {
        if (_state == MonsterState.Attacking) return;

        _state = MonsterState.Attacking;
        _attackCooldownTimer = attackCooldown;
        StopAgent();
        PlayAnimation(AnimatorIds.AttackState);
        SetSpeed(0f);
        PlaySound(_attackSound, 0.5f);
    }

    private void FinishHurt()
    {
        _hurtTimer = 0f;
        _state = MonsterState.Idle;
        PlayAnimation(AnimatorIds.IdleState);
        SetSpeed(0f);
    }

    private void StopAgent()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;

        _agent.isStopped = true;
        _agent.ResetPath();
        _agent.velocity = Vector3.zero;
    }

    private void ResumeAgent()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
        _agent.isStopped = false;
    }

    private void TrySetDestinationToTarget()
    {
        if (_target == null || _agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
        if (_destinationUpdateTimer < destinationUpdateInterval) return;

        if (!NavMesh.SamplePosition(_target.position, out NavMeshHit hit, targetNavMeshSampleDistance, NavMesh.AllAreas))
            return;

        _destinationUpdateTimer = 0f;
        if ((_lastDestination - hit.position).sqrMagnitude < destinationUpdateDistance * destinationUpdateDistance)
            return;

        _lastDestination = hit.position;
        ResumeAgent();
        _agent.SetDestination(hit.position);
    }

    #endregion

    #region Animation Sync

    private void PlayAnimation(int stateHash)
    {
        if (_networkIdentity != null && NetworkServer.active)
        {
            RpcPlayAnimation(stateHash);
            return;
        }

        ApplyAnimation(stateHash);
    }

    private void SetSpeed(float speed)
    {
        if (_networkIdentity != null && NetworkServer.active)
        {
            RpcSetSpeed(speed);
            return;
        }

        ApplySpeed(speed);
    }

    private void ApplyAnimation(int stateHash)
    {
        if (_animator != null)
            _animator.CrossFadeInFixedTime(stateHash, AnimationFadeTime);
    }

    private void ApplySpeed(float speed)
    {
        if (_animator != null)
            _animator.SetFloat(AnimatorIds.SpeedParameter, speed);
    }

    [ClientRpc]
    private void RpcPlayAnimation(int stateHash)
    {
        ApplyAnimation(stateHash);
    }

    [ClientRpc]
    private void RpcSetSpeed(float speed)
    {
        ApplySpeed(speed);
    }

    #endregion

    #region Combat

    // 血量變化時更新血條
    private void OnHealthChanged(int oldHp, int newHp)
    {
        _worldHealthBar?.UpdateHP(newHp, maxHealth);
    }

    [Server]
    public void TakeDamage(int damage, Vector3 attackerForward)
    {
        TakeDamage(damage, attackerForward, null);
    }

    [Server]
    public void TakeDamage(int damage, Vector3 attackerForward, PlayCol damageDealer)
    {
        if (_currentHealth <= 0 || damage <= 0)
        {
            Debug.Log($"[MonsterDamage] {name} ignored damage. CurrentHP={_currentHealth}, Damage={damage}");
            return;
        }

        if (damageDealer != null)
            _lastDamageDealer = damageDealer;

        int previousHealth = _currentHealth;
        _currentHealth = Mathf.Max(0, _currentHealth - damage);
        Debug.Log($"[MonsterDamage] {name} took {damage} damage. HP {previousHealth} -> {_currentHealth}");

        if (_currentHealth > 0)
        {
            PlayHurt();
            return;
        }

        Debug.Log($"[MonsterDamage] {name} died.");
        Die();
    }

    [Server]
    private void PlayHurt()
    {
        _state = MonsterState.Hurt;
        _hurtTimer = GetHurtLockDuration();
        StopAgent();
        SetSpeed(0f);
        PlaySound(_hurtSound, 0.5f);
        if (_animator != null && _animator.HasState(BaseLayerIndex, _hurtStateHash))
        {
            PlayAnimation(_hurtStateHash);
            return;
        }

        if (_warnedMissingHurtState) return;

        _warnedMissingHurtState = true;
        Debug.LogWarning($"[MonsterAI] {name} cannot play hurt animation: state '{hurtAnimationStateName}' missing.");
    }

    private float GetHurtLockDuration()
    {
        if (hurtAnimationClip != null)
            return Mathf.Max(hurtLockDuration, hurtAnimationClip.length);

        if (_animator == null || _animator.runtimeAnimatorController == null)
            return hurtLockDuration;

        AnimationClip[] clips = _animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null || clip.name != hurtAnimationStateName) continue;
            return Mathf.Max(hurtLockDuration, clip.length);
        }

        return hurtLockDuration;
    }

    [Server]
    private void Die()
    {
        StopAgent();
        RollSceneDrops();

        if (_networkIdentity != null)
        {
            NetworkServer.Destroy(gameObject);
            return;
        }
        PlaySound(_dieSound);
        Destroy(gameObject);
    }

    [Server]
    private void RollSceneDrops()
    {
        DropTableData dropTable = ResolveDropTable();
        if (dropTable == null || dropTable.dropEntries == null || dropTable.dropEntries.Count == 0)
        {
            Debug.Log($"[MonsterLoot] {name} has no drop table.");
            return;
        }

        for (int i = 0; i < dropTable.dropEntries.Count; i++)
        {
            DropEntry entry = dropTable.dropEntries[i];
            if (entry == null || entry.partID <= 0) continue;

            float roll = Random.value;
            if (roll > entry.dropRate)
            {
                Debug.Log($"[MonsterLoot] {name} did not drop partID={entry.partID}. Roll={roll:F2}, Rate={entry.dropRate:F2}");
                continue;
            }

            int count = Random.Range(entry.minCount, entry.maxCount + 1);
            if (count <= 0) continue;

            Vector3 dropPosition = transform.position + Random.insideUnitSphere;
            dropPosition.y = transform.position.y + 0.5f;

            int lootId = SceneLootPickup.ServerRegisterLoot(entry.partID, count, dropPosition);
            if (lootId <= 0) continue;

            BroadcastSceneLoot(lootId, entry.partID, count, dropPosition);
            Debug.Log($"[MonsterLoot] {name} spawned scene loot partID={entry.partID} x{count}.");
        }
    }

    [Server]
    private void BroadcastSceneLoot(int lootId, int partId, int count, Vector3 position)
    {
        if (_lastDamageDealer != null)
        {
            _lastDamageDealer.RpcSpawnSceneLoot(lootId, partId, count, position);
            return;
        }

        RpcSpawnSceneLoot(lootId, partId, count, position);
    }

    private DropTableData ResolveDropTable()
    {
        if (dropTableOverride != null) return dropTableOverride;

        if (monsterData == null || monsterData.dropTableID <= 0 || gameDatabase == null)
            return null;

        return gameDatabase.GetDropTableData(monsterData.dropTableID);
    }

    [ClientRpc]
    private void RpcSpawnSceneLoot(int lootId, int partId, int count, Vector3 position)
    {
        PartData partData = ResolvePartData(partId);
        GameObject visualPrefab = partData != null ? partData.partPrefab : null;
        SceneLootPickup.ClientSpawnLoot(lootId, partId, count, position, visualPrefab);
    }

    private PartData ResolvePartData(int partId)
    {
        if (gameDatabase == null || partId <= 0) return null;
        return gameDatabase.GetPartData(partId);
    }

    #endregion

    #region Animation Events

    public void OnAttackHit()
    {
        if (!ShouldRunServerAI() || _target == null) return;

        float sqrDistanceToTarget = (_target.position - transform.position).sqrMagnitude;
        if (sqrDistanceToTarget > attackDistance * attackDistance) return;

        PlayCol player = _target.GetComponent<PlayCol>();
        if (player == null) return;

        Vector3 hitDirection = (_target.position - transform.position).normalized;
        player.TakeDamage(attackDamage, hitDirection);
    }

    public void OnActionComplete()
    {
        if (!ShouldRunServerAI()) return;

        if (_state == MonsterState.Attacking || _state == MonsterState.Hurt)
            _state = MonsterState.Idle;
    }

    #endregion
}