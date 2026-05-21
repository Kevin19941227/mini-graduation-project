using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主選單 UI - 處理按鈕事件，決定使用哪種連線方式
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    #region UI 組件

    [Header("KCP 連線")]
    public Button lanHostButton;
    public Button lanJoinButton;
    public InputField lanIpInput;

    [Header("Steam 連線")]
    public Button steamHostButton;

    #endregion

    #region 組件引用

    [Header("LobbyProvider 組件")]
    public KcpLobbyProvider kcpProvider;
    public SteamLobbyProvider steamProvider;

    #endregion

    #region Unity 生命週期

    private void Start()
    {
        lanHostButton.onClick.AddListener(OnLanHostClicked);
        lanJoinButton.onClick.AddListener(OnLanJoinClicked);
        steamHostButton.onClick.AddListener(OnSteamHostClicked);
    }

    #endregion

    #region 按鈕事件

    private void OnLanHostClicked()
    {
        networkmanager.instance.SetLobbyProvider(kcpProvider);
        kcpProvider.CreateLobby();
    }

    private void OnLanJoinClicked()
    {
        string ip = lanIpInput.text;
        if (string.IsNullOrEmpty(ip))
        {
            Debug.LogWarning("[MainMenuUI] IP 欄位是空的！");
            return;
        }

        networkmanager.instance.SetLobbyProvider(kcpProvider);
        kcpProvider.JoinLobby(ip);
    }

    private void OnSteamHostClicked()
    {
        networkmanager.instance.SetLobbyProvider(steamProvider);
        steamProvider.CreateLobby();
    }

    #endregion
}