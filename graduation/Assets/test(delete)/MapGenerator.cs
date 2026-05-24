using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 優化記憶體分配、邊界運算與程式碼結構 (Clean Code)
/// </summary>
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

    // 效能優化：快取旋轉角度陣列，避免在迴圈中重複分配記憶體
    private readonly float[] rotationAngles = { 0f, 90f, 180f, 270f };

    public void GenerateWorld()
    {
        if (mapPrefabs == null || mapPrefabs.Count == 0)
        {
            Debug.LogError("警告：Map Prefabs 清單是空的！無法生成地圖，請檢查 Inspector 設定。");
            return; // 直接中止生成，保護遊戲不崩潰
        }

        if (!ValidatePrefabs()) return;

        ClearOldMaps();
        InitializeRandomSeed();
        UpdateTileSizeIfAuto();

        int totalTiles = gridWidth * gridHeight;

        // 效能優化：預先分配 List 容量，減少動態擴容的 GC 消耗
        List<GameObject> mapPool = new List<GameObject>(totalTiles);
        var validPrefabs = mapPrefabs.Where(p => p != null).ToList();

        for (int i = 0; i < totalTiles; i++)
        {
            mapPool.Add(validPrefabs[i % validPrefabs.Count]);
        }

        Shuffle(mapPool);
        PlaceTiles(mapPool);
    }

    // --- 以下將複雜邏輯拆分為獨立方法 (Clean Code) ---

    private bool ValidatePrefabs()
    {
        if (mapPrefabs == null || mapPrefabs.Count == 0 || mapPrefabs.All(p => p == null))
        {
            Debug.LogError("[Carzy man]: 地圖清單內沒有任何 Prefab，請先拖入地圖物件！");
            return false;
        }
        return true;
    }

    private void InitializeRandomSeed()
    {
        if (seed != 0) Random.InitState(seed);
        else Random.InitState(System.Environment.TickCount);
    }

    private void UpdateTileSizeIfAuto()
    {
        if (autoTileSize)
        {
            var firstPrefab = mapPrefabs.FirstOrDefault(p => p != null);
            if (firstPrefab != null)
            {
                tileSize = GetTargetBounds(firstPrefab).size.x;
            }
        }
    }

    private void PlaceTiles(List<GameObject> mapPool)
    {
        float offsetX = (gridWidth - 1) * tileSize * 0.5f;
        float offsetZ = (gridHeight - 1) * tileSize * 0.5f;

        for (int z = 0; z < gridHeight; z++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                Vector3 targetGridPos = transform.position + new Vector3(x * tileSize - offsetX, 0, z * tileSize - offsetZ);
                Quaternion rotation = GetRandomRotation();
                SpawnTile(mapPool[z * gridWidth + x], targetGridPos, rotation, x, z);
            }
        }
    }

    private Quaternion GetRandomRotation()
    {
        if (!useRandomRotation) return Quaternion.identity;
        return Quaternion.Euler(0, rotationAngles[Random.Range(0, rotationAngles.Length)], 0);
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
        // 效能優化：使用 for 迴圈取代 foreach，減少陣列迭代器的額外開銷
        for (int i = 1; i < rs.Length; i++)
        {
            b.Encapsulate(rs[i].bounds);
        }
        return b;
    }

    public void ClearOldMaps()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(transform.GetChild(i).gameObject);
            }
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

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
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
                Vector3 center = transform.position + new Vector3(x * tileSize - offsetX, 0, z * tileSize - offsetZ);
                Gizmos.DrawWireCube(center, new Vector3(tileSize, 0.1f, tileSize));
            }
        }
    }
}

//#if UNITY_EDITOR
//[CustomEditor(typeof(MapGenerator))]
//public class MapGeneratorEditor : Editor
//{
//    public override void OnInspectorGUI()
//    {
//        serializedObject.Update();
//        DrawDefaultInspector();

//        MapGenerator script = (MapGenerator)target;
//        GUILayout.Space(15);
//        using (new EditorGUILayout.HorizontalScope())
//        {
//            GUI.backgroundColor = Color.green;
//            if (GUILayout.Button("立即生成隨機世界", GUILayout.Height(40))) script.GenerateWorld();
//            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
//            if (GUILayout.Button("清空", GUILayout.Height(40), GUILayout.Width(60))) script.ClearOldMaps();
//        }
//        serializedObject.ApplyModifiedProperties();
//    }
//}
//#endif