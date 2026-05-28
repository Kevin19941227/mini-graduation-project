using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class Thirdpersocamera : NetworkBehaviour
{
    private const float LookThreshold = 0.00001f;

    #region Inspector Settings

    [Header("Camera Target")]
    public GameObject CameraTarget;

    [Header("Camera Speed")]
    [Range(0.1f, 3.0f)] public float mouseSensitivity = 1.0f;
    [Range(0.1f, 3.0f)] public float horizontalSpeed = 1.0f;
    [Range(0.1f, 3.0f)] public float verticalSpeed = 1.0f;

    [Header("Pitch Clamp")]
    public float TopClamp = 70.0f;
    public float BottomClamp = -50.0f;

    [Header("Invert")]
    public bool invertY;

    [Header("Mouse Delta Scale")]
    [Range(0.01f, 0.2f)] public float deltaScale = 0.05f;

    [Header("Input Smooth")]
    [Range(0f, 0.15f)] public float inputSmoothTime = 0.08f;

    #endregion

    #region Runtime State

    private float _cinemachineTargetPitch;
    private float _cinemachineTargetYaw;
    private Vector2 _look;
    private Vector2 _smoothedLook;
    private Vector2 _lookVelocity;
    private bool _isCameraOwner;

    #endregion

    #region Mirror Callbacks

    /// <summary>Disables camera control on remote player objects.</summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        _isCameraOwner = false;
    }

    /// <summary>Assigns the scene virtual camera to follow this local player's camera target.</summary>
    public override void OnStartLocalPlayer()
    {
        StartCoroutine(EnableCameraWhenMapReady());
    }

    private System.Collections.IEnumerator EnableCameraWhenMapReady()
    {
        while (!MapGenerator.IsNavMeshReady)
        {
            yield return null;
        }

        _isCameraOwner = true;

        if (CameraTarget == null)
        {
            Debug.LogError("[Thirdpersocamera] CameraTarget is not assigned.");
            yield break;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _cinemachineTargetYaw = transform.eulerAngles.y;
        CameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0f);

        if (CameraHandler.Instance != null)
        {
            CameraHandler.Instance.SetTarget(CameraTarget.transform);
        }
        else
        {
            Debug.LogError("[Thirdpersocamera] Cannot find CameraHandler in the scene.");
        }
    }

    #endregion

    #region Unity Lifecycle

    private void LateUpdate()
    {
        if (!_isCameraOwner || CameraTarget == null) return;

        UpdateLookInput();
        RotateCameraTarget();
    }

    #endregion

    #region Camera Control

    private void UpdateLookInput()
    {
        _look = Mouse.current != null
            ? Mouse.current.delta.ReadValue() * deltaScale
            : Vector2.zero;

        _smoothedLook = inputSmoothTime > 0f
            ? Vector2.SmoothDamp(_smoothedLook, _look, ref _lookVelocity, inputSmoothTime)
            : _look;
    }

    private void RotateCameraTarget()
    {
        if (_smoothedLook.sqrMagnitude >= LookThreshold)
        {
            float yawInput = _smoothedLook.x * horizontalSpeed * mouseSensitivity;
            float pitchInput = _smoothedLook.y * verticalSpeed * mouseSensitivity;

            if (invertY)
            {
                pitchInput = -pitchInput;
            }

            _cinemachineTargetYaw += yawInput;
            _cinemachineTargetPitch += pitchInput;
        }

        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);
        CameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0f);
    }

    private static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360f) angle += 360f;
        if (angle > 360f) angle -= 360f;

        return Mathf.Clamp(angle, min, max);
    }

    #endregion
}
