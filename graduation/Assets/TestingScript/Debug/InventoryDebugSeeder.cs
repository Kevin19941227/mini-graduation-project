using Mirror;
using UnityEngine;

public class InventoryDebugSeeder : NetworkBehaviour
{
    #region Settings

    [Header("Debug")]
    [SerializeField] private bool addDebugPartsOnServer = true;
    [SerializeField] private bool addAllDatabaseParts = true;
    [SerializeField] private GameDatabase gameDatabase;
    [SerializeField] private int debugPartCount = 3;
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

        if (addAllDatabaseParts)
        {
            AddAllDatabaseParts();
            return;
        }

        inventoryNetwork.ServerAddPart(firstTestPartID, debugPartCount);
        inventoryNetwork.ServerAddPart(secondTestPartID, debugPartCount);
    }

    #endregion

    #region Seed Logic

    private void AddAllDatabaseParts()
    {
        if (gameDatabase == null)
        {
            Debug.LogWarning("[InventoryDebugSeeder] GameDatabase is missing.");
            return;
        }

        for (int i = 0; i < gameDatabase.parts.Count; i++)
        {
            PartData partData = gameDatabase.parts[i];

            if (partData == null || partData.partID <= 0)
            {
                continue;
            }

            inventoryNetwork.ServerAddPart(partData.partID, debugPartCount);
        }
    }

    #endregion
}
