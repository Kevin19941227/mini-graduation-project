using UnityEngine;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject _loadingPanel;
    [SerializeField] private Text _loadingText;

    void OnEnable()
    {
        MapGenerator.OnNavMeshReady += OnMapReady;
    }

    void OnDisable()
    {
        MapGenerator.OnNavMeshReady -= OnMapReady;
    }

    void Start()
    {
        // 進入遊戲場景就顯示 Loading
        ShowLoading();
    }

    private void ShowLoading()
    {
        if (_loadingPanel != null)
            _loadingPanel.SetActive(true);

        if (_loadingText != null)
            _loadingText.text = "地圖生成中...";
    }

    private void OnMapReady()
    {
        if (_loadingPanel != null)
            _loadingPanel.SetActive(false);

        Debug.Log("[LoadingScreen] 地圖就緒，隱藏 Loading 畫面");
    }
}