using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class SceneLootPickup : MonoBehaviour
{
    private const float DefaultInteractRadius = 2f;
    private const float DefaultVisualScale = 0.5f;
    private const float SpinDegreesPerSecond = 60f;

    private static readonly Dictionary<int, ServerLootData> ServerLoot = new Dictionary<int, ServerLootData>();
    private static readonly Dictionary<int, SceneLootPickup> ClientLoot = new Dictionary<int, SceneLootPickup>();
    private static int nextLootId = 1;

    private int lootId;
    private int partId;
    private int count;
    private float interactRadius;
    private bool localPlayerInside;

    private struct ServerLootData
    {
        public int partId;
        public int count;
        public Vector3 position;
        public bool picked;
    }

    #region Server API

    [Server]
    public static int ServerRegisterLoot(int partId, int count, Vector3 position)
    {
        if (partId <= 0 || count <= 0)
        {
            return 0;
        }

        int id = nextLootId++;
        ServerLoot[id] = new ServerLootData
        {
            partId = partId,
            count = count,
            position = position,
            picked = false
        };

        return id;
    }

    [Server]
    public static bool ServerTryPickup(int id, PlayCol player, float pickupRadius)
    {
        if (player == null || !ServerLoot.TryGetValue(id, out ServerLootData loot) || loot.picked)
        {
            return false;
        }

        float radius = Mathf.Max(DefaultInteractRadius, pickupRadius);
        if ((player.transform.position - loot.position).sqrMagnitude > radius * radius)
        {
            Debug.Log($"[SceneLoot] {player.name} is too far to pick lootId={id}.");
            return false;
        }

        PlayerInventoryNetwork inventory = player.GetComponent<PlayerInventoryNetwork>();
        if (inventory == null)
        {
            Debug.LogWarning($"[SceneLoot] {player.name} has no PlayerInventoryNetwork.");
            return false;
        }

        if (!inventory.ServerAddPart(loot.partId, loot.count))
        {
            return false;
        }

        loot.picked = true;
        ServerLoot[id] = loot;
        ServerLoot.Remove(id);
        Debug.Log($"[SceneLoot] {player.name} picked partID={loot.partId} x{loot.count}.");
        return true;
    }

    #endregion

    #region Client API

    public static void ClientSpawnLoot(int id, int spawnedPartId, int spawnedCount, Vector3 position, GameObject visualPrefab)
    {
        if (id <= 0)
        {
            return;
        }

        visualPrefab = ResolveVisualPrefab(spawnedPartId, visualPrefab);
        ClientRemoveLoot(id);

        GameObject lootObject = new GameObject($"Loot_{spawnedPartId}_{id}");
        lootObject.transform.position = position;

        SceneLootPickup pickup = lootObject.AddComponent<SceneLootPickup>();
        pickup.Initialize(id, spawnedPartId, spawnedCount, DefaultInteractRadius);

        CreateVisual(lootObject.transform, visualPrefab, spawnedPartId);

        SphereCollider trigger = lootObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = DefaultInteractRadius;

        ClientLoot[id] = pickup;
        Debug.Log($"[SceneLoot] Client spawned lootId={id}, partID={spawnedPartId} x{spawnedCount} at {position}.");
    }

    public static void ClientRemoveLoot(int id)
    {
        if (!ClientLoot.TryGetValue(id, out SceneLootPickup pickup))
        {
            return;
        }

        ClientLoot.Remove(id);
        if (pickup != null)
        {
            Destroy(pickup.gameObject);
        }
    }

    private static void DisableVisualColliders(GameObject visual)
    {
        Collider[] colliders = visual.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private static GameObject ResolveVisualPrefab(int spawnedPartId, GameObject visualPrefab)
    {
        if (visualPrefab != null || spawnedPartId <= 0)
        {
            return visualPrefab;
        }

        GameDatabase[] databases = Resources.FindObjectsOfTypeAll<GameDatabase>();
        for (int i = 0; i < databases.Length; i++)
        {
            GameDatabase database = databases[i];
            if (database == null) continue;

            PartData partData = database.GetPartData(spawnedPartId);
            if (partData == null || partData.partPrefab == null) continue;

            return partData.partPrefab;
        }

        Debug.LogWarning($"[SceneLoot] Missing visual prefab for partID={spawnedPartId}. Using cube fallback.");
        return null;
    }

    private static void CreateVisual(Transform parent, GameObject visualPrefab, int spawnedPartId)
    {
        GameObject visual;
        if (visualPrefab != null)
        {
            visual = Instantiate(visualPrefab, parent);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = $"LootVisual_{spawnedPartId}";
            visual.transform.SetParent(parent, false);
        }

        visual.transform.localScale = Vector3.one * DefaultVisualScale;
        DisableVisualColliders(visual);
    }

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        transform.Rotate(Vector3.up, SpinDegreesPerSecond * Time.deltaTime, Space.World);

        PlayCol localPlayer = GetLocalPlayer();
        localPlayerInside = IsPlayerInRange(localPlayer);

        if (!localPlayerInside || Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame)
        {
            return;
        }

        localPlayer.CmdTryPickupSceneLoot(lootId);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayCol player = other.GetComponentInParent<PlayCol>();
        if (player != null && player.isLocalPlayer)
        {
            localPlayerInside = true;
            Debug.Log($"[SceneLoot] Press F to pick partID={partId} x{count}.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayCol player = other.GetComponentInParent<PlayCol>();
        if (player != null && player.isLocalPlayer)
        {
            localPlayerInside = false;
        }
    }

    #endregion

    private void Initialize(int initializedLootId, int initializedPartId, int initializedCount, float initializedInteractRadius)
    {
        lootId = initializedLootId;
        partId = initializedPartId;
        count = initializedCount;
        interactRadius = initializedInteractRadius;
    }

    private static PlayCol GetLocalPlayer()
    {
        if (NetworkClient.localPlayer == null)
        {
            return null;
        }

        return NetworkClient.localPlayer.GetComponent<PlayCol>();
    }

    private bool IsPlayerInRange(PlayCol player)
    {
        if (player == null)
        {
            return false;
        }

        float radius = Mathf.Max(DefaultInteractRadius, interactRadius);
        return (player.transform.position - transform.position).sqrMagnitude <= radius * radius;
    }
}
