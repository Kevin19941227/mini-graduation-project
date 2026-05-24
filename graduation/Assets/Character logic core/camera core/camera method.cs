/* 為什麼相機會抖？（完整原因）

  問題一：Slerp + 角色旋轉 = 每幀起點不同

  Update()       → _motor.SetRotation() → CameraTarget（子物件）被父物件帶著轉
  LateUpdate()   → Slerp(CameraTarget.rotation, target, t)

  Slerp 的起點每幀都是「被角色帶跑的位置」，不是上一幀結束的位置。它永遠從一個略偏的地方往目標靠近，從不收斂 →
  持續微抖。

  修正：改成直接 =，每幀強制蓋回正確角度，角色帶跑多少都無所謂。

  ---
  問題二：OnLook 收不到輸入 → 客戶端不能轉向

  // 原本：PlayerInput 用 SendMessage 發給同一個 GameObject 上的所有腳本
  public void OnLook(InputValue value) { _look = value.Get<Vector2>(); }

  Thirdpersocamera 若掛在和 PlayerInput 不同的 GameObject 上，SendMessage 就發不到，_look 永遠是
  (0,0)，相機不動，角色也就不轉向。

  修正：改成直接讀 Mouse.current.delta，不經過 PlayerInput 路由，掛在哪都能收到。

  ---
  問題三：蓄力時每幀 GenerateImpulse → 持續震動
  
  
  蓄力
  兩個問題已修正：

  ---
  根本原因：Input Actions 的 Press(behavior=2) 設定錯誤

  Press(behavior=2) 是 PressAndRelease 模式，會讓 performed
  在「按下」和「放開」時各觸發一次。問題在於：放開時的回調中，InputValue.isPressed 有時候仍回傳 true（因為 action 仍處於
   performed 狀態），導致邏輯跑進 if (value.isPressed) 的分支，但 _currentPose == Charging ≠
  Grounded，條件不成立，攻擊從未觸發。

  修正一（Input Actions）：將 Attack action 的 interactions 改回空字串（預設行為）。預設模式下：
  - 按下 → performed → OnAttack(isPressed=true) ✓
  - 放開 → canceled → OnAttack(isPressed=false) ✓

  修正二（PlayCol.cs）：在 UpdateInput()
  每幀輪詢滑鼠左鍵狀態。只要蓄力中且滑鼠已放開，就直接觸發攻擊——這是保險機制，確保即使 InputSystem
  回調漏了放開事件，角色也不會永遠卡在蓄力動畫。

*/