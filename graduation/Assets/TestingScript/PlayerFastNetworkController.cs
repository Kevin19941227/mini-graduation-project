using System;
using Mirror;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class PlayerSpecialEffectEvent : UnityEvent<PlayerFastNetworkController>
{
}

[Serializable]
public class PositionSpecialEffectEvent : UnityEvent<Vector3>
{
}

[Serializable]
public class DurationSpecialEffectEvent : UnityEvent<float>
{
}

public class PlayerFastNetworkController : NetworkBehaviour, IGameplayInputModeReceiver
{
    #region Part Effect IDs

    private const int VirusHeadPartID = 101;
    private const int TimeHeadPartID = 201;
    private const int MotionHatPartID = 304;
    private const float TwoGravity = 2f;

    #endregion

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
    [SerializeField] private GameDatabase gameDatabase;

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

    [Header("Jump")]
    [SerializeField] private KeyCode jumpKey = KeyCode.None;
    [SerializeField] private float jumpHeight = 1.5f;

    [Header("Roll")]
    [SerializeField] private float rollDistance = 4f;
    [SerializeField] private float rollDuration = 0.25f;
    [SerializeField] private float rollCooldown = 0.75f;

    [Header("Invincible")]
    [SerializeField] private float hurtInvincibleDuration = 0.25f;

    [Header("Input")]
    [SerializeField] private KeyCode assemblyKey = KeyCode.B;
    [SerializeField] private KeyCode timeBlinkKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode rollKey = KeyCode.Space;
    [SerializeField] private int attackMouseButton = 0;

    [Header("Special Effects")]
    [SerializeField] private float timeBlinkHalfExtent = 5f;
    [SerializeField] private float timeBlinkCooldown = 3f;
    [SerializeField] private float virusPoisonTrailRadius = 1.25f;
    [SerializeField] private float virusPoisonTrailTickInterval = 0.5f;
    [SerializeField] private float virusPoisonDuration = 3f;
    [SerializeField] private int virusPoisonDamage = 1;
    [SerializeField] private float motionAttackRangeMultiplier = 3f;
    [SerializeField] private float motionBlackoutDuration = 2f;
    [SerializeField] private PlayerSpecialEffectEvent virusTrailEffectRequested;
    [SerializeField] private PositionSpecialEffectEvent timeBlinkEffectRequested;
    [SerializeField] private PlayerSpecialEffectEvent motionAttackEffectRequested;
    [SerializeField] private DurationSpecialEffectEvent blackoutRequested;

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

    [SyncVar]
    private int cheatMaxHealth = -1;

    [SyncVar]
    private int cheatAttackDamage = -1;

    [SyncVar]
    private int cheatDefense = -1;

    [SyncVar]
    private float cheatMoveSpeed = -1f;

    [SyncVar]
    private float cheatAttackSpeed = -1f;

    #endregion

    #region Runtime Data

    private float verticalVelocity;
    private float nextLocalAttackTime;
    private float nextLocalRollTime;
    private float nextServerAttackTime;
    private float nextServerRollTime;
    private float nextLocalTimeBlinkTime;
    private float nextServerTimeBlinkTime;
    private float nextServerVirusTrailTickTime;
    private float poisonEndTime;
    private float nextPoisonDamageTime;
    private float localBlackoutEndTime;
    private float invincibleEndTime;
    private float serverRollEndTime;
    private Vector3 rollDirection;
    private float rollEndTime;
    private bool localAssemblyOpen;
    private bool gameplayInputEnabled = true;
    private Camera cachedMainCamera;
    private PlayerInventoryNetwork inventory;
    private float equippedJumpHeightMultiplier = 1f;
    private float equippedAttackRangeMultiplier = 1f;
    private bool hasVirusHeadPoisonTrail;
    private bool hasTimeHeadBlink;
    private bool hasMotionHeadWideAttack;
    private bool isSubscribedToInventory;

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
    public int CurrentMaxHealth => GetMaxHealth();
    public int CurrentAttackDamage => GetAttackDamage();
    public int CurrentDefense => GetDefense();
    public float CurrentMoveSpeed => GetMoveSpeed();
    public float CurrentAttackSpeed => GetAttackSpeed();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheReferences();
    }

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        controlModeController = GetComponent<PlayerControlModeController>();
        inventory = GetComponent<PlayerInventoryNetwork>();
    }

    private void Start()
    {
        cachedMainCamera = Camera.main;
        CacheReferences();
        RecalculateEquippedSpecialEffects();

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

        if (isServer)
        {
            ServerUpdatePoisonState();
            ServerUpdateVirusPoisonTrail();
        }

        if (!isLocalPlayer || isDead)
        {
            return;
        }

        HandleLocalInput();
        HandleLocalMovement();
        UpdateAnimatorMoveSpeed();
    }

    private void OnGUI()
    {
        if (!isLocalPlayer || Time.time >= localBlackoutEndTime)
        {
            return;
        }

        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.blackTexture);
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
        SubscribeInventory();
        RecalculateEquippedSpecialEffects();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        OnHealthChanged(currentHealth, currentHealth);
        OnDeadChanged(isDead, isDead);
        OnRollingChanged(isRolling, isRolling);
        OnAssemblyModeChanged(isAssemblyMode, isAssemblyMode);
        SubscribeInventory();
        RecalculateEquippedSpecialEffects();
    }

    public override void OnStopServer()
    {
        UnsubscribeInventory();
        base.OnStopServer();
    }

    public override void OnStopClient()
    {
        UnsubscribeInventory();
        base.OnStopClient();
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

    /// <summary>
    /// Applies debug stat overrides on the authoritative server.
    /// </summary>
    [Server]
    public void ServerSetCheatStats(int maxHealth, int attackDamage, int defense, float moveSpeed, float attackSpeed)
    {
        int oldMaxHealth = GetMaxHealth();

        cheatMaxHealth = Mathf.Max(1, maxHealth);
        cheatAttackDamage = Mathf.Max(1, attackDamage);
        cheatDefense = Mathf.Max(0, defense);
        cheatMoveSpeed = Mathf.Max(0f, moveSpeed);
        cheatAttackSpeed = Mathf.Max(0.1f, attackSpeed);

        int newMaxHealth = GetMaxHealth();
        currentHealth = Mathf.Clamp(currentHealth + newMaxHealth - oldMaxHealth, 1, newMaxHealth);
        isDead = false;
    }

    /// <summary>
    /// Restores this player's current HP to its current max HP on the server.
    /// </summary>
    [Server]
    public void ServerHealToFull()
    {
        currentHealth = GetMaxHealth();
        isDead = false;
    }

    /// <summary>
    /// Clears debug stat overrides and returns to base data values on the server.
    /// </summary>
    [Server]
    public void ServerClearCheatStats()
    {
        int oldMaxHealth = GetMaxHealth();

        cheatMaxHealth = -1;
        cheatAttackDamage = -1;
        cheatDefense = -1;
        cheatMoveSpeed = -1f;
        cheatAttackSpeed = -1f;

        int newMaxHealth = GetMaxHealth();
        currentHealth = Mathf.Clamp(currentHealth + newMaxHealth - oldMaxHealth, 1, newMaxHealth);
        isDead = false;
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

        if (Input.GetKeyDown(timeBlinkKey))
        {
            TryRequestTimeBlink();
        }

        if (jumpKey != KeyCode.None && Input.GetKeyDown(jumpKey))
        {
            TryJump();
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

    private void TryRequestTimeBlink()
    {
        if (!hasTimeHeadBlink || Time.time < nextLocalTimeBlinkTime)
        {
            return;
        }

        nextLocalTimeBlinkTime = Time.time + timeBlinkCooldown;
        CmdRequestTimeBlink();
    }

    private void TryJump()
    {
        if (characterController == null || !characterController.isGrounded)
        {
            return;
        }

        verticalVelocity = Mathf.Sqrt(Mathf.Max(0f, jumpHeight * equippedJumpHeightMultiplier) * -TwoGravity * gravity);
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
    private void CmdRequestTimeBlink()
    {
        if (isDead || isAssemblyMode || !hasTimeHeadBlink || Time.time < nextServerTimeBlinkTime)
        {
            return;
        }

        nextServerTimeBlinkTime = Time.time + timeBlinkCooldown;
        Vector3 destination = ResolveTimeBlinkDestination();
        ServerTeleportTo(destination);
        RpcPlayTimeBlinkEffect(destination);
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
        Collider[] hits = Physics.OverlapSphere(origin.position, GetAttackRadius(), attackMask, QueryTriggerInteraction.Ignore);
        bool hitAnyTarget = false;

        for (int i = 0; i < hits.Length; i++)
        {
            PlayerFastNetworkController target = hits[i].GetComponentInParent<PlayerFastNetworkController>();

            if (target == null || target == this)
            {
                continue;
            }

            target.ServerTakeDamage(GetAttackDamage());
            hitAnyTarget = true;

            if (hasMotionHeadWideAttack && target.connectionToClient != null)
            {
                target.TargetRequestBlackout(target.connectionToClient, motionBlackoutDuration);
            }
        }

        if (hasMotionHeadWideAttack && hitAnyTarget)
        {
            RpcPlayMotionAttackEffect();
        }
    }

    [Server]
    private void ServerUpdatePoisonState()
    {
        if (Time.time >= poisonEndTime || Time.time < nextPoisonDamageTime)
        {
            return;
        }

        nextPoisonDamageTime = Time.time + virusPoisonTrailTickInterval;
        ServerTakeDamage(virusPoisonDamage);
    }

    [Server]
    private void ServerUpdateVirusPoisonTrail()
    {
        if (isDead || !hasVirusHeadPoisonTrail || Time.time < nextServerVirusTrailTickTime)
        {
            return;
        }

        nextServerVirusTrailTickTime = Time.time + virusPoisonTrailTickInterval;
        RpcPlayVirusTrailEffect();

        Collider[] hits = Physics.OverlapSphere(transform.position, virusPoisonTrailRadius, attackMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            PlayerFastNetworkController target = hits[i].GetComponentInParent<PlayerFastNetworkController>();

            if (target == null || target == this)
            {
                continue;
            }

            target.ServerApplyPoison(virusPoisonDuration);
        }
    }

    [Server]
    private void ServerApplyPoison(float duration)
    {
        poisonEndTime = Mathf.Max(poisonEndTime, Time.time + Mathf.Max(0f, duration));

        if (nextPoisonDamageTime < Time.time)
        {
            nextPoisonDamageTime = Time.time;
        }
    }

    [Server]
    private Vector3 ResolveTimeBlinkDestination()
    {
        float xOffset = UnityEngine.Random.Range(-timeBlinkHalfExtent, timeBlinkHalfExtent);
        float zOffset = UnityEngine.Random.Range(-timeBlinkHalfExtent, timeBlinkHalfExtent);
        return transform.position + new Vector3(xOffset, 0f, zOffset);
    }

    [Server]
    private void ServerTeleportTo(Vector3 destination)
    {
        if (characterController != null)
        {
            characterController.enabled = false;
            transform.position = destination;
            characterController.enabled = true;
            return;
        }

        transform.position = destination;
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

    [ClientRpc]
    private void RpcPlayVirusTrailEffect()
    {
        virusTrailEffectRequested?.Invoke(this);
    }

    [ClientRpc]
    private void RpcPlayTimeBlinkEffect(Vector3 destination)
    {
        timeBlinkEffectRequested?.Invoke(destination);
    }

    [ClientRpc]
    private void RpcPlayMotionAttackEffect()
    {
        motionAttackEffectRequested?.Invoke(this);
    }

    [TargetRpc]
    private void TargetRequestBlackout(NetworkConnectionToClient targetConnection, float duration)
    {
        localBlackoutEndTime = Time.time + Mathf.Max(0f, duration);
        blackoutRequested?.Invoke(duration);
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

    #region Equipment Special Effects

    private void CacheReferences()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventoryNetwork>();
        }

        if (gameDatabase == null && inventory != null)
        {
            gameDatabase = inventory.AssignedGameDatabase;
        }
    }

    private void SubscribeInventory()
    {
        CacheReferences();

        if (inventory == null || isSubscribedToInventory)
        {
            return;
        }

        inventory.OnInventoryChanged += RecalculateEquippedSpecialEffects;
        isSubscribedToInventory = true;
    }

    private void UnsubscribeInventory()
    {
        if (inventory == null || !isSubscribedToInventory)
        {
            return;
        }

        inventory.OnInventoryChanged -= RecalculateEquippedSpecialEffects;
        isSubscribedToInventory = false;
    }

    /// <summary>
    /// Rebuilds special runtime effects from equipped monster parts.
    /// </summary>
    public void RecalculateEquippedSpecialEffects()
    {
        equippedJumpHeightMultiplier = 1f;
        equippedAttackRangeMultiplier = 1f;
        hasVirusHeadPoisonTrail = false;
        hasTimeHeadBlink = false;
        hasMotionHeadWideAttack = false;

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

        if (partData != null)
        {
            equippedJumpHeightMultiplier *= Mathf.Max(0.01f, partData.jumpHeightMultiplier);
            equippedAttackRangeMultiplier *= Mathf.Max(0.01f, partData.attackRangeMultiplier);
            ApplySpecialEffectType(partData.specialEffect);
        }

        ApplyKnownMonsterHeadFallback(partID);
    }

    private void ApplySpecialEffectType(PartSpecialEffect specialEffect)
    {
        switch (specialEffect)
        {
            case PartSpecialEffect.VirusHeadPoisonTrail:
                hasVirusHeadPoisonTrail = true;
                break;

            case PartSpecialEffect.TimeHeadBlink:
                hasTimeHeadBlink = true;
                break;

            case PartSpecialEffect.MotionHeadWideAttack:
                hasMotionHeadWideAttack = true;
                break;
        }
    }

    private void ApplyKnownMonsterHeadFallback(int partID)
    {
        switch (partID)
        {
            case VirusHeadPartID:
                hasVirusHeadPoisonTrail = true;
                break;

            case TimeHeadPartID:
                hasTimeHeadBlink = true;
                break;

            case MotionHatPartID:
                hasMotionHeadWideAttack = true;
                break;
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
        if (cheatMaxHealth > 0)
        {
            return cheatMaxHealth;
        }

        return baseData != null ? Mathf.Max(1, baseData.baseHP) : 100;
    }

    private int GetAttackDamage()
    {
        if (cheatAttackDamage > 0)
        {
            return cheatAttackDamage;
        }

        return baseData != null ? Mathf.Max(1, baseData.baseAttack) : fallbackAttackDamage;
    }

    private int GetDefense()
    {
        if (cheatDefense >= 0)
        {
            return cheatDefense;
        }

        return baseData != null ? Mathf.Max(0, baseData.baseDefense) : 0;
    }

    private float GetMoveSpeed()
    {
        if (cheatMoveSpeed >= 0f)
        {
            return cheatMoveSpeed;
        }

        return baseData != null ? Mathf.Max(0f, baseData.baseMoveSpeed) : fallbackMoveSpeed;
    }

    private float GetAttackCooldown()
    {
        return attackCooldown / GetAttackSpeed();
    }

    private float GetAttackRadius()
    {
        float rangeMultiplier = equippedAttackRangeMultiplier;

        if (hasMotionHeadWideAttack)
        {
            rangeMultiplier = Mathf.Max(rangeMultiplier, motionAttackRangeMultiplier);
        }

        return Mathf.Max(0f, attackRadius * rangeMultiplier);
    }

    private float GetAttackSpeed()
    {
        if (cheatAttackSpeed >= 0.1f)
        {
            return cheatAttackSpeed;
        }

        return baseData != null ? Mathf.Max(0.1f, baseData.baseAttackSpeed) : 1f;
    }

    #endregion
}
