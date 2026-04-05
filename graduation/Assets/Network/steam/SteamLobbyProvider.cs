using Mirror;
using Steamworks;
using UnityEngine;

//配對大廳層//

public class SteamLobbyProvider : MonoBehaviour, ILobbyProvider
{
    #region 內部狀態

    private CSteamID _currentLobbyId;

    #endregion

    #region Steam Callbacks

    protected Callback<LobbyCreated_t> _lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> _joinRequested;
    protected Callback<LobbyEnter_t> _lobbyEntered;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("[SteamLobbyProvider] 建立房間失敗");
            return;
        }

        _currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(_currentLobbyId, "name", SteamFriends.GetPersonaName() + " 的房間");
        SteamMatchmaking.SetLobbyData(_currentLobbyId, "HostAddress", SteamUser.GetSteamID().ToString());
        networkmanager.instance.StartHost();
        Debug.Log("[SteamLobbyProvider] 房間建立成功");
    }

    private void OnJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
        Debug.Log("[SteamLobbyProvider] 收到 Steam 邀請，加入中...");
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        if (NetworkServer.active) return;

        _currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        string hostId = SteamMatchmaking.GetLobbyData(_currentLobbyId, "HostAddress");

        if (string.IsNullOrEmpty(hostId))
        {
            Debug.LogError("[SteamLobbyProvider] 無法取得 Host 資訊");
            return;
        }

        networkmanager.instance.SwitchToSteam();
        networkmanager.instance.networkAddress = hostId;
        networkmanager.instance.StartClient();
        Debug.Log($"[SteamLobbyProvider] 加入房間，Host ID: {hostId}");
    }

    public void InviteFriend()
    {
        Debug.Log($"[SteamLobbyProvider] 當前 LobbyID: {_currentLobbyId}");

        if (!_currentLobbyId.IsValid())
        {
            Debug.LogWarning("[SteamLobbyProvider] 還沒有有效的 LobbyID");
            return;
        }

        SteamFriends.ActivateGameOverlayInviteDialog(_currentLobbyId);
    }

    #endregion

    #region ILobbyProvider 實作

    public void CreateLobby()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobbyProvider] Steam 未初始化");
            return;
        }

        networkmanager.instance.SwitchToSteam();
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, networkmanager.instance.maxConnections);
        Debug.Log("[SteamLobbyProvider] 建立 Steam 房間中...");
    }

    public void JoinLobby(string address)
    {
        networkmanager.instance.SwitchToSteam();
        SteamMatchmaking.JoinLobby(new CSteamID(ulong.Parse(address)));
        Debug.Log($"[SteamLobbyProvider] 加入 Steam 房間，LobbyID: {address}");
    }

    public void LeaveLobby()
    {
        if (_currentLobbyId.IsValid())
            SteamMatchmaking.LeaveLobby(_currentLobbyId);

        if (NetworkServer.active)
            networkmanager.instance.StopHost();
        else
            networkmanager.instance.StopClient();

        Debug.Log("[SteamLobbyProvider] 離開 Steam 房間");
    }

    public string GetLobbyName() =>
        _currentLobbyId.IsValid()
            ? SteamMatchmaking.GetLobbyData(_currentLobbyId, "name")
            : "未知房間";

    public int GetMemberCount() =>
        _currentLobbyId.IsValid()
            ? SteamMatchmaking.GetNumLobbyMembers(_currentLobbyId)
            : 0;

    public int GetMaxMembers() =>
        _currentLobbyId.IsValid()
            ? SteamMatchmaking.GetLobbyMemberLimit(_currentLobbyId)
            : 0;

    #endregion
}