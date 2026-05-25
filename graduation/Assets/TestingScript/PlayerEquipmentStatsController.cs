using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class PlayerEquipmentStatsController : NetworkBehaviour
{
    #region References

    [Header("Data")]
    [SerializeField] private PlayerBaseData baseData;
    [SerializeField] private GameDatabase gameDatabase;

    [Header("Debug")]
    [SerializeField] private bool showLocalDebugGUI = true;
    [SerializeField] private bool logRecalculateResult = false;

    #endregion

    #region Runtime Data

    private readonly PlayerRuntimeData runtimeData = new PlayerRuntimeData();
    private readonly List<PartData> equippedPartDataBuffer = new List<PartData>();
    private readonly List<BuffData> activeBuffDataBuffer = new List<BuffData>();
    private PlayerInventoryNetwork inventory;
    private bool isSubscribedToInventory;

    #endregion

    #region Events

    public event Action<PlayerRuntimeData> OnStatsChanged;

    #endregion

    #region Properties

    public int CurrentMaxHP => runtimeData.currentMaxHP;
    public int CurrentHP => runtimeData.currentHP;
    public int CurrentAttack => runtimeData.currentAttack;
    public float CurrentMoveSpeed => runtimeData.currentMoveSpeed;
    public int CurrentDefense => runtimeData.currentDefense;
    public float CurrentAttackSpeed => runtimeData.currentAttackSpeed;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheReferences();
    }

    private void Start()
    {
        RecalculateStats();
    }

    private void OnGUI()
    {
        if (!showLocalDebugGUI || !isLocalPlayer)
        {
            return;
        }

        GUILayout.Label($"Equip MaxHP: {CurrentMaxHP}");
        GUILayout.Label($"Equip HP: {CurrentHP}");
        GUILayout.Label($"Equip Attack: {CurrentAttack}");
        GUILayout.Label($"Equip MoveSpeed: {CurrentMoveSpeed:F2}");
        GUILayout.Label($"Equip Defense: {CurrentDefense}");
        GUILayout.Label($"Equip AttackSpeed: {CurrentAttackSpeed:F2}");
    }

    #endregion

    #region Mirror Callbacks

    public override void OnStartClient()
    {
        base.OnStartClient();
        SubscribeInventory();
        RecalculateStats();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        SubscribeInventory();
        RecalculateStats();
    }

    public override void OnStopClient()
    {
        UnsubscribeInventory();
        base.OnStopClient();
    }

    public override void OnStopServer()
    {
        UnsubscribeInventory();
        base.OnStopServer();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Rebuilds current player stats from base data and installed parts.
    /// </summary>
    public void RecalculateStats()
    {
        CacheReferences();
        BuildEquippedPartDataBuffer();

        PlayerStatCalculator.RecalculatePlayerStats(
            ResolveBaseData(),
            equippedPartDataBuffer,
            activeBuffDataBuffer,
            runtimeData
        );

        OnStatsChanged?.Invoke(runtimeData);
        LogStatsIfNeeded();
    }

    /// <summary>
    /// Copies the latest calculated runtime stats into the provided target.
    /// </summary>
    public void CopyStatsTo(PlayerRuntimeData target)
    {
        if (target == null)
        {
            return;
        }

        target.currentMaxHP = runtimeData.currentMaxHP;
        target.currentHP = runtimeData.currentHP;
        target.currentAttack = runtimeData.currentAttack;
        target.currentMoveSpeed = runtimeData.currentMoveSpeed;
        target.currentDefense = runtimeData.currentDefense;
        target.currentAttackSpeed = runtimeData.currentAttackSpeed;
    }

    #endregion

    #region Setup

    private void CacheReferences()
    {
        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventoryNetwork>();
        }

        if (baseData == null && gameDatabase != null)
        {
            baseData = gameDatabase.playerBaseData;
        }
    }

    private void SubscribeInventory()
    {
        CacheReferences();

        if (inventory == null || isSubscribedToInventory)
        {
            return;
        }

        inventory.OnInventoryChanged += RecalculateStats;
        isSubscribedToInventory = true;
    }

    private void UnsubscribeInventory()
    {
        if (inventory == null || !isSubscribedToInventory)
        {
            return;
        }

        inventory.OnInventoryChanged -= RecalculateStats;
        isSubscribedToInventory = false;
    }

    #endregion

    #region Calculation

    private PlayerBaseData ResolveBaseData()
    {
        return baseData != null ? baseData : gameDatabase != null ? gameDatabase.playerBaseData : null;
    }

    private void BuildEquippedPartDataBuffer()
    {
        equippedPartDataBuffer.Clear();
        activeBuffDataBuffer.Clear();

        if (inventory == null || gameDatabase == null)
        {
            return;
        }

        for (int i = 0; i < inventory.EquippedParts.Count; i++)
        {
            EquippedPartRuntimeData equippedPart = inventory.EquippedParts[i];
            PartData partData = gameDatabase.GetPartData(equippedPart.partID);

            if (partData == null)
            {
                continue;
            }

            equippedPartDataBuffer.Add(partData);
            AppendPartPassiveBuffs(partData);
        }
    }

    private void AppendPartPassiveBuffs(PartData partData)
    {
        if (partData.passiveBuffs == null)
        {
            return;
        }

        for (int i = 0; i < partData.passiveBuffs.Count; i++)
        {
            BuffData buffData = partData.passiveBuffs[i];

            if (buffData != null)
            {
                activeBuffDataBuffer.Add(buffData);
            }
        }
    }

    #endregion

    #region Debug

    private void LogStatsIfNeeded()
    {
        if (!logRecalculateResult)
        {
            return;
        }

        Debug.Log(
            $"[EquipmentStats] MaxHP={CurrentMaxHP}, HP={CurrentHP}, " +
            $"Attack={CurrentAttack}, MoveSpeed={CurrentMoveSpeed:F2}, " +
            $"Defense={CurrentDefense}, AttackSpeed={CurrentAttackSpeed:F2}"
        );
    }

    #endregion
}
