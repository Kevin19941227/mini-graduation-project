using Mirror;
using UnityEngine;

/// <summary>
/// 網路地圖管理器 (網路同步層)
/// 負責由 Server 產生 Seed 並同步給所有 Client，讓大家生成相同的地圖。
/// </summary>
public class NetworkMapManager : NetworkBehaviour
{
    [Header("--- 依賴的系統 ---")]
    [Tooltip("拖入你寫好的 MapGenerator")]
    [SerializeField] private MapGenerator mapGenerator;

    // [SyncVar]：Server 改變數值時，會自動廣播給所有 Client
    // hook：當 Client 收到新數值時，自動呼叫對應的方法
    [SyncVar(hook = nameof(OnSeedChanged))]
    private int mapSeed;

    #region Mirror Callbacks (生命週期)

    /// <summary>
    /// 只有 Server 會執行：遊戲開始時，決定這場遊戲的地圖 Seed
    /// </summary>
    public override void OnStartServer()
    {
        // 隨機產生一個大於 0 的 Seed
        mapSeed = UnityEngine.Random.Range(1, int.MaxValue);
        
        // Server 本身（如果是 Host）也要在本地端生成地圖
        GenerateMapLocally(mapSeed);
    }

    /// <summary>
    /// Client 初始化時執行：處理「中途加入」的玩家
    /// </summary>
    public override void OnStartClient()
    {
        // 如果這個 Client 是後加入的，SyncVar 已經有值了，強制它生成地圖
        // (如果是 Host，isServer 為 true，剛才在 OnStartServer 已經生成過了，就不重複做)
        if (mapSeed != 0 && !isServer)
        {
            GenerateMapLocally(mapSeed);
        }
    }

    #endregion

    #region 網路同步與本地生成邏輯

    /// <summary>
    /// 當 Client 收到 Server 傳來的新 Seed 時觸發
    /// </summary>
    private void OnSeedChanged(int oldSeed, int newSeed)
    {
        GenerateMapLocally(newSeed);
    }

    /// <summary>
    /// 呼叫底層邏輯層，在本地生成實際物件
    /// </summary>
    private void GenerateMapLocally(int seed)
    {
        if (mapGenerator == null)
        {
            Debug.LogError("[NetworkMapManager]: 尚未綁定 MapGenerator！");
            return;
        }

        // 強制寫入 Server 決定的 Seed，確保大家生出來的結果一致
        mapGenerator.seed = seed;
        mapGenerator.GenerateWorld();
        
        Debug.Log($"[NetworkMapManager]: 地圖生成完畢，使用的 Seed: {seed}");
        
        // (下一階段：在這裡通知系統「地圖生好了，可以放玩家進來了」)
    }

    #endregion
}