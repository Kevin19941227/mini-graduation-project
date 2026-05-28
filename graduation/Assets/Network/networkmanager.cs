using Mirror;
using System.Reflection;
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
    private const int KcpTimeoutMilliseconds = 60000;

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
        if (kcpTransport == null || steamTransport == null)
            AutoAssignTransports();

        if (transport == null && kcpTransport != null)
        {
            ApplyTransport(kcpTransport);
        }

        base.Awake();

        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            if (kcpTransport != null)
            {
                ApplyTransport(kcpTransport);
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
        ApplyTransport(kcpTransport);
        Debug.Log("[NetworkManager] 切換到 KCP");
    }

    public void SwitchToSteam()
    {
        ApplyTransport(steamTransport);
        Debug.Log("[NetworkManager] 切換到 Steam");
    }

    private void ApplyTransport(Transport targetTransport)
    {
        if (targetTransport == null) return;

        Transport.active = targetTransport;
        transport = targetTransport;
        TryApplyKcpTimeout(targetTransport);
    }

    private void TryApplyKcpTimeout(Transport targetTransport)
    {
        if (targetTransport == null || !targetTransport.GetType().Name.Contains("Kcp")) return;

        SetNumericMemberIfExists(targetTransport, "Timeout", KcpTimeoutMilliseconds);
        SetNumericMemberIfExists(targetTransport, "timeout", KcpTimeoutMilliseconds);
        SetNumericMemberIfExists(targetTransport, "DisconnectTimeout", KcpTimeoutMilliseconds);
        SetNumericMemberIfExists(targetTransport, "disconnectTimeout", KcpTimeoutMilliseconds);
        SetNestedNumericMemberIfExists(targetTransport, "config", "Timeout", KcpTimeoutMilliseconds);
        SetNestedNumericMemberIfExists(targetTransport, "config", "timeout", KcpTimeoutMilliseconds);
    }

    private static void SetNumericMemberIfExists(object target, string memberName, int value)
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        System.Type type = target.GetType();

        FieldInfo field = type.GetField(memberName, flags);
        if (field != null)
        {
            SetFieldNumericValue(target, field, value);
            return;
        }

        PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null && property.CanWrite)
        {
            SetPropertyNumericValue(target, property, value);
        }
    }

    private static void SetNestedNumericMemberIfExists(object target, string parentMemberName, string childMemberName, int value)
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        System.Type type = target.GetType();
        object parentValue = null;

        FieldInfo parentField = type.GetField(parentMemberName, flags);
        if (parentField != null)
        {
            parentValue = parentField.GetValue(target);
        }

        PropertyInfo parentProperty = type.GetProperty(parentMemberName, flags);
        if (parentValue == null && parentProperty != null)
        {
            parentValue = parentProperty.GetValue(target);
        }

        if (parentValue == null) return;

        SetNumericMemberIfExists(parentValue, childMemberName, value);
    }

    private static void SetFieldNumericValue(object target, FieldInfo field, int value)
    {
        System.Type valueType = field.FieldType;
        if (valueType == typeof(int)) field.SetValue(target, value);
        else if (valueType == typeof(uint)) field.SetValue(target, (uint)value);
        else if (valueType == typeof(float)) field.SetValue(target, value / 1000f);
        else if (valueType == typeof(double)) field.SetValue(target, value / 1000d);
    }

    private static void SetPropertyNumericValue(object target, PropertyInfo property, int value)
    {
        System.Type valueType = property.PropertyType;
        if (valueType == typeof(int)) property.SetValue(target, value);
        else if (valueType == typeof(uint)) property.SetValue(target, (uint)value);
        else if (valueType == typeof(float)) property.SetValue(target, value / 1000f);
        else if (valueType == typeof(double)) property.SetValue(target, value / 1000d);
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
        return _lobbyProvider;
    }
    #endregion
}
