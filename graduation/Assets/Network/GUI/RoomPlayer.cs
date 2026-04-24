using Mirror;
using UnityEngine;
using Steamworks;
/// <summary>
/// 房間玩家 - 管理玩家在大廳等待時的準備狀態
/// 每個連線進來的玩家都會有一個 RoomPlayer
/// </summary>
public class RoomPlayer : NetworkRoomPlayer
{
    #region 同步變數

    [SyncVar(hook = nameof(OnReadyChanged))]
    public bool isReady = false;

    [SyncVar(hook = nameof(OnPlayerNameChanged))]
    public string playerName = "";

    #endregion

    #region Unity 生命週期

    public override void OnStartLocalPlayer()
    {
        // 設定自己的名稱（之後可以改成 Steam 名稱）
        if (SteamManager.Initialized)
            CmdSetPlayerName(SteamFriends.GetPersonaName());
        else
            CmdSetPlayerName("Player " + netId);
    }

    #endregion

    #region Command（Client → Server）

    [Command]
    public void CmdSetReady(bool ready)
    {
        isReady = ready;
        CmdChangeReadyState(ready);
    }

    [Command]
    public void CmdSetPlayerName(string name)
    {
        playerName = name;
    }

    #endregion

    #region SyncVar Hook

    private void OnReadyChanged(bool oldValue, bool newValue)
    {
        // UI 更新，之後在 RoomUI 裡處理
        Debug.Log($"[RoomPlayer] {playerName} 準備狀態：{newValue}");
    }

    private void OnPlayerNameChanged(string oldValue, string newValue)
    {
        Debug.Log($"[RoomPlayer] 玩家名稱更新：{newValue}");
    }

    #endregion
}