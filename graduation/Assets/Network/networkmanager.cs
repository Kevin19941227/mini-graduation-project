using Mirror;
using UnityEngine;

/// <summary>
/// 網路管理器 - 處理 Mirror 房間系統與雙 Transport 切換
/// 只認識 ILobbyProvider，不知道底層是 KCP 還是 Steam
/// 
/// !! 重要：Inspector 上的 Transport 欄位必須設為 None
///    Transport 完全由程式碼控制
/// </summary>
public class networkmanager : NetworkRoomManager
{
    #region 單例模式

    public static networkmanager instance;

    #endregion

    #region Transport 組件

    [Header("Transport 組件（Inspector 的 Transport 欄位請設為 None）")]
    public Transport kcpTransport;
    public Transport steamTransport;

    #endregion

    #region UI

    [Header("大廳 UI")]
    public GameObject startGameButton;

    #endregion

    #region 當前 Provider

    private ILobbyProvider _lobbyProvider;

    #endregion

    #region Unity 生命週期

    public override void Awake()
    {
        base.Awake();

        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            if (kcpTransport == null || steamTransport == null)
                AutoAssignTransports();

            if (kcpTransport != null)
            {
                Transport.active = kcpTransport;
                transport = kcpTransport;
                Debug.Log("[NetworkManager] 預設使用 KCP Transport");
            }
            else
            {
                Debug.LogError("[NetworkManager] 找不到 KCP Transport！");
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Provider 設定

    /// <summary>
    /// 由主選單 UI 呼叫，設定目前使用哪種連線方式
    /// </summary>
    public void SetLobbyProvider(ILobbyProvider provider)
    {
        _lobbyProvider = provider;
        Debug.Log($"[NetworkManager] 切換 Provider：{provider.GetType().Name}");
    }

    #endregion

    #region Transport 切換（給 Provider 呼叫）

    public void SwitchToKCP()
    {
        Transport.active = kcpTransport;
        transport = kcpTransport;
        Debug.Log("[NetworkManager] 切換到 KCP");
    }

    public void SwitchToSteam()
    {
        Transport.active = steamTransport;
        transport = steamTransport;
        Debug.Log("[NetworkManager] 切換到 Steam");
    }

    #endregion

    #region 自動抓取 Transport

    private void AutoAssignTransports()//要優化時弄掉
    {
        foreach (Transport t in GetComponents<Transport>())
        {
            string name = t.GetType().Name;
            if (name.Contains("Kcp") && kcpTransport == null)
                kcpTransport = t;
            if ((name.Contains("Fizzy") || name.Contains("Steam")) && steamTransport == null)
                steamTransport = t;
        }
    }

    #endregion

    #region NetworkRoomManager 覆寫

    public override void OnRoomServerPlayersReady()
    {
        if (startGameButton != null)
            startGameButton.SetActive(true);
    }

    public override void OnRoomServerPlayersNotReady()
    {
        if (startGameButton != null)
            startGameButton.SetActive(false);
    }

    #endregion

    #region 遊戲流程

    public void StartGame()
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[NetworkManager] 只有房主可以開始遊戲！");
            return;
        }

        ServerChangeScene(GameplayScene);
    }

    #endregion

    #region 應用程式生命週期

    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();

        if (NetworkServer.active) StopServer();
        if (NetworkClient.active) StopClient();
    }
    public ILobbyProvider GetCurrentProvider()
    {
        Debug.Log($"[NetworkManager] GetCurrentProvider: {(_lobbyProvider == null ? "NULL" : _lobbyProvider.GetType().Name)}");
        return _lobbyProvider;
    }
    #endregion

    #region 房間切換遊戲 - Loading UI 攔截

    [Header("--- 載入畫面 UI 系統 ---")]
    [Tooltip("請拖曳跨場景不銷毀的 AdvancedLoadingUIManager")]
    public AdvancedLoadingUIManager loadingUIManager;

    /// <summary>
    /// 覆寫 Server 端場景切換：加入 Debug.Log 追蹤問題
    /// </summary>
    public override void ServerChangeScene(string newSceneName)
    {
        // 1. 印出目前準備進入的場景，以及系統設定的遊戲場景
        Debug.Log($"[Loading 偵錯] 準備切換場景至: '{newSceneName}' | 系統設定的遊戲場景為: '{GameplayScene}'");

        bool isEnteringGame = (newSceneName == GameplayScene);
        Debug.Log($"[Loading 偵錯] 是否判定為進入遊戲？ {isEnteringGame}");

        if (isEnteringGame)
        {
            if (loadingUIManager != null)
            {
                Debug.Log("[Loading 偵錯] 成功觸發 UI 顯示！");
                loadingUIManager.ShowLoadingScreen();
            }
            else
            {
                Debug.LogError("[Loading 偵錯] 嚴重錯誤：loadingUIManager 沒有綁定，請檢查 Inspector！");
            }
        }

        // 呼叫底層原始邏輯
        base.ServerChangeScene(newSceneName);

        if (isEnteringGame)
        {
            StartCoroutine(UpdateGameplayLoadingProgress());
        }
    }

    /// <summary>
    /// 覆寫 Client 端場景切換：一般玩家同步切換至遊戲場景時觸發
    /// </summary>
    public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)
    {
        bool isEnteringGame = (newSceneName == GameplayScene);

        if (isEnteringGame && loadingUIManager != null)
        {
            loadingUIManager.ShowLoadingScreen();
        }

        base.OnClientChangeScene(newSceneName, sceneOperation, customHandling);

        if (isEnteringGame)
        {
            StartCoroutine(UpdateGameplayLoadingProgress());
        }
    }

    /// <summary>
    /// 協程：每幀擷取 Mirror 非同步載入進度並遞交給 UI
    /// </summary>
    private System.Collections.IEnumerator UpdateGameplayLoadingProgress()
    {
        // === 🕵️ 測試用：強迫等待 2 秒鐘 ===
        // 為了確認是不是因為載入太快導致畫面閃現，我們強迫它停在這裡 2 秒
        yield return new WaitForSeconds(2f);

        // loadingSceneAsync 是 Mirror 內建紀錄 Unity 非同步載入狀態的變數
        while (NetworkManager.loadingSceneAsync != null && !NetworkManager.loadingSceneAsync.isDone)
        {
            if (loadingUIManager != null)
            {
                loadingUIManager.UpdateProgress(NetworkManager.loadingSceneAsync.progress);
            }
            yield return null;
        }

        // 載入完成，確保填滿並隱藏
        if (loadingUIManager != null)
        {
            loadingUIManager.UpdateProgress(1f);
            loadingUIManager.HideLoadingScreen();
        }
    }

    #endregion

}