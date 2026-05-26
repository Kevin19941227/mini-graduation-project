using Mirror;
using UnityEngine;

/// <summary>
/// 地圖資料同步層 (Layer 2: State / Data)。
/// 確保 Server 決定的隨機種子能同步給所有 Client，讓所有玩家生成完全一致的地圖。
/// </summary>
public class MapDataSync : NetworkBehaviour
{
    [Header("基礎依賴")]
    [Tooltip("請將你寫好的 MapGenerator 腳本拖入此處")]
    public MapGenerator localMapGenerator;

    #region 網路變數同步 (Server -> Client)

    // 1. 定義 Server 權威管理的資料：地圖隨機種子
    // 當數值改變時，會自動觸發 OnSeedChanged 函式，通知 Client 開始生成地圖
    [SyncVar(hook = nameof(OnSeedChanged))]
    private int _mapSeed;

    private bool _mapGenerated = false;

    #endregion

    #region Server 端權威邏輯

    /// <summary>
    /// 當 Server 啟動並載入場景時執行。
    /// 只有 Server 有權力決定真正的隨機種子。
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();

        // Server 決定一個隨機的種子
        _mapSeed = System.Environment.TickCount;

        // Server 本身（或 Host）也需要生成地圖
        localMapGenerator.seed = _mapSeed;
        localMapGenerator.buildNavMeshAtRuntime = true;
        localMapGenerator.GenerateWorld();

        Debug.Log($"[Server] 已決定地圖種子為：{_mapSeed}，並同步給所有玩家。");
    }

    #endregion

    #region Client 端接收與生成邏輯

    /// <summary>
    /// 當 Client 端初始化完成時呼叫。
    /// 專案規範解法：中途加入的玩家不會收到之前的 SyncVar Hook 狀態，所以必須手動強制呼叫一次。
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();

        if (isServer) return;

        // 只在種子已到達且尚未生成時才觸發（處理中途加入的玩家）
        if (_mapSeed != 0 && !_mapGenerated)
            OnSeedChanged(0, _mapSeed);
    }

    /// <summary>
    /// SyncVar 的 Hook 函式。這段程式碼會在 Client 端執行。
    /// 收到 Server 的種子後，呼叫本地的 MapGenerator 生成地圖。
    /// </summary>
    /// <param name="oldSeed">舊種子</param>
    /// <param name="newSeed">新種子</param>
    private void OnSeedChanged(int oldSeed, int newSeed)
    {
        if (isServer) return;
        if (newSeed == 0) return;
        if (_mapGenerated) return;

        _mapGenerated = true;
        Debug.Log($"[Client] 收到權威地圖種子：{newSeed}，開始生成本地地圖！");

        localMapGenerator.seed = newSeed;
        localMapGenerator.buildNavMeshAtRuntime = false;
        localMapGenerator.GenerateWorld();
    }

    #endregion
}
