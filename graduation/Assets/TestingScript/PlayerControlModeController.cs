using Mirror;
using UnityEngine;

public class PlayerControlModeController : NetworkBehaviour
{
    #region Settings

    [Header("Input")]
    [SerializeField] private bool controlGameplayInputReceivers = true;
    [SerializeField] private Behaviour[] gameplayInputModeReceivers;
    [SerializeField] private Behaviour[] gameplayInputBehaviours;
    [SerializeField] private Behaviour[] gameplayCameraInputBehaviours;
    [SerializeField] private Behaviour[] assemblyCameraInputBehaviours;

    [Header("UI")]
    [SerializeField] private BackpackUIController backpackUIController;

    [Header("Cursor")]
    [SerializeField] private CursorLockMode gameplayCursorLockMode = CursorLockMode.Locked;
    [SerializeField] private CursorLockMode uiCursorLockMode = CursorLockMode.None;

    #endregion

    #region Runtime Data

    private PlayerControlMode currentMode = PlayerControlMode.Gameplay;

    #endregion

    #region Properties

    public PlayerControlMode CurrentMode => currentMode;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (!isLocalPlayer)
        {
            enabled = false;
            return;
        }

        ApplyMode(currentMode);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the local player's active control mode.
    /// </summary>
    public void SetMode(PlayerControlMode mode)
    {
        if (!isLocalPlayer)
        {
            return;
        }

        if (currentMode == mode)
        {
            ApplyMode(currentMode);
            return;
        }

        currentMode = mode;
        ApplyMode(currentMode);
    }

    /// <summary>
    /// Toggles between gameplay and assembly modes for the local player.
    /// </summary>
    public void ToggleAssemblyMode()
    {
        SetMode(currentMode == PlayerControlMode.Assembly
            ? PlayerControlMode.Gameplay
            : PlayerControlMode.Assembly);
    }

    /// <summary>
    /// Sets the backpack UI controlled by this local mode controller.
    /// </summary>
    public void SetBackpackUIController(BackpackUIController controller)
    {
        backpackUIController = controller;
    }

    #endregion

    #region Mode Apply

    private void ApplyMode(PlayerControlMode mode)
    {
        switch (mode)
        {
            case PlayerControlMode.Gameplay:
                ApplyGameplayMode();
                break;
            case PlayerControlMode.Assembly:
                ApplyAssemblyMode();
                break;
            case PlayerControlMode.UIOnly:
                ApplyUIOnlyMode();
                break;
        }
    }

    private void ApplyGameplayMode()
    {
        SetGameplayInputEnabled(true);
        SetGameplayCameraInputEnabled(true);
        SetAssemblyCameraInputEnabled(false);

        if (backpackUIController != null)
        {
            backpackUIController.Hide();
        }

        if (PlayerCameraManager.HasInstance)
        {
            PlayerCameraManager.Instance.SwitchToGameplayCamera();
        }

        Cursor.lockState = gameplayCursorLockMode;
        Cursor.visible = false;
    }

    private void ApplyAssemblyMode()
    {
        SetGameplayInputEnabled(false);
        SetGameplayCameraInputEnabled(false);
        SetAssemblyCameraInputEnabled(true);

        if (backpackUIController != null)
        {
            backpackUIController.Show();
        }

        if (PlayerCameraManager.HasInstance)
        {
            PlayerCameraManager.Instance.SwitchToAssemblyCamera();
        }

        Cursor.lockState = uiCursorLockMode;
        Cursor.visible = true;
    }

    private void ApplyUIOnlyMode()
    {
        SetGameplayInputEnabled(false);
        SetGameplayCameraInputEnabled(false);
        SetAssemblyCameraInputEnabled(false);

        if (backpackUIController != null)
        {
            backpackUIController.Hide();
        }

        Cursor.lockState = uiCursorLockMode;
        Cursor.visible = true;
    }

    #endregion

    #region Component Toggles

    private void SetGameplayInputEnabled(bool enabledValue)
    {
        SetGameplayInputReceiversEnabled(enabledValue);
        SetBehavioursEnabled(gameplayInputBehaviours, enabledValue);
    }

    private void SetGameplayInputReceiversEnabled(bool enabledValue)
    {
        if (!controlGameplayInputReceivers)
        {
            return;
        }

        if (gameplayInputModeReceivers == null)
        {
            return;
        }

        for (int i = 0; i < gameplayInputModeReceivers.Length; i++)
        {
            if (gameplayInputModeReceivers[i] is IGameplayInputModeReceiver receiver)
            {
                receiver.SetGameplayInputEnabled(enabledValue);
            }
        }
    }

    private void SetGameplayCameraInputEnabled(bool enabledValue)
    {
        SetBehavioursEnabled(gameplayCameraInputBehaviours, enabledValue);
    }

    private void SetAssemblyCameraInputEnabled(bool enabledValue)
    {
        SetBehavioursEnabled(assemblyCameraInputBehaviours, enabledValue);
    }

    private void SetBehavioursEnabled(Behaviour[] behaviours, bool enabledValue)
    {
        if (behaviours == null)
        {
            return;
        }

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
            {
                behaviours[i].enabled = enabledValue;
            }
        }
    }

    #endregion
}
