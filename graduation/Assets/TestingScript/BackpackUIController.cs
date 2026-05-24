using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BackpackUIController : MonoBehaviour
{
    #region References

    private const char AttachPointSeparator = '|';

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Panel Runtime Style")]
    [SerializeField] private bool applyRuntimePanelStyle = true;
    [SerializeField] private bool fitSlotGridToPanel = true;
    [SerializeField] private bool disableNonSlotGraphicRaycasts = true;
    [SerializeField] private Vector2 slotGridPadding = new Vector2(16f, 16f);
    [SerializeField] private Color panelBackgroundColor = new Color(0.08f, 0.10f, 0.12f, 0.88f);
    [SerializeField] private Color panelShadowColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private Vector2 panelShadowDistance = new Vector2(6f, -6f);

    [Header("Slot UI")]
    [SerializeField] private Transform slotGridRoot;
    [SerializeField] private BackpackSlotUI slotPrefab;

    [Header("Drag Install")]
    [SerializeField] private Camera placementCamera;
    [SerializeField] private LayerMask placementMask = ~0;
    [SerializeField] private float placementRayDistance = 100f;
    [SerializeField] private float fallbackPlacementDistance = 3f;
    [SerializeField] private float minPlacementDistance = 0.5f;
    [SerializeField] private float maxPlacementDistance = 12f;
    [SerializeField] private float placementDistanceScrollSpeed = 1f;
    [SerializeField] private float placementProbeRadius = 2f;
    [SerializeField] private float placementRotationSpeed = 120f;
    // Attach surface setup:
    // MeshCollider should be Convex = false, Is Trigger = false, with a non-null sharedMesh.
    // Put attach surfaces on an AttachSurface layer excluded from Player, Default, and AttachSurface physics collisions.
    // placementMask should include only that AttachSurface layer, and PartAttachSurface must be on the MeshCollider object or a parent.
    [Tooltip("Attach MeshColliders should be non-convex, non-trigger, on an AttachSurface layer excluded from normal physics collisions.")]
    [SerializeField] private bool requireMeshColliderAttachSurface = true;
    [Tooltip("Keep disabled unless a part prefab needs temporary local preview colliders.")]
    [SerializeField] private bool generateRuntimeMeshAttachColliders = false;
    [SerializeField] private bool logInstallDragDebug = false;
    [SerializeField] private bool logAttachColliderDebug = false;
    [SerializeField] private bool disableLocalPlayerCapsuleCollidersWhileOpen = false;

    [Header("Database")]
    [SerializeField] private GameDatabase gameDatabase;

    #endregion

    #region Runtime Data

    private PlayerInventoryNetwork localInventory;
    private StoredPartRuntimeData selectedSlotData;
    private PartData selectedPartData;
    private Vector2 lastPointerScreenPosition;

    private GameObject dragPreviewInstance;
    private Vector3 dragPreviewRotationOffset;
    private float dragPreviewDistance;
    private PartAttachSurface currentAttachSurface;
    private Transform currentAttachRootOverride;
    private string currentAttachPointIDOverride;
    private bool isDraggingBackpackPreview;
    private bool defaultPanelBlocksRaycasts = true;
    private PlayerAttachColliderController localAttachColliderController;
    private readonly List<BackpackSlotUI> slotUIs = new List<BackpackSlotUI>();
    private readonly List<CapsuleCollider> disabledLocalCapsuleColliders = new List<CapsuleCollider>();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CachePanelCanvasGroup();
        ApplyPanelRuntimeStyle();
        DisableNonSlotGraphicRaycasts();
    }

    private void LateUpdate()
    {
        if (isDraggingBackpackPreview && dragPreviewInstance != null)
        {
            MoveDragPreview(lastPointerScreenPosition);
        }
    }

    private void OnDestroy()
    {
        UnbindInventory();
        ClearDragPreview();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Binds the inventory data shown by this backpack UI.
    /// </summary>
    public void Bind(PlayerInventoryNetwork inventory)
    {
        if (localInventory == inventory)
        {
            RefreshUI();
            return;
        }

        UnbindInventory();
        localInventory = inventory;

        if (localInventory == null)
        {
            ClearInventoryView();
            return;
        }

        localInventory.OnInventoryChanged += RefreshUI;

        RefreshUI();
    }

    /// <summary>
    /// Opens the backpack UI.
    /// </summary>
    public void Show()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        SetBackpackBlocksRaycasts(true);
        DisableLocalPlayerCapsuleColliders();
        SetLocalAttachMeshCollidersEnabled(true);
        UnlockCursorForBackpack();
        SwitchToAssemblyCamera();
        DisableNonSlotGraphicRaycasts();
        RefreshUI();
        DebugLocalPlayerMeshColliders();
    }

    /// <summary>
    /// Closes the backpack UI.
    /// </summary>
    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        isDraggingBackpackPreview = false;
        SetBackpackBlocksRaycasts(true);
        ClearDragPreview();
        SetLocalAttachMeshCollidersEnabled(false);
        RestoreLocalPlayerCapsuleColliders();
        SwitchToGameplayCamera();
        LockCursorForGameplay();
    }

    /// <summary>
    /// Toggles the backpack UI.
    /// </summary>
    public void Toggle()
    {
        if (panelRoot == null)
        {
            return;
        }

        if (panelRoot.activeSelf)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    /// <summary>
    /// Checks whether a screen position is inside the backpack panel.
    /// </summary>
    public bool IsScreenPositionOverBackpack(Vector2 screenPosition)
    {
        if (panelRoot == null || !panelRoot.activeInHierarchy)
        {
            return false;
        }

        RectTransform panelRect = panelRoot.transform as RectTransform;

        if (panelRect == null)
        {
            return false;
        }

        Canvas canvas = panelRoot.GetComponentInParent<Canvas>();
        Camera uiCamera = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(panelRect, screenPosition, uiCamera);
    }

    /// <summary>
    /// Returns whether the backpack UI is currently open.
    /// </summary>
    public bool IsBackpackOpen()
    {
        return panelRoot != null && panelRoot.activeInHierarchy;
    }

    /// <summary>
    /// Returns whether the player is dragging an install preview.
    /// </summary>
    public bool IsDraggingInstallPreview()
    {
        return dragPreviewInstance != null;
    }

    /// <summary>
    /// Enables or disables backpack UI raycast blocking during world installation drags.
    /// </summary>
    public void SetBackpackBlocksRaycasts(bool blocksRaycasts)
    {
        CachePanelCanvasGroup();

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.blocksRaycasts = blocksRaycasts;
        }
    }

    /// <summary>
    /// Requests all installed parts to be returned to the local player's backpack.
    /// </summary>
    public void ReturnAllInstalledPartsToBackpack()
    {
        if (localInventory == null)
        {
            return;
        }

        ClearDragPreview();
        localInventory.CmdReturnAllInstalledPartsToBackpack();
    }

    #endregion

    #region UI Refresh

    /// <summary>
    /// Refreshes slots from the currently bound inventory.
    /// </summary>
    public void RefreshUI()
    {
        if (localInventory == null || gameDatabase == null || slotPrefab == null || slotGridRoot == null)
        {
            return;
        }

        EnsureSlotCount(localInventory.StoredParts.Count);

        for (int i = 0; i < slotUIs.Count; i++)
        {
            if (i >= localInventory.StoredParts.Count)
            {
                slotUIs[i].Clear();
                continue;
            }

            StoredPartRuntimeData slotData = localInventory.StoredParts[i];

            if (slotData.IsEmpty)
            {
                slotUIs[i].Clear();
                continue;
            }

            PartData partData = gameDatabase.GetPartData(slotData.partID);

            if (partData == null)
            {
                slotUIs[i].Clear();
                continue;
            }

            slotUIs[i].Set(
                slotData,
                partData,
                HandleSlotClicked,
                HandleSlotBeginDrag,
                HandleSlotDrag,
                HandleSlotEndDrag
            );
        }

        if (selectedPartData != null && !localInventory.ClientHasPart(selectedPartData.partID, 1))
        {
            ClearSelection();
        }
    }

    private void EnsureSlotCount(int count)
    {
        while (slotUIs.Count < count)
        {
            BackpackSlotUI newSlot = Instantiate(slotPrefab, slotGridRoot);
            slotUIs.Add(newSlot);
        }
    }

    private void ClearInventoryView()
    {
        for (int i = 0; i < slotUIs.Count; i++)
        {
            slotUIs[i].Clear();
        }

        ClearDragPreview();
    }

    #endregion

    #region Slot Events

    private void HandleSlotClicked(StoredPartRuntimeData slotData, PartData partData)
    {
        SelectPart(slotData, partData);
    }

    private void HandleSlotBeginDrag(StoredPartRuntimeData slotData, PartData partData, Vector2 screenPosition)
    {
        lastPointerScreenPosition = screenPosition;
        SelectPart(slotData, partData);
        isDraggingBackpackPreview = true;
        SetBackpackBlocksRaycasts(false);
        LogInstallDragDebug($"Begin drag partID={slotData.partID}, prefab={(partData.partPrefab != null ? partData.partPrefab.name : "NULL")}");
        BeginDragPreview(partData, screenPosition);
    }

    private void HandleSlotDrag(Vector2 screenPosition)
    {
        lastPointerScreenPosition = screenPosition;
    }

    private void HandleSlotEndDrag(Vector2 screenPosition)
    {
        lastPointerScreenPosition = screenPosition;
        bool installed = false;

        try
        {
            installed = FinishDragInstall(screenPosition);
        }
        finally
        {
            isDraggingBackpackPreview = false;
            SetBackpackBlocksRaycasts(true);

            if (!installed)
            {
                ClearDragPreview();
            }

            RefreshUI();
        }
    }

    private void SelectPart(StoredPartRuntimeData slotData, PartData partData)
    {
        selectedSlotData = slotData;
        selectedPartData = partData;
    }

    #endregion

    #region Panel Style

    private void ApplyPanelRuntimeStyle()
    {
        if (panelRoot == null)
        {
            return;
        }

        FitSlotGridToPanel();

        if (applyRuntimePanelStyle)
        {
            Image panelImage = panelRoot.GetComponent<Image>();

            if (panelImage == null)
            {
                panelImage = panelRoot.AddComponent<Image>();
            }

            panelImage.color = panelBackgroundColor;
            panelImage.raycastTarget = true;

            Shadow panelShadow = panelRoot.GetComponent<Shadow>();

            if (panelShadow == null)
            {
                panelShadow = panelRoot.AddComponent<Shadow>();
            }

            panelShadow.effectColor = panelShadowColor;
            panelShadow.effectDistance = panelShadowDistance;
            panelShadow.useGraphicAlpha = true;
        }
    }

    private void FitSlotGridToPanel()
    {
        if (!fitSlotGridToPanel)
        {
            return;
        }

        RectTransform slotGridRect = slotGridRoot as RectTransform;

        if (slotGridRect == null)
        {
            return;
        }

        slotGridRect.anchorMin = Vector2.zero;
        slotGridRect.anchorMax = Vector2.one;
        slotGridRect.pivot = new Vector2(0.5f, 0.5f);
        slotGridRect.offsetMin = slotGridPadding;
        slotGridRect.offsetMax = -slotGridPadding;
        slotGridRect.localScale = Vector3.one;
    }

    #endregion

    #region Selection

    private void ClearSelection()
    {
        selectedSlotData = default;
        selectedPartData = null;
    }

    private void DisableNonSlotGraphicRaycasts()
    {
        if (!disableNonSlotGraphicRaycasts || panelRoot == null)
        {
            return;
        }

        Graphic[] graphics = panelRoot.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic == null || graphic.GetComponentInParent<BackpackSlotUI>() != null)
            {
                continue;
            }

            if (graphic.GetComponentInParent<Selectable>() != null)
            {
                continue;
            }

            graphic.raycastTarget = false;
        }
    }

    #endregion

    #region Drag Install

    private void BeginDragPreview(PartData partData, Vector2 screenPosition)
    {
        ClearDragPreview();

        if (partData == null || partData.partPrefab == null)
        {
            LogInstallDragDebug("BeginDragPreview failed: partData or partPrefab is null.");
            return;
        }

        dragPreviewRotationOffset = Vector3.zero;
        dragPreviewDistance = Mathf.Clamp(fallbackPlacementDistance, minPlacementDistance, maxPlacementDistance);

        if (!TryGetPlacementPose(partData, screenPosition, false, out Vector3 worldPosition, out Quaternion worldRotation))
        {
            LogInstallDragDebug("BeginDragPreview failed: no placement pose.");
            return;
        }

        dragPreviewInstance = Instantiate(partData.partPrefab, worldPosition, worldRotation);
        dragPreviewInstance.name = partData.partPrefab.name + " Preview";
        EnsureRuntimeMeshAttachColliders(dragPreviewInstance, true);
        SetPartCollidersForInstallMode(dragPreviewInstance);
        LogInstallDragDebug($"Preview created: {dragPreviewInstance.name}");
    }

    private void MoveDragPreview(Vector2 screenPosition)
    {
        if (dragPreviewInstance == null)
        {
            return;
        }

        UpdateDragPreviewRotationInput();

        if (!TryGetPlacementPose(selectedPartData, screenPosition, false, out Vector3 worldPosition, out Quaternion worldRotation))
        {
            return;
        }

        dragPreviewInstance.transform.SetPositionAndRotation(worldPosition, worldRotation);
    }

    private bool FinishDragInstall(Vector2 screenPosition)
    {
        MoveDragPreview(screenPosition);

        PartData installedPartData = selectedPartData;

        if (dragPreviewInstance == null)
        {
            LogInstallDragDebug("FinishDragInstall failed: dragPreviewInstance is null.");
            return false;
        }

        if (!TryGetPlacementPose(installedPartData, screenPosition, true, out Vector3 worldPosition, out Quaternion worldRotation))
        {
            LogInstallDragDebug("FinishDragInstall failed: no attach surface under pointer.");
            ClearDragPreview();
            return false;
        }

        dragPreviewInstance.transform.SetPositionAndRotation(worldPosition, worldRotation);

        if (!TryRequestInstallSelected(currentAttachSurface))
        {
            LogInstallDragDebug("FinishDragInstall failed: server install request was not sent.");
            ClearDragPreview();
            return false;
        }

        ClearDragPreview();
        LogInstallDragDebug("FinishDragInstall succeeded.");
        return true;
    }

    private bool TryGetPlacementPose(
        PartData partData,
        Vector2 screenPosition,
        bool requireAttachTarget,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        Camera cameraToUse = placementCamera != null ? placementCamera : Camera.main;

        if (cameraToUse == null)
        {
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;
            return false;
        }

        Ray ray = cameraToUse.ScreenPointToRay(screenPosition);

        if (TryGetAttachPoseFromScreenRay(
            ray,
            partData,
            cameraToUse.transform.forward,
            out PartAttachSurface rayAttachSurface,
            out worldPosition,
            out worldRotation))
        {
            currentAttachSurface = rayAttachSurface;
            currentAttachRootOverride = null;
            currentAttachPointIDOverride = string.Empty;
            return true;
        }

        currentAttachSurface = null;

        if (TryGetBoneAttachPose(
            cameraToUse,
            screenPosition,
            out Transform boneAttachRoot,
            out string boneAttachPointID,
            out worldPosition,
            out worldRotation))
        {
            currentAttachRootOverride = boneAttachRoot;
            currentAttachPointIDOverride = boneAttachPointID;
            return true;
        }

        currentAttachRootOverride = null;
        currentAttachPointIDOverride = string.Empty;

        if (requireAttachTarget)
        {
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;
            return false;
        }

        Vector3 freeWorldPosition = ray.GetPoint(dragPreviewDistance);
        worldPosition = freeWorldPosition;
        worldRotation = Quaternion.Euler(dragPreviewRotationOffset);
        return true;
    }

    private bool TryGetBoneAttachPose(
        Camera cameraToUse,
        Vector2 screenPosition,
        out Transform attachRoot,
        out string attachPointID,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        attachRoot = null;
        attachPointID = string.Empty;
        worldPosition = Vector3.zero;
        worldRotation = Quaternion.identity;

        PlayerAttachColliderController attachColliderController = GetLocalAttachColliderController();

        if (attachColliderController == null)
        {
            return false;
        }

        return attachColliderController.TryResolveBoneAttachPose(
            cameraToUse,
            screenPosition,
            dragPreviewRotationOffset,
            out attachRoot,
            out attachPointID,
            out worldPosition,
            out worldRotation
        );
    }

    private bool TryGetAttachPoseFromScreenRay(
        Ray ray,
        PartData partData,
        Vector3 cameraForward,
        out PartAttachSurface attachSurface,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        attachSurface = null;
        worldPosition = Vector3.zero;
        worldRotation = Quaternion.identity;

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            placementRayDistance,
            placementMask,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0)
        {
            LogInstallDragDebug("Raycast found no AttachSurface hits.");
            return false;
        }

        System.Array.Sort(hits, CompareRaycastHitDistance);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;

            if (IsIgnoredAttachCollider(hitCollider))
            {
                continue;
            }

            MeshCollider meshCollider = hitCollider as MeshCollider;

            if (requireMeshColliderAttachSurface && meshCollider == null)
            {
                continue;
            }

            if (meshCollider != null && meshCollider.sharedMesh == null)
            {
                continue;
            }

            PartAttachSurface surface = hitCollider.GetComponentInParent<PartAttachSurface>();

            if (surface == null || !surface.CanAttach(partData))
            {
                LogInstallDragDebug($"Raycast skipped {hitCollider.name}: missing surface or CanAttach failed.");
                continue;
            }

            attachSurface = surface;
            attachSurface.BuildAttachPoseFromHit(
                hits[i],
                cameraForward,
                dragPreviewRotationOffset,
                out worldPosition,
                out worldRotation
            );
            return true;
        }

        return false;
    }

    private static int CompareRaycastHitDistance(RaycastHit left, RaycastHit right)
    {
        return left.distance.CompareTo(right.distance);
    }

    private bool TryRequestInstallSelected(PartAttachSurface attachSurface)
    {
        Transform attachRoot = ResolveCurrentAttachRoot(attachSurface);

        if (localInventory == null || selectedPartData == null || selectedSlotData.IsEmpty || attachRoot == null)
        {
            LogInstallDragDebug("TryRequestInstallSelected failed: missing inventory, selected part, slot, or attach root.");
            return false;
        }

        if (!localInventory.ClientHasPart(selectedPartData.partID, 1))
        {
            LogInstallDragDebug($"TryRequestInstallSelected failed: client does not have partID={selectedPartData.partID}.");
            return false;
        }

        Vector3 installedPosition = attachRoot != null && dragPreviewInstance != null
            ? attachRoot.InverseTransformPoint(dragPreviewInstance.transform.position)
            : Vector3.zero;
        Vector3 installedEulerAngles = attachRoot != null && dragPreviewInstance != null
            ? (Quaternion.Inverse(attachRoot.rotation) * dragPreviewInstance.transform.rotation).eulerAngles
            : Vector3.zero;
        Vector3 installedScale = dragPreviewInstance != null ? dragPreviewInstance.transform.localScale : Vector3.one;

        localInventory.CmdInstallPartFromBackpack(
            selectedPartData.partID,
            BuildCurrentSyncedAttachPointID(attachSurface, attachRoot),
            installedPosition,
            installedEulerAngles,
            installedScale
        );

        return true;
    }

    private bool IsDragPreviewCollider(Collider hitCollider)
    {
        return dragPreviewInstance != null && hitCollider.transform.IsChildOf(dragPreviewInstance.transform);
    }

    private bool IsIgnoredAttachCollider(Collider hitCollider)
    {
        return hitCollider == null
            || hitCollider is CapsuleCollider
            || IsDragPreviewCollider(hitCollider);
    }

    private void AttachInstalledPartToSurface(GameObject installedPart, PartAttachSurface attachSurface)
    {
        if (installedPart == null || attachSurface == null)
        {
            return;
        }

        Transform attachRoot = ResolveAttachRoot(attachSurface);

        if (attachRoot == null)
        {
            return;
        }

        Transform attachPoint = CreateRuntimeAttachPoint(attachRoot, installedPart.transform);
        installedPart.transform.SetParent(attachPoint, true);
        installedPart.transform.localPosition = Vector3.zero;
        installedPart.transform.localRotation = Quaternion.identity;
    }

    private Transform CreateRuntimeAttachPoint(Transform attachRoot, Transform installedPart)
    {
        GameObject attachPointObject = new GameObject(installedPart.name + " AttachPoint");
        Transform attachPoint = attachPointObject.transform;
        attachPoint.SetParent(attachRoot, false);
        attachPoint.SetPositionAndRotation(installedPart.position, installedPart.rotation);
        attachPoint.localScale = Vector3.one;
        return attachPoint;
    }

    private Transform ResolveAttachRoot(PartAttachSurface attachSurface)
    {
        if (attachSurface == null)
        {
            return null;
        }

        Vector3 attachPosition = dragPreviewInstance != null
            ? dragPreviewInstance.transform.position
            : attachSurface.transform.position;

        return attachSurface.ResolveAttachRoot(attachPosition);
    }

    private Transform ResolveCurrentAttachRoot(PartAttachSurface attachSurface)
    {
        if (currentAttachRootOverride != null)
        {
            return currentAttachRootOverride;
        }

        return ResolveAttachRoot(attachSurface);
    }

    private string BuildCurrentSyncedAttachPointID(PartAttachSurface attachSurface, Transform attachRoot)
    {
        if (!string.IsNullOrWhiteSpace(currentAttachPointIDOverride))
        {
            return currentAttachPointIDOverride;
        }

        return BuildSyncedAttachPointID(attachSurface, attachRoot);
    }

    private string BuildSyncedAttachPointID(PartAttachSurface attachSurface, Transform attachRoot)
    {
        if (attachSurface == null)
        {
            return string.Empty;
        }

        if (attachRoot == null)
        {
            return attachSurface.AttachPointID;
        }

        return $"{attachSurface.AttachPointID}{AttachPointSeparator}{attachRoot.name}";
    }

    private void EnsureAttachSurface(GameObject installedPart, PartData partData)
    {
        if (installedPart == null || partData == null)
        {
            return;
        }

        PartAttachSurface attachSurface = installedPart.GetComponent<PartAttachSurface>();

        if (attachSurface == null)
        {
            installedPart.AddComponent<PartAttachSurface>();
        }
    }

    private void EnsureRuntimeMeshAttachColliders(GameObject target, bool enabled)
    {
        if (!generateRuntimeMeshAttachColliders)
        {
            return;
        }

        EnsureMeshAttachColliders(target, enabled);
    }

    private void EnsureMeshAttachColliders(GameObject target, bool enabled)
    {
        if (target == null)
        {
            return;
        }

        MeshFilter[] meshFilters = target.GetComponentsInChildren<MeshFilter>();

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];

            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            MeshCollider meshCollider = meshFilter.GetComponent<MeshCollider>();

            if (meshCollider == null)
            {
                meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
            }

            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = true;
            meshCollider.isTrigger = true;
            meshCollider.enabled = enabled;
        }

        SkinnedMeshRenderer[] skinnedMeshRenderers = target.GetComponentsInChildren<SkinnedMeshRenderer>();

        for (int i = 0; i < skinnedMeshRenderers.Length; i++)
        {
            SkinnedMeshRenderer skinnedMeshRenderer = skinnedMeshRenderers[i];

            if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null)
            {
                continue;
            }

            MeshCollider meshCollider = skinnedMeshRenderer.GetComponent<MeshCollider>();

            if (meshCollider == null)
            {
                meshCollider = skinnedMeshRenderer.gameObject.AddComponent<MeshCollider>();
            }

            meshCollider.sharedMesh = skinnedMeshRenderer.sharedMesh;
            meshCollider.convex = true;
            meshCollider.isTrigger = true;
            meshCollider.enabled = enabled;
        }
    }

    private void SetPartCollidersForInstallMode(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = true;
            colliders[i].isTrigger = true;
        }

        Rigidbody[] rigidbodies = target.GetComponentsInChildren<Rigidbody>();
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
        }
    }

    private void ClearDragPreview()
    {
        currentAttachSurface = null;
        currentAttachRootOverride = null;
        currentAttachPointIDOverride = string.Empty;

        if (dragPreviewInstance == null)
        {
            return;
        }

        Destroy(dragPreviewInstance);
        dragPreviewInstance = null;
    }

    private void InitializePlacedPartDragHandler(GameObject placedPart, PartData partData)
    {
        if (placedPart == null || partData == null || localInventory == null)
        {
            return;
        }

        PlacedPartDragHandler dragHandler = placedPart.GetComponent<PlacedPartDragHandler>();

        if (dragHandler == null)
        {
            dragHandler = placedPart.AddComponent<PlacedPartDragHandler>();
        }

        dragHandler.Initialize(
            partData.partID,
            localInventory,
            this,
            placementCamera,
            placementMask,
            placementRayDistance,
            placementProbeRadius,
            placementRotationSpeed,
            minPlacementDistance,
            maxPlacementDistance,
            placementDistanceScrollSpeed
        );
    }

    #endregion

    #region Inventory Binding

    private void UnbindInventory()
    {
        SetLocalAttachMeshCollidersEnabled(false);
        RestoreLocalPlayerCapsuleColliders();

        if (localInventory == null)
        {
            return;
        }

        localInventory.OnInventoryChanged -= RefreshUI;
        localInventory = null;
        localAttachColliderController = null;
    }

    private void DisableLocalPlayerCapsuleColliders()
    {
        if (!disableLocalPlayerCapsuleCollidersWhileOpen || localInventory == null)
        {
            return;
        }

        RestoreLocalPlayerCapsuleColliders();

        CapsuleCollider[] capsuleColliders = localInventory.GetComponentsInChildren<CapsuleCollider>();

        for (int i = 0; i < capsuleColliders.Length; i++)
        {
            CapsuleCollider capsuleCollider = capsuleColliders[i];

            if (capsuleCollider == null || !capsuleCollider.enabled)
            {
                continue;
            }

            capsuleCollider.enabled = false;
            disabledLocalCapsuleColliders.Add(capsuleCollider);
        }
    }

    private void RestoreLocalPlayerCapsuleColliders()
    {
        for (int i = 0; i < disabledLocalCapsuleColliders.Count; i++)
        {
            CapsuleCollider capsuleCollider = disabledLocalCapsuleColliders[i];

            if (capsuleCollider != null)
            {
                capsuleCollider.enabled = true;
            }
        }

        disabledLocalCapsuleColliders.Clear();
    }

    private void SetLocalAttachMeshCollidersEnabled(bool isEnabled)
    {
        PlayerAttachColliderController attachColliderController = GetLocalAttachColliderController();

        if (attachColliderController != null)
        {
            attachColliderController.SetAttachMeshCollidersEnabled(isEnabled);
            LogInstallDragDebug($"Local attach MeshColliders enabled={isEnabled}.");
            return;
        }

        LogInstallDragDebug("Local PlayerAttachColliderController was not found.");
    }

    private PlayerAttachColliderController GetLocalAttachColliderController()
    {
        if (localAttachColliderController != null)
        {
            return localAttachColliderController;
        }

        if (localInventory == null)
        {
            return null;
        }

        localAttachColliderController = localInventory.GetComponentInParent<PlayerAttachColliderController>();
        return localAttachColliderController;
    }

    private void LogInstallDragDebug(string message)
    {
        if (logInstallDragDebug)
        {
            Debug.Log($"[BackpackInstall] {message}");
        }
    }

    private void UpdateDragPreviewRotationInput()
    {
        UpdateDragPreviewDistanceInput();

        Vector3 direction = Vector3.zero;

        if (Input.GetKey(KeyCode.Q))
        {
            direction.x -= 1f;
        }

        if (Input.GetKey(KeyCode.E))
        {
            direction.x += 1f;
        }

        if (Input.GetKey(KeyCode.Alpha1))
        {
            direction.y -= 1f;
        }

        if (Input.GetKey(KeyCode.Alpha2))
        {
            direction.y += 1f;
        }

        if (Input.GetKey(KeyCode.Alpha3))
        {
            direction.z -= 1f;
        }

        if (Input.GetKey(KeyCode.Alpha4))
        {
            direction.z += 1f;
        }

        if (direction.sqrMagnitude > 0f)
        {
            dragPreviewRotationOffset += direction * placementRotationSpeed * Time.deltaTime;
        }
    }

    private void UpdateDragPreviewDistanceInput()
    {
        float scroll = Input.mouseScrollDelta.y;

        if (Mathf.Approximately(scroll, 0f))
        {
            return;
        }

        dragPreviewDistance = Mathf.Clamp(
            dragPreviewDistance + scroll * placementDistanceScrollSpeed,
            minPlacementDistance,
            maxPlacementDistance
        );
    }

    private void CachePanelCanvasGroup()
    {
        if (panelCanvasGroup != null || panelRoot == null)
        {
            return;
        }

        panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();

        if (panelCanvasGroup == null)
        {
            panelCanvasGroup = panelRoot.AddComponent<CanvasGroup>();
        }

        defaultPanelBlocksRaycasts = true;
        panelCanvasGroup.blocksRaycasts = true;
    }

    private void UnlockCursorForBackpack()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void LockCursorForGameplay()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SwitchToAssemblyCamera()
    {
        if (PlayerCameraManager.HasInstance)
        {
            PlayerCameraManager.Instance.SwitchToAssemblyCamera();
        }
    }

    private void SwitchToGameplayCamera()
    {
        if (PlayerCameraManager.HasInstance)
        {
            PlayerCameraManager.Instance.SwitchToGameplayCamera();
        }
    }

    #endregion


    /// <summary>
    /// Logs all MeshColliders under the local player for attach debugging.
    /// </summary>
    public void DebugLocalPlayerMeshColliders()
    {
        if (!logAttachColliderDebug)
        {
            return;
        }

        if (localInventory == null)
        {
            Debug.LogWarning("[AttachDebug] localInventory is null.");
            return;
        }

        MeshCollider[] meshColliders = localInventory.GetComponentsInChildren<MeshCollider>(true);

        Debug.Log($"[AttachDebug] MeshCollider count: {meshColliders.Length}");

        for (int i = 0; i < meshColliders.Length; i++)
        {
            MeshCollider meshCollider = meshColliders[i];

            Debug.Log(
                $"[AttachDebug] {meshCollider.name} | " +
                $"activeInHierarchy: {meshCollider.gameObject.activeInHierarchy} | " +
                $"enabled: {meshCollider.enabled} | " +
                $"isTrigger: {meshCollider.isTrigger} | " +
                $"convex: {meshCollider.convex} | " +
                $"layer: {LayerMask.LayerToName(meshCollider.gameObject.layer)} | " +
                $"sharedMesh: {(meshCollider.sharedMesh != null ? meshCollider.sharedMesh.name : "NULL")} | " +
                $"has PartAttachSurface parent: {meshCollider.GetComponentInParent<PartAttachSurface>() != null}"
            );
        }
    }
}
