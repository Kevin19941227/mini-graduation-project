using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("血量條")]
    public Image hpFillImage;
    public Image delayedHpFillImage;

    [Header("蓄力條")]
    public Image chargeFillImage;
    public CanvasGroup chargeGroup;

    [Header("遊戲結束面板")]
    public GameObject gameOverPanel;

    private float _delayedFill = 1f;
    private float _decayTimer = 0f;
    private const float DecayDelay = 0.5f;
    private const float DecaySpeed = 0.4f;

    void Awake()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (hpFillImage != null) { hpFillImage.fillAmount = 1f; hpFillImage.color = Color.green; }
        if (delayedHpFillImage != null) delayedHpFillImage.fillAmount = 1f;
        if (chargeFillImage != null) chargeFillImage.fillAmount = 0f;
        if (chargeGroup != null) chargeGroup.alpha = 0f;
    }

    public void Init(float maxHp)
    {
        _delayedFill = 1f;
        _decayTimer = 0f;
        if (hpFillImage != null) hpFillImage.fillAmount = 1f;
        if (delayedHpFillImage != null) delayedHpFillImage.fillAmount = 1f;
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

        if (delayedHpFillImage != null)
        {
            if (ratio < _delayedFill)
                _decayTimer = DecayDelay;
            else
                _delayedFill = ratio;

            delayedHpFillImage.fillAmount = _delayedFill;
        }
    }

    void Update()
    {
        if (delayedHpFillImage == null || hpFillImage == null) return;

        if (_decayTimer > 0f)
        {
            _decayTimer -= Time.deltaTime;
            return;
        }

        float mainFill = hpFillImage.fillAmount;
        if (_delayedFill > mainFill)
        {
            _delayedFill = Mathf.Max(mainFill, _delayedFill - DecaySpeed * Time.deltaTime);
            delayedHpFillImage.fillAmount = _delayedFill;
        }
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
