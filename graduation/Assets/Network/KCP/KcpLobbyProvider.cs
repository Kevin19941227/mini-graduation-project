using Mirror;
using UnityEngine;

//配對大廳層//
    public class KcpLobbyProvider : MonoBehaviour, ILobbyProvider
    {
        #region ILobbyProvider 實作

        public void CreateLobby()
        {
            networkmanager.instance.SwitchToKCP();
            networkmanager.instance.StartHost();
            Debug.Log("[KcpLobbyProvider] 建立本地房間");
        }//呼叫networkmanger切換網路傳輸方式

        public void JoinLobby(string address)
        {
            networkmanager.instance.SwitchToKCP();
            networkmanager.instance.networkAddress = address;
            networkmanager.instance.StartClient();
            Debug.Log($"[KcpLobbyProvider] 加入本地房間，IP: {address}");
        }

        public void LeaveLobby()
        {
            if (NetworkServer.active)
                networkmanager.instance.StopHost();
            else
                networkmanager.instance.StopClient();

            Debug.Log("[KcpLobbyProvider] 離開本地房間");
        }

        public string GetLobbyName() => "本地測試房間";

        public int GetMemberCount() => NetworkServer.connections.Count;

        public int GetMaxMembers() => networkmanager.instance.maxConnections;

        #endregion
    }
