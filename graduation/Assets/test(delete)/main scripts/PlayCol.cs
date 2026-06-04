using UnityEngine;
using Mirror;
using Steamworks;
using UnityEngine.InputSystem;
using KinematicCharacterController;
using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

[System.Serializable]
public class PlayColSpecialEffectEvent : UnityEvent<PlayCol>
{
}

[System.Serializable]
public class PlayColPositionEffectEvent : UnityEvent<Vector3>
{
}

[System.Serializable]
public class PlayColDurationEffectEvent : UnityEvent<float>
{
}

public class PlayCol : NetworkBehaviour, ICharacterController, IGameplayInputModeReceiver
{
    #region Special Part IDs

    private const int VirusHeadPartID = 101;
    private const int TimeHeadPartID = 201;
    private const int MotionHatPartID = 304;
    private const int MotionAttackFanDebugSegments = 18;

    #endregion

    public enum PlayerPose
    {
        Grounded, Jump, Fall, Charging, Attack, Hit, Die, Dash
    }
    public enum MoveState
    {
        Idle, Walk, Run, Stop
    }

    private struct VirusPoisonCircleRuntime
    {
        public Vector3 center;
        public float radius;
        public float endTime;
        public float nextTickTime;

        public VirusPoisonCircleRuntime(Vector3 center, float radius, float endTime, float nextTickTime)
        {
            this.center = center;
            this.radius = radius;
            this.endTime = endTime;
            this.nextTickTime = nextTickTime;
        }
    }

    [Header("UI")]
    [SerializeField] private TextMesh playerNameText;
    [SerializeField] private WorldHealthBar _worldHealthBar;
    [SerializeField] private PlayerHUD hud;

    [Header("音效設定")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _attackSound;
    [SerializeField] private AudioClip _chargeSound;
    [SerializeField] private AudioClip _jumpSound;
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private AudioClip _dieSound;
    [SerializeField] private AudioClip _dashSound;
    [SerializeField] private AudioClip _runSound;

    [SyncVar(hook = nameof(OnNameChanged))]
    private string playerName;

    [SyncVar(hook = nameof(OnSyncPoseChanged))]
    private PlayerPose _syncPose = PlayerPose.Grounded;

    [SyncVar(hook = nameof(OnSyncSpeedXChanged))]
    private float _syncSpeedX = 0f;

    [SyncVar(hook = nameof(OnSyncSpeedZChanged))]
    private float _syncSpeedZ = 0f;

    [SyncVar(hook = nameof(OnSyncLastDirXChanged))]
    private float _syncLastDirX = 0f;

    [SyncVar(hook = nameof(OnSyncLastDirZChanged))]
    private float _syncLastDirZ = 0f;

    [SyncVar(hook = nameof(OnSyncIsMovingChanged))]
    private bool _syncIsMoving = false;

    [SyncVar(hook = nameof(OnSyncIsRunningChanged))]
    private bool _syncIsRunning = false;

    [SyncVar(hook = nameof(OnSyncIsStoppingChanged))]
    private bool _syncIsStopping = false;

    [SyncVar(hook = nameof(OnSyncChargeRatioChanged))]
    private float _syncChargeRatio = 0f;

    [Header("移動設定")]
    public float walkSpeed = 4f;
    public float runSpeed = 10f;
    public float rotationSmoothTime = 0.1f;

    [Header("跳躍設定")]
    public float jumpForce = 15f;
    [SerializeField] private float jumpCooldown = 1f;

    [Header("重力設定")]
    public float gravity = -30f;

    [Header("蓄力攻擊設定")]
    public float maxChargeTime = 1.5f;
    public float dashDistance = 3f;
    public float dashDuration = 0.2f;
    public float chargeMoveSpeedMultiplier = 0.4f;

    [Header("Dash 設定")]
    public float dashSpeed = 20f;
    public float dashTime = 1f;
    public float dashTapThreshold = 0.2f;

    [Header("戰鬥設定")]
    public float attackRadius = 1.5f;
    public float attackRange = 1.2f;
    [SerializeField] private float attackBoxHeight = 2f;
    public int attackDamage = 10;
    public int maxHp = 100;
    [SerializeField] private LayerMask attackHitMask = Physics.AllLayers;

    [Header("Passive Regen")]
    [SerializeField, Min(0f)] private float stationaryRegenDelay = 2f;
    [SerializeField, Min(1)] private int stationaryRegenAmount = 5;
    [SerializeField, Min(0.1f)] private float stationaryRegenInterval = 1f;

    [Header("打擊感設定")]
    public float hitStopDuration = 0.08f;
    public float hitKnockbackTime = 0.15f;
    public float cameraShakeStrength = 0.3f;
    public float hitInvincibilityDuration = 0.5f;
    public CinemachineImpulseSource impulseSource;

    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField, Min(0f)] private float hitEffectLifetime = 2f;

    [Header("Control Mode")]
    [SerializeField] private PlayerControlModeController controlModeController;
    [SerializeField] private Key assemblyModeKey = Key.B;

    [Header("Equipment Stats")]
    [SerializeField] private PlayerEquipmentStatsController equipmentStatsController;

    [Header("Special Equipment Effects")]
    [SerializeField] private PlayerInventoryNetwork inventory;
    [SerializeField] private GameDatabase gameDatabase;
    [SerializeField] private float timeBlinkHalfExtent = 5f;
    [SerializeField] private float timeBlinkCooldown = 3f;
    [SerializeField] private int timeBlinkMaxPositionAttempts = 16;
    [SerializeField] private float timeBlinkGroundRayHeight = 8f;
    [SerializeField] private float timeBlinkGroundRayDistance = 20f;
    [SerializeField] private float timeBlinkGroundOffset = 0.05f;
    [SerializeField] private LayerMask timeBlinkGroundMask = Physics.AllLayers;
    [SerializeField] private LayerMask timeBlinkBlockMask = Physics.AllLayers;
    [SerializeField] private float timeBlinkCapsuleRadius = 0.5f;
    [SerializeField] private float timeBlinkCapsuleHeight = 2f;
    [SerializeField] private float timeBlinkCapsuleYOffset = 1f;
    [SerializeField] private float virusPoisonCircleBaseDiameter = 2f;
    [SerializeField] private float virusPoisonCircleDiameterPerExtraHead = 1f;
    [SerializeField] private float virusPoisonCircleSpawnInterval = 5f;
    [SerializeField] private float virusPoisonCircleDuration = 4f;
    [SerializeField] private float virusPoisonCircleHeight = 2f;
    [SerializeField] private LayerMask virusPoisonHitMask = Physics.AllLayers;
    [SerializeField] private float virusPoisonTrailTickInterval = 0.5f;
    [SerializeField] private float virusPoisonDuration = 3f;
    [SerializeField] private int virusPoisonDamage = 1;
    [SerializeField] private float motionAttackRangeMultiplier = 3f;
    [SerializeField] private float motionBlackoutDuration = 2f;
    [SerializeField] private float motionAttackEffectMinInterval = 0.1f;
    [SerializeField] private Vector3 motionAttackEffectPositionOffset = new Vector3(0f, -1f, 0f);
    [SerializeField] private Vector3 motionAttackEffectEulerOffset = new Vector3(0f, -90f, 0f);
    [SerializeField] private float motionAttackFanRadius = 6f;
    [SerializeField, Range(1f, 180f)] private float motionAttackFanAngle = 75f;
    [SerializeField] private float motionAttackFanHeight = 2f;
    [SerializeField] private float motionAttackFanForwardOffset = 0.5f;
    [SerializeField] private PlayColSpecialEffectEvent virusTrailEffectRequested;
    [SerializeField] private PlayColPositionEffectEvent timeBlinkEffectRequested;
    [SerializeField] private PlayColSpecialEffectEvent motionAttackEffectRequested;
    [SerializeField] private PlayColDurationEffectEvent blackoutRequested;

    [Header("Special Effect Prefabs")]
    [SerializeField] private ParticleSystem virusTrailEffectPrefab;
    [SerializeField] private ParticleSystem timeBlinkEffectPrefab;
    [SerializeField] private ParticleSystem motionAttackEffectPrefab;
    [SerializeField, Min(0f)] private float specialEffectLifetime = 3f;

    [Header("Debug Visualizers")]
    [SerializeField] private float attackRangeDebugLineWidth = 0.06f;
    [SerializeField] private float attackRangeDebugHeightOffset = 0.05f;

    [SyncVar(hook = nameof(OnHpChanged))]
    private int _hp = 100;

    private float _serverInvincibilityEndTime = -1f;

    private Animator _animator;
    private KinematicCharacterMotor _motor;
    private PlayerInput _playerInput;
    private Camera _mainCamera;
    private float _runSpeedMultiplier;
    private int _currentDefense;
    private bool _hasAppliedEquipmentStats;

    private Vector2 _moveInput;
    private Vector3 _moveDirection;
    private float _verticalVelocity;
    private float _rotationVelocity;
    private bool _isRunning;
    private bool _jumpRequested;

    private float _lastDirX;
    private float _lastDirZ;

    private PlayerPose _currentPose = PlayerPose.Grounded;
    private MoveState _moveState = MoveState.Idle;
    private bool _canChangeState = true;

    private bool _isCharging = false;
    private float _chargeStartTime = 0f;
    private float _chargeRatio = 0f;

    private Vector3 _dashDirection = Vector3.zero;
    private float _dashTimer = 0f;
    private bool _attackFired = false;
    private int _localAttackSequence;
    private int _activeLocalAttackSequence;
    private readonly HashSet<int> _attackHitTargetIds = new HashSet<int>();

    private float _shiftPressTime = -1f;
    private bool _isDashing = false;
    private float _dashElapsed = 0f;

    private float _hitInvincibilityTimer = 0f;

    private float _syncTimer = 0f;
    private const float SYNC_INTERVAL = 0.1f;
    private const float ANIM_FLOAT_SYNC_EPSILON = 0.02f;
    private const float NETWORK_TRANSFORM_SYNC_INTERVAL = 0.05f;
    private const float FALLBACK_RUN_SPEED_MULTIPLIER = 1f;
    private const int MIN_DAMAGE_AFTER_DEFENSE = 1;
    private const string SurvivalMatchLogPrefix = "[SurvivalMatch]";

    private PlayerPose _lastSentPose = PlayerPose.Grounded;
    private float _lastSentSpeedX;
    private float _lastSentSpeedZ;
    private float _lastSentLastDirX;
    private float _lastSentLastDirZ;
    private bool _lastSentIsMoving;
    private bool _lastSentIsRunning;
    private bool _lastSentIsStopping;
    private float _lastSentChargeRatio;
    private bool _hasSentAnimState;

    private bool _wasJumpRequested = false;
    private float _jumpStartTime = -1f;
    private float _nextJumpTime = 0f;
    private const float MIN_JUMP_AIRTIME = 0.15f;
    private bool _gameplayInputEnabled = true;
    private bool _hasReportedDeathToMatch;

    private float _stopAnimTimer = 0f;
    private const float MAX_STOP_ANIM_DURATION = 1.2f;
    private float _serverStationaryTimer;
    private float _serverRegenTimer;
    private bool _isSubscribedToInventory;
    private float _equippedJumpForceMultiplier = 1f;
    private float _equippedAttackRangeMultiplier = 1f;
    private float _nextLocalTimeBlinkTime;
    private float _nextServerTimeBlinkTime;
    private float _nextServerVirusTrailTickTime;
    private float _poisonEndTime;
    private float _nextPoisonDamageTime;
    private float _localBlackoutEndTime;
    private float _lastServerMotionAttackEffectTime = -999f;
    private int _lastServerMotionAttackEffectSequence = -1;
    private int _equippedVirusHeadCount;
    private bool _hasVirusHeadPoisonTrail;
    private bool _hasTimeHeadBlink;
    private bool _hasMotionHeadWideAttack;
    private bool _showAttackRangeDebug;
    private Color _attackRangeDebugColor = Color.red;
    private LineRenderer _attackRangeDebugRenderer;
    private readonly List<VirusPoisonCircleRuntime> _activeVirusPoisonCircles = new List<VirusPoisonCircleRuntime>();

    public bool IsDead => _hp <= 0 || _currentPose == PlayerPose.Die;
    public int CurrentHp => _hp;
    public int CurrentMaxHp => maxHp;
    public int CurrentAttackDamage => attackDamage;
    public int CurrentDefense => _currentDefense;
    public float CurrentMoveSpeed => walkSpeed;
    public float CurrentAttackSpeed => 1f;

    void Awake()
    {
        _motor = GetComponent<KinematicCharacterMotor>();
        _animator = GetComponent<Animator>();
        controlModeController = GetComponent<PlayerControlModeController>();
        equipmentStatsController = GetComponent<PlayerEquipmentStatsController>();
        CacheSpecialEffectReferences();
        _runSpeedMultiplier = walkSpeed > 0f ? runSpeed / walkSpeed : FALLBACK_RUN_SPEED_MULTIPLIER;
        _motor.CharacterController = this;
        ConfigureNetworkSyncComponents();
        _audioSource = GetComponentInChildren<AudioSource>();
    }

    private void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (!isLocalPlayer) return;
        if (_audioSource == null || clip == null) return;
        _audioSource.PlayOneShot(clip, volume);
    }

    [Server]
    public void TakeZoneDamage(int damage)
    {
        if (_hp <= 0) return;

        _hp -= damage;
        ResetServerPassiveRegen();
        if (_hp <= 0)
        {
            _hp = 0;
            ServerHandleDeath();
        }
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
                behaviour.syncInterval = NETWORK_TRANSFORM_SYNC_INTERVAL;
            else if (behaviourName.Contains("NetworkAnimator"))
                behaviour.enabled = false;
        }
    }

    void Start()
    {
        _playerInput = GetComponent<PlayerInput>();
        SubscribeEquipmentStats();
        RecalculateEquippedSpecialEffects();

        if (_worldHealthBar != null)
            _worldHealthBar.Init(transform);

        if (isLocalPlayer)
        {
            _mainCamera = Camera.main;
            InitializeLocalHud();
            _playerInput.enabled = false;
            _motor.enabled = false;
            StartCoroutine(EnableLocalPlayerWhenMapReady());
        }
        else
        {
            _playerInput.enabled = false;
            _motor.enabled = false;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        SubscribeSpecialEffectInventory();
        RecalculateEquippedSpecialEffects();
        SurvivalMatchController.EnsureServerInstance()?.ServerRegisterPlayer(this);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        SubscribeSpecialEffectInventory();
        RecalculateEquippedSpecialEffects();
    }

    public override void OnStopServer()
    {
        UnsubscribeSpecialEffectInventory();
        SurvivalMatchController.GetServerInstance()?.ServerUnregisterPlayer(this);
        base.OnStopServer();
    }

    public override void OnStopClient()
    {
        UnsubscribeSpecialEffectInventory();
        base.OnStopClient();
    }

    private void OnDestroy()
    {
        UnsubscribeEquipmentStats();
        UnsubscribeSpecialEffectInventory();
    }

    private void InitializeLocalHud()
    {
        if (hud == null)
            hud = PlayerHUD.GetOrCreateRuntimeHud();

        if (hud == null) return;

        hud.Init();
        hud.UpdateHP(_hp, maxHp);
        hud.UpdateCharge(_chargeRatio, _isCharging);
    }

    private void UpdateLocalHudHealth(int currentHp)
    {
        if (!isLocalPlayer || hud == null) return;

        hud.UpdateHP(currentHp, maxHp);
    }

    private void UpdateLocalHudCharge()
    {
        if (!isLocalPlayer || hud == null) return;

        hud.UpdateCharge(_chargeRatio, _isCharging);
    }

    private void SubscribeEquipmentStats()
    {
        if (equipmentStatsController == null)
            equipmentStatsController = GetComponent<PlayerEquipmentStatsController>();

        if (equipmentStatsController == null) return;

        equipmentStatsController.OnStatsChanged += ApplyEquipmentStats;
        equipmentStatsController.RecalculateStats();
    }

    private void UnsubscribeEquipmentStats()
    {
        if (equipmentStatsController == null) return;
        equipmentStatsController.OnStatsChanged -= ApplyEquipmentStats;
    }

    private void ApplyEquipmentStats(PlayerRuntimeData stats)
    {
        if (stats == null) return;

        int previousMaxHp = maxHp;
        maxHp = stats.currentMaxHP;
        attackDamage = stats.currentAttack;
        walkSpeed = stats.currentMoveSpeed;
        runSpeed = walkSpeed * _runSpeedMultiplier;
        _currentDefense = stats.currentDefense;

        if (isServer)
            ApplyServerHpAfterMaxHpChanged(previousMaxHp);

        _worldHealthBar?.UpdateHP(_hp, maxHp);
        UpdateLocalHudHealth(_hp);
    }

    [Server]
    public void ServerSetCheatStats(int cheatMaxHp, int cheatAttackDamage, int cheatDefense, float cheatMoveSpeed, float cheatAttackSpeed)
    {
        int previousMaxHp = maxHp;
        maxHp = Mathf.Max(1, cheatMaxHp);
        attackDamage = Mathf.Max(1, cheatAttackDamage);
        _currentDefense = Mathf.Max(0, cheatDefense);
        walkSpeed = Mathf.Max(0f, cheatMoveSpeed);
        runSpeed = walkSpeed * _runSpeedMultiplier;

        ApplyServerHpAfterMaxHpChanged(previousMaxHp);
        _worldHealthBar?.UpdateHP(_hp, maxHp);
        UpdateLocalHudHealth(_hp);
    }

    [Server]
    public void ServerHealToFull()
    {
        _hp = maxHp;
        _currentPose = PlayerPose.Grounded;
        _moveState = MoveState.Idle;
        _hasReportedDeathToMatch = false;
        _worldHealthBar?.UpdateHP(_hp, maxHp);
        UpdateLocalHudHealth(_hp);
    }

    [Server]
    public void ServerClearCheatStats()
    {
        if (equipmentStatsController != null)
        {
            equipmentStatsController.RecalculateStats();
            return;
        }

        int previousMaxHp = maxHp;
        maxHp = 100;
        attackDamage = 10;
        walkSpeed = 4f;
        runSpeed = walkSpeed * _runSpeedMultiplier;
        _currentDefense = 0;
        ApplyServerHpAfterMaxHpChanged(previousMaxHp);
        _worldHealthBar?.UpdateHP(_hp, maxHp);
        UpdateLocalHudHealth(_hp);
    }

    [Server]
    private void ApplyServerHpAfterMaxHpChanged(int previousMaxHp)
    {
        if (!_hasAppliedEquipmentStats)
        {
            _hp = maxHp;
            _hasAppliedEquipmentStats = true;
            return;
        }

        _hp = Mathf.Clamp(_hp, 0, maxHp);
    }

    private IEnumerator EnableLocalPlayerWhenMapReady()
    {
        while (!MapGenerator.IsNavMeshReady)
            yield return null;

        _playerInput.enabled = true;
        _motor.enabled = true;
        _worldHealthBar?.UpdateHP(_hp, maxHp);
        UpdateLocalHudHealth(_hp);
        UpdateLocalHudCharge();

        if (NetworkClient.ready)
        {
            string name = SteamManager.Initialized
                ? SteamFriends.GetPersonaName()
                : "Player " + Random.Range(100, 999);
            CmdSetPlayerName(name);
        }
    }

    void Update()
    {
        UpdateServerDeathWatch();
        UpdateServerPassiveRegen();
        ServerUpdatePoisonState();
        ServerUpdateVirusPoisonTrail();

        if (!isLocalPlayer) return;

        HandleControlModeToggleInput();

        if (!_gameplayInputEnabled)
        {
            UpdateGameplayInputDisabled();
            return;
        }

        UpdateInput();
        UpdateState();
        UpdateMovement();
        UpdateAnimation();
        UpdateLocalHudCharge();
        SyncAnimationToServer();
    }

    private void LateUpdate()
    {
        if (!isLocalPlayer)
            return;

        UpdateAttackRangeDebugVisualizer();
    }

    private void OnGUI()
    {
        if (!isLocalPlayer || Time.time >= _localBlackoutEndTime)
            return;

        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.blackTexture);
    }

    [ServerCallback]
    private void UpdateServerDeathWatch()
    {
        if (_hp > 0 || _hasReportedDeathToMatch)
            return;

        Debug.Log($"{SurvivalMatchLogPrefix} Server death watch caught netId={netId}, hp={_hp}.");
        ServerHandleDeath();
    }

    [ServerCallback]
    private void UpdateServerPassiveRegen()
    {
        if (!CanServerPassiveRegen())
        {
            ResetServerPassiveRegen();
            return;
        }

        _serverStationaryTimer += Time.deltaTime;
        if (_serverStationaryTimer < stationaryRegenDelay)
        {
            _serverRegenTimer = 0f;
            return;
        }

        _serverRegenTimer += Time.deltaTime;
        if (_serverRegenTimer < stationaryRegenInterval)
            return;

        _serverRegenTimer -= stationaryRegenInterval;
        _hp = Mathf.Min(maxHp, _hp + stationaryRegenAmount);
    }

    [Server]
    private bool CanServerPassiveRegen()
    {
        return _hp > 0
            && _hp < maxHp
            && !_syncIsMoving
            && _syncPose == PlayerPose.Grounded;
    }

    [Server]
    private void ResetServerPassiveRegen()
    {
        _serverStationaryTimer = 0f;
        _serverRegenTimer = 0f;
    }

    private void HandleControlModeToggleInput()
    {
        if (Keyboard.current == null || !Keyboard.current[assemblyModeKey].wasPressedThisFrame)
            return;

        if (controlModeController == null)
            controlModeController = GetComponent<PlayerControlModeController>();

        controlModeController?.ToggleAssemblyMode();
        hud?.ToggleBackpack();
    }

    public void SetGameplayInputEnabled(bool enabledValue)
    {
        _gameplayInputEnabled = enabledValue;

        if (!_gameplayInputEnabled)
        {
            ResetLocalGameplayInput();
            return;
        }

        if (_currentPose != PlayerPose.Die && _currentPose != PlayerPose.Hit)
            _canChangeState = true;
    }

    private void UpdateGameplayInputDisabled()
    {
        ResetLocalGameplayInput();
        UpdateAnimation();
        UpdateLocalHudCharge();
    }

    private void ResetLocalGameplayInput()
    {
        _moveInput = Vector2.zero;
        _moveDirection = Vector3.zero;
        _isRunning = false;
        _jumpRequested = false;
        _wasJumpRequested = false;
        _isCharging = false;
        _chargeRatio = 0f;
        _dashDirection = Vector3.zero;
        _dashTimer = 0f;
        _attackFired = false;
        _verticalVelocity = 0f;

        if (_currentPose != PlayerPose.Die && _currentPose != PlayerPose.Hit)
        {
            _canChangeState = true;
            ChangeState(PlayerPose.Grounded);
            _moveState = MoveState.Idle;
        }

        if (_animator != null)
        {
            _animator.SetBool("IsMoving", false);
            _animator.SetBool("IsRunning", false);
            _animator.SetBool("IsStopping", false);
            _animator.SetFloat("SpeedX", 0f);
            _animator.SetFloat("SpeedZ", 0f);
            _animator.SetFloat("ChargeRatio", 0f);
        }
    }

    private void UpdateInput()
    {
        var shift = Keyboard.current.leftShiftKey;

        if (shift.wasPressedThisFrame)
            _shiftPressTime = Time.time;

        if (shift.wasReleasedThisFrame)
        {
            float holdTime = Time.time - _shiftPressTime;
            if (holdTime < dashTapThreshold
                && _canChangeState
                && _currentPose == PlayerPose.Grounded
                && !_isDashing)
            {
                if (_hasTimeHeadBlink)
                    TryRequestTimeBlink();
                else
                    StartDash();
            }
            _shiftPressTime = -1f;
        }

        _isRunning = shift.isPressed
            && _shiftPressTime >= 0
            && (Time.time - _shiftPressTime) >= dashTapThreshold;

        if (_isCharging && _currentPose == PlayerPose.Charging
            && !Mouse.current.leftButton.isPressed)
        {
            if (_audioSource != null)
            {
                _audioSource.loop = false;
                _audioSource.Stop();
                _audioSource.volume = 1f;
            }
            ReleaseChargedAttack();
        }
    }

    private void StartDash()
    {
        _isDashing = true;
        _dashElapsed = 0f;
        _dashDirection = _moveDirection != Vector3.zero
            ? _moveDirection.normalized
            : transform.forward;
        ChangeState(PlayerPose.Dash);
    }

    private void UpdateState()
    {
        if (_hitInvincibilityTimer > 0f)
            _hitInvincibilityTimer -= Time.deltaTime;

        if (_currentPose == PlayerPose.Die) return;
        if (!_canChangeState) return;

        if (_currentPose == PlayerPose.Dash && !_isDashing)
        {
            ChangeState(PlayerPose.Grounded);
            return;
        }

        if (_currentPose == PlayerPose.Jump && _verticalVelocity <= 0f
            && Time.time - _jumpStartTime >= MIN_JUMP_AIRTIME)
        {
            ChangeState(PlayerPose.Fall);
            return;
        }

        if (_motor.GroundingStatus.IsStableOnGround)
        {
            if (_wasJumpRequested)
            {
                _wasJumpRequested = false;
                return;
            }

            if (_currentPose == PlayerPose.Fall || _currentPose == PlayerPose.Jump)
            {
                _moveState = MoveState.Idle;
                ChangeState(PlayerPose.Grounded);
            }

            if (_currentPose == PlayerPose.Charging && !_isCharging)
                ChangeState(PlayerPose.Grounded);

            if (_currentPose != PlayerPose.Charging && _currentPose != PlayerPose.Dash)
                UpdateMoveState();
        }
        else
        {
            if (_currentPose == PlayerPose.Charging)
            {
                _isCharging = false;
                ChangeState(PlayerPose.Fall);
            }
            else if (_currentPose == PlayerPose.Grounded)
            {
                ChangeState(PlayerPose.Fall);
            }
        }
    }

    private void UpdateMoveState()
    {
        MoveState previousState = _moveState;

        if (_moveInput == Vector2.zero)
        {
            if (previousState == MoveState.Run)
            {
                _moveState = MoveState.Stop;
                _stopAnimTimer = 0f;
                _animator.SetBool("IsStopping", true);
            }
            else if (_moveState == MoveState.Stop)
            {
                _stopAnimTimer += Time.deltaTime;
                AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                bool animDone = stateInfo.normalizedTime >= 0.85f || _stopAnimTimer >= MAX_STOP_ANIM_DURATION;
                if (animDone)
                {
                    _stopAnimTimer = 0f;
                    _moveState = MoveState.Idle;
                    _animator.SetBool("IsStopping", false);
                }
            }
            else
            {
                _moveState = MoveState.Idle;
                _animator.SetBool("IsStopping", false);
            }
        }
        else
        {
            _animator.SetBool("IsStopping", false);
            _moveState = _isRunning ? MoveState.Run : MoveState.Walk;
        }

        _animator.SetBool("IsMoving", _moveInput != Vector2.zero);
        _animator.SetBool("IsRunning", _isRunning);

        if (previousState != _moveState)
        {
            if (_moveState == MoveState.Idle)
            {
                _animator.CrossFadeInFixedTime("idle", 0.15f);
                if (_audioSource != null && _audioSource.clip == _runSound)
                {
                    _audioSource.loop = false;
                    _audioSource.Stop();
                }
            }
            else if (_moveState == MoveState.Walk || _moveState == MoveState.Run)
            {
                _animator.CrossFadeInFixedTime("move", 0.15f);

                if (_moveState == MoveState.Run && _runSound != null && _audioSource != null)
                {
                    _audioSource.clip = _runSound;
                    _audioSource.loop = true;
                    _audioSource.Play();
                }
                else if (_moveState == MoveState.Walk && _audioSource != null
                         && _audioSource.clip == _runSound)
                {
                    _audioSource.loop = false;
                    _audioSource.Stop();
                }
            }
            else if (_moveState == MoveState.Stop)
            {
                if (_audioSource != null && _audioSource.clip == _runSound)
                {
                    _audioSource.loop = false;
                    _audioSource.Stop();
                }
            }
        }
    }

    private void UpdateMovement()
    {
        if (_currentPose == PlayerPose.Die || _currentPose == PlayerPose.Hit) return;
        if (_currentPose == PlayerPose.Attack) return;
        if (_currentPose == PlayerPose.Dash) return;

        if (_moveInput != Vector2.zero)
        {
            float moveAngle = Mathf.Atan2(_moveInput.x, _moveInput.y) * Mathf.Rad2Deg
                              + _mainCamera.transform.eulerAngles.y;
            _moveDirection = Quaternion.Euler(0f, moveAngle, 0f) * Vector3.forward;

            float facingAngle = _mainCamera.transform.eulerAngles.y;
            float rotation = Mathf.SmoothDampAngle(
                transform.eulerAngles.y, facingAngle,
                ref _rotationVelocity, rotationSmoothTime);
            _motor.SetRotation(Quaternion.Euler(0f, rotation, 0f));
        }
        else
        {
            _moveDirection = Vector3.zero;
        }
    }

    private void UpdateAnimation()
    {
        if (_currentPose == PlayerPose.Grounded || _currentPose == PlayerPose.Charging)
        {
            float targetX = 0f;
            float targetZ = 0f;

            if (_moveInput != Vector2.zero)
            {
                _lastDirX = _moveInput.x;
                _lastDirZ = _moveInput.y;
                targetX = _moveInput.x;
                targetZ = _moveInput.y;

                if (!_isRunning)
                {
                    targetX *= 0.5f;
                    targetZ *= 0.5f;
                }
            }

            _animator.SetFloat("SpeedX", Mathf.Lerp(_animator.GetFloat("SpeedX"), targetX, Time.deltaTime * 10f));
            _animator.SetFloat("SpeedZ", Mathf.Lerp(_animator.GetFloat("SpeedZ"), targetZ, Time.deltaTime * 10f));
            _animator.SetFloat("LastDirX", _lastDirX);
            _animator.SetFloat("LastDirZ", _lastDirZ);
        }

        if (_currentPose == PlayerPose.Charging)
        {
            _chargeRatio = Mathf.Clamp01((Time.time - _chargeStartTime) / maxChargeTime);
            _animator.SetFloat("ChargeRatio", _chargeRatio);
        }
    }

    private void SyncAnimationToServer()
    {
        if (!NetworkClient.active || !NetworkClient.ready) return;

        _syncTimer += Time.deltaTime;
        if (_syncTimer < SYNC_INTERVAL) return;
        _syncTimer = 0f;

        PlayerPose pose = _currentPose;
        float speedX = _animator.GetFloat("SpeedX");
        float speedZ = _animator.GetFloat("SpeedZ");
        float lastDirX = _lastDirX;
        float lastDirZ = _lastDirZ;
        bool isMoving = _animator.GetBool("IsMoving");
        bool isRunning = _animator.GetBool("IsRunning");
        bool isStopping = _animator.GetBool("IsStopping");
        float chargeRatio = _chargeRatio;

        if (!HasAnimationStateChanged(pose, speedX, speedZ, lastDirX, lastDirZ,
            isMoving, isRunning, isStopping, chargeRatio)) return;

        CacheSentAnimationState(pose, speedX, speedZ, lastDirX, lastDirZ,
            isMoving, isRunning, isStopping, chargeRatio);

        CmdSyncAnimState(pose, speedX, speedZ, lastDirX, lastDirZ,
            isMoving, isRunning, isStopping, chargeRatio);
    }

    private bool HasAnimationStateChanged(
        PlayerPose pose, float speedX, float speedZ,
        float lastDirX, float lastDirZ,
        bool isMoving, bool isRunning, bool isStopping, float chargeRatio)
    {
        if (!_hasSentAnimState) return true;
        if (_lastSentPose != pose) return true;
        if (_lastSentIsMoving != isMoving) return true;
        if (_lastSentIsRunning != isRunning) return true;
        if (_lastSentIsStopping != isStopping) return true;

        return Mathf.Abs(_lastSentSpeedX - speedX) > ANIM_FLOAT_SYNC_EPSILON
            || Mathf.Abs(_lastSentSpeedZ - speedZ) > ANIM_FLOAT_SYNC_EPSILON
            || Mathf.Abs(_lastSentLastDirX - lastDirX) > ANIM_FLOAT_SYNC_EPSILON
            || Mathf.Abs(_lastSentLastDirZ - lastDirZ) > ANIM_FLOAT_SYNC_EPSILON
            || Mathf.Abs(_lastSentChargeRatio - chargeRatio) > ANIM_FLOAT_SYNC_EPSILON;
    }

    private void CacheSentAnimationState(
        PlayerPose pose, float speedX, float speedZ,
        float lastDirX, float lastDirZ,
        bool isMoving, bool isRunning, bool isStopping, float chargeRatio)
    {
        _lastSentPose = pose;
        _lastSentSpeedX = speedX;
        _lastSentSpeedZ = speedZ;
        _lastSentLastDirX = lastDirX;
        _lastSentLastDirZ = lastDirZ;
        _lastSentIsMoving = isMoving;
        _lastSentIsRunning = isRunning;
        _lastSentIsStopping = isStopping;
        _lastSentChargeRatio = chargeRatio;
        _hasSentAnimState = true;
    }

    [Command(requiresAuthority = true)]
    private void CmdSyncAnimState(
        PlayerPose pose, float speedX, float speedZ,
        float lastDirX, float lastDirZ,
        bool isMoving, bool isRunning, bool isStopping, float chargeRatio)
    {
        _syncPose        = pose;
        _syncSpeedX      = speedX;
        _syncSpeedZ      = speedZ;
        _syncLastDirX    = lastDirX;
        _syncLastDirZ    = lastDirZ;
        _syncIsMoving    = isMoving;
        _syncIsRunning   = isRunning;
        _syncIsStopping  = isStopping;
        _syncChargeRatio = chargeRatio;
    }

    private void OnSyncPoseChanged(PlayerPose oldPose, PlayerPose newPose)
    {
        if (isLocalPlayer) return;
        ApplyPoseToAnimator(newPose);
    }

    private void OnHpChanged(int oldHp, int newHp)
    {
        _worldHealthBar?.UpdateHP(newHp, maxHp);
        UpdateLocalHudHealth(newHp);

        if (isServer && newHp <= 0 && !_hasReportedDeathToMatch)
        {
            Debug.Log($"{SurvivalMatchLogPrefix} HP hook caught death netId={netId}, oldHp={oldHp}, newHp={newHp}.");
            ServerHandleDeath();
        }
    }

    private void OnSyncSpeedXChanged(float oldVal, float newVal)
    {
        if (isLocalPlayer) return;
        _animator.SetFloat("SpeedX", newVal);
    }

    private void OnSyncSpeedZChanged(float oldVal, float newVal)
    {
        if (isLocalPlayer) return;
        _animator.SetFloat("SpeedZ", newVal);
    }

    private void OnSyncLastDirXChanged(float oldVal, float newVal)
    {
        if (isLocalPlayer) return;
        _animator.SetFloat("LastDirX", newVal);
    }

    private void OnSyncLastDirZChanged(float oldVal, float newVal)
    {
        if (isLocalPlayer) return;
        _animator.SetFloat("LastDirZ", newVal);
    }

    private void OnSyncIsMovingChanged(bool oldVal, bool newVal)
    {
        if (isLocalPlayer) return;
        _animator.SetBool("IsMoving", newVal);
    }

    private void OnSyncIsRunningChanged(bool oldVal, bool newVal)
    {
        if (isLocalPlayer) return;
        _animator.SetBool("IsRunning", newVal);
    }

    private void OnSyncIsStoppingChanged(bool oldVal, bool newVal)
    {
        if (isLocalPlayer) return;
        _animator.SetBool("IsStopping", newVal);
    }

    private void OnSyncChargeRatioChanged(float oldVal, float newVal)
    {
        if (isLocalPlayer) return;
        _animator.SetFloat("ChargeRatio", newVal);
    }

    private void ApplyPoseToAnimator(PlayerPose pose)
    {
        switch (pose)
        {
            case PlayerPose.Grounded:  _animator.CrossFadeInFixedTime("idle",    0.2f);  break;
            case PlayerPose.Charging:  _animator.CrossFadeInFixedTime("Charging",0.15f); break;
            case PlayerPose.Jump:      _animator.CrossFadeInFixedTime("Jump",    0.1f);  break;
            case PlayerPose.Fall:      _animator.CrossFadeInFixedTime("Fall",    0.1f);  break;
            case PlayerPose.Attack:    _animator.CrossFadeInFixedTime("Attack",  0.05f); break;
            case PlayerPose.Hit:       _animator.CrossFadeInFixedTime("hurt",    0.1f);  break;
            case PlayerPose.Die:       _animator.CrossFadeInFixedTime("死亡",    0.1f);  break;
            case PlayerPose.Dash:      _animator.CrossFadeInFixedTime("Dash",    0.05f); break;
        }
    }

    private void ChangeState(PlayerPose newPose)
    {
        if (_currentPose == newPose) return;
        _currentPose = newPose;

        switch (newPose)
        {
            case PlayerPose.Grounded:
                _animator.CrossFadeInFixedTime("idle", 0.2f);
                break;
            case PlayerPose.Charging:
                if (!_animator.GetCurrentAnimatorStateInfo(0).IsName("Charging"))
                    _animator.CrossFadeInFixedTime("Charging", 0.15f);
                else
                    _animator.Play("Charging", 0, 4f / 30f);
                if (_audioSource != null && _chargeSound != null)
                {
                    _audioSource.clip = _chargeSound;
                    _audioSource.loop = true;
                    _audioSource.volume = 0.25f;
                    _audioSource.Play();
                }
                break;
            case PlayerPose.Jump:
                _jumpStartTime = Time.time;
                _animator.CrossFadeInFixedTime("Jump", 0.1f);
                if (_audioSource != null && _audioSource.clip == _runSound)
                {
                    _audioSource.loop = false;
                    _audioSource.Stop();
                }
                PlaySound(_jumpSound, 0.19f);
                break;
            case PlayerPose.Fall:
                _animator.CrossFadeInFixedTime("Fall", 0.15f);
                break;
            case PlayerPose.Attack:
                _animator.CrossFadeInFixedTime("Attack", 0.05f);
                _canChangeState = false;
                PlayEquippedPartAttackPresentation();
                PlaySound(_attackSound, 0.25f);
                break;
            case PlayerPose.Hit:
                _animator.CrossFadeInFixedTime("hurt", 0.1f);
                _canChangeState = false;
                PlaySound(_hitSound, 0.25f);
                break;
            case PlayerPose.Die:
                _animator.CrossFadeInFixedTime("死亡", 0.1f);
                _canChangeState = false;
                PlaySound(_dieSound, 0.5f);
                break;
            case PlayerPose.Dash:
                _animator.CrossFadeInFixedTime(GetDashAnimName(), 0.05f);
                PlaySound(_dashSound, 0.25f);
                break;
        }
    }

    public void OnActionComplete()
    {
        if (_currentPose == PlayerPose.Die) return;

        _attackFired = false;
        _canChangeState = true;
        ChangeState(PlayerPose.Grounded);
    }

    private void PlayEquippedPartAttackPresentation()
    {
        EquippedPartPresentation[] equippedPartPresentations = GetComponentsInChildren<EquippedPartPresentation>(true);

        for (int i = 0; i < equippedPartPresentations.Length; i++)
        {
            EquippedPartPresentation equippedPartPresentation = equippedPartPresentations[i];

            if (equippedPartPresentation == null)
            {
                continue;
            }

            equippedPartPresentation.PlayAttack();
        }
    }

    /// <summary>
    /// Shows or hides the local attack range debug circle with the given color.
    /// </summary>
    public void SetAttackRangeDebugVisible(bool visible, Color color)
    {
        _showAttackRangeDebug = visible;
        _attackRangeDebugColor = color;

        if (!_showAttackRangeDebug && _attackRangeDebugRenderer != null)
            _attackRangeDebugRenderer.enabled = false;
    }

    /// <summary>
    /// Returns whether the local attack range debug circle is currently visible.
    /// </summary>
    public bool IsAttackRangeDebugVisible()
    {
        return _showAttackRangeDebug;
    }

    private string GetDashAnimName()
    {
        Vector3 localDir = transform.InverseTransformDirection(_dashDirection);
        float x = localDir.x;
        float z = localDir.z;

        if (Mathf.Abs(z) >= Mathf.Abs(x))
            return z >= 0 ? "forward dash" : "back dash";
        else
            return x >= 0 ? "right dash" : "left dash";
    }

    public void OnStopAnimationComplete()
    {
        if (_moveState != MoveState.Stop) return;
        _stopAnimTimer = 0f;
        _moveState = MoveState.Idle;
        _animator.SetBool("IsStopping", false);
        _animator.CrossFadeInFixedTime("idle", 0.15f);
    }

    public void OnAttackHit()
    {
        if (!isLocalPlayer) return;
        if (!_gameplayInputEnabled) return;
        if (!NetworkClient.active || !NetworkClient.ready) return;
        TriggerLocalAttack();
    }

    private void TriggerLocalAttack()
    {
        if (_attackFired) return;
        _attackFired = true;

        impulseSource?.GenerateImpulse(cameraShakeStrength);
        StartCoroutine(HitStop(hitStopDuration));

        if (NetworkClient.active && NetworkClient.ready)
            CmdDoAttack(transform.forward, _activeLocalAttackSequence);
    }

    private void ReleaseChargedAttack()
    {
        if (!_isCharging)
            return;

        if (_audioSource != null)
        {
            _audioSource.loop = false;
            _audioSource.Stop();
            _audioSource.volume = 1f;
        }

        _isCharging = false;
        _chargeRatio = Mathf.Clamp01((Time.time - _chargeStartTime) / maxChargeTime);
        _dashDirection = _moveDirection != Vector3.zero
            ? _moveDirection.normalized
            : transform.forward;
        _dashTimer = _hasMotionHeadWideAttack ? 0f : dashDuration;
        _attackFired = false;
        _activeLocalAttackSequence = ++_localAttackSequence;
        ChangeState(PlayerPose.Attack);

        if (_hasMotionHeadWideAttack)
            TriggerLocalAttack();
    }

    private IEnumerator HitStop(float duration)
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    private void TryRequestTimeBlink()
    {
        if (!_hasTimeHeadBlink || Time.time < _nextLocalTimeBlinkTime)
            return;

        _nextLocalTimeBlinkTime = Time.time + timeBlinkCooldown;
        CmdRequestTimeBlink();
    }

    [Command]
    private void CmdRequestTimeBlink()
    {
        if (_hp <= 0 || !_hasTimeHeadBlink || Time.time < _nextServerTimeBlinkTime)
            return;

        _nextServerTimeBlinkTime = Time.time + timeBlinkCooldown;
        Vector3 destination = ResolveTimeBlinkDestination();
        ServerSetBlinkPosition(destination);

        if (connectionToClient != null)
            TargetApplyTimeBlink(connectionToClient, destination);

        RpcPlayTimeBlinkEffect(destination);
    }

    [Server]
    private Vector3 ResolveTimeBlinkDestination()
    {
        Vector3 forward = GetFlattenedAttackForward(transform.forward);
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        for (int i = 0; i < timeBlinkMaxPositionAttempts; i++)
        {
            float sideOffset = UnityEngine.Random.Range(-timeBlinkHalfExtent, timeBlinkHalfExtent);
            float forwardOffset = UnityEngine.Random.Range(0f, timeBlinkHalfExtent);
            Vector3 candidate = transform.position + right * sideOffset + forward * forwardOffset;

            if (TryResolveGroundedBlinkDestination(candidate, out Vector3 groundedDestination))
                return groundedDestination;
        }

        return transform.position;
    }

    private bool TryResolveGroundedBlinkDestination(Vector3 candidate, out Vector3 destination)
    {
        Vector3 rayOrigin = candidate + Vector3.up * timeBlinkGroundRayHeight;

        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, timeBlinkGroundRayDistance, timeBlinkGroundMask, QueryTriggerInteraction.Ignore))
        {
            destination = transform.position;
            return false;
        }

        destination = groundHit.point + Vector3.up * timeBlinkGroundOffset;
        return IsBlinkCapsuleClear(destination);
    }

    private bool IsBlinkCapsuleClear(Vector3 destination)
    {
        float radius = Mathf.Max(0f, timeBlinkCapsuleRadius);
        float height = Mathf.Max(radius * 2f, timeBlinkCapsuleHeight);
        float yOffset = timeBlinkCapsuleYOffset;
        float clampedHalfSegment = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 capsuleCenter = destination + Vector3.up * yOffset;
        Vector3 capsuleBottom = capsuleCenter + Vector3.down * clampedHalfSegment;
        Vector3 capsuleTop = capsuleCenter + Vector3.up * clampedHalfSegment;
        Collider[] overlaps = Physics.OverlapCapsule(capsuleBottom, capsuleTop, radius, timeBlinkBlockMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];

            if (overlap == null || overlap.transform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }

    [Server]
    private void ServerSetBlinkPosition(Vector3 destination)
    {
        transform.position = destination;

        if (_motor != null && _motor.enabled)
            _motor.SetPosition(destination);
    }

    [TargetRpc]
    private void TargetApplyTimeBlink(NetworkConnectionToClient targetConnection, Vector3 destination)
    {
        transform.position = destination;

        if (_motor != null)
            _motor.SetPosition(destination);
    }

    [ClientRpc]
    private void RpcPlayTimeBlinkEffect(Vector3 destination)
    {
        PlaySpecialEffect(timeBlinkEffectPrefab, destination, Quaternion.identity);
        timeBlinkEffectRequested?.Invoke(destination);
    }

    [Command]
    private void CmdDoAttack(Vector3 attackerForward, int attackSequence)
    {
        _attackHitTargetIds.Clear();

        if (_hasMotionHeadWideAttack)
            ServerApplyMotionFanAttack(attackerForward);
        else
            ServerApplyBoxAttack(attackerForward);

        if (_hasMotionHeadWideAttack
            && attackSequence != _lastServerMotionAttackEffectSequence
            && Time.time >= _lastServerMotionAttackEffectTime + motionAttackEffectMinInterval)
        {
            _lastServerMotionAttackEffectTime = Time.time;
            _lastServerMotionAttackEffectSequence = attackSequence;
            RpcPlayMotionAttackEffect(attackerForward);
        }
    }

    [Server]
    private void ServerApplyBoxAttack(Vector3 attackerForward)
    {
        Quaternion attackRotation = GetAttackBoxRotation(attackerForward);
        Vector3 hitPoint = GetAttackBoxCenter(attackerForward);
        Collider[] hits = Physics.OverlapBox(hitPoint, GetCurrentAttackBoxHalfExtents(), attackRotation, attackHitMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            TryApplyAttackHit(hits[i], attackerForward, false);
        }
    }

    [Server]
    private void ServerApplyMotionFanAttack(Vector3 attackerForward)
    {
        Vector3 origin = GetMotionAttackFanOrigin(attackerForward);
        float radius = Mathf.Max(0f, motionAttackFanRadius);
        Collider[] hits = Physics.OverlapSphere(origin, radius, attackHitMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];

            if (!IsInsideMotionAttackFan(hit, attackerForward, origin))
                continue;

            TryApplyAttackHit(hit, attackerForward, true);
        }
    }

    [Server]
    private bool TryApplyAttackHit(Collider hit, Vector3 attackerForward, bool applyMotionBlackout)
    {
        if (hit == null || hit.gameObject == gameObject)
            return false;

        PlayCol playerTarget = hit.GetComponentInParent<PlayCol>();
        if (playerTarget != null)
        {
            if (playerTarget == this || !TryRegisterAttackTarget(playerTarget))
                return false;

            playerTarget.TakeDamage(attackDamage, attackerForward);

            if (applyMotionBlackout && playerTarget.connectionToClient != null)
                playerTarget.TargetRequestBlackout(playerTarget.connectionToClient, motionBlackoutDuration);

            RpcPlayHitEffect(GetHitEffectPosition(hit, playerTarget.transform));
            return true;
        }

        MonsterAI monsterTarget = hit.GetComponentInParent<MonsterAI>();
        if (monsterTarget == null || !TryRegisterAttackTarget(monsterTarget))
            return false;

        monsterTarget.TakeDamage(attackDamage, attackerForward, this);
        RpcPlayHitEffect(GetHitEffectPosition(hit, monsterTarget.transform));
        return true;
    }

    private bool TryRegisterAttackTarget(Component target)
    {
        return target != null && _attackHitTargetIds.Add(target.GetInstanceID());
    }

    private Vector3 GetHitEffectPosition(Collider hitCollider, Transform targetTransform)
    {
        if (hitCollider != null)
        {
            Vector3 closestPoint = hitCollider.ClosestPoint(transform.position);
            if ((closestPoint - transform.position).sqrMagnitude > Mathf.Epsilon)
                return closestPoint;
        }

        if (targetTransform != null)
            return targetTransform.position + Vector3.up;

        return GetAttackBoxCenter(transform.forward);
    }

    [ClientRpc]
    private void RpcPlayHitEffect(Vector3 position)
    {
        if (hitEffectPrefab == null) return;

        GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
        if (hitEffectLifetime > 0f)
            Destroy(effect, hitEffectLifetime);
    }

    [ClientRpc]
    private void RpcPlayMotionAttackEffect(Vector3 attackerForward)
    {
        PlaySpecialEffect(motionAttackEffectPrefab, GetMotionAttackEffectPosition(attackerForward), GetMotionAttackEffectRotation(attackerForward));
    }

    [TargetRpc]
    private void TargetRequestBlackout(NetworkConnectionToClient targetConnection, float duration)
    {
        _localBlackoutEndTime = Time.time + Mathf.Max(0f, duration);
        blackoutRequested?.Invoke(duration);
    }

    private void ServerUpdateVirusPoisonTrail()
    {
        if (!isServer)
            return;

        ServerUpdateVirusPoisonCircles();

        if (_hp <= 0 || !_hasVirusHeadPoisonTrail || Time.time < _nextServerVirusTrailTickTime)
            return;

        _nextServerVirusTrailTickTime = Time.time + virusPoisonCircleSpawnInterval;
        ServerSpawnVirusPoisonCircle();
    }

    [Server]
    private void ServerSpawnVirusPoisonCircle()
    {
        float radius = GetCurrentVirusPoisonCircleRadius();
        VirusPoisonCircleRuntime poisonCircle = new VirusPoisonCircleRuntime(
            transform.position,
            radius,
            Time.time + virusPoisonCircleDuration,
            Time.time
        );

        _activeVirusPoisonCircles.Add(poisonCircle);
        RpcPlayVirusTrailEffect(poisonCircle.center, radius * 2f, virusPoisonCircleDuration);
    }

    [Server]
    private void ServerUpdateVirusPoisonCircles()
    {
        for (int i = _activeVirusPoisonCircles.Count - 1; i >= 0; i--)
        {
            VirusPoisonCircleRuntime poisonCircle = _activeVirusPoisonCircles[i];

            if (Time.time >= poisonCircle.endTime)
            {
                _activeVirusPoisonCircles.RemoveAt(i);
                continue;
            }

            if (Time.time < poisonCircle.nextTickTime)
                continue;

            poisonCircle.nextTickTime = Time.time + virusPoisonTrailTickInterval;
            _activeVirusPoisonCircles[i] = poisonCircle;
            ServerApplyVirusPoisonCircle(poisonCircle);
        }
    }

    [Server]
    private void ServerApplyVirusPoisonCircle(VirusPoisonCircleRuntime poisonCircle)
    {
        float height = Mathf.Max(0.01f, virusPoisonCircleHeight);
        Vector3 capsuleBottom = poisonCircle.center;
        Vector3 capsuleTop = poisonCircle.center + Vector3.up * height;
        Collider[] hits = Physics.OverlapCapsule(capsuleBottom, capsuleTop, poisonCircle.radius, virusPoisonHitMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            PlayCol target = hits[i] != null ? hits[i].GetComponentInParent<PlayCol>() : null;

            if (target == null || target == this)
                continue;

            target.ServerApplyPoison(virusPoisonDuration);
        }
    }

    [ClientRpc]
    private void RpcPlayVirusTrailEffect(Vector3 center, float diameter, float duration)
    {
        float visualScale = Mathf.Approximately(virusPoisonCircleBaseDiameter, 0f)
            ? 1f
            : diameter / virusPoisonCircleBaseDiameter;
        PlaySpecialEffect(virusTrailEffectPrefab, center, Quaternion.identity, visualScale, duration);
        virusTrailEffectRequested?.Invoke(this);
    }

    private void PlaySpecialEffect(ParticleSystem effectPrefab, Vector3 position, Quaternion rotation)
    {
        PlaySpecialEffect(effectPrefab, position, rotation, 1f, specialEffectLifetime);
    }

    private void PlaySpecialEffect(ParticleSystem effectPrefab, Vector3 position, Quaternion rotation, float uniformScale, float lifetime)
    {
        if (effectPrefab == null)
            return;

        ParticleSystem effectInstance;
        Vector3 effectScale = Vector3.one * Mathf.Max(0.01f, uniformScale);

        if (effectPrefab.gameObject.scene.IsValid())
        {
            effectInstance = effectPrefab;
            effectInstance.transform.SetPositionAndRotation(position, rotation);
            effectInstance.transform.localScale = effectScale;
            effectInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        else
        {
            effectInstance = Instantiate(effectPrefab, position, rotation);
            effectInstance.transform.localScale = effectScale;
        }

        effectInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        effectInstance.Play(true);

        if (!effectPrefab.gameObject.scene.IsValid() && lifetime > 0f)
            Destroy(effectInstance.gameObject, lifetime);
    }

    [Server]
    private void ServerApplyPoison(float duration)
    {
        _poisonEndTime = Mathf.Max(_poisonEndTime, Time.time + Mathf.Max(0f, duration));

        if (_nextPoisonDamageTime < Time.time)
            _nextPoisonDamageTime = Time.time;
    }

    private void ServerUpdatePoisonState()
    {
        if (!isServer || Time.time >= _poisonEndTime || Time.time < _nextPoisonDamageTime)
            return;

        _nextPoisonDamageTime = Time.time + virusPoisonTrailTickInterval;
        ServerTakePoisonDamage(virusPoisonDamage);
    }

    [Server]
    private void ServerTakePoisonDamage(int damage)
    {
        if (_hp <= 0 || damage <= 0)
            return;

        _hp = Mathf.Max(0, _hp - damage);
        ResetServerPassiveRegen();

        if (_hp <= 0)
            ServerHandleDeath();
    }

    private void CacheSpecialEffectReferences()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventoryNetwork>();

        if (gameDatabase == null && inventory != null)
            gameDatabase = inventory.AssignedGameDatabase;
    }

    private void SubscribeSpecialEffectInventory()
    {
        CacheSpecialEffectReferences();

        if (inventory == null || _isSubscribedToInventory)
            return;

        inventory.OnInventoryChanged += RecalculateEquippedSpecialEffects;
        _isSubscribedToInventory = true;
    }

    private void UnsubscribeSpecialEffectInventory()
    {
        if (inventory == null || !_isSubscribedToInventory)
            return;

        inventory.OnInventoryChanged -= RecalculateEquippedSpecialEffects;
        _isSubscribedToInventory = false;
    }

    /// <summary>
    /// Rebuilds runtime special effects from the currently equipped parts.
    /// </summary>
    public void RecalculateEquippedSpecialEffects()
    {
        CacheSpecialEffectReferences();

        _equippedJumpForceMultiplier = 1f;
        _equippedAttackRangeMultiplier = 1f;
        _equippedVirusHeadCount = 0;
        _hasVirusHeadPoisonTrail = false;
        _hasTimeHeadBlink = false;
        _hasMotionHeadWideAttack = false;

        if (inventory != null)
        {
            for (int i = 0; i < inventory.EquippedParts.Count; i++)
            {
                ApplyEquippedPartSpecialEffect(inventory.EquippedParts[i].partID);
            }
        }
    }

    private void ApplyEquippedPartSpecialEffect(int partID)
    {
        PartData partData = gameDatabase != null ? gameDatabase.GetPartData(partID) : null;
        PartSpecialEffect specialEffect = PartSpecialEffect.None;

        if (partData != null)
        {
            _equippedJumpForceMultiplier *= Mathf.Max(0.01f, partData.jumpHeightMultiplier);
            _equippedAttackRangeMultiplier *= Mathf.Max(0.01f, partData.attackRangeMultiplier);
            specialEffect = partData.specialEffect;
            ApplyPartSpecialEffect(specialEffect);
        }

        if (specialEffect == PartSpecialEffect.None)
            ApplyKnownSpecialPartFallback(partID);
    }

    private void ApplyPartSpecialEffect(PartSpecialEffect specialEffect)
    {
        switch (specialEffect)
        {
            case PartSpecialEffect.VirusHeadPoisonTrail:
                _equippedVirusHeadCount++;
                _hasVirusHeadPoisonTrail = true;
                break;

            case PartSpecialEffect.TimeHeadBlink:
                _hasTimeHeadBlink = true;
                break;

            case PartSpecialEffect.MotionHeadWideAttack:
                _hasMotionHeadWideAttack = true;
                break;
        }
    }

    private void ApplyKnownSpecialPartFallback(int partID)
    {
        switch (partID)
        {
            case VirusHeadPartID:
                _equippedVirusHeadCount++;
                _hasVirusHeadPoisonTrail = true;
                break;

            case TimeHeadPartID:
                _hasTimeHeadBlink = true;
                break;

            case MotionHatPartID:
                _hasMotionHeadWideAttack = true;
                break;
        }
    }

    private float GetCurrentVirusPoisonCircleRadius()
    {
        int extraHeadCount = Mathf.Max(0, _equippedVirusHeadCount - 1);
        float diameter = virusPoisonCircleBaseDiameter + extraHeadCount * virusPoisonCircleDiameterPerExtraHead;
        return Mathf.Max(0f, diameter * 0.5f);
    }

    private float GetCurrentAttackRange()
    {
        return Mathf.Max(0f, attackRange * GetCurrentAttackRangeMultiplier());
    }

    private Vector3 GetCurrentAttackBoxHalfExtents()
    {
        return new Vector3(
            GetCurrentAttackRange() * 0.5f,
            Mathf.Max(0.01f, attackBoxHeight * 0.5f),
            GetCurrentAttackHalfDepth() * 0.5f
        );
    }

    private Vector3 GetAttackBoxCenter(Vector3 attackerForward)
    {
        Vector3 forward = GetFlattenedAttackForward(attackerForward);
        return transform.position + forward * (GetCurrentAttackHalfDepth() * 0.5f) + Vector3.up * (attackBoxHeight * 0.5f);
    }

    private Quaternion GetAttackBoxRotation(Vector3 attackerForward)
    {
        return Quaternion.LookRotation(GetFlattenedAttackForward(attackerForward), Vector3.up);
    }

    private Vector3 GetMotionAttackEffectPosition(Vector3 attackerForward)
    {
        return GetAttackBoxCenter(attackerForward) + motionAttackEffectPositionOffset;
    }

    private Vector3 GetMotionAttackFanOrigin(Vector3 attackerForward)
    {
        Vector3 forward = GetFlattenedAttackForward(attackerForward);
        return transform.position + forward * Mathf.Max(0f, motionAttackFanForwardOffset) + Vector3.up * (motionAttackFanHeight * 0.5f);
    }

    private bool IsInsideMotionAttackFan(Collider hit, Vector3 attackerForward, Vector3 origin)
    {
        if (hit == null || hit.transform.IsChildOf(transform))
            return false;

        Vector3 closestPoint = hit.ClosestPoint(origin);
        Vector3 toTarget = closestPoint - origin;
        float halfHeight = Mathf.Max(0.01f, motionAttackFanHeight * 0.5f);

        if (Mathf.Abs(toTarget.y) > halfHeight)
            return false;

        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= Mathf.Epsilon)
            return true;

        Vector3 forward = GetFlattenedAttackForward(attackerForward);
        float angle = Vector3.Angle(forward, toTarget.normalized);
        return angle <= motionAttackFanAngle * 0.5f;
    }

    private Quaternion GetMotionAttackEffectRotation(Vector3 attackerForward)
    {
        return GetAttackBoxRotation(attackerForward) * Quaternion.Euler(motionAttackEffectEulerOffset);
    }

    private Vector3 GetFlattenedAttackForward(Vector3 attackerForward)
    {
        Vector3 forward = attackerForward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= Mathf.Epsilon)
            forward = transform.forward;

        forward.y = 0f;
        return forward.sqrMagnitude > Mathf.Epsilon ? forward.normalized : Vector3.forward;
    }

    private float GetCurrentAttackHalfDepth()
    {
        return Mathf.Max(0f, attackRadius * GetCurrentAttackRangeMultiplier());
    }

    private float GetCurrentAttackRangeMultiplier()
    {
        float multiplier = _equippedAttackRangeMultiplier;

        if (_hasMotionHeadWideAttack)
            multiplier = Mathf.Max(multiplier, motionAttackRangeMultiplier);

        return multiplier;
    }

    private void UpdateAttackRangeDebugVisualizer()
    {
        if (!_showAttackRangeDebug)
        {
            if (_attackRangeDebugRenderer != null)
                _attackRangeDebugRenderer.enabled = false;

            return;
        }

        EnsureAttackRangeDebugRenderer();

        if (_attackRangeDebugRenderer == null)
            return;

        _attackRangeDebugRenderer.enabled = true;
        _attackRangeDebugRenderer.startColor = _attackRangeDebugColor;
        _attackRangeDebugRenderer.endColor = _attackRangeDebugColor;
        _attackRangeDebugRenderer.startWidth = attackRangeDebugLineWidth;
        _attackRangeDebugRenderer.endWidth = attackRangeDebugLineWidth;

        if (_hasMotionHeadWideAttack)
        {
            UpdateMotionAttackFanDebugVisualizer();
            return;
        }

        _attackRangeDebugRenderer.positionCount = 5;

        Vector3 forward = GetFlattenedAttackForward(transform.forward);
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float depth = GetCurrentAttackHalfDepth();
        float halfWidth = GetCurrentAttackRange() * 0.5f;
        Vector3 origin = transform.position + Vector3.up * attackRangeDebugHeightOffset;
        Vector3 frontCenter = origin + forward * depth;

        _attackRangeDebugRenderer.SetPosition(0, origin - right * halfWidth);
        _attackRangeDebugRenderer.SetPosition(1, origin + right * halfWidth);
        _attackRangeDebugRenderer.SetPosition(2, frontCenter + right * halfWidth);
        _attackRangeDebugRenderer.SetPosition(3, frontCenter - right * halfWidth);
        _attackRangeDebugRenderer.SetPosition(4, origin - right * halfWidth);
    }

    private void UpdateMotionAttackFanDebugVisualizer()
    {
        int positionCount = MotionAttackFanDebugSegments + 3;
        _attackRangeDebugRenderer.positionCount = positionCount;

        Vector3 origin = transform.position + Vector3.up * attackRangeDebugHeightOffset;
        Vector3 forward = GetFlattenedAttackForward(transform.forward);
        Quaternion leftRotation = Quaternion.AngleAxis(-motionAttackFanAngle * 0.5f, Vector3.up);
        float radius = Mathf.Max(0f, motionAttackFanRadius);

        _attackRangeDebugRenderer.SetPosition(0, origin);

        for (int i = 0; i <= MotionAttackFanDebugSegments; i++)
        {
            float t = i / (float)MotionAttackFanDebugSegments;
            float angle = motionAttackFanAngle * t;
            Vector3 arcDirection = Quaternion.AngleAxis(angle, Vector3.up) * (leftRotation * forward);
            _attackRangeDebugRenderer.SetPosition(i + 1, origin + arcDirection * radius);
        }

        _attackRangeDebugRenderer.SetPosition(positionCount - 1, origin);
    }

    private void EnsureAttackRangeDebugRenderer()
    {
        if (_attackRangeDebugRenderer != null)
            return;

        GameObject debugObject = new GameObject("Attack Range Debug");
        debugObject.transform.SetParent(transform, false);
        _attackRangeDebugRenderer = debugObject.AddComponent<LineRenderer>();
        _attackRangeDebugRenderer.loop = false;
        _attackRangeDebugRenderer.useWorldSpace = true;
        _attackRangeDebugRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _attackRangeDebugRenderer.receiveShadows = false;
        _attackRangeDebugRenderer.material = CreateAttackRangeDebugMaterial();
    }

    private Material CreateAttackRangeDebugMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        return shader != null ? new Material(shader) : null;
    }

    [Command]
    public void CmdTryPickupSceneLoot(int lootId)
    {
        if (SceneLootPickup.ServerTryPickup(lootId, this, 2f))
            RpcRemoveSceneLoot(lootId);
    }

    [ClientRpc]
    private void RpcRemoveSceneLoot(int lootId)
    {
        SceneLootPickup.ClientRemoveLoot(lootId);
    }

    [ClientRpc]
    public void RpcSpawnSceneLoot(int lootId, int partId, int count, Vector3 position)
    {
        SceneLootPickup.ClientSpawnLoot(lootId, partId, count, position, null);
    }

    [Server]
    public void TakeDamage(int damage, Vector3 attackerForward)
    {
        if (_hp <= 0) return;
        if (_currentPose == PlayerPose.Dash) return;

        if (Time.time < _serverInvincibilityEndTime) return;
        _serverInvincibilityEndTime = Time.time + hitInvincibilityDuration;

        int finalDamage = Mathf.Max(MIN_DAMAGE_AFTER_DEFENSE, damage - _currentDefense);
        _hp -= finalDamage;
        ResetServerPassiveRegen();
        if (_hp <= 0)
        {
            _hp = 0;
            ServerHandleDeath();
        }
        else
        {
            RpcOnHit(attackerForward);
        }
    }

    [Server]
    private void ServerHandleDeath()
    {
        if (_hasReportedDeathToMatch)
            return;

        _hasReportedDeathToMatch = true;
        Debug.Log($"{SurvivalMatchLogPrefix} Player death reported netId={netId}, name={name}.");
        RpcOnDie();
        SurvivalMatchController.EnsureServerInstance()?.ServerReportPlayerDied(this);
    }

    [ClientRpc]
    private void RpcOnHit(Vector3 attackerForward)
    {
        _hitInvincibilityTimer = hitInvincibilityDuration;

        if (_currentPose == PlayerPose.Die) return;

        _dashDirection = -attackerForward;
        _dashTimer = hitKnockbackTime;

        if (_currentPose == PlayerPose.Attack || _currentPose == PlayerPose.Charging)
        {
            _canChangeState = true;
            _isCharging = false;
            _dashTimer = hitKnockbackTime;
        }

        _currentPose = PlayerPose.Grounded;
        ChangeState(PlayerPose.Hit);
    }

    [ClientRpc]
    private void RpcOnDie()
    {
        ChangeState(PlayerPose.Die);

        if (isLocalPlayer && hud == null)
            hud = PlayerHUD.GetOrCreateRuntimeHud();

        if (isLocalPlayer && hud != null)
            hud.ShowGameOver();
    }

    [ClientRpc]
    public void RpcShowMatchResult(uint winnerNetId)
    {
        if (!isLocalPlayer)
            return;

        if (hud == null)
            hud = PlayerHUD.GetOrCreateRuntimeHud();

        bool isWinner = netId == winnerNetId;
        hud?.ShowResult(isWinner, RequestReturnToLobby, isServer);
        SetGameplayInputEnabled(false);
    }

    private void RequestReturnToLobby()
    {
        if (!isLocalPlayer || !NetworkClient.active || !NetworkClient.ready)
            return;

        CmdRequestReturnToLobby();
    }

    [Command]
    private void CmdRequestReturnToLobby()
    {
        SurvivalMatchController.GetServerInstance()?.ServerRequestReturnToLobby(this);
    }

    public void OnMove(InputValue value)
    {
        if (!isLocalPlayer) return;
        if (!_gameplayInputEnabled)
        {
            _moveInput = Vector2.zero;
            return;
        }
        _moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (!isLocalPlayer) return;
        if (!_gameplayInputEnabled) return;

        if (value.isPressed && _currentPose == PlayerPose.Grounded
            && _motor.GroundingStatus.IsStableOnGround && _canChangeState
            && Time.time >= _nextJumpTime)
        {
            if (_isCharging) _isCharging = false;
            _jumpRequested = true;
            _wasJumpRequested = true;
            _nextJumpTime = Time.time + jumpCooldown;
            ChangeState(PlayerPose.Jump);
        }
    }

    public void OnAttack(InputValue value)
    {
        if (!isLocalPlayer) return;
        if (!_gameplayInputEnabled) return;

        if (value.isPressed)
        {
            if (_canChangeState && _currentPose == PlayerPose.Grounded)
            {
                _isCharging = true;
                _chargeStartTime = Time.time;
                _chargeRatio = 0f;
                ChangeState(PlayerPose.Charging);
            }
        }
        else
        {
            if (_isCharging)
            {
                ReleaseChargedAttack();
            }
        }
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (!isLocalPlayer) return;

        if (!_gameplayInputEnabled)
        {
            currentVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            return;
        }

        if (_isDashing)
        {
            _dashElapsed += deltaTime;
            if (_dashElapsed >= dashTime)
                _isDashing = false;
            else
            {
                currentVelocity = _dashDirection * dashSpeed;
                currentVelocity.y = 0f;
                return;
            }
        }

        if (_dashTimer > 0f)
        {
            _dashTimer -= deltaTime;
            currentVelocity = _dashDirection * (dashDistance / dashDuration);
            currentVelocity.y = 0f;

            if (_currentPose == PlayerPose.Attack && !_attackFired)
            {
                TriggerLocalAttack();
            }

            return;
        }

        if (_currentPose == PlayerPose.Attack && _hasMotionHeadWideAttack)
        {
            currentVelocity = Vector3.up * _verticalVelocity;
            return;
        }

        if (_currentPose == PlayerPose.Hit || _currentPose == PlayerPose.Die)
        {
            currentVelocity = Vector3.zero;
            return;
        }

        float speed = _isRunning ? runSpeed : walkSpeed;

        if (_currentPose == PlayerPose.Charging)
            speed *= chargeMoveSpeedMultiplier;

        currentVelocity = _moveDirection.normalized * speed * _moveInput.magnitude;

        if (_jumpRequested)
        {
            _verticalVelocity = jumpForce * _equippedJumpForceMultiplier;
            _jumpRequested = false;
            _motor.ForceUnground();
        }
        else if (_motor.GroundingStatus.IsStableOnGround)
        {
            _verticalVelocity = 0f;
        }
        else
        {
            _verticalVelocity += gravity * deltaTime;
        }

        currentVelocity.y = _verticalVelocity;
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime) { }
    public void BeforeCharacterUpdate(float deltaTime) { }
    public void PostGroundingUpdate(float deltaTime) { }
    public void AfterCharacterUpdate(float deltaTime) { }
    public bool IsColliderValidForCollisions(Collider coll) => true;
    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport) { }
    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport) { }
    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        Vector3 atCharacterPosition, Quaternion atCharacterRotation,
        ref HitStabilityReport hitStabilityReport) { }
    public void OnDiscreteCollisionDetected(Collider hitCollider) { }

    private void OnNameChanged(string oldName, string newName)
    {
        if (playerNameText != null)
            playerNameText.text = newName;
    }

    [Command]
    private void CmdSetPlayerName(string name)
    {
        playerName = name;
    }
}
