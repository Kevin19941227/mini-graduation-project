using Mirror;
using UnityEngine;

public class LocalBackpackBinder : MonoBehaviour
{
    #region References

    [SerializeField] private BackpackUIController backpackUIController;

    #endregion

    #region Runtime Data

    private bool isBound;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        TryBindLocalPlayer();

        if (Input.GetKeyDown(KeyCode.B))
        {
            if (backpackUIController != null)
            {
                backpackUIController.Toggle();
            }
        }
    }

    #endregion

    #region Bind Logic

    /// <summary>
    /// 嘗試綁定本地玩家背包。
    /// </summary>
    private void TryBindLocalPlayer()
    {
        if (isBound)
        {
            return;
        }

        if (NetworkClient.localPlayer == null)
        {
            return;
        }

        PlayerInventoryNetwork inventory =
            NetworkClient.localPlayer.GetComponent<PlayerInventoryNetwork>();

        if (inventory == null)
        {
            return;
        }

        if (backpackUIController == null)
        {
            return;
        }

        backpackUIController.Bind(inventory);
        isBound = true;
    }

    #endregion
}