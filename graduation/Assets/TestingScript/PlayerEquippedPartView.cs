using System.Collections.Generic;
using UnityEngine;

public class PlayerEquippedPartView : MonoBehaviour
{
    #region Settings

    [SerializeField] private PlayerInventoryNetwork inventory;
    [SerializeField] private GameDatabase gameDatabase;

    #endregion

    #region Runtime Data

    private const char AttachPointSeparator = '|';
    private readonly Dictionary<int, GameObject> spawnedParts = new Dictionary<int, GameObject>();

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        inventory = GetComponent<PlayerInventoryNetwork>();
    }

    private void Awake()
    {
        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventoryNetwork>();
        }
    }

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged += RebuildEquippedParts;
        }
    }

    private void Start()
    {
        RebuildEquippedParts();
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= RebuildEquippedParts;
        }

        ClearSpawnedParts();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Rebuilds local visual part objects from the synced equipped part list.
    /// </summary>
    public void RebuildEquippedParts()
    {
        if (inventory == null || gameDatabase == null)
        {
            return;
        }

        ClearSpawnedParts();

        for (int i = 0; i < inventory.EquippedParts.Count; i++)
        {
            SpawnEquippedPart(inventory.EquippedParts[i]);
        }
    }

    #endregion

    #region Spawn Logic

    private void SpawnEquippedPart(EquippedPartRuntimeData equippedPart)
    {
        PartData partData = gameDatabase.GetPartData(equippedPart.partID);

        if (partData == null || partData.partPrefab == null)
        {
            return;
        }

        Transform attachRoot = ResolveAttachRoot(equippedPart.attachPointID);

        if (attachRoot == null)
        {
            attachRoot = transform;
        }

        GameObject partInstance = Instantiate(partData.partPrefab, attachRoot);
        partInstance.name = partData.partName;
        partInstance.transform.localPosition = equippedPart.localPosition;
        partInstance.transform.localRotation = Quaternion.Euler(equippedPart.localEulerAngles);
        partInstance.transform.localScale = equippedPart.localScale;
        SetVisualCollidersForEquippedPart(partInstance);
        spawnedParts[equippedPart.equipIndex] = partInstance;
    }

    private Transform ResolveAttachRoot(string syncedAttachPointID)
    {
        string boneName = ExtractBoneName(syncedAttachPointID);

        if (!string.IsNullOrWhiteSpace(boneName) && TryFindChildByName(transform, boneName, out Transform bone))
        {
            return bone;
        }

        string surfaceID = ExtractSurfaceID(syncedAttachPointID);
        PartAttachSurface[] surfaces = GetComponentsInChildren<PartAttachSurface>(true);

        for (int i = 0; i < surfaces.Length; i++)
        {
            PartAttachSurface surface = surfaces[i];

            if (surface != null && surface.AttachPointID == surfaceID)
            {
                return surface.ResolveAttachRoot(surface.transform.position);
            }
        }

        return null;
    }

    private string ExtractSurfaceID(string syncedAttachPointID)
    {
        if (string.IsNullOrWhiteSpace(syncedAttachPointID))
        {
            return string.Empty;
        }

        int separatorIndex = syncedAttachPointID.IndexOf(AttachPointSeparator);
        return separatorIndex >= 0 ? syncedAttachPointID.Substring(0, separatorIndex) : syncedAttachPointID;
    }

    private string ExtractBoneName(string syncedAttachPointID)
    {
        if (string.IsNullOrWhiteSpace(syncedAttachPointID))
        {
            return string.Empty;
        }

        int separatorIndex = syncedAttachPointID.IndexOf(AttachPointSeparator);

        if (separatorIndex < 0 || separatorIndex >= syncedAttachPointID.Length - 1)
        {
            return string.Empty;
        }

        return syncedAttachPointID.Substring(separatorIndex + 1);
    }

    private bool TryFindChildByName(Transform root, string childName, out Transform result)
    {
        if (root.name == childName)
        {
            result = root;
            return true;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            if (TryFindChildByName(root.GetChild(i), childName, out result))
            {
                return true;
            }
        }

        result = null;
        return false;
    }

    private void SetVisualCollidersForEquippedPart(GameObject partInstance)
    {
        Collider[] colliders = partInstance.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] rigidbodies = partInstance.GetComponentsInChildren<Rigidbody>(true);

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
        }
    }

    private void ClearSpawnedParts()
    {
        foreach (KeyValuePair<int, GameObject> pair in spawnedParts)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value);
            }
        }

        spawnedParts.Clear();
    }

    #endregion
}
