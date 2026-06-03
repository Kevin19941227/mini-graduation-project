using Mirror;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Steamworks;

/// <summary>
/// 大廳等待室 UI - 顯示玩家槽位、準備狀態、開始遊戲
/// </summary>
public class RoomUI : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;

    #region UI 組件

    [Header("玩家槽位（4個）")]
    public Text[] playerNameTexts;
    public Text[] playerStatusTexts;

    [Header("按鈕")]
    public Button readyButton;
    public Button cancelReadyButton;
    public Button startGameButton;
    public Button leaveButton;

    [Header("房間資訊")]
    public Text roomNameText;
    public Text playerCountText;

    [Header("Steam 功能")]
    public Button inviteFriendButton;

    [Header("Scene Navigation")]
    [SerializeField] private string fallbackStartSceneName = "startScene";
    [SerializeField, Min(0f)] private float returnSceneLoadDelay = 0.1f;

    #endregion

    #region Runtime State

    private float _refreshTimer;

    #endregion

    #region Unity 生命週期

    private void Start()
    {
        readyButton.onClick.AddListener(OnReadyClicked);
        cancelReadyButton.onClick.AddListener(OnCancelReadyClicked);
        startGameButton.onClick.AddListener(OnStartGameClicked);
        leaveButton.onClick.AddListener(OnLeaveClicked);

        // 不管哪種連線都先綁定事件
        inviteFriendButton.onClick.AddListener(OnInviteFriendClicked);

        // 判斷是否為 Steam 連線，決定按鈕顯示
        bool isSteam = networkmanager.instance.GetCurrentProvider() is SteamLobbyProvider;
        inviteFriendButton.gameObject.SetActive(isSteam);

        // 開始遊戲按鈕只有 Host 看得到
        startGameButton.gameObject.SetActive(NetworkServer.active);

        // 預設隱藏取消準備
        cancelReadyButton.gameObject.SetActive(false);
    }

    private void Update()
    {
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer < RefreshInterval) return;

        _refreshTimer = 0f;
        RefreshUI();
    }

    #endregion

    #region UI 更新

    public void RefreshUI()
    {
        if (networkmanager.instance == null) return;

        ILobbyProvider provider = networkmanager.instance.GetCurrentProvider();
        if (provider != null)
        {
            roomNameText.text = provider.GetLobbyName();
            playerCountText.text = $"{provider.GetMemberCount()} / {provider.GetMaxMembers()}";
        }

        var roomPlayers = new System.Collections.Generic.List<NetworkRoomPlayer>(networkmanager.instance.roomSlots);

        for (int i = 0; i < playerNameTexts.Length; i++)
        {
            if (i < roomPlayers.Count && roomPlayers[i] != null)
            {
                RoomPlayer player = roomPlayers[i] as RoomPlayer;
                if (player != null)
                {
                    playerNameTexts[i].text = player.playerName;
                    playerStatusTexts[i].text = player.isReady ? "已準備" : "未準備";
                }
            }
            else
            {
                playerNameTexts[i].text = "等待玩家...";
                playerStatusTexts[i].text = "";
            }
        }
    }

    #endregion

    #region 按鈕事件

    private void OnReadyClicked()
    {
        RoomPlayer localPlayer = GetLocalRoomPlayer();
        if (localPlayer == null) return;

        localPlayer.CmdSetReady(true);
        readyButton.gameObject.SetActive(false);
        cancelReadyButton.gameObject.SetActive(true);
    }

    private void OnCancelReadyClicked()
    {
        RoomPlayer localPlayer = GetLocalRoomPlayer();
        if (localPlayer == null) return;

        localPlayer.CmdSetReady(false);
        readyButton.gameObject.SetActive(true);
        cancelReadyButton.gameObject.SetActive(false);
    }

    private void OnStartGameClicked()
    {
        networkmanager.instance.StartGame();
    }

    private void OnLeaveClicked()
    {
        ReturnToStartScene();
    }

    /// <summary>Leaves the current room and returns to the configured start/offline scene.</summary>
    public void ReturnToStartScene()
    {
        if (networkmanager.instance == null)
        {
            Debug.LogWarning("[RoomUI] Cannot return to start scene because NetworkManager is missing.");
            return;
        }

        ILobbyProvider provider = networkmanager.instance.GetCurrentProvider();
        if (provider != null)
        {
            provider.LeaveLobby();
            StartCoroutine(LoadStartSceneAfterNetworkStops());
            return;
        }

        if (NetworkServer.active && NetworkClient.active)
        {
            networkmanager.instance.StopHost();
            StartCoroutine(LoadStartSceneAfterNetworkStops());
            return;
        }

        if (NetworkClient.active)
        {
            networkmanager.instance.StopClient();
            StartCoroutine(LoadStartSceneAfterNetworkStops());
            return;
        }

        if (NetworkServer.active)
        {
            networkmanager.instance.StopServer();
            StartCoroutine(LoadStartSceneAfterNetworkStops());
            return;
        }

        LoadConfiguredStartScene();
    }

    private IEnumerator LoadStartSceneAfterNetworkStops()
    {
        if (returnSceneLoadDelay > 0f)
            yield return new WaitForSeconds(returnSceneLoadDelay);

        LoadConfiguredStartScene();
    }

    private void LoadConfiguredStartScene()
    {
        string sceneName = fallbackStartSceneName;

        if (networkmanager.instance != null && !string.IsNullOrEmpty(networkmanager.instance.offlineScene))
            sceneName = networkmanager.instance.offlineScene;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[RoomUI] Cannot return to start scene because no fallback or offline scene is configured.");
            return;
        }

        if (SceneManager.GetActiveScene().name == sceneName)
            return;

        SceneManager.LoadScene(sceneName);
    }

    private void OnInviteFriendClicked()
    {
        Debug.Log("[RoomUI] 邀請按鈕被按下");
        SteamLobbyProvider steamProvider = networkmanager.instance.GetCurrentProvider() as SteamLobbyProvider;
        if (steamProvider != null)
            steamProvider.InviteFriend();
        else
            Debug.LogWarning("[RoomUI] 不是 Steam 連線，無法邀請");
    }

    #endregion

    #region 工具方法

    private RoomPlayer GetLocalRoomPlayer()
    {
        foreach (var slot in networkmanager.instance.roomSlots)
        {
            RoomPlayer player = slot as RoomPlayer;
            if (player != null && player.isLocalPlayer)
                return player;
        }
        return null;
    }

    #endregion
}
