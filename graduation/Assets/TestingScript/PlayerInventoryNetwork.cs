using System;
using Mirror;
using UnityEngine;

public class PlayerInventoryNetwork : NetworkBehaviour
{
    #region Settings

    [Header("Debug Cheat UI")]
    [SerializeField] private bool enableCheatUI = true;
    [SerializeField] private GameDatabase cheatGameDatabase;
    [SerializeField] private int defaultCheatPartCount = 1;

    #endregion

    #region SyncLists

    public readonly SyncList<StoredPartRuntimeData> StoredParts = new SyncList<StoredPartRuntimeData>();
    public readonly SyncList<EquippedPartRuntimeData> EquippedParts = new SyncList<EquippedPartRuntimeData>();

    #endregion

    #region Events

    public event Action OnInventoryChanged;

    #endregion

    #region Cheat UI State

    private bool showCheatUI;
    private int cheatPartIndex;
    private string cheatPartCountText = "1";
    private string cheatMaxHealthText = "100";
    private string cheatAttackText = "10";
    private string cheatDefenseText = "0";
    private string cheatMoveSpeedText = "5";
    private string cheatAttackSpeedText = "1";
    private Vector2 cheatScrollPosition;
    private PlayerFastNetworkController cachedPlayerController;
    private PlayCol cachedPlayCol;
    private bool cheatDisabledGameplayInput;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (!enableCheatUI || !isLocalPlayer)
        {
            return;
        }

        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.X))
        {
            SetCheatUIVisible(!showCheatUI);
        }
    }

    private void OnDisable()
    {
        SetCheatUIVisible(false);
    }

    private void OnGUI()
    {
        if (!enableCheatUI || !isLocalPlayer || !showCheatUI)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(20f, 20f, 360f, 520f), GUI.skin.box);
        GUILayout.Label("Cheat UI (Ctrl+X)");
        cheatScrollPosition = GUILayout.BeginScrollView(cheatScrollPosition);

        DrawPartCheatUI();
        GUILayout.Space(12f);
        DrawStatCheatUI();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    #endregion

    #region Mirror Callbacks

    public override void OnStartClient()
    {
        base.OnStartClient();

        StoredParts.Callback += HandleStoredPartsChanged;
        EquippedParts.Callback += HandleEquippedPartsChanged;

        OnInventoryChanged?.Invoke();
    }

    public override void OnStopClient()
    {
        StoredParts.Callback -= HandleStoredPartsChanged;
        EquippedParts.Callback -= HandleEquippedPartsChanged;

        base.OnStopClient();
    }

    #endregion

    #region Client Helpers

    public bool ClientHasPart(int partID, int count)
    {
        if (partID <= 0 || count <= 0)
        {
            return false;
        }

        for (int i = 0; i < StoredParts.Count; i++)
        {
            StoredPartRuntimeData slot = StoredParts[i];

            if (slot.partID == partID && slot.count >= count)
            {
                return true;
            }
        }

        return false;
    }

    [Command]
    public void CmdInstallPartFromBackpack(
        int partID,
        string attachPointID,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        ServerInstallPartFromBackpack(partID, attachPointID, localPosition, localEulerAngles, localScale);
    }

    [Command]
    public void CmdReturnInstalledPartToBackpack(int partID)
    {
        ServerReturnInstalledPartToBackpack(partID);
    }

    [Command]
    public void CmdReturnAllInstalledPartsToBackpack()
    {
        ServerReturnAllInstalledPartsToBackpack();
    }

    [Command]
    private void CmdCheatAddPart(int partID, int count)
    {
        ServerAddPart(partID, count);
    }

    [Command]
    private void CmdCheatClearInventory()
    {
        StoredParts.Clear();
        EquippedParts.Clear();
        NotifyInventoryChanged();
    }

    [Command]
    private void CmdCheatApplyStats(int maxHealth, int attackDamage, int defense, float moveSpeed, float attackSpeed)
    {
        PlayerFastNetworkController playerController = GetPlayerController();

        if (playerController != null)
        {
            playerController.ServerSetCheatStats(maxHealth, attackDamage, defense, moveSpeed, attackSpeed);
            return;
        }

        PlayCol playCol = GetPlayCol();

        if (playCol != null)
        {
            playCol.ServerSetCheatStats(maxHealth, attackDamage, defense, moveSpeed, attackSpeed);
        }
    }

    [Command]
    private void CmdCheatHealToFull()
    {
        PlayerFastNetworkController playerController = GetPlayerController();

        if (playerController != null)
        {
            playerController.ServerHealToFull();
            return;
        }

        PlayCol playCol = GetPlayCol();

        if (playCol != null)
        {
            playCol.ServerHealToFull();
        }
    }

    [Command]
    private void CmdCheatClearStats()
    {
        PlayerFastNetworkController playerController = GetPlayerController();

        if (playerController != null)
        {
            playerController.ServerClearCheatStats();
            return;
        }

        PlayCol playCol = GetPlayCol();

        if (playCol != null)
        {
            playCol.ServerClearCheatStats();
        }
    }

    #endregion

    #region Server Methods

    [Server]
    public bool ServerAddPart(int partID, int count)
    {
        if (partID <= 0 || count <= 0)
        {
            return false;
        }

        for (int i = 0; i < StoredParts.Count; i++)
        {
            StoredPartRuntimeData slot = StoredParts[i];

            if (slot.partID != partID)
            {
                continue;
            }

            slot.count += count;
            StoredParts[i] = slot;
            NotifyInventoryChanged();
            return true;
        }

        StoredPartRuntimeData newSlot = new StoredPartRuntimeData(
            StoredParts.Count,
            partID,
            count
        );

        StoredParts.Add(newSlot);
        NotifyInventoryChanged();
        return true;
    }

    [Server]
    public bool ServerRemovePart(int partID, int count)
    {
        if (partID <= 0 || count <= 0)
        {
            return false;
        }

        for (int i = 0; i < StoredParts.Count; i++)
        {
            StoredPartRuntimeData slot = StoredParts[i];

            if (slot.partID != partID)
            {
                continue;
            }

            if (slot.count < count)
            {
                return false;
            }

            slot.count -= count;

            if (slot.count <= 0)
            {
                StoredParts.RemoveAt(i);
                RebuildSlotIndexes();
            }
            else
            {
                StoredParts[i] = slot;
            }

            NotifyInventoryChanged();
            return true;
        }

        return false;
    }

    [Server]
    public bool ServerHasPart(int partID, int count)
    {
        if (partID <= 0 || count <= 0)
        {
            return false;
        }

        for (int i = 0; i < StoredParts.Count; i++)
        {
            StoredPartRuntimeData slot = StoredParts[i];

            if (slot.partID == partID && slot.count >= count)
            {
                return true;
            }
        }

        return false;
    }

    [Server]
    public bool ServerInstallPartFromBackpack(
        int partID,
        string attachPointID,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        if (!ServerRemovePart(partID, 1))
        {
            return false;
        }

        EquippedPartRuntimeData equippedPart = new EquippedPartRuntimeData(
            EquippedParts.Count,
            partID,
            attachPointID,
            localPosition,
            localEulerAngles,
            localScale
        );

        EquippedParts.Add(equippedPart);
        NotifyInventoryChanged();
        return true;
    }

    [Server]
    public bool ServerReturnInstalledPartToBackpack(int partID)
    {
        if (partID <= 0)
        {
            return false;
        }

        int equippedIndex = FindEquippedPartIndex(partID);

        if (equippedIndex < 0)
        {
            return false;
        }

        EquippedParts.RemoveAt(equippedIndex);
        RebuildEquippedIndexes();
        ServerAddPart(partID, 1);
        NotifyInventoryChanged();
        return true;
    }

    [Server]
    public int ServerReturnAllInstalledPartsToBackpack()
    {
        int returnedCount = 0;

        for (int i = EquippedParts.Count - 1; i >= 0; i--)
        {
            int partID = EquippedParts[i].partID;

            if (partID <= 0)
            {
                continue;
            }

            EquippedParts.RemoveAt(i);
            ServerAddPart(partID, 1);
            returnedCount++;
        }

        RebuildEquippedIndexes();
        NotifyInventoryChanged();
        return returnedCount;
    }

    #endregion

    #region SyncList Events

    private void HandleStoredPartsChanged(
        SyncList<StoredPartRuntimeData>.Operation operation,
        int index,
        StoredPartRuntimeData oldItem,
        StoredPartRuntimeData newItem)
    {
        OnInventoryChanged?.Invoke();
    }

    private void HandleEquippedPartsChanged(
        SyncList<EquippedPartRuntimeData>.Operation operation,
        int index,
        EquippedPartRuntimeData oldItem,
        EquippedPartRuntimeData newItem)
    {
        OnInventoryChanged?.Invoke();
    }

    private void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    #endregion

    #region Cheat UI

    private void DrawPartCheatUI()
    {
        GUILayout.Label("Parts");

        if (cheatGameDatabase == null || cheatGameDatabase.parts == null || cheatGameDatabase.parts.Count == 0)
        {
            GUILayout.Label("GameDatabase or parts list is missing.");
            return;
        }

        cheatPartIndex = Mathf.Clamp(cheatPartIndex, 0, cheatGameDatabase.parts.Count - 1);
        PartData selectedPart = cheatGameDatabase.parts[cheatPartIndex];

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("<", GUILayout.Width(40f)))
        {
            cheatPartIndex = Mathf.Max(0, cheatPartIndex - 1);
        }

        GUILayout.Label(selectedPart != null ? $"{selectedPart.partID} - {selectedPart.partName}" : "Missing Part");

        if (GUILayout.Button(">", GUILayout.Width(40f)))
        {
            cheatPartIndex = Mathf.Min(cheatGameDatabase.parts.Count - 1, cheatPartIndex + 1);
        }

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Count", GUILayout.Width(80f));
        cheatPartCountText = GUILayout.TextField(cheatPartCountText, GUILayout.Width(80f));

        if (GUILayout.Button("Add Selected") && selectedPart != null)
        {
            CmdCheatAddPart(selectedPart.partID, ParseInt(cheatPartCountText, defaultCheatPartCount, 1, 99));
        }

        GUILayout.EndHorizontal();

        if (GUILayout.Button("Clear Stored + Equipped Parts"))
        {
            CmdCheatClearInventory();
        }
    }

    private void DrawStatCheatUI()
    {
        GUILayout.Label("Stats");

        cheatMaxHealthText = DrawTextField("Max HP", cheatMaxHealthText);
        cheatAttackText = DrawTextField("Attack", cheatAttackText);
        cheatDefenseText = DrawTextField("Defense", cheatDefenseText);
        cheatMoveSpeedText = DrawTextField("Move Speed", cheatMoveSpeedText);
        cheatAttackSpeedText = DrawTextField("Attack Speed", cheatAttackSpeedText);

        if (GUILayout.Button("Apply Stats"))
        {
            CmdCheatApplyStats(
                ParseInt(cheatMaxHealthText, 100, 1, 9999),
                ParseInt(cheatAttackText, 10, 1, 9999),
                ParseInt(cheatDefenseText, 0, 0, 9999),
                ParseFloat(cheatMoveSpeedText, 5f, 0f, 100f),
                ParseFloat(cheatAttackSpeedText, 1f, 0.1f, 100f)
            );
        }

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Heal Full"))
        {
            CmdCheatHealToFull();
        }

        if (GUILayout.Button("Clear Stat Overrides"))
        {
            CmdCheatClearStats();
            InitializeCheatFields();
        }

        GUILayout.EndHorizontal();
    }

    private static string DrawTextField(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(100f));
        string result = GUILayout.TextField(value, GUILayout.Width(120f));
        GUILayout.EndHorizontal();
        return result;
    }

    private void InitializeCheatFields()
    {
        PlayerFastNetworkController playerController = GetPlayerController();

        if (playerController != null)
        {
            cheatMaxHealthText = playerController.CurrentMaxHealth.ToString();
            cheatAttackText = playerController.CurrentAttackDamage.ToString();
            cheatDefenseText = playerController.CurrentDefense.ToString();
            cheatMoveSpeedText = playerController.CurrentMoveSpeed.ToString("0.##");
            cheatAttackSpeedText = playerController.CurrentAttackSpeed.ToString("0.##");
            cheatPartCountText = Mathf.Max(1, defaultCheatPartCount).ToString();
            return;
        }

        PlayCol playCol = GetPlayCol();

        if (playCol == null)
        {
            return;
        }

        cheatMaxHealthText = playCol.CurrentMaxHp.ToString();
        cheatAttackText = playCol.CurrentAttackDamage.ToString();
        cheatDefenseText = playCol.CurrentDefense.ToString();
        cheatMoveSpeedText = playCol.CurrentMoveSpeed.ToString("0.##");
        cheatAttackSpeedText = playCol.CurrentAttackSpeed.ToString("0.##");
        cheatPartCountText = Mathf.Max(1, defaultCheatPartCount).ToString();
    }

    private void SetCheatUIVisible(bool visible)
    {
        if (showCheatUI == visible)
        {
            return;
        }

        showCheatUI = visible;

        if (showCheatUI)
        {
            InitializeCheatFields();
            SetLocalGameplayInput(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            cheatDisabledGameplayInput = true;
            return;
        }

        if (cheatDisabledGameplayInput)
        {
            SetLocalGameplayInput(true);
            cheatDisabledGameplayInput = false;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetLocalGameplayInput(bool enabledValue)
    {
        IGameplayInputModeReceiver[] receivers = GetComponents<IGameplayInputModeReceiver>();

        for (int i = 0; i < receivers.Length; i++)
        {
            receivers[i].SetGameplayInputEnabled(enabledValue);
        }
    }

    private PlayerFastNetworkController GetPlayerController()
    {
        if (cachedPlayerController == null)
        {
            cachedPlayerController = GetComponent<PlayerFastNetworkController>();
        }

        return cachedPlayerController;
    }

    private PlayCol GetPlayCol()
    {
        if (cachedPlayCol == null)
        {
            cachedPlayCol = GetComponent<PlayCol>();
        }

        return cachedPlayCol;
    }

    private static int ParseInt(string text, int fallback, int min, int max)
    {
        if (!int.TryParse(text, out int value))
        {
            value = fallback;
        }

        return Mathf.Clamp(value, min, max);
    }

    private static float ParseFloat(string text, float fallback, float min, float max)
    {
        if (!float.TryParse(text, out float value))
        {
            value = fallback;
        }

        return Mathf.Clamp(value, min, max);
    }

    #endregion

    #region Internal Methods

    [Server]
    private void RebuildSlotIndexes()
    {
        for (int i = 0; i < StoredParts.Count; i++)
        {
            StoredPartRuntimeData slot = StoredParts[i];
            slot.slotIndex = i;
            StoredParts[i] = slot;
        }
    }

    [Server]
    private int FindEquippedPartIndex(int partID)
    {
        for (int i = 0; i < EquippedParts.Count; i++)
        {
            if (EquippedParts[i].partID == partID)
            {
                return i;
            }
        }

        return -1;
    }

    [Server]
    private void RebuildEquippedIndexes()
    {
        for (int i = 0; i < EquippedParts.Count; i++)
        {
            EquippedPartRuntimeData equippedPart = EquippedParts[i];
            equippedPart.equipIndex = i;
            EquippedParts[i] = equippedPart;
        }
    }

    #endregion
}
