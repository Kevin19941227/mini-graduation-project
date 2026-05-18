using UnityEngine;
using Cinemachine;

public class PlayerCameraManager : MonoBehaviour
{
    #region Singleton

    public static PlayerCameraManager Instance { get; private set; }

    public static bool HasInstance => Instance != null;

    #endregion

    #region Inspector 設定

    [SerializeField] private CinemachineVirtualCamera gameplayCamera;
    [SerializeField] private CinemachineVirtualCamera assemblyCamera;
    [SerializeField] private Transform assemblyCameraPivot;

    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 0;

    #endregion

    #region 私有變數

    private Transform gameplayTarget;
    private Transform assemblyTarget;

    #endregion

    #region Unity 生命週期

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #endregion

    #region 相機目標設定

    /// <summary>
    /// 設定本地玩家的遊玩與組裝相機目標。
    /// </summary>
    public void SetPlayerTargets(Transform gameplayTarget, Transform assemblyTarget)
    {
        this.gameplayTarget = gameplayTarget;
        this.assemblyTarget = assemblyTarget;

        if (gameplayCamera != null)
        {
            gameplayCamera.Follow = gameplayTarget;
            gameplayCamera.LookAt = gameplayTarget;
        }

        if (assemblyCamera != null)
        {
            assemblyCamera.LookAt = assemblyTarget;
        }

        if (assemblyCameraPivot != null && assemblyTarget != null)
        {
            assemblyCameraPivot.position = assemblyTarget.position;
        }

        SwitchToGameplayCamera();
    }

    /// <summary>
    /// 切換到遊玩相機。
    /// </summary>
    public void SwitchToGameplayCamera()
    {
        if (gameplayCamera != null)
        {
            gameplayCamera.Priority = activePriority;
        }

        if (assemblyCamera != null)
        {
            assemblyCamera.Priority = inactivePriority;
        }
    }

    /// <summary>
    /// 切換到組裝相機。
    /// </summary>
    public void SwitchToAssemblyCamera()
    {
        if (assemblyTarget == null || assemblyCamera == null || assemblyCameraPivot == null)
        {
            return;
        }

        assemblyCameraPivot.position = assemblyTarget.position;
        assemblyCamera.Follow = assemblyCameraPivot;
        assemblyCamera.LookAt = assemblyTarget;

        if (gameplayCamera != null)
        {
            gameplayCamera.Priority = inactivePriority;
        }

        assemblyCamera.Priority = activePriority;
    }

    #endregion
}
