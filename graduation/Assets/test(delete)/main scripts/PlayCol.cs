using UnityEngine;
using Mirror;
using Steamworks;
using UnityEngine.InputSystem;
using KinematicCharacterController;
using Cinemachine;
using System.Collections;
using System.Collections.Generic;

public class PlayCol : NetworkBehaviour, ICharacterController, IGameplayInputModeReceiver
{
    public enum PlayerPose
    {
        Grounded, Jump, Fall, Charging, Attack, Hit, Die, Dash
    }
    public enum MoveState
    {
        Idle, Walk, Run, Stop
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
    public int attackDamage = 10;
    public int maxHp = 100;
    [SerializeField] private LayerMask attackHitMask = Physics.AllLayers;

    [Header("打擊感設定")]
    public float hitStopDuration = 0.08f;
    public float hitKnockbackTime = 0.15f;
    public float cameraShakeStrength = 0.3f;
    public float hitInvincibilityDuration = 0.5f;
    public CinemachineImpulseSource impulseSource;

    [Header("Control Mode")]
    [SerializeField] private PlayerControlModeController controlModeController;
    [SerializeField] private Key assemblyModeKey = Key.B;

    [Header("Equipment Stats")]
    [SerializeField] private PlayerEquipmentStatsController equipmentStatsController;

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
    private const float MIN_JUMP_AIRTIME = 0.15f;
    private bool _gameplayInputEnabled = true;
    private bool _hasReportedDeathToMatch;

    private float _stopAnimTimer = 0f;
    private const float MAX_STOP_ANIM_DURATION = 1.2f;

    public bool IsDead => _hp <= 0 || _currentPose == PlayerPose.Die;

    void Awake()
    {
        _motor = GetComponent<KinematicCharacterMotor>();
        _animator = GetComponent<Animator>();
        controlModeController = GetComponent<PlayerControlModeController>();
        equipmentStatsController = GetComponent<PlayerEquipmentStatsController>();
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
        SurvivalMatchController.EnsureServerInstance()?.ServerRegisterPlayer(this);
    }

    public override void OnStopServer()
    {
        SurvivalMatchController.GetServerInstance()?.ServerUnregisterPlayer(this);
        base.OnStopServer();
    }

    private void OnDestroy()
    {
        UnsubscribeEquipmentStats();
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
    private void ApplyServerHpAfterMaxHpChanged(int previousMaxHp)
    {
        if (!_hasAppliedEquipmentStats)
        {
            _hp = maxHp;
            _hasAppliedEquipmentStats = true;
            return;
        }

        int maxHpDelta = maxHp - previousMaxHp;
        if (maxHpDelta > 0 && _hp > 0)
            _hp += maxHpDelta;

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

    [ServerCallback]
    private void UpdateServerDeathWatch()
    {
        if (_hp > 0 || _hasReportedDeathToMatch)
        {
            return;
        }

        Debug.Log($"{SurvivalMatchLogPrefix} Server death watch caught netId={netId}, hp={_hp}.");
        ServerHandleDeath();
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
            _isCharging = false;
            _chargeRatio = Mathf.Clamp01((Time.time - _chargeStartTime) / maxChargeTime);
            _dashDirection = _moveDirection != Vector3.zero
                ? _moveDirection.normalized
                : transform.forward;
            _dashTimer = dashDuration;
            _attackFired = false;
            ChangeState(PlayerPose.Attack);
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
                _moveState = MoveState.Idle; // ← 落地時重置，讓 UpdateMoveState 正確偵測變化
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
                PlaySound(_jumpSound, 0.19f);
                break;
            case PlayerPose.Fall:
                _animator.CrossFadeInFixedTime("Fall", 0.15f);
                break;
            case PlayerPose.Attack:
                _animator.CrossFadeInFixedTime("Attack", 0.05f);
                _canChangeState = false;
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
        if (_attackFired) return;
        _attackFired = true;

        impulseSource?.GenerateImpulse(cameraShakeStrength);
        StartCoroutine(HitStop(hitStopDuration));
        CmdDoAttack(transform.forward);
    }

    private IEnumerator HitStop(float duration)
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    [Command]
    private void CmdDoAttack(Vector3 attackerForward)
    {
        Vector3 hitPoint = transform.position + attackerForward * attackRange;
        Collider[] hits = Physics.OverlapSphere(hitPoint, attackRadius, attackHitMask, QueryTriggerInteraction.Collide);

        _attackHitTargetIds.Clear();
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.gameObject == gameObject) continue;

            PlayCol playerTarget = hit.GetComponentInParent<PlayCol>();
            if (playerTarget != null)
            {
                if (playerTarget == this || !TryRegisterAttackTarget(playerTarget)) continue;
                playerTarget.TakeDamage(attackDamage, attackerForward);
                continue;
            }

            MonsterAI monsterTarget = hit.GetComponentInParent<MonsterAI>();
            if (monsterTarget == null) continue;
            if (!TryRegisterAttackTarget(monsterTarget)) continue;

            monsterTarget.TakeDamage(attackDamage, attackerForward, this);
        }
    }

    private bool TryRegisterAttackTarget(Component target)
    {
        return target != null && _attackHitTargetIds.Add(target.GetInstanceID());
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
        {
            return;
        }

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
        {
            return;
        }

        if (hud == null)
        {
            hud = PlayerHUD.GetOrCreateRuntimeHud();
        }

        bool isWinner = netId == winnerNetId;
        hud?.ShowResult(isWinner, RequestReturnToLobby, isServer);
        SetGameplayInputEnabled(false);
    }

    private void RequestReturnToLobby()
    {
        if (!isLocalPlayer || !NetworkClient.active || !NetworkClient.ready)
        {
            return;
        }

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
            && _motor.GroundingStatus.IsStableOnGround && _canChangeState)
        {
            if (_isCharging) _isCharging = false;
            _jumpRequested = true;
            _wasJumpRequested = true;
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
                if (_audioSource != null)
                {
                    _audioSource.loop = false;
                    _audioSource.Stop();
                }
                _isCharging = false;
                _chargeRatio = Mathf.Clamp01((Time.time - _chargeStartTime) / maxChargeTime);
                _dashDirection = _moveDirection != Vector3.zero
                    ? _moveDirection.normalized
                    : transform.forward;
                _dashTimer = dashDuration;
                _attackFired = false;
                ChangeState(PlayerPose.Attack);
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
                _attackFired = true;
                impulseSource?.GenerateImpulse(cameraShakeStrength);
                StartCoroutine(HitStop(hitStopDuration));

                if (NetworkClient.active && NetworkClient.ready)
                    CmdDoAttack(transform.forward);
            }

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
            _verticalVelocity = jumpForce;
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
