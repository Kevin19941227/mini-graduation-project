using UnityEngine;
using Cinemachine;

public class CameraHandler : MonoBehaviour
{
    public static CameraHandler Instance; // 單例
    public CinemachineVirtualCamera VirtualCamera;

    private void Awake()
    {
        // 1. 單例模式保護：確保只有一個相機管理者
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("警告：場景中發現重複的 CameraHandler，已自動刪除多餘的。");
            Destroy(gameObject);
            return;
        }

        // 2. 抓取相機組件 (並檢查有沒有抓到)
        VirtualCamera = GetComponent<CinemachineVirtualCamera>();
        
        if (VirtualCamera == null)
        {
            Debug.LogError("【嚴重錯誤】CameraHandler 找不到 CinemachineVirtualCamera！請確認這個腳本跟虛擬相機掛在同一個物件上！");
        }
        else
        {
            Debug.Log("【系統】CameraHandler 初始化成功，相機組件已抓取。");
        }
    }

    public void SetTarget(Transform target)
    {
        if (VirtualCamera == null)
        {
            Debug.LogError("【失敗】想設定目標，但找不到虛擬相機！");
            return;
        }

        // 3. 設定跟隨目標
        VirtualCamera.Follow = target;
        
        // 4. 確保 LookAt 是空的 (因為你的 Thirdpersocamera 腳本已經在處理旋轉了)
        VirtualCamera.LookAt = null; 

        // ★★★ 關鍵確認：如果有印出這行，代表連線成功 ★★★
        Debug.Log($"【連線成功】相機現在開始跟隨玩家目標：{target.name}");
    }
}