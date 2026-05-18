using Mirror;
using UnityEngine;

public class PlayerCameraTargetProvider : NetworkBehaviour
{
    #region Inspector 設定

    [SerializeField] private Transform gameplayCameraTarget;
    [SerializeField] private Transform assemblyCameraTarget;

    #endregion

    #region Mirror Callbacks

    /// <summary>
    /// 本地玩家生成後，將自己的相機目標註冊給 CameraManager。
    /// </summary>
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (!PlayerCameraManager.HasInstance)
        {
            Debug.LogWarning("[PlayerCameraTargetProvider] PlayerCameraManager is missing in the scene.");
            return;
        }

        PlayerCameraManager.Instance.SetPlayerTargets(
            gameplayCameraTarget,
            assemblyCameraTarget
        );
    }

    #endregion
}
