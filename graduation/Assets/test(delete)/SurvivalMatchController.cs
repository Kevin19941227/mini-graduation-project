using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class SurvivalMatchController : MonoBehaviour
{
    private const int MinPlayersToEnableVictory = 2;
    private const string LogPrefix = "[SurvivalMatch]";

    private static SurvivalMatchController instance;

    private readonly Dictionary<uint, PlayCol> knownPlayers = new Dictionary<uint, PlayCol>();
    private readonly HashSet<uint> knownPlayerIds = new HashSet<uint>();
    private readonly HashSet<uint> alivePlayerIds = new HashSet<uint>();
    private readonly HashSet<uint> deadPlayerIds = new HashSet<uint>();

    private bool matchEnded;

    #region Instance Access

    public static SurvivalMatchController EnsureServerInstance()
    {
        if (!NetworkServer.active)
        {
            return null;
        }

        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<SurvivalMatchController>();

        if (instance != null)
        {
            return instance;
        }

        GameObject controllerObject = new GameObject(nameof(SurvivalMatchController));
        instance = controllerObject.AddComponent<SurvivalMatchController>();
        return instance;
    }

    public static SurvivalMatchController GetServerInstance()
    {
        if (!NetworkServer.active)
        {
            return null;
        }

        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<SurvivalMatchController>();
        return instance;
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Server API

    /// <summary>Registers a server-side player as a participant in the survival match.</summary>
    [Server]
    public void ServerRegisterPlayer(PlayCol player)
    {
        if (player == null || matchEnded)
        {
            return;
        }

        if (!IsConnectedPlayer(player))
        {
            Debug.Log($"{LogPrefix} Ignored non-connection PlayCol name={player.name}.");
            return;
        }

        uint playerId = player.netId;
        if (playerId == 0)
        {
            return;
        }

        knownPlayers[playerId] = player;
        knownPlayerIds.Add(playerId);

        if (player.IsDead)
        {
            deadPlayerIds.Add(playerId);
            alivePlayerIds.Remove(playerId);
        }
        else if (!deadPlayerIds.Contains(playerId))
        {
            alivePlayerIds.Add(playerId);
        }

        Debug.Log($"{LogPrefix} Registered player netId={playerId}, players={knownPlayerIds.Count}, alive={alivePlayerIds.Count}.");
        EvaluateVictory();
    }

    /// <summary>Removes a server-side player from the active survival match.</summary>
    [Server]
    public void ServerUnregisterPlayer(PlayCol player)
    {
        if (player == null || matchEnded)
        {
            return;
        }

        uint playerId = player.netId;
        knownPlayers.Remove(playerId);

        if (alivePlayerIds.Remove(playerId) && knownPlayerIds.Count >= MinPlayersToEnableVictory)
        {
            deadPlayerIds.Add(playerId);
            EvaluateVictory();
        }
    }

    /// <summary>Reports a player death and checks whether the remaining survivor has won.</summary>
    [Server]
    public void ServerReportPlayerDied(PlayCol player)
    {
        if (player == null || matchEnded)
        {
            return;
        }

        uint playerId = player.netId;
        if (playerId == 0 || deadPlayerIds.Contains(playerId))
        {
            return;
        }

        knownPlayers[playerId] = player;
        knownPlayerIds.Add(playerId);
        deadPlayerIds.Add(playerId);
        alivePlayerIds.Remove(playerId);

        Debug.Log($"{LogPrefix} Death received netId={playerId}.");
        RebuildPlayerStateFromServerConnections();
        EvaluateVictory();
    }

    /// <summary>Returns every connected player to the room lobby after the match has ended.</summary>
    [Server]
    public void ServerRequestReturnToLobby(PlayCol requester)
    {
        if (!CanRequesterControlMatch(requester))
        {
            Debug.LogWarning($"{LogPrefix} Return lobby rejected. requester={GetRequesterName(requester)}.");
            return;
        }

        networkmanager manager = networkmanager.instance;
        if (manager == null || string.IsNullOrEmpty(manager.RoomScene))
        {
            Debug.LogWarning($"{LogPrefix} Return lobby rejected because NetworkRoomManager or RoomScene is missing.");
            return;
        }

        Debug.Log($"{LogPrefix} Return lobby requested by host.");
        manager.ServerChangeScene(manager.RoomScene);
    }

    #endregion

    #region Victory Evaluation

    [Server]
    private void EvaluateVictory()
    {
        if (matchEnded || knownPlayerIds.Count < MinPlayersToEnableVictory)
        {
            return;
        }

        if (alivePlayerIds.Count != 1)
        {
            return;
        }

        uint winnerId = 0;
        foreach (uint alivePlayerId in alivePlayerIds)
        {
            winnerId = alivePlayerId;
            break;
        }

        if (winnerId == 0)
        {
            return;
        }

        matchEnded = true;
        Debug.Log($"{LogPrefix} Winner decided: netId={winnerId}.");
        BroadcastMatchResult(winnerId);
    }

    [Server]
    private void RebuildPlayerStateFromServerConnections()
    {
        knownPlayers.Clear();
        knownPlayerIds.Clear();
        alivePlayerIds.Clear();

        foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
        {
            if (connection == null || connection.identity == null)
            {
                continue;
            }

            PlayCol currentPlayer = connection.identity.GetComponent<PlayCol>();
            if (currentPlayer == null || currentPlayer.netId == 0)
            {
                continue;
            }

            uint playerId = currentPlayer.netId;
            knownPlayers[playerId] = currentPlayer;
            knownPlayerIds.Add(playerId);

            if (currentPlayer.IsDead || deadPlayerIds.Contains(playerId))
            {
                deadPlayerIds.Add(playerId);
                continue;
            }

            alivePlayerIds.Add(playerId);
        }

        Debug.Log($"{LogPrefix} Players={knownPlayerIds.Count}, Alive={alivePlayerIds.Count}, Dead={deadPlayerIds.Count}.");
    }

    [Server]
    private void BroadcastMatchResult(uint winnerId)
    {
        foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
        {
            if (connection == null || connection.identity == null)
            {
                continue;
            }

            PlayCol player = connection.identity.GetComponent<PlayCol>();
            if (player == null)
            {
                continue;
            }

            player.RpcShowMatchResult(winnerId);
        }
    }

    #endregion

    #region Helpers

    [Server]
    private static bool IsConnectedPlayer(PlayCol player)
    {
        if (player == null || player.connectionToClient == null || player.connectionToClient.identity == null)
        {
            return false;
        }

        return player.connectionToClient.identity.GetComponent<PlayCol>() == player;
    }

    [Server]
    private bool CanRequesterControlMatch(PlayCol requester)
    {
        return matchEnded && requester != null && requester.isLocalPlayer && IsConnectedPlayer(requester);
    }

    private static string GetRequesterName(PlayCol requester)
    {
        return requester != null ? requester.name : "null";
    }

    #endregion
}
