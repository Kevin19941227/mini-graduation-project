using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MapGenerator : MonoBehaviour
{
    [Header("--- 地圖資源庫 ---")]
    public List<GameObject> mapPrefabs = new List<GameObject>();

    [Header("--- 網格配置 ---")]
    public float tileSize = 1500f;
    [Min(1)] public int gridWidth = 3;
    [Min(1)] public int gridHeight = 3;

    [Header("--- 隨機與效能設定 ---")]
    public bool useRandomRotation = true;
    public bool setStatic = true;
    public int seed = 0;
    [SerializeField, Min(1)] private int tilesPerFrame = 1;

    [Header("--- Mesh GPU 上傳 ---")]
    [Tooltip("生成完畢後對所有 tile 的 Mesh 呼叫 UploadMeshData(true)，釋放 CPU 端副本節省 RAM")]
    [SerializeField] private bool uploadMeshDataOnComplete = true;

    [Header("--- 分塊串流 ---")]
    [Tooltip("依玩家距離動態啟用/停用 tile，減少 GPU 渲染範圍")]
    [SerializeField] private bool enableChunking = false;
    [Tooltip("若為空則自動尋找本地玩家")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("載入半徑（tile 數）：玩家周圍這麼多格以內的 tile 會保持啟用")]
    [SerializeField, Min(1)] private int loadRadiusTiles = 2;
    [Tooltip("卸載半徑（tile 數，需大於載入半徑）：超出後停用 tile")]
    [SerializeField, Min(2)] private int unloadRadiusTiles = 3;
    [Tooltip("每幾幀檢查一次區塊狀態")]
    [SerializeField, Min(1)] private int chunkCheckEveryNFrames = 60;

    [Header("--- 網格密度 ---")]
    [Tooltip("每個 tile 的頂點數警告上限；超過時印出提示，建議精簡 mesh 或加入 LOD Group")]
    [SerializeField, Min(100)] private int maxVerticesPerTile = 100000;

    [Header("--- 自動校正設定 ---")]
    [Tooltip("開啟後，程式會自動計算模型中心點並將其置中")]
    public bool autoCenter = true;
    [Tooltip("開啟後，程式會自動根據第一個模型的寬度來設定 tileSize")]
    public bool autoTileSize = false;

    [Header("--- 偵錯工具 ---")]
    public bool showGizmos = true;
    public Color gizmoColor = Color.white;

    [SerializeField, HideInInspector]
    private List<GameObject> spawnedMaps = new List<GameObject>();

    private readonly List<Vector3> tileGridCenters = new List<Vector3>();
    private readonly Dictionary<Vector2Int, GameObject> tileMap = new Dictionary<Vector2Int, GameObject>();
    private Vector3 _mapOrigin;

    private readonly float[] rotationAngles = { 0f, 90f, 180f, 270f };
    private Coroutine _generateRoutine;
    private System.Random _generationRandom;
    private int _chunkCheckCounter;

    public static event System.Action OnNavMeshReady;
    public static bool IsNavMeshReady { get; private set; }

    private void Update()
    {
        if (!enableChunking || tileMap.Count == 0) return;

        _chunkCheckCounter++;
        if (_chunkCheckCounter % chunkCheckEveryNFrames != 0) return;

        if (playerTransform == null)
            playerTransform = FindLocalPlayerTransform();

        if (playerTransform != null)
            UpdateChunks();
    }

    private Transform FindLocalPlayerTransform()
    {
        PlayCol[] playCols = FindObjectsOfType<PlayCol>();
        for (int i = 0; i < playCols.Length; i++)
        {
            if (playCols[i] != null && playCols[i].isLocalPlayer)
                return playCols[i].transform;
        }

        PlayerFastNetworkController[] controllers = FindObjectsOfType<PlayerFastNetworkController>();
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null && controllers[i].isLocalPlayer)
                return controllers[i].transform;
        }

        return null;
    }

    private void UpdateChunks()
    {
        Vector3 playerPos = playerTransform.position;
        int playerTileX = Mathf.RoundToInt((playerPos.x - _mapOrigin.x) / tileSize);
        int playerTileZ = Mathf.RoundToInt((playerPos.z - _mapOrigin.z) / tileSize);

        foreach (KeyValuePair<Vector2Int, GameObject> entry in tileMap)
        {
            if (entry.Value == null) continue;

            int dist = Mathf.Max(
                Mathf.Abs(entry.Key.x - playerTileX),
                Mathf.Abs(entry.Key.y - playerTileZ));

            if (dist <= loadRadiusTiles && !entry.Value.activeSelf)
                entry.Value.SetActive(true);
            else if (dist > unloadRadiusTiles && entry.Value.activeSelf)
                entry.Value.SetActive(false);
        }
    }

    public void GenerateWorld()
    {
        if (_generateRoutine != null)
            StopCoroutine(_generateRoutine);

        _generateRoutine = StartCoroutine(GenerateWorldRoutine());
    }

    private IEnumerator GenerateWorldRoutine()
    {
        if (mapPrefabs == null || mapPrefabs.Count == 0)
        {
            Debug.LogError("警告：Map Prefabs 清單是空的！");
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
        InitializeRandomSeed();
        UpdateTileSizeIfAuto();

        float offsetX = (gridWidth - 1) * tileSize * 0.5f;
        float offsetZ = (gridHeight - 1) * tileSize * 0.5f;
        _mapOrigin = transform.position + new Vector3(-offsetX, 0f, -offsetZ);

        int totalTiles = gridWidth * gridHeight;
        List<GameObject> mapPool = new List<GameObject>(totalTiles);
        var validPrefabs = mapPrefabs.Where(p => p != null).ToList();

        for (int i = 0; i < totalTiles; i++)
            mapPool.Add(validPrefabs[i % validPrefabs.Count]);

        Shuffle(mapPool);
        yield return PlaceTilesOverFrames(mapPool);

        yield return null;

        if (uploadMeshDataOnComplete)
            UploadAllTileMeshData();

        NotifyNavMeshReady();
        _generateRoutine = null;
    }

    private void UploadAllTileMeshData()
    {
        for (int i = 0; i < spawnedMaps.Count; i++)
        {
            if (spawnedMaps[i] == null) continue;

            MeshFilter[] filters = spawnedMaps[i].GetComponentsInChildren<MeshFilter>(true);
            for (int j = 0; j < filters.Length; j++)
            {
                Mesh m = filters[j].sharedMesh;
                if (m != null && m.isReadable)
                    m.UploadMeshData(true);
            }
        }
    }

    private IEnumerator PlaceTilesOverFrames(List<GameObject> mapPool)
    {
        float offsetX = (gridWidth - 1) * tileSize * 0.5f;
        float offsetZ = (gridHeight - 1) * tileSize * 0.5f;
        int spawnedThisFrame = 0;
        int centerX = gridWidth / 2;
        int centerZ = gridHeight / 2;

        for (int z = 0; z < gridHeight; z++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                Vector3 targetGridPos = transform.position + new Vector3(
                    x * tileSize - offsetX, 0, z * tileSize - offsetZ);
                Quaternion rotation = GetRandomRotation();

                GameObject tile = SpawnTile(mapPool[z * gridWidth + x], targetGridPos, rotation, x, z);
                tileGridCenters.Add(targetGridPos);

                if (tile != null)
                {
                    tileMap[new Vector2Int(x, z)] = tile;

                    WarnIfTooManyVertices(tile);

                    if (enableChunking)
                    {
                        int dist = Mathf.Max(Mathf.Abs(x - centerX), Mathf.Abs(z - centerZ));
                        if (dist > loadRadiusTiles)
                            tile.SetActive(false);
                    }
                }

                spawnedThisFrame++;
                if (spawnedThisFrame >= tilesPerFrame)
                {
                    spawnedThisFrame = 0;
                    yield return null;
                }
            }
        }
    }

    private void WarnIfTooManyVertices(GameObject tile)
    {
        int total = 0;
        MeshFilter[] filters = tile.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].sharedMesh != null)
                total += filters[i].sharedMesh.vertexCount;
        }

        if (total > maxVerticesPerTile)
            Debug.LogWarning($"[MapGenerator] '{tile.name}' 頂點數 {total:N0} 超過上限 {maxVerticesPerTile:N0}，建議精簡 mesh 或加入 LOD Group。");
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
            Debug.LogError("[MapGenerator]: 地圖清單內沒有任何 Prefab！");
            return false;
        }
        return true;
    }

    private void InitializeRandomSeed()
    {
        if (seed == 0)
            seed = System.Environment.TickCount;

        _generationRandom = new System.Random(seed);
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

    private Quaternion GetRandomRotation()
    {
        if (!useRandomRotation) return Quaternion.identity;
        int randomIndex = _generationRandom.Next(0, rotationAngles.Length);
        return Quaternion.Euler(0, rotationAngles[randomIndex], 0);
    }

    private GameObject SpawnTile(GameObject prefab, Vector3 gridPosition, Quaternion rotation, int x, int z)
    {
        GameObject instance = CreateInstance(prefab);
        if (instance == null) return null;

        instance.transform.rotation = rotation;
        ApplyPosition(instance, gridPosition);
        instance.transform.SetParent(this.transform);
        if (setStatic) instance.isStatic = true;
        instance.name = $"Tile_{x}_{z}_{prefab.name}";

        spawnedMaps.Add(instance);
        return instance;
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

    public Vector3 GetMapCenter()
    {
        if (spawnedMaps == null || spawnedMaps.Count == 0)
            return transform.position;

        float sumX = 0f, sumZ = 0f;
        int count = 0;
        foreach (var tile in spawnedMaps)
        {
            if (tile == null) continue;
            sumX += tile.transform.position.x;
            sumZ += tile.transform.position.z;
            count++;
        }
        return count > 0
            ? new Vector3(sumX / count, transform.position.y, sumZ / count)
            : transform.position;
    }

    public Vector3 GetRandomTileCenter()
    {
        return GetRandomTileCenter(tileGridCenters.Count);
    }

    public Vector3 GetRandomTileCenter(int candidateTileCount)
    {
        if (tileGridCenters.Count == 0)
        {
            Debug.LogWarning("[MapGenerator] GetRandomTileCenter: tileGridCenters is empty, falling back to transform.position");
            return transform.position;
        }

        int safeCandidateCount = Mathf.Clamp(candidateTileCount, 1, tileGridCenters.Count);
        int idx = Random.Range(0, safeCandidateCount);
        Debug.Log($"[MapGenerator] GetRandomTileCenter: count={tileGridCenters.Count}, candidates={safeCandidateCount}, picked index={idx}, pos={tileGridCenters[idx]}");
        return tileGridCenters[idx];
    }

    public void ClearOldMaps()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(transform.GetChild(i).gameObject);
            spawnedMaps.Clear();
            tileMap.Clear();
            tileGridCenters.Clear();
            return;
        }
#endif
        foreach (var map in spawnedMaps)
        {
            if (map != null) Destroy(map);
        }
        spawnedMaps.Clear();
        tileMap.Clear();
        tileGridCenters.Clear();
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = _generationRandom.Next(i, list.Count);
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
