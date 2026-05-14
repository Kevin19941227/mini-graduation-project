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
    
    [Header("平滑設定")]
    [Range(5f, 30f)]
    public float smoothSpeed = 20f; // 越大越快，20 幾乎即時
    
    [Header("控制選項")]
    public bool invertY = false;
    
    private const float _threshold = 0.00001f;
    private float _cinemachineTargetPitch;
    private float _cinemachineTargetYaw;
    private Vector2 _look;

    public override void OnStartLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (CameraHandler.Instance != null)
        {
            CameraHandler.Instance.SetTarget(CameraTarget.transform);
        }
        else
        {
            Debug.LogError("找不到 CameraHandler！請確認場景裡有掛載 CameraHandler 的物件。");
        }
    }

    void Start()
    {
        if (!isLocalPlayer) 
        {
            enabled = false;
            return;
        }
    }
    
    void LateUpdate()
    {
        if (!isLocalPlayer) return;
        
        // 處理輸入
        if (_look.sqrMagnitude >= _threshold)
        {
            float yawInput = _look.x * horizontalSpeed * mouseSensitivity;
            float pitchInput = _look.y * verticalSpeed * mouseSensitivity;
            
            if (invertY) pitchInput = -pitchInput;
            
            _cinemachineTargetYaw += yawInput;
            _cinemachineTargetPitch += pitchInput;
        }   
        
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);
        
        // 平滑旋轉
        if (CameraTarget != null)
        {
            Quaternion targetRotation = Quaternion.Euler(
                _cinemachineTargetPitch, _cinemachineTargetYaw, 0.0f);

            CameraTarget.transform.rotation = Quaternion.Slerp(
                CameraTarget.transform.rotation, 
                targetRotation, 
                Time.deltaTime * smoothSpeed
            );
        }
    }
    
    public void OnLook(InputValue value)
    {
        if (!isLocalPlayer) return;
        _look = value.Get<Vector2>();
    }
    
    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
}