using UnityEngine;
using UnityEngine.UI;

public class WorldHealthBar : MonoBehaviour
{
    [SerializeField] private Image hpFillImage;
    [SerializeField] private Image delayedHpFillImage;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, 0f);

    private Transform _target;
    private Camera _mainCamera;
    private float _delayedFill = 1f;
    private float _decayTimer = 0f;
    private const float DecayDelay = 0.5f;
    private const float DecaySpeed = 0.4f;

    public void Init(Transform target)
    {
        _target = target;
        _mainCamera = Camera.main;
        _delayedFill = 1f;
        _decayTimer = 0f;
        if (hpFillImage != null) hpFillImage.fillAmount = 1f;
        if (delayedHpFillImage != null) delayedHpFillImage.fillAmount = 1f;
    }

    public void UpdateHP(int current, int max)
    {
        if (hpFillImage == null) return;
        float ratio = Mathf.Clamp01((float)current / max);
        hpFillImage.fillAmount = ratio;

        if (ratio > 0.5f)
            hpFillImage.color = Color.white;
        else if (ratio > 0.25f)
            hpFillImage.color = Color.yellow;
        else
            hpFillImage.color = Color.red;

        if (delayedHpFillImage != null)
        {
            if (ratio < _delayedFill)
                _decayTimer = DecayDelay;
            else
                _delayedFill = ratio;

            delayedHpFillImage.fillAmount = _delayedFill;
        }
    }

    void LateUpdate()
    {
        if (_target == null || _mainCamera == null) return;
        transform.position = _target.position + offset;
        transform.forward = _mainCamera.transform.forward;

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
}
