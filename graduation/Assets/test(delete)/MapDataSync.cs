using Mirror;
using UnityEngine;

/// <summary>
/// Synchronizes the procedural map seed from the server to every client.
/// </summary>
public class MapDataSync : NetworkBehaviour
{
    private const float SeedWaitTimeout = 10f;

    #region Inspector Settings

    [Header("Map Generator")]
    [SerializeField] private MapGenerator localMapGenerator;

    #endregion

    #region SyncVar State

    [SyncVar(hook = nameof(OnSeedChanged))]
    private int _mapSeed;

    private bool _mapGenerated;
    private bool _waitingForSeed;

    #endregion

    #region Mirror Callbacks

    /// <summary>Generates the authoritative server map and publishes its seed.</summary>
    public override void OnStartServer()
    {
        base.OnStartServer();

        if (!TryResolveMapGenerator())
        {
            Debug.LogError("[MapDataSync] Server cannot find a MapGenerator.");
            return;
        }

        _mapSeed = System.Environment.TickCount;
        GenerateMap(_mapSeed);
        Debug.Log($"[MapDataSync] Server generated map with seed {_mapSeed}.");
    }

    /// <summary>Starts client-side map generation once the synchronized seed is available.</summary>
    public override void OnStartClient()
    {
        base.OnStartClient();

        if (isServer) return;

        TryGenerateClientMap(_mapSeed);

        if (!_mapGenerated && !_waitingForSeed)
        {
            StartCoroutine(WaitForSeedAndGenerate());
        }
    }

    #endregion

    #region Seed Sync

    private void OnSeedChanged(int oldSeed, int newSeed)
    {
        if (isServer) return;

        TryGenerateClientMap(newSeed);
    }

    private System.Collections.IEnumerator WaitForSeedAndGenerate()
    {
        _waitingForSeed = true;
        float elapsed = 0f;

        while (!_mapGenerated && _mapSeed == 0 && elapsed < SeedWaitTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!_mapGenerated)
        {
            TryGenerateClientMap(_mapSeed);
        }

        if (!_mapGenerated)
        {
            Debug.LogError("[MapDataSync] Client did not receive a valid map seed.");
        }

        _waitingForSeed = false;
    }

    private void TryGenerateClientMap(int seed)
    {
        if (_mapGenerated || seed == 0) return;

        if (!TryResolveMapGenerator())
        {
            Debug.LogError("[MapDataSync] Client cannot find a MapGenerator.");
            return;
        }

        GenerateMap(seed);
        Debug.Log($"[MapDataSync] Client generated map with seed {seed}.");
    }

    #endregion

    #region Map Generation

    private bool TryResolveMapGenerator()
    {
        if (localMapGenerator != null) return true;

        localMapGenerator = FindObjectOfType<MapGenerator>();
        return localMapGenerator != null;
    }

    private void GenerateMap(int seed)
    {
        _mapGenerated = true;
        localMapGenerator.seed = seed;
        localMapGenerator.GenerateWorld();
    }

    #endregion
}
