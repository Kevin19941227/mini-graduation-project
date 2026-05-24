using System;
using Mirror;
using UnityEngine;

public class PlayerFastNetworkController : NetworkBehaviour, IGameplayInputModeReceiver
{
    #region Animator IDs

    private static class AnimatorID
    {
        public static readonly int MoveSpeedID = Animator.StringToHash("MoveSpeed");
        public static readonly int AttackID = Animator.StringToHash("Attack");
        public static readonly int RollID = Animator.StringToHash("Roll");
        public static readonly int HurtID = Animator.StringToHash("Hurt");
        public static readonly int DeadID = Animator.StringToHash("Dead");
        public static readonly int AssemblyModeID = Animator.StringToHash("AssemblyMode");
    }

    #endregion

    #region Settings

    [Header("Data")]
    [SerializeField] private PlayerBaseData baseData;

    [Header("Movement")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private float fallbackMoveSpeed = 5f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float groundedGravity = -2f;

    [Header("Attack")]
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private LayerMask attackMask = ~0;
    [SerializeField] private float attackRadius = 1.5f;
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private int fallbackAttackDamage = 10;

    [Header("Roll")]
    [SerializeField] private float rollDistance = 4f;
    [SerializeField] private float rollDuration = 0.25f;
    [SerializeField] private float rollCooldown = 0.75f;

    [Header("Invincible")]
    [SerializeField] private float hurtInvincibleDuration = 0.25f;

    [Header("Input")]
    [SerializeField] private KeyCode assemblyKey = KeyCode.B;
    [SerializeField] private KeyCode rollKey = KeyCode.Space;
    [SerializeField] private int attackMouseButton = 0;

    [Header("Local UI")]
    [SerializeField] private BackpackUIController backpackUIController;
    [SerializeField] private PlayerControlModeController controlModeController;

    [Header("Presentation")]
    [SerializeField] private Animator animator;
    [SerializeField] private Behaviour kinematicCharacterMotor;
    [SerializeField] private Behaviour[] localOnlyBehaviours;

    #endregion

    #region SyncVar

    [SyncVar(hook = nameof(OnHealthChanged))]
    private int currentHealth;

    [SyncVar(hook = nameof(OnDeadChanged))]
    private bool isDead;

    [SyncVar(hook = nameof(OnRollingChanged))]
    private bool isRolling;

    [SyncVar(hook = nameof(OnAssemblyModeChanged))]
    private bool isAssemblyMode;

    #endregion

    #region Runtime Data

    private float verticalVelocity;
    private float nextLocalAttackTime;
    private float nextLocalRollTime;
    private float nextServerAttackTime;
    private float nextServerRollTime;
    private float invincibleEndTime;
    private float serverRollEndTime;
    private Vector3 rollDirection;
    private float rollEndTime;
    private bool localAssemblyOpen;
    private bool gameplayInputEnabled = true;
    private Camera cachedMainCamera;

    #endregion

    #region Events

    public event Action<int, int> OnLocalHealthChanged;
    public event Action<bool> OnLocalDeadChanged;
    public event Action<bool> OnLocalAssemblyModeChanged;

    #endregion

    #region Properties

    public int CurrentHealth => currentHealth;
    public bool IsDead => isDead;
    public bool IsAssemblyMode => isAssemblyMode;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        controlModeController = GetComponent<PlayerControlModeController>();
    }

    private void Start()
    {
        cachedMainCamera = Camera.main;

        if (!isLocalPlayer)
        {
            DisableRemotePlayerLocalControl();
            return;
        }

        if (controlModeController == null)
        {
            controlModeController = GetComponent<PlayerControlModeController>();
        }
    }

    private void Update()
    {
        if (isServer && isRolling && Time.time >= serverRollEndTime)
        {
            isRolling = false;
        }

        if (!isLocalPlayer || isDead)
        {
            return;
        }

        HandleLocalInput();
        HandleLocalMovement();
        UpdateAnimatorMoveSpeed();
    }

    #endregion

    #region Mirror Callbacks

    public override void OnStartServer()
    {
        base.OnStartServer();

        currentHealth = GetMaxHealth();
        isDead = false;
        isRolling = false;
        isAssemblyMode = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        OnHealthChanged(currentHealth, currentHealth);
        OnDeadChanged(isDead, isDead);
        OnRollingChanged(isRolling, isRolling);
        OnAssemblyModeChanged(isAssemblyMode, isAssemblyMode);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Requests damage from gameplay code; the server remains authoritative.
    /// </summary>
    public void RequestTakeDamage(int damage)
    {
        if (isServer)
        {
            ServerTakeDamage(damage);
            return;
        }

        CmdRequestTakeDamage(damage);
    }

    /// <summary>
    /// Applies damage on the server after defense, invincible, and death checks.
    /// </summary>
    [Server]
    public void ServerTakeDamage(int rawDamage)
    {
        if (isDead || rawDamage <= 0 || Time.time < invincibleEndTime)
        {
            return;
        }

        int finalDamage = Mathf.Max(1, rawDamage - GetDefense());
        currentHealth = Mathf.Max(0, currentHealth - finalDamage);
        invincibleEndTime = Time.time + hurtInvincibleDuration;

        RpcPlayHurt();

        if (currentHealth <= 0)
        {
            isDead = true;
        }
    }

    /// <summary>
    /// Sets the local assembly page reference used by the controller.
    /// </summary>
    public void SetBackpackUIController(BackpackUIController controller)
    {
        backpackUIController = controller;

        if (controlModeController != null)
        {
            controlModeController.SetBackpackUIController(controller);
        }
    }

    /// <summary>
    /// Enables or disables local gameplay input without disabling the local KCC motor.
    /// </summary>
    public void SetGameplayInputEnabled(bool enabledValue)
    {
        gameplayInputEnabled = enabledValue;

        if (!gameplayInputEnabled)
        {
            verticalVelocity = 0f;
            isRolling = false;
            rollDirection = Vector3.zero;
        }
    }

    #endregion

    #region Input

    private void HandleLocalInput()
    {
        if (Input.GetKeyDown(assemblyKey))
        {
            ToggleAssemblyMode();
            return;
        }

        if (!gameplayInputEnabled || isAssemblyMode)
        {
            return;
        }

        if (Input.GetMouseButtonDown(attackMouseButton))
        {
            TryRequestAttack();
        }

        if (Input.GetKeyDown(rollKey))
        {
            TryRequestRoll();
        }
    }

    private void ToggleAssemblyMode()
    {
        bool nextValue = !localAssemblyOpen;
        localAssemblyOpen = nextValue;

        if (controlModeController != null)
        {
            controlModeController.SetMode(nextValue ? PlayerControlMode.Assembly : PlayerControlMode.Gameplay);
        }
        else
        {
            SetLocalAssemblyUI(nextValue);
        }

        CmdSetAssemblyMode(nextValue);
    }

    private void TryRequestAttack()
    {
        if (Time.time < nextLocalAttackTime)
        {
            return;
        }

        nextLocalAttackTime = Time.time + GetAttackCooldown();
        PlayAttackAnimation();
        CmdRequestAttack();
    }

    private void TryRequestRoll()
    {
        if (Time.time < nextLocalRollTime)
        {
            return;
        }

        Vector3 inputDirection = GetMoveDirection();
        if (inputDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            inputDirection = transform.forward;
        }

        nextLocalRollTime = Time.time + rollCooldown;
        BeginLocalRoll(inputDirection.normalized);
        CmdRequestRoll(inputDirection.normalized);
    }

    #endregion

    #region Movement

    private void HandleLocalMovement()
    {
        Vector3 motion;

        if (!gameplayInputEnabled || isAssemblyMode)
        {
            isRolling = false;
            rollDirection = Vector3.zero;
            verticalVelocity = 0f;
            motion = Vector3.zero;
        }
        else if (isRolling)
        {
            motion = rollDirection * (rollDistance / Mathf.Max(rollDuration, Time.deltaTime));

            if (Time.time >= rollEndTime)
            {
                isRolling = false;
            }
        }
        else
        {
            motion = GetMoveDirection() * GetMoveSpeed();
        }

        if (characterController != null)
        {
            ApplyCharacterControllerMove(motion);
            return;
        }

        transform.position += motion * Time.deltaTime;
    }

    private void ApplyCharacterControllerMove(Vector3 horizontalMotion)
    {
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedGravity;
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 motion = horizontalMotion;
        motion.y = verticalVelocity;
        characterController.Move(motion * Time.deltaTime);
    }

    private Vector3 GetMoveDirection()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(horizontal, 0f, vertical);

        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        if (cachedMainCamera == null)
        {
            cachedMainCamera = Camera.main;

            if (cachedMainCamera == null)
            {
                return direction;
            }
        }

        Transform cameraTransform = cachedMainCamera.transform;
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return forward * direction.z + right * direction.x;
    }

    private void BeginLocalRoll(Vector3 direction)
    {
        rollDirection = direction;
        rollEndTime = Time.time + rollDuration;
        isRolling = true;
        PlayRollAnimation();
    }

    #endregion

    #region Commands

    [Command]
    private void CmdRequestAttack()
    {
        if (isDead || isAssemblyMode || Time.time < nextServerAttackTime)
        {
            return;
        }

        nextServerAttackTime = Time.time + GetAttackCooldown();
        ServerExecuteAttack();
        RpcPlayAttack();
    }

    [Command]
    private void CmdRequestRoll(Vector3 direction)
    {
        if (isDead || isAssemblyMode || Time.time < nextServerRollTime)
        {
            return;
        }

        nextServerRollTime = Time.time + rollCooldown;
        serverRollEndTime = Time.time + rollDuration;
        isRolling = true;
        RpcPlayRoll(direction);
    }

    [Command]
    private void CmdSetAssemblyMode(bool value)
    {
        if (isDead)
        {
            return;
        }

        isAssemblyMode = value;
    }

    [Command]
    private void CmdRequestTakeDamage(int damage)
    {
        ServerTakeDamage(damage);
    }

    #endregion

    #region Server Logic

    [Server]
    private void ServerExecuteAttack()
    {
        Transform origin = attackOrigin != null ? attackOrigin : transform;
        Collider[] hits = Physics.OverlapSphere(origin.position, attackRadius, attackMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            PlayerFastNetworkController target = hits[i].GetComponentInParent<PlayerFastNetworkController>();

            if (target == null || target == this)
            {
                continue;
            }

            target.ServerTakeDamage(GetAttackDamage());
        }
    }

    #endregion

    #region ClientRpc

    [ClientRpc]
    private void RpcPlayAttack()
    {
        PlayAttackAnimation();
    }

    [ClientRpc]
    private void RpcPlayRoll(Vector3 direction)
    {
        if (isLocalPlayer)
        {
            return;
        }

        BeginLocalRoll(direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : transform.forward);
    }

    [ClientRpc]
    private void RpcPlayHurt()
    {
        if (animator != null)
        {
            animator.SetTrigger(AnimatorID.HurtID);
        }
    }

    #endregion

    #region SyncVar Hooks

    private void OnHealthChanged(int oldValue, int newValue)
    {
        OnLocalHealthChanged?.Invoke(oldValue, newValue);
    }

    private void OnDeadChanged(bool oldValue, bool newValue)
    {
        if (animator != null)
        {
            animator.SetBool(AnimatorID.DeadID, newValue);
        }

        OnLocalDeadChanged?.Invoke(newValue);
    }

    private void OnRollingChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            PlayRollAnimation();
        }
    }

    private void OnAssemblyModeChanged(bool oldValue, bool newValue)
    {
        if (animator != null)
        {
            animator.SetBool(AnimatorID.AssemblyModeID, newValue);
        }

        if (isLocalPlayer && localAssemblyOpen != newValue)
        {
            localAssemblyOpen = newValue;

            if (controlModeController != null)
            {
                controlModeController.SetMode(newValue ? PlayerControlMode.Assembly : PlayerControlMode.Gameplay);
            }
            else
            {
                SetLocalAssemblyUI(newValue);
            }
        }

        OnLocalAssemblyModeChanged?.Invoke(newValue);
    }

    #endregion

    #region Presentation

    private void PlayAttackAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger(AnimatorID.AttackID);
        }
    }

    private void PlayRollAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger(AnimatorID.RollID);
        }
    }

    private void UpdateAnimatorMoveSpeed()
    {
        if (animator == null)
        {
            return;
        }

        Vector3 inputDirection = GetMoveDirection();
        animator.SetFloat(AnimatorID.MoveSpeedID, gameplayInputEnabled && !isAssemblyMode ? inputDirection.magnitude : 0f);
    }

    private void SetLocalAssemblyUI(bool open)
    {
        if (backpackUIController == null)
        {
            return;
        }

        if (open)
        {
            backpackUIController.Show();
        }
        else
        {
            backpackUIController.Hide();
        }
    }

    #endregion

    #region Helpers

    private void DisableRemotePlayerLocalControl()
    {
        if (kinematicCharacterMotor != null)
        {
            kinematicCharacterMotor.enabled = false;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (localOnlyBehaviours == null)
        {
            return;
        }

        for (int i = 0; i < localOnlyBehaviours.Length; i++)
        {
            if (localOnlyBehaviours[i] != null)
            {
                localOnlyBehaviours[i].enabled = false;
            }
        }
    }

    private int GetMaxHealth()
    {
        return baseData != null ? Mathf.Max(1, baseData.baseHP) : 100;
    }

    private int GetAttackDamage()
    {
        return baseData != null ? Mathf.Max(1, baseData.baseAttack) : fallbackAttackDamage;
    }

    private int GetDefense()
    {
        return baseData != null ? Mathf.Max(0, baseData.baseDefense) : 0;
    }

    private float GetMoveSpeed()
    {
        return baseData != null ? Mathf.Max(0f, baseData.baseMoveSpeed) : fallbackMoveSpeed;
    }

    private float GetAttackCooldown()
    {
        float attackSpeed = baseData != null ? Mathf.Max(0.1f, baseData.baseAttackSpeed) : 1f;
        return attackCooldown / attackSpeed;
    }

    #endregion
}
