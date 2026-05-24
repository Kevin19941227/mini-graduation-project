using Mirror;
using UnityEngine;

public class LocalBackpackBinder : MonoBehaviour
{
    #region References

    [SerializeField] private BackpackUIController backpackUIController;

    #endregion

    #region Runtime Data

    private bool isBound;
    private NetworkIdentity boundLocalPlayer;
    private NetworkIdentity lastObservedLocalPlayer;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        NetworkIdentity localPlayer = NetworkClient.localPlayer;

        if (lastObservedLocalPlayer == localPlayer)
        {
            return;
        }

        lastObservedLocalPlayer = localPlayer;
        TryBindLocalPlayer(localPlayer);
    }

    #endregion

    #region Bind Logic

    /// <summary>
    /// 嘗試綁定本地玩家背包。
    /// </summary>
    private void TryBindLocalPlayer(NetworkIdentity localPlayer)
    {
        if (localPlayer == null)
        {
            ClearBinding();
            return;
        }

        if (isBound && boundLocalPlayer == localPlayer)
        {
            return;
        }

        PlayerInventoryNetwork inventory =
            localPlayer.GetComponent<PlayerInventoryNetwork>();

        if (inventory == null)
        {
            return;
        }

        if (backpackUIController == null)
        {
            return;
        }

        backpackUIController.Bind(inventory);

        PlayerControlModeController controlModeController =
            localPlayer.GetComponent<PlayerControlModeController>();

        if (controlModeController != null)
        {
            controlModeController.SetBackpackUIController(backpackUIController);
        }

        boundLocalPlayer = localPlayer;
        isBound = true;
    }

    private void ClearBinding()
    {
        if (!isBound && boundLocalPlayer == null)
        {
            return;
        }

        if (backpackUIController != null)
        {
            backpackUIController.Bind(null);
        }

        boundLocalPlayer = null;
        lastObservedLocalPlayer = null;
        isBound = false;
    }
    #endregion
}
