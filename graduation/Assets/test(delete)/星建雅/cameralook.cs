using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

public class Thirdpersocamera : NetworkBehaviour
{
    [Header("相機跟隨點")]
    public GameObject CameraTarget;
    
    [Header("相機靈敏度")]
    [Range(0.1f, 3.0f)]
    public float mouseSensitivity = 1.0f;
    [Range(0.1f, 3.0f)]
    public float horizontalSpeed = 1.0f;
    [Range(0.1f, 3.0f)]
    public float verticalSpeed = 1.0f;
    
    [Header("視角限制")]
    public float TopClamp = 70.0f;
    public float BottomClamp = -50.0f;
    
    [Header("控制選項")]
    public bool invertY = false;

    [Header("靈敏度縮放（Mouse.delta 是像素值，預設 0.05 接近原本 InputAction 的手感）")]
    [Range(0.01f, 0.2f)]
    public float deltaScale = 0.05f;

    [Header("輸入平滑（0 = 不平滑，0.08 接近原本 Slerp 的跟手感）")]
    [Range(0f, 0.15f)]
    public float inputSmoothTime = 0.08f;

    private const float _threshold = 0.00001f;
    private float _cinemachineTargetPitch;
    private float _cinemachineTargetYaw;
    private Vector2 _look;
    private Vector2 _smoothedLook;
    private Vector2 _lookVelocity;

    public override void OnStartLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (CameraHandler.Instance != null)
            CameraHandler.Instance.SetTarget(CameraTarget.transform);
        else
            Debug.LogError("找不到 CameraHandler！請確認場景裡有掛載 CameraHandler 的物件。");
    }

    void LateUpdate()
    {
        if (!isLocalPlayer) return;

        if (Mouse.current != null)
            _look = Mouse.current.delta.ReadValue() * deltaScale;

        // 平滑輸入而非平滑旋轉，不會產生抖動
        _smoothedLook = inputSmoothTime > 0f
            ? Vector2.SmoothDamp(_smoothedLook, _look, ref _lookVelocity, inputSmoothTime)
            : _look;

        if (_smoothedLook.sqrMagnitude >= _threshold)
        {
            float yawInput   = _smoothedLook.x * horizontalSpeed * mouseSensitivity;
            float pitchInput = _smoothedLook.y * verticalSpeed   * mouseSensitivity;

            if (invertY) pitchInput = -pitchInput;

            _cinemachineTargetYaw   += yawInput;
            _cinemachineTargetPitch += pitchInput;
        }

        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

        if (CameraTarget != null)
            CameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0f);
    }
    
    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
}