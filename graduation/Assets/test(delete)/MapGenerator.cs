using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public struct MapTilePlacement
{
    public int prefabIndex;
    public int rotationIndex;

    public MapTilePlacement(int prefabIndex, int rotationIndex)
    {
        this.prefabIndex = prefabIndex;
        this.rotationIndex = rotationIndex;
    }
}

public class MapGenerator : MonoBehaviour
{
    [Header("--- Map Prefabs ---")]
    public List<GameObject> mapPrefabs = new List<GameObject>();

    [Header("--- Grid Settings ---")]
    public float tileSize = 1500f;
    [Min(1)] public int gridWidth = 3;
    [Min(1)] public int gridHeight = 3;

    [Header("--- Random And Performance ---")]
    public bool useRandomRotation = true;
    public bool setStatic = true;
    public int seed = 0;
    [SerializeField, Min(1)] private int tilesPerFrame = 1;

    [Header("--- Auto Alignment ---")]
    [Tooltip("Centers each spawned map by its rendered bounds.")]
    public bool autoCenter = true;
    [Tooltip("Uses the first prefab render width as tileSize.")]
    public bool autoTileSize = false;

    [Header("--- Debug ---")]
    public bool showGizmos = true;
    public Color gizmoColor = Color.white;

    [SerializeField, HideInInspector]
    private List<GameObject> spawnedMaps = new List<GameObject>();

    private readonly float[] rotationAngles = { 0f, 90f, 180f, 270f };

    private Coroutine _generateRoutine;

    public static event System.Action OnNavMeshReady;
    public static bool IsNavMeshReady { get; private set; }

    public void GenerateWorld()
    {
        GenerateWorld(null);
    }

    public void GenerateWorld(MapTilePlacement[] layout)
    {
        if (_generateRoutine != null)
        {
            StopCoroutine(_generateRoutine);
        }

        _generateRoutine = StartCoroutine(GenerateWorldRoutine(layout));
    }

    private System.Collections.IEnumerator GenerateWorldRoutine(MapTilePlacement[] layout)
    {
        if (mapPrefabs == null || mapPrefabs.Count == 0)
        {
            Debug.LogError("Map Prefabs is empty.");
            _generateRoutine = null;
            yield break;
        }

        if (!ValidatePrefabs())
        {
            _generateRoutine = null;
            yield break;
        }

        IsNavMeshReady = false;
        ClearOldMaps();
        UpdateTileSizeIfAuto();

        if (layout == null || layout.Length == 0)
        {
            int layoutSeed = seed != 0 ? seed : System.Environment.TickCount;
            seed = layoutSeed;
            layout = CreateLayout(layoutSeed);
        }

        if (!ValidateLayout(layout))
        {
            _generateRoutine = null;
            yield break;
        }

        yield return PlaceTilesOverFrames(layout);

        yield return null;
        NotifyNavMeshReady();

        _generateRoutine = null;
    }

    private void NotifyNavMeshReady()
    {
        IsNavMeshReady = true;
        OnNavMeshReady?.Invoke();
    }

    private bool ValidatePrefabs()
    {
        if (mapPrefabs == null || mapPrefabs.Count == 0 || mapPrefabs.All(p => p == null))
        {
            Debug.LogError("[MapGenerator] Map Prefabs is empty or all entries are missing.");
            return false;
        }
        return true;
    }

    public MapTilePlacement[] CreateLayout(int layoutSeed)
    {
        if (!ValidatePrefabs())
        {
            return new MapTilePlacement[0];
        }

        int totalTiles = gridWidth * gridHeight;
        List<int> validPrefabIndices = new List<int>();

        for (int i = 0; i < mapPrefabs.Count; i++)
        {
            if (mapPrefabs[i] != null)
            {
                validPrefabIndices.Add(i);
            }
        }

        if (validPrefabIndices.Count == 0)
        {
            return new MapTilePlacement[0];
        }

        System.Random layoutRandom = new System.Random(NormalizeSeed(layoutSeed));
        List<int> prefabPool = new List<int>(totalTiles);

        for (int i = 0; i < totalTiles; i++)
        {
            prefabPool.Add(validPrefabIndices[i % validPrefabIndices.Count]);
        }

        Shuffle(prefabPool, layoutRandom);

        MapTilePlacement[] layout = new MapTilePlacement[totalTiles];
        for (int i = 0; i < totalTiles; i++)
        {
            layout[i] = new MapTilePlacement(prefabPool[i], GetRandomRotationIndex(layoutRandom));
        }

        return layout;
    }

    private static int NormalizeSeed(int value)
    {
        return value == int.MinValue ? int.MaxValue : System.Math.Abs(value);
    }

    private void UpdateTileSizeIfAuto()
    {
        if (autoTileSize)
        {
            var firstPrefab = mapPrefabs.FirstOrDefault(p => p != null);
            if (firstPrefab != null)
                tileSize = GetTargetBounds(firstPrefab).size.x;
        }
    }

    private bool ValidateLayout(MapTilePlacement[] layout)
    {
        int totalTiles = gridWidth * gridHeight;
        if (layout == null || layout.Length != totalTiles)
        {
            Debug.LogError($"[MapGenerator] Layout tile count mismatch. Expected {totalTiles}, got {(layout == null ? 0 : layout.Length)}.");
            return false;
        }

        for (int i = 0; i < layout.Length; i++)
        {
            int prefabIndex = layout[i].prefabIndex;
            if (prefabIndex < 0 || prefabIndex >= mapPrefabs.Count || mapPrefabs[prefabIndex] == null)
            {
                Debug.LogError($"[MapGenerator] Layout has invalid prefab index {prefabIndex} at tile {i}.");
                return false;
            }
        }

        return true;
    }

    private System.Collections.IEnumerator PlaceTilesOverFrames(MapTilePlacement[] layout)
    {
        float offsetX = (gridWidth - 1) * tileSize * 0.5f;
        float offsetZ = (gridHeight - 1) * tileSize * 0.5f;
        int spawnedThisFrame = 0;

        for (int z = 0; z < gridHeight; z++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                Vector3 targetGridPos = transform.position + new Vector3(
                    x * tileSize - offsetX, 0, z * tileSize - offsetZ);
                int tileIndex = z * gridWidth + x;
                MapTilePlacement placement = layout[tileIndex];
                Quaternion rotation = GetRotationByIndex(placement.rotationIndex);
                SpawnTile(mapPrefabs[placement.prefabIndex], targetGridPos, rotation, x, z);

                spawnedThisFrame++;
                if (spawnedThisFrame >= tilesPerFrame)
                {
                    spawnedThisFrame = 0;
                    yield return null;
                }
            }
        }
    }

    private int GetRandomRotationIndex(System.Random layoutRandom)
    {
        return useRandomRotation ? layoutRandom.Next(rotationAngles.Length) : 0;
    }

    private Quaternion GetRotationByIndex(int rotationIndex)
    {
        int safeIndex = rotationIndex % rotationAngles.Length;
        if (safeIndex < 0)
        {
            safeIndex += rotationAngles.Length;
        }

        return Quaternion.Euler(0, rotationAngles[safeIndex], 0);
    }

    private void SpawnTile(GameObject prefab, Vector3 gridPosition, Quaternion rotation, int x, int z)
    {
        GameObject instance = CreateInstance(prefab);
        if (instance == null) return;

        instance.transform.rotation = rotation;
        ApplyPosition(instance, gridPosition);
        instance.transform.SetParent(this.transform);
        if (setStatic) instance.isStatic = true;
        instance.name = $"Tile_{x}_{z}_{prefab.name}";

        spawnedMaps.Add(instance);
    }

    private GameObject CreateInstance(GameObject prefab)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Generate Map Tile");
            return instance;
        }
#endif
        return Instantiate(prefab);
    }

    private void ApplyPosition(GameObject instance, Vector3 gridPosition)
    {
        if (autoCenter)
        {
            Bounds b = GetTargetBounds(instance);
            Vector3 centerOffset = instance.transform.position - b.center;
            centerOffset.y = 0;
            instance.transform.position = gridPosition + centerOffset;
        }
        else
        {
            instance.transform.position = gridPosition;
        }
    }

    private Bounds GetTargetBounds(GameObject obj)
    {
        Renderer[] rs = obj.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(obj.transform.position, Vector3.zero);

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++)
            b.Encapsulate(rs[i].bounds);
        return b;
    }

    public void ClearOldMaps()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(transform.GetChild(i).gameObject);
            spawnedMaps.Clear();
            return;
        }
#endif
        foreach (var map in spawnedMaps)
        {
            if (map != null) Destroy(map);
        }
        spawnedMaps.Clear();
    }

    private void Shuffle<T>(List<T> list, System.Random layoutRandom)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = layoutRandom.Next(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = gizmoColor;
        float offsetX = (gridWidth - 1) * tileSize * 0.5f;
        float offsetZ = (gridHeight - 1) * tileSize * 0.5f;

        for (int z = 0; z < gridHeight; z++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                Vector3 center = transform.position + new Vector3(
                    x * tileSize - offsetX, 0, z * tileSize - offsetZ);
                Gizmos.DrawWireCube(center, new Vector3(tileSize, 0.1f, tileSize));
            }
        }
    }
}
