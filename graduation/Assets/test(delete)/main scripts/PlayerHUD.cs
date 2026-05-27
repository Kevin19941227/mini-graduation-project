using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("血量條")]
    public Image hpFillImage;

    [Header("蓄力條")]
    public Image chargeFillImage;
    public CanvasGroup chargeGroup;

    [Header("遊戲結束面板")]
    public GameObject gameOverPanel;

    void Awake()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (hpFillImage != null)   { hpFillImage.fillAmount = 1f; hpFillImage.color = Color.green; }
        if (chargeFillImage != null) chargeFillImage.fillAmount = 0f;
        if (chargeGroup != null)     chargeGroup.alpha = 0f;
    }

    public void Init(float maxHp)
    {
        if (hpFillImage != null) hpFillImage.fillAmount = 1f;
        if (chargeFillImage != null) chargeFillImage.fillAmount = 0f;
        if (chargeGroup != null) chargeGroup.alpha = 0f;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    public void UpdateHp(float current, float max)
    {
        if (hpFillImage == null) return;
        float ratio = Mathf.Clamp01(current / max);
        hpFillImage.fillAmount = ratio;
        hpFillImage.color = Color.Lerp(Color.red, Color.green, ratio);
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
