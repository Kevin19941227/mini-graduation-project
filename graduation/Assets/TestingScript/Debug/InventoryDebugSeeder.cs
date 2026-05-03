using Mirror;
using UnityEngine;

public class InventoryDebugSeeder : NetworkBehaviour
{
    #region Settings

    [Header("Debug")]
    [SerializeField] private bool addDebugPartsOnServer = true;
    [SerializeField] private int firstTestPartID = 1;
    [SerializeField] private int secondTestPartID = 2;

    #endregion

    #region References

    private PlayerInventoryNetwork inventoryNetwork;

    #endregion

    #region Mirror Callbacks

    public override void OnStartServer()
    {
        base.OnStartServer();

        inventoryNetwork = GetComponent<PlayerInventoryNetwork>();

        if (!addDebugPartsOnServer || inventoryNetwork == null)
        {
            return;
        }

        inventoryNetwork.ServerAddPart(firstTestPartID, 1);
        inventoryNetwork.ServerAddPart(secondTestPartID, 2);
    }

    #endregion
}