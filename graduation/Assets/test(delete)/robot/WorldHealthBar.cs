using UnityEngine;
using UnityEngine.UI;

public class WorldHealthBar : MonoBehaviour
{
    [SerializeField] private Image hpFillImage;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, 0f);

    private Transform _target;
    private Camera _mainCamera;

    public void Init(Transform target)
    {
        _target = target;
        _mainCamera = Camera.main;
    }

    public void UpdateHP(int current, int max)
    {
        if (hpFillImage == null) return;
        hpFillImage.fillAmount = Mathf.Clamp01((float)current / max);
    }

    void LateUpdate()
    {
        if (_target == null || _mainCamera == null) return;
        transform.position = _target.position + offset;
        transform.forward = _mainCamera.transform.forward;
    }
}