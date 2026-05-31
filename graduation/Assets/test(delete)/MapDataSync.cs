using Mirror;
using System.Text;
using UnityEngine;

/// <summary>
/// Synchronizes the server-authored procedural map layout to every client.
/// </summary>
public class MapDataSync : NetworkBehaviour
{
    private const float LayoutWaitTimeout = 10f;
    private const char TileDelimiter = ';';
    private const char ValueDelimiter = ',';

    #region Inspector Settings

    [Header("Map Generator")]
    [SerializeField] private MapGenerator localMapGenerator;

    #endregion

    #region SyncVar State

    [SyncVar]
    private int _mapSeed;

    [SyncVar(hook = nameof(OnLayoutChanged))]
    private string _mapLayout;

    private bool _mapGenerated;
    private bool _waitingForLayout;

    #endregion

    #region Mirror Callbacks

    /// <summary>Generates the authoritative server map layout and publishes it.</summary>
    public override void OnStartServer()
    {
        base.OnStartServer();

        if (!TryResolveMapGenerator())
        {
            Debug.LogError("[MapDataSync] Server cannot find a MapGenerator.");
            return;
        }

        _mapSeed = System.Environment.TickCount;
        MapTilePlacement[] layout = localMapGenerator.CreateLayout(_mapSeed);
        if (layout == null || layout.Length == 0)
        {
            Debug.LogError("[MapDataSync] Server could not create a valid map layout.");
            return;
        }

        _mapLayout = EncodeLayout(layout);

        GenerateMap(layout);
        Debug.Log($"[MapDataSync] Server generated map layout with seed {_mapSeed}.");
    }

    /// <summary>Starts client-side map generation once the synchronized layout is available.</summary>
    public override void OnStartClient()
    {
        base.OnStartClient();

        if (isServer) return;

        TryGenerateClientMap(_mapLayout);

        if (!_mapGenerated && !_waitingForLayout)
        {
            StartCoroutine(WaitForLayoutAndGenerate());
        }
    }

    #endregion

    #region Layout Sync

    private void OnLayoutChanged(string oldLayout, string newLayout)
    {
        if (isServer) return;

        TryGenerateClientMap(newLayout);
    }

    private System.Collections.IEnumerator WaitForLayoutAndGenerate()
    {
        _waitingForLayout = true;
        float elapsed = 0f;

        while (!_mapGenerated && string.IsNullOrEmpty(_mapLayout) && elapsed < LayoutWaitTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!_mapGenerated)
        {
            TryGenerateClientMap(_mapLayout);
        }

        if (!_mapGenerated)
        {
            Debug.LogError("[MapDataSync] Client did not receive a valid map layout.");
        }

        _waitingForLayout = false;
    }

    private void TryGenerateClientMap(string layoutText)
    {
        if (_mapGenerated || string.IsNullOrEmpty(layoutText)) return;

        if (!TryResolveMapGenerator())
        {
            Debug.LogError("[MapDataSync] Client cannot find a MapGenerator.");
            return;
        }

        if (!TryDecodeLayout(layoutText, out MapTilePlacement[] layout))
        {
            Debug.LogError("[MapDataSync] Client received an invalid map layout.");
            return;
        }

        GenerateMap(layout);
        Debug.Log($"[MapDataSync] Client generated map layout with seed {_mapSeed}.");
    }

    #endregion

    #region Map Generation

    private bool TryResolveMapGenerator()
    {
        if (localMapGenerator != null) return true;

        localMapGenerator = FindObjectOfType<MapGenerator>();
        return localMapGenerator != null;
    }

    private void GenerateMap(MapTilePlacement[] layout)
    {
        _mapGenerated = true;
        localMapGenerator.seed = _mapSeed;
        localMapGenerator.GenerateWorld(layout);
    }

    private static string EncodeLayout(MapTilePlacement[] layout)
    {
        if (layout == null || layout.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(layout.Length * 4);
        for (int i = 0; i < layout.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(TileDelimiter);
            }

            builder.Append(layout[i].prefabIndex);
            builder.Append(ValueDelimiter);
            builder.Append(layout[i].rotationIndex);
        }

        return builder.ToString();
    }

    private static bool TryDecodeLayout(string layoutText, out MapTilePlacement[] layout)
    {
        layout = null;
        if (string.IsNullOrEmpty(layoutText))
        {
            return false;
        }

        string[] tileTexts = layoutText.Split(TileDelimiter);
        MapTilePlacement[] decodedLayout = new MapTilePlacement[tileTexts.Length];

        for (int i = 0; i < tileTexts.Length; i++)
        {
            string[] values = tileTexts[i].Split(ValueDelimiter);
            if (values.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(values[0], out int prefabIndex)
                || !int.TryParse(values[1], out int rotationIndex))
            {
                return false;
            }

            decodedLayout[i] = new MapTilePlacement(prefabIndex, rotationIndex);
        }

        layout = decodedLayout;
        return true;
    }

    #endregion
}
