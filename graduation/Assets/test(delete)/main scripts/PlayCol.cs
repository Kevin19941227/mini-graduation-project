using UnityEngine;
using Mirror;
using Steamworks;
using UnityEngine.InputSystem;
using KinematicCharacterController;
using Cinemachine;

public class PlayCol : NetworkBehaviour, ICharacterController
{
    // ===== 狀態枚舉 =====
    public enum PlayerPose
    {
        Grounded,
        Jump,
        Fall,
        Charging,   // 蓄力中（可移動）
        Attack,     // 釋放攻擊（鎖定 + 位移）
        Hit,
        Die
    }

    public enum MoveState
    {
        Idle,
        Walk,
        Run,
        Stop
    }

    [Header("UI")]
    [SerializeField] private TextMesh playerNameText;

    [SyncVar(hook = nameof(OnNameChanged))]
    private string playerName;

    // ===== 動畫同步用 SyncVar =====
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

    [Header("相機震動")]
    public CinemachineImpulseSource impulseSource;

    [Header("蓄力攻擊設定")]
    public float maxChargeTime = 1.5f;
    public float dashDistance = 3f;   // 衝刺總距離（單位：Unity units）
    public float dashDuration = 0.2f; // 衝刺時間（秒），配合攻擊動畫長度調

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

    // 蓄力
    private bool _isCharging = false;
    private float _chargeStartTime = 0f;
    private float _chargeRatio = 0f;

    // 位移
    private Vector3 _dashDirection = Vector3.zero;
    private float _dashTimer = 0f;

    // 頻率控制
    private float _syncTimer = 0f;
    private const float SYNC_INTERVAL = 0.05f;

    // ===== 初始化 =====
    void Awake()
    {
        _motor = GetComponent<KinematicCharacterMotor>();
        _animator = GetComponent<Animator>();
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

    // ===== 主更新迴圈 =====
    void Update()
    {
        if (!isLocalPlayer) return;

        UpdateInput();
        UpdateState();
        UpdateMovement();
        UpdateAnimation();
        SyncAnimationToServer();
    }

    // ===== 讀取輸入 =====
    private void UpdateInput()
    {
        _isRunning = Keyboard.current.leftShiftKey.isPressed;
    }

    // ===== 大狀態判斷 =====
    private void UpdateState()
    {
        if (_currentPose == PlayerPose.Die) return;
        if (!_canChangeState) return;

        if (_motor.GroundingStatus.IsStableOnGround)
        {
            if (_currentPose == PlayerPose.Fall || _currentPose == PlayerPose.Jump)
                ChangeState(PlayerPose.Grounded);

            // Charging 時不呼叫 UpdateMoveState，避免打斷蓄力
            if (_currentPose != PlayerPose.Charging)
                UpdateMoveState();
        }
        else
        {
            // 蓄力中離地（掉下去）→ 中斷蓄力
            if (_currentPose == PlayerPose.Charging)
            {
                _isCharging = false;
                ChangeState(PlayerPose.Fall);
            }
            else if (_currentPose != PlayerPose.Jump)
            {
                ChangeState(PlayerPose.Fall);
            }
        }
    }

    // ===== 小狀態判斷 =====
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
    }

    // ===== 移動方向與旋轉 =====
    private void UpdateMovement()
    {
        if (_currentPose == PlayerPose.Die || _currentPose == PlayerPose.Hit) return;

        // Attack 鎖定期間不更新移動方向
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

    // ===== 動畫更新（本地） =====
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

        // 蓄力進度（給 Animator blend 用，選用）
        if (_currentPose == PlayerPose.Charging)
        {
            _chargeRatio = Mathf.Clamp01((Time.time - _chargeStartTime) / maxChargeTime);
            _animator.SetFloat("ChargeRatio", _chargeRatio);

            // 蓄力越久震動越強
            if (impulseSource != null)
                impulseSource.GenerateImpulse(_chargeRatio * 0.05f);
        }
    }

    // ===== 定期把動畫狀態送給 Server =====
    private void SyncAnimationToServer()
    {
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

    // ===== Command：Client → Server 送動畫狀態 =====
    [Command(requiresAuthority = true)]
    private void CmdSyncAnimState(
        PlayerPose pose,
        float speedX, float speedZ,
        float lastDirX, float lastDirZ,
        bool isMoving, bool isRunning, bool isStopping,
        float chargeRatio)
    {
        _syncPose       = pose;
        _syncSpeedX     = speedX;
        _syncSpeedZ     = speedZ;
        _syncLastDirX   = lastDirX;
        _syncLastDirZ   = lastDirZ;
        _syncIsMoving   = isMoving;
        _syncIsRunning  = isRunning;
        _syncIsStopping = isStopping;
        _syncChargeRatio = chargeRatio;
    }

    // ===== SyncVar Hooks =====
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

    // ===== 依據 Pose 驅動其他人的動畫 =====
    private void ApplyPoseToAnimator(PlayerPose pose)
    {
        switch (pose)
        {
            case PlayerPose.Grounded:
                _animator.CrossFadeInFixedTime("move", 0.2f);
                break;
            case PlayerPose.Charging:
                _animator.CrossFadeInFixedTime("Charging", 0.15f);
                break;
            case PlayerPose.Jump:
                _animator.CrossFadeInFixedTime("Jump", 0.1f);
                break;
            case PlayerPose.Fall:
                _animator.CrossFadeInFixedTime("Fall", 0.1f);
                break;
            case PlayerPose.Attack:
                _animator.CrossFadeInFixedTime("攻擊", 0.05f);
                break;
            case PlayerPose.Hit:
                _animator.CrossFadeInFixedTime("Hit", 0.1f);
                break;
            case PlayerPose.Die:
                _animator.CrossFadeInFixedTime("Die", 0.1f);
                break;
        }
    }

    // ===== 狀態切換 =====
    private void ChangeState(PlayerPose newPose)
    {
        if (_currentPose == newPose) return;
        _currentPose = newPose;

        switch (newPose)
        {
            case PlayerPose.Grounded:
                _animator.CrossFadeInFixedTime("move", 0.2f);
                break;
            case PlayerPose.Charging:
                if (!_animator.GetCurrentAnimatorStateInfo(0).IsName("Charging"))
                    _animator.CrossFadeInFixedTime("Charging", 0.15f);
                else
                    _animator.Play("Charging", 0, 4f / 30f); // 放開再按從第4幀繼續
                // ⚠️ 不設 _canChangeState = false，蓄力中保持可移動
                break;
            case PlayerPose.Jump:
                _animator.CrossFadeInFixedTime("Jump", 0.1f);
                break;
            case PlayerPose.Fall:
                _animator.CrossFadeInFixedTime("Fall", 0.1f);
                break;
            case PlayerPose.Attack:
                _animator.CrossFadeInFixedTime("攻擊", 0.05f);
                _canChangeState = false; // 釋放攻擊才鎖定
                break;
            case PlayerPose.Hit:
                _animator.CrossFadeInFixedTime("Hit", 0.1f);
                _canChangeState = false;
                break;
            case PlayerPose.Die:
                _animator.CrossFadeInFixedTime("Die", 0.1f);
                _canChangeState = false;
                break;
        }
    }

    // ===== 動畫事件回調 =====
    public void OnActionComplete()
    {
        _canChangeState = true;
        ChangeState(PlayerPose.Grounded);
    }

    public void OnStopAnimationComplete()
    {
        if (_moveState != MoveState.Stop) return;
        _moveState = MoveState.Idle;
        _animator.SetBool("IsStopping", false);
    }

    // ===== 觸發型輸入 =====
    public void OnMove(InputValue value)
    {
        if (!isLocalPlayer) return;
        _moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (!isLocalPlayer) return;
        if (value.isPressed && _currentPose == PlayerPose.Grounded
            && _motor.GroundingStatus.IsStableOnGround && _canChangeState)
        {
            // 蓄力中跳躍 → 中斷蓄力
            if (_isCharging)
                _isCharging = false;

            _jumpRequested = true;
            ChangeState(PlayerPose.Jump);
        }
    }

    public void OnAttack(InputValue value)
    {
        if (!isLocalPlayer) return;

        if (value.isPressed)
        {
            // 按下 → 開始蓄力（只要在地上且不在鎖定狀態）
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
            // 放開 → 釋放攻擊
            if (_isCharging && _currentPose == PlayerPose.Charging)
            {
                _isCharging = false;
                _chargeRatio = Mathf.Clamp01((Time.time - _chargeStartTime) / maxChargeTime);

                // 決定衝刺方向：有輸入就往輸入方向，否則面朝方向
                _dashDirection = _moveDirection != Vector3.zero
                    ? _moveDirection.normalized
                    : transform.forward;

                _dashTimer = dashDuration;

                ChangeState(PlayerPose.Attack); // 這裡才鎖定
            }
        }
    }

    // ===== KCC ICharacterController =====
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (!isLocalPlayer) return;

        // Attack 釋放後的衝刺位移
        if (_dashTimer > 0f)
        {
            _dashTimer -= deltaTime;
            currentVelocity = _dashDirection * (dashDistance / dashDuration);
            currentVelocity.y = 0f;
            return;
        }

        // Hit / Die 時不動
        if (_currentPose == PlayerPose.Hit || _currentPose == PlayerPose.Die)
        {
            currentVelocity = Vector3.zero;
            return;
        }

        // Grounded 或 Charging 都照常移動
        float speed = _isRunning ? runSpeed : walkSpeed;
        currentVelocity = _moveDirection.normalized * speed * _moveInput.magnitude;

        if (_jumpRequested)
        {
            _verticalVelocity = jumpForce;
            _jumpRequested = false;
            _motor.ForceUnground();
        }

        if (_motor.GroundingStatus.IsStableOnGround)
            _verticalVelocity = 0f;
        else
            _verticalVelocity += gravity * deltaTime;

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

    // ===== Mirror 網路 =====
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

    // ===== Debug =====
    void OnGUI()
    {
        if (!isLocalPlayer) return;
        GUILayout.Label($"Pose: {_currentPose}");
        GUILayout.Label($"Move: {_moveState}");
        GUILayout.Label($"ChargeRatio: {_chargeRatio:F2}");
        GUILayout.Label($"SpeedX: {_animator.GetFloat("SpeedX"):F2}");
        GUILayout.Label($"SpeedZ: {_animator.GetFloat("SpeedZ"):F2}");
        GUILayout.Label($"LastDirX: {_lastDirX:F2}");
        GUILayout.Label($"LastDirZ: {_lastDirZ:F2}");
        GUILayout.Label($"IsRunning: {_isRunning}");
        GUILayout.Label($"IsStopping: {_animator.GetBool("IsStopping")}");
        GUILayout.Label($"OnGround: {_motor.GroundingStatus.IsStableOnGround}");
        GUILayout.Label($"Pos: {transform.position}");
    }
}