using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

public class MonsterSpawner : NetworkBehaviour
{
    private const float FullRotationDegrees = 360f;
    private const float NavMeshDataWaitTimeout = 10f;

    #region Inspector Settings

    [Header("Monster Prefabs")]
    [SerializeField] private List<GameObject> monsterPrefabs = new List<GameObject>();
    [SerializeField, Min(1)] private int spawnCountPerPrefab = 1;

    [Header("NavMesh Spawn Settings")]
    [SerializeField, Min(1)] private int spawnAttemptsPerMonster = 24;
    [SerializeField, Min(0.1f)] private float navMeshSampleDistance = 4f;
    [SerializeField, Min(1)] private int monstersPerFrame = 1;

    [Header("Spawn Validation")]
    [Tooltip("是否限制生成高度，用來避免怪物出現在屋頂（需先確認地面實際 Y 高度再開啟）")]
    [SerializeField] private bool useHeightConstraint = false;
    [Tooltip("允許生成的最大 Y 高度（地面通常在 0 附近，依實際場景調整）")]
    [SerializeField] private float maxSpawnHeight = 2f;
    [Tooltip("建築物所在的 Layer，用射線偵測是否在建築物內部或屋頂（設為 Nothing 則停用）")]
    [SerializeField] private LayerMask obstacleLayerMask = 0;
    [Tooltip("往上射線的最大距離，用來偵測天花板（建築物內部）")]
    [SerializeField, Min(1f)] private float ceilingCheckDistance = 50f;
    [Tooltip("往下射線的距離，用來偵測腳下是否為建築物屋頂")]
    [SerializeField, Min(0.1f)] private float rooftopCheckDistance = 1.5f;

    #endregion

    #region Runtime State

    private readonly List<GameObject> _spawnedMonsters = new List<GameObject>();
    private bool _hasSpawned;
    private bool _spawnRequested;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        MapGenerator.OnNavMeshReady += OnNavMeshReady;
    }

    private void OnDisable()
    {
        MapGenerator.OnNavMeshReady -= OnNavMeshReady;
    }

    #endregion

    #region Mirror Callbacks

    /// <summary>Requests server-side monster spawning when the spawner becomes active on the server.</summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        RequestSpawn();
    }

    /// <summary>Registers monster prefabs so clients can instantiate server-spawned monsters.</summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        RegisterClientSpawnPrefabs();
    }

    #endregion

    #region Client Spawn Registration

    private void RegisterClientSpawnPrefabs()
    {
        if (monsterPrefabs == null) return;

        for (int i = 0; i < monsterPrefabs.Count; i++)
        {
            GameObject prefab = monsterPrefabs[i];
            if (prefab == null) continue;

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogWarning($"[MonsterSpawner] {prefab.name} has no NetworkIdentity and cannot be client-spawned.");
                continue;
            }

            NetworkClient.RegisterPrefab(prefab);
        }
    }

    #endregion

    #region Spawn Flow

    private void OnNavMeshReady()
    {
        if (!NetworkServer.active) return;

        RequestSpawn();
    }

    private void RequestSpawn()
    {
        if (_spawnRequested || _hasSpawned) return;

        _spawnRequested = true;
        StartCoroutine(SpawnWhenReady());
    }

    private System.Collections.IEnumerator SpawnWhenReady()
    {
        while (!MapGenerator.IsNavMeshReady)
        {
            yield return null;
        }

        float navMeshWaitTime = 0f;
        while (!HasReadableNavMesh() && navMeshWaitTime < NavMeshDataWaitTimeout)
        {
            navMeshWaitTime += Time.deltaTime;
            yield return null;
        }

        if (!HasReadableNavMesh())
        {
            Debug.LogWarning("[MonsterSpawner] Map is ready, but no readable NavMesh data was found.");
            _spawnRequested = false;
            yield break;
        }

        yield return SpawnAllOverFrames();
        _spawnRequested = false;
    }

    private System.Collections.IEnumerator SpawnAllOverFrames()
    {
        if (_hasSpawned) yield break;

        if (monsterPrefabs == null || monsterPrefabs.Count == 0)
        {
            Debug.LogWarning("[MonsterSpawner] No monster prefabs assigned.");
            yield break;
        }

        _hasSpawned = true;
        int spawnedThisFrame = 0;

        for (int prefabIndex = 0; prefabIndex < monsterPrefabs.Count; prefabIndex++)
        {
            GameObject prefab = monsterPrefabs[prefabIndex];
            if (prefab == null) continue;

            for (int count = 0; count < spawnCountPerPrefab; count++)
            {
                SpawnOne(prefab);
                spawnedThisFrame++;

                if (spawnedThisFrame >= monstersPerFrame)
                {
                    spawnedThisFrame = 0;
                    yield return null;
                }
            }
        }
    }

    private void SpawnOne(GameObject prefab)
    {
        if (!TryGetRandomNavMeshPosition(out Vector3 spawnPosition))
        {
            Debug.LogWarning($"[MonsterSpawner] Cannot find a valid NavMesh spawn position for {prefab.name}.");
            return;
        }

        Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, FullRotationDegrees), 0f);
        GameObject monster = Instantiate(prefab, spawnPosition, spawnRotation);

        NetworkServer.Spawn(monster);
        _spawnedMonsters.Add(monster);

        Debug.Log($"[MonsterSpawner] Spawned {prefab.name} at {spawnPosition}.");
    }

    #endregion

    #region NavMesh Sampling

    private bool TryGetRandomNavMeshPosition(out Vector3 position)
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        Vector3[] vertices = triangulation.vertices;
        int[] indices = triangulation.indices;

        if (vertices == null || indices == null || vertices.Length == 0 || indices.Length < 3)
        {
            position = Vector3.zero;
            return false;
        }

        int triangleCount = indices.Length / 3;

        for (int attempt = 0; attempt < spawnAttemptsPerMonster; attempt++)
        {
            int triangleStartIndex = Random.Range(0, triangleCount) * 3;
            Vector3 randomPoint = GetRandomPointInTriangle(
                vertices[indices[triangleStartIndex]],
                vertices[indices[triangleStartIndex + 1]],
                vertices[indices[triangleStartIndex + 2]]);

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                if (IsValidSpawnPosition(hit.position))
                {
                    position = hit.position;
                    return true;
                }
            }
        }

        position = Vector3.zero;
        return false;
    }

    private bool IsValidSpawnPosition(Vector3 position)
    {
        if (useHeightConstraint && position.y > maxSpawnHeight)
        {
            Debug.Log($"[MonsterSpawner] 拒絕位置 {position}：高度 {position.y:F1} > maxSpawnHeight {maxSpawnHeight}");
            return false;
        }

        if (obstacleLayerMask != 0)
        {
            // 往上射線：打到天花板 → 在建築物內部
            if (Physics.Raycast(position + Vector3.up * 0.05f, Vector3.up, ceilingCheckDistance, obstacleLayerMask))
            {
                Debug.Log($"[MonsterSpawner] 拒絕位置 {position}：上方有建築物天花板（在內部）");
                return false;
            }

            // 往下射線：腳下就是建築物表面 → 站在屋頂
            if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, rooftopCheckDistance, obstacleLayerMask))
            {
                Debug.Log($"[MonsterSpawner] 拒絕位置 {position}：腳下是建築物屋頂");
                return false;
            }
        }

        return true;
    }

    private static bool HasReadableNavMesh()
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        return triangulation.vertices != null
            && triangulation.indices != null
            && triangulation.vertices.Length > 0
            && triangulation.indices.Length >= 3;
    }

    private static Vector3 GetRandomPointInTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        float u = Random.value;
        float v = Random.value;

        if (u + v > 1f)
        {
            u = 1f - u;
            v = 1f - v;
        }

        return a + u * (b - a) + v * (c - a);
    }

    #endregion

    #region Public API

    /// <summary>Destroys all monsters spawned by this server spawner and allows spawning again.</summary>
    public void DespawnAll()
    {
        if (!isServer) return;

        for (int i = _spawnedMonsters.Count - 1; i >= 0; i--)
        {
            GameObject monster = _spawnedMonsters[i];
            if (monster != null)
            {
                NetworkServer.Destroy(monster);
            }
        }

        _spawnedMonsters.Clear();
        _hasSpawned = false;
        _spawnRequested = false;
    }

    #endregion
}
