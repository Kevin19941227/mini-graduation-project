using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class MonsterSpawner : NetworkBehaviour
{
    [Header("怪物清單（Prefab 需有 NetworkIdentity / NetworkTransform / NavMeshAgent / MonsterAI）")]
    public List<GameObject> monsterPrefabs = new List<GameObject>();

    [Header("固定生成點（在場景放空物件，拖進來）")]
    public List<Transform> spawnPoints = new List<Transform>();

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _hasSpawned;

    void OnEnable()  => MapGenerator.OnNavMeshReady += OnNavMeshReady;
    void OnDisable() => MapGenerator.OnNavMeshReady -= OnNavMeshReady;

    /// <summary>Server 啟動後，如果地圖已經準備好就補生成怪物。</summary>
    public override void OnStartServer()
    {
        base.OnStartServer();

        if (MapGenerator.IsNavMeshReady)
            StartCoroutine(SpawnAll());
    }

    private void OnNavMeshReady()
    {
        if (!NetworkServer.active) return;
        StartCoroutine(SpawnAll());
    }

    private System.Collections.IEnumerator SpawnAll()
    {
        if (_hasSpawned) yield break;
        yield return null;

        if (monsterPrefabs == null || monsterPrefabs.Count == 0)
        {
            Debug.LogWarning("[MonsterSpawner] 沒有設定怪物 Prefab！");
            yield break;
        }

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("[MonsterSpawner] 沒有設定生成點！請在場景放空物件並拖進 Spawn Points。");
            yield break;
        }

        // 怪物 i 對應生成點 i，生成點不夠則循環使用
        _hasSpawned = true;

        int pointIndex = 0;
        foreach (var prefab in monsterPrefabs)
        {
            if (prefab == null) continue;

            Transform point = spawnPoints[pointIndex % spawnPoints.Count];
            pointIndex++;

            if (point == null) continue;

            var monster = Instantiate(prefab, point.position, point.rotation);
            NetworkServer.Spawn(monster);
            _spawned.Add(monster);
            Debug.Log($"[MonsterSpawner] {prefab.name} 生成於 {point.position}");
        }
    }

    public void DespawnAll()
    {
        if (!isServer) return;
        foreach (var m in _spawned)
            if (m != null) NetworkServer.Destroy(m);
        _spawned.Clear();
        _hasSpawned = false;
    }
}
