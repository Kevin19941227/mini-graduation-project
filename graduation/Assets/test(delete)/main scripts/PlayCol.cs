using UnityEngine;
using Mirror;
using Steamworks;
using UnityEngine.InputSystem;
using KinematicCharacterController;
using Cinemachine;
using System.Collections;

public class PlayCol : NetworkBehaviour, ICharacterController, IGameplayInputModeReceiver
{
    public enum PlayerPose
    {
        Grounded, Jump, Fall, Charging, Attack, Hit, Die
    }

    public enum MoveState
    {
        Idle, Walk, Run, Stop
    }

    [Header("UI")]
    [SerializeField] private TextMesh playerNameText;
    [SerializeField] private PlayerHUD _hud;

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
    public float chargeMoveSpeedMultiplier = 0.4f; // 蓄力時移動速度倍率

    [Header("戰鬥設定")]
    public float attackRadius = 1.5f;
    public float attackRange = 1.2f;
    public int attackDamage = 10;
    public int maxHp = 100;

    [Header("打擊感設定")]
    public float hitStopDuration = 0.08f;
    public float hitKnockbackTime = 0.15f;
    public float cameraShakeStrength = 0.3f;
    public CinemachineImpulseSource impulseSource;

    [Header("Control Mode")]
    [SerializeField] private PlayerControlModeController controlModeController;
    [SerializeField] private Key assemblyModeKey = Key.B;

    [SyncVar(hook = nameof(OnHpChanged))]
    private int _hp = 100;

    private Animator _animator;
    private KinematicCharacterMotor _motor;
    private PlayerInput _playerInput;
    private Camera _mainCamera;

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

    private float _syncTimer = 0f;
    private const float SYNC_INTERVAL = 0.05f;

    private bool _wasJumpRequested = false;
    private float _jumpStartTime = -1f;
    private const float MIN_JUMP_AIRTIME = 0.15f;
    private bool _gameplayInputEnabled = true;

    void Awake()
    {
        _motor = GetComponent<KinematicCharacterMotor>();
        _animator = GetComponent<Animator>();
        controlModeController = GetComponent<PlayerControlModeController>();
        _motor.CharacterController = this;
    }

    void Start()
    {
        _playerInput = GetComponent<PlayerInput>();

        if (isLocalPlayer)
        {
            _playerInput.enabled = true;
            _motor.enabled = true;
            _mainCamera = Camera.main;
            _hud?.Init(maxHp);
            _hud?.UpdateHp(_hp, maxHp);

            if (NetworkClient.ready)
            {
                string name = SteamManager.Initialized
                    ? SteamFriends.GetPersonaName()
                    : "Player " + Random.Range(100, 999);
                CmdSetPlayerName(name);
            }
        }
        else
        {
            _playerInput.enabled = false;
            _motor.enabled = false;
        }
    }

    void Update()
    {
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
        SyncAnimationToServer();
    }

    private void HandleControlModeToggleInput()
    {
        if (Keyboard.current == null || !Keyboard.current[assemblyModeKey].wasPressedThisFrame)
        {
            return;
        }

        if (controlModeController == null)
        {
            controlModeController = GetComponent<PlayerControlModeController>();
        }

        if (controlModeController != null)
        {
            controlModeController.ToggleAssemblyMode();
        }
    }

    /// <summary>
    /// Enables or disables local gameplay input without disabling KCC or Mirror components.
    /// </summary>
    public void SetGameplayInputEnabled(bool enabledValue)
    {
        _gameplayInputEnabled = enabledValue;

        if (!_gameplayInputEnabled)
        {
            ResetLocalGameplayInput();
            return;
        }

        if (_currentPose != PlayerPose.Die && _currentPose != PlayerPose.Hit)
        {
            _canChangeState = true;
        }
    }

    private void UpdateGameplayInputDisabled()
    {
        ResetLocalGameplayInput();
        UpdateAnimation();
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
        _isRunning = Keyboard.current.leftShiftKey.isPressed;

        if (_isCharging && _currentPose == PlayerPose.Charging
            && !Mouse.current.leftButton.isPressed)
        {
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

    private void UpdateState()
    {
        if (_currentPose == PlayerPose.Die) return;
        if (!_canChangeState) return;

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
                ChangeState(PlayerPose.Grounded);

            if (_currentPose == PlayerPose.Charging && !_isCharging)
                ChangeState(PlayerPose.Grounded);

            if (_currentPose != PlayerPose.Charging)
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
                _animator.SetBool("IsStopping", true);
            }
            else if (_moveState == MoveState.Stop)
            {
                AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName("急停") && stateInfo.normalizedTime >= 0.8f)
                {
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
                _animator.CrossFadeInFixedTime("idle", 0.15f);
            else if (_moveState == MoveState.Walk || _moveState == MoveState.Run)
                _animator.CrossFadeInFixedTime("move", 0.15f);
        }
    }

    private void UpdateMovement()
    {
        if (_currentPose == PlayerPose.Die || _currentPose == PlayerPose.Hit) return;
        if (_currentPose == PlayerPose.Attack) return;

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

        _hud?.UpdateCharge(_chargeRatio, _isCharging);
    }

    private void SyncAnimationToServer()
    {
        if (!NetworkClient.active || !NetworkClient.ready)
        {
            return;
        }

        _syncTimer += Time.deltaTime;
        if (_syncTimer < SYNC_INTERVAL) return;
        _syncTimer = 0f;

        CmdSyncAnimState(
            _currentPose,
            _animator.GetFloat("SpeedX"),
            _animator.GetFloat("SpeedZ"),
            _lastDirX,
            _lastDirZ,
            _animator.GetBool("IsMoving"),
            _animator.GetBool("IsRunning"),
            _animator.GetBool("IsStopping"),
            _chargeRatio
        );
    }

    [Command(requiresAuthority = true)]
    private void CmdSyncAnimState(
        PlayerPose pose,
        float speedX, float speedZ,
        float lastDirX, float lastDirZ,
        bool isMoving, bool isRunning, bool isStopping,
        float chargeRatio)
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
                break;
            case PlayerPose.Jump:
                _jumpStartTime = Time.time;
                _animator.CrossFadeInFixedTime("Jump", 0.1f);
                break;
            case PlayerPose.Fall:
                _animator.CrossFadeInFixedTime("Fall", 0.15f);
                break;
            case PlayerPose.Attack:
                _animator.CrossFadeInFixedTime("Attack", 0.05f);
                _canChangeState = false;
                break;
            case PlayerPose.Hit:
                _animator.CrossFadeInFixedTime("hurt", 0.1f);
                _canChangeState = false;
                break;
            case PlayerPose.Die:
                _animator.CrossFadeInFixedTime("死亡", 0.1f);
                _canChangeState = false;
                break;
        }
    }

    public void OnActionComplete()
    {
        _attackFired = false;
        _canChangeState = true;
        ChangeState(PlayerPose.Grounded);
    }

    public void OnStopAnimationComplete()
    {
        if (_moveState != MoveState.Stop) return;
        _moveState = MoveState.Idle;
        _animator.SetBool("IsStopping", false);
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
        Collider[] hits = Physics.OverlapSphere(hitPoint, attackRadius, LayerMask.GetMask("Player"));

        Debug.Log($"CmdDoAttack 執行，找到 {hits.Length} 個碰撞體");

        foreach (var hit in hits)
        {
            Debug.Log($"碰到: {hit.gameObject.name}，Layer={LayerMask.LayerToName(hit.gameObject.layer)}");
            if (hit.gameObject == gameObject) continue;
            var target = hit.GetComponent<PlayCol>();
            if (target != null)
                target.TakeDamage(attackDamage, attackerForward);
        }
    }

    [Server]
    public void TakeDamage(int damage, Vector3 attackerForward)
    {
        _hp -= damage;
        if (_hp <= 0)
        {
            _hp = 0;
            RpcOnDie();
        }
        else
        {
            RpcOnHit(attackerForward);
        }
    }

    [ClientRpc]
    private void RpcOnHit(Vector3 attackerForward)
    {
        Debug.Log($"RpcOnHit 被叫到，isLocalPlayer={isLocalPlayer}，currentPose={_currentPose}");
        _dashDirection = -attackerForward;
        _dashTimer = hitKnockbackTime;
        _canChangeState = true;
        _currentPose = PlayerPose.Grounded;
        ChangeState(PlayerPose.Hit);
    }

    [ClientRpc]
    private void RpcOnDie()
    {
        ChangeState(PlayerPose.Die);
        if (isLocalPlayer)
            _hud?.ShowGameOver();
    }

    private void OnHpChanged(int oldHp, int newHp)
    {
        if (isLocalPlayer)
            _hud?.UpdateHp(newHp, maxHp);
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
                {
                    CmdDoAttack(transform.forward);
                }
            }

            return;
        }

        if (_currentPose == PlayerPose.Hit || _currentPose == PlayerPose.Die)
        {
            currentVelocity = Vector3.zero;
            return;
        }

        float speed = _isRunning ? runSpeed : walkSpeed;

        // 蓄力中速度減慢
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

    // void OnGUI()
    // {
    //     if (!isLocalPlayer) return;
    //     GUILayout.Label($"Pose: {_currentPose}");
    //     GUILayout.Label($"Move: {_moveState}");
    //     GUILayout.Label($"HP: {_hp}");
    //     GUILayout.Label($"ChargeRatio: {_chargeRatio:F2}");
    //     GUILayout.Label($"IsRunning: {_isRunning}");
    //     GUILayout.Label($"OnGround: {_motor.GroundingStatus.IsStableOnGround}");
    // }
}
