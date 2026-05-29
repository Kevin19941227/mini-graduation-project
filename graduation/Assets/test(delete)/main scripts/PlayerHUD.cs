using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("蓄力條")]
    public Image chargeFillImage;
    public CanvasGroup chargeGroup;

    [Header("遊戲結束面板")]
    public GameObject gameOverPanel;

    void Awake()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (chargeFillImage != null) chargeFillImage.fillAmount = 0f;
        if (chargeGroup != null) chargeGroup.alpha = 0f;
    }

    public void Init()
    {
        if (chargeFillImage != null) chargeFillImage.fillAmount = 0f;
        if (chargeGroup != null) chargeGroup.alpha = 0f;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    public void UpdateCharge(float ratio, bool isCharging)
    {
        if (chargeGroup != null)
            chargeGroup.alpha = Mathf.Lerp(chargeGroup.alpha, isCharging ? 1f : 0f, Time.deltaTime * 10f);
        if (chargeFillImage != null)
            chargeFillImage.fillAmount = ratio;
    }

    public void ShowGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
    }
}
