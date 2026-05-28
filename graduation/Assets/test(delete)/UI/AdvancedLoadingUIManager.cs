using UnityEngine;
using UnityEngine.UI;

public class AdvancedLoadingUIManager : MonoBehaviour
{
    [Header("--- UI 參考 ---")]
    [SerializeField] private GameObject _loadingScreenPanel;
    [SerializeField] private Slider _progressBar;
    [SerializeField] private Text _progressText;

    private const float MaxPercentage = 100f;
    private const string PercentageFormat = "0%";

    /// <summary>
    /// 顯示載入畫面並初始化
    /// </summary>
    public void ShowLoadingScreen()
    {
        if (_loadingScreenPanel != null)
        {
            _loadingScreenPanel.SetActive(true);
        }
        UpdateProgress(0f);
    }

    /// <summary>
    /// 更新進度條與文字
    /// </summary>
    public void UpdateProgress(float progress)
    {
        if (_progressBar != null) _progressBar.value = progress;
        if (_progressText != null)
        {
            float displayPercentage = progress * MaxPercentage;
            _progressText.text = displayPercentage.ToString(PercentageFormat);
        }
    }

    /// <summary>
    /// 隱藏載入畫面
    /// </summary>
    public void HideLoadingScreen()
    {
        if (_loadingScreenPanel != null)
        {
            _loadingScreenPanel.SetActive(false);
        }
    }
    private void Awake()
    {
        // 讓這個 UI 畫布自己跨場景存活，不需要依賴別人
        DontDestroyOnLoad(this.transform.root.gameObject);
    }
}