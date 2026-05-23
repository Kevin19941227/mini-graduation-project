using UnityEngine;
using Cinemachine;

public class CameraHandler : MonoBehaviour
{
    public static CameraHandler Instance;
    public CinemachineVirtualCamera VirtualCamera;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        VirtualCamera = GetComponent<CinemachineVirtualCamera>();

        // CinemachineInputProvider 由 Thirdpersocamera 接管旋轉，停用避免衝突
        var inputProvider = GetComponent<CinemachineInputProvider>();
        if (inputProvider != null)
            inputProvider.enabled = false;
    }

    public void SetTarget(Transform target)
    {
        if (VirtualCamera == null) return;

        VirtualCamera.Follow = target;
        VirtualCamera.LookAt = null;
    }
}