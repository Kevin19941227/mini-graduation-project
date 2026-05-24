using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BackpackUIController : MonoBehaviour
{
    #region References

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("Panel Runtime Style")]
    [SerializeField] private bool applyRuntimePanelStyle = true;
    [SerializeField] private bool fitSlotGridToPanel = true;
    [SerializeField] private Vector2 slotGridPadding = new Vector2(16f, 16f);
    [SerializeField] private Color panelBackgroundColor = new Color(0.08f, 0.10f, 0.12f, 0.88f);
    [SerializeField] private Color panelShadowColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private Vector2 panelShadowDistance = new Vector2(6f, -6f);

    [Header("Slot UI")]
    [SerializeField] private Transform slotGridRoot;
    [SerializeField] private BackpackSlotUI slotPrefab;

    [Header("Detail UI")]
    [SerializeField] private RectTransform detailPanelRoot;
    [SerializeField] private Image detailIconImage;
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailStatText;
    [SerializeField] private Button equipButton;

    [Header("Detail Position")]
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private Vector2 detailOffset = new Vector2(20f, -20f);

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
    [SerializeField] private bool requireMeshColliderAttachSurface = true;
    [SerializeField] private bool disableLocalPlayerCapsuleCollidersWhileOpen = true;

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
    private bool isDraggingBackpackPreview;
    private readonly List<BackpackSlotUI> slotUIs = new List<BackpackSlotUI>();
    private readonly List<CapsuleCollider> disabledLocalCapsuleColliders = new List<CapsuleCollider>();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ApplyPanelRuntimeStyle();
        DisableDetailPanel();

        if (equipButton != null)
        {
            equipButton.onClick.AddListener(HandleEquipClicked);
        }
    }

    private void LateUpdate()
    {
        if (IsBackpackOpen())
        {
            UnlockCursorForBackpack();
        }

        if (isDraggingBackpackPreview && dragPreviewInstance != null)
        {
            MoveDragPreview(lastPointerScreenPosition);
        }
    }

    private void OnDestroy()
    {
        if (equipButton != null)
        {
            equipButton.onClick.RemoveListener(HandleEquipClicked);
        }

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

        UnlockCursorForBackpack();
        DisableLocalPlayerCapsuleColliders();
        SwitchToAssemblyCamera();
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

        HideDetailPanel();
        isDraggingBackpackPreview = false;
        ClearDragPreview();
        RestoreLocalPlayerCapsuleColliders();
        SwitchToGameplayCamera();
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
                HandleSlotHovered,
                HandleSlotMoved,
                HandleSlotHoverExit,
                HandleSlotBeginDrag,
                HandleSlotDrag,
                HandleSlotEndDrag
            );
        }

        if (selectedPartData != null && !localInventory.ClientHasPart(selectedPartData.partID, 1))
        {
            ClearSelection();
            ClearDetailPanel();
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

        ClearDetailPanel();
        ClearDragPreview();
    }

    #endregion

    #region Slot Events

    private void HandleSlotClicked(StoredPartRuntimeData slotData, PartData partData)
    {
        SelectPart(slotData, partData);
        ShowDetailPanel();
        RefreshDetailPanel();
        MoveDetailPanelToScreenPosition(lastPointerScreenPosition);
    }

    private void HandleSlotHovered(StoredPartRuntimeData slotData, PartData partData, Vector2 screenPosition)
    {
        lastPointerScreenPosition = screenPosition;
    }

    private void HandleSlotMoved(Vector2 screenPosition)
    {
        lastPointerScreenPosition = screenPosition;
    }

    private void HandleSlotHoverExit()
    {
    }

    private void HandleSlotBeginDrag(StoredPartRuntimeData slotData, PartData partData, Vector2 screenPosition)
    {
        lastPointerScreenPosition = screenPosition;
        SelectPart(slotData, partData);
        HideDetailPanel();
        isDraggingBackpackPreview = true;
        BeginDragPreview(partData, screenPosition);
    }

    private void HandleSlotDrag(Vector2 screenPosition)
    {
        lastPointerScreenPosition = screenPosition;
    }

    private void HandleSlotEndDrag(Vector2 screenPosition)
    {
        lastPointerScreenPosition = screenPosition;
        FinishDragInstall(screenPosition);
        isDraggingBackpackPreview = false;
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

    #region Detail Panel Disabled

    private void DisableDetailPanel()
    {
        ClearSelection();

        if (detailPanelRoot != null)
        {
            detailPanelRoot.gameObject.SetActive(false);
        }

        if (equipButton != null)
        {
            equipButton.interactable = false;
        }
    }

    private void ClearSelection()
    {
        selectedSlotData = default;
        selectedPartData = null;
    }

    #endregion

    #region Detail Panel

    private void ShowDetailPanel()
    {
        if (detailPanelRoot != null)
        {
            detailPanelRoot.gameObject.SetActive(true);
        }
    }

    private void HideDetailPanel()
    {
        if (detailPanelRoot != null)
        {
            detailPanelRoot.gameObject.SetActive(false);
        }
    }

    private void RefreshDetailPanel()
    {
        if (selectedPartData == null)
        {
            ClearDetailPanel();
            return;
        }

        if (detailIconImage != null)
        {
            detailIconImage.enabled = true;
            detailIconImage.sprite = selectedPartData.icon;
        }

        if (detailNameText != null)
        {
            detailNameText.text = selectedPartData.partName;
        }

        if (detailStatText != null)
        {
            detailStatText.text = BuildStatText(selectedPartData);
        }

        if (equipButton != null)
        {
            equipButton.interactable = true;
        }
    }

    private void ClearDetailPanel()
    {
        selectedSlotData = default;
        selectedPartData = null;

        if (detailIconImage != null)
        {
            detailIconImage.enabled = false;
            detailIconImage.sprite = null;
        }

        if (detailNameText != null)
        {
            detailNameText.text = "No part selected";
        }

        if (detailStatText != null)
        {
            detailStatText.text = string.Empty;
        }

        if (equipButton != null)
        {
            equipButton.interactable = false;
        }
    }

    private void MoveDetailPanelToScreenPosition(Vector2 screenPosition)
    {
        if (detailPanelRoot == null)
        {
            return;
        }

        Canvas canvas = detailPanelRoot.GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            return;
        }

        RectTransform parentRect = detailPanelRoot.parent as RectTransform;

        if (parentRect == null)
        {
            return;
        }

        Camera uiCamera = null;

        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
        }

        Vector2 offsetScreenPosition = screenPosition + detailOffset;

        bool hasWorldPoint = RectTransformUtility.ScreenPointToWorldPointInRectangle(
            parentRect,
            offsetScreenPosition,
            uiCamera,
            out Vector3 worldPoint
        );

        if (!hasWorldPoint)
        {
            return;
        }

        detailPanelRoot.position = worldPoint;
    }

    private string BuildStatText(PartData partData)
    {
        if (partData == null)
        {
            return string.Empty;
        }

        List<StatModifier> modifiers = new List<StatModifier>();
        partData.AppendStatModifiers(modifiers);

        if (modifiers.Count == 0)
        {
            return "No stat bonus";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
            }

            builder.Append(modifiers[i].GetDisplayText());
        }

        return builder.ToString();
    }

    #endregion

    #region Drag Install

    private void BeginDragPreview(PartData partData, Vector2 screenPosition)
    {
        ClearDragPreview();

        if (partData == null || partData.partPrefab == null)
        {
            return;
        }

        dragPreviewRotationOffset = Vector3.zero;
        dragPreviewDistance = Mathf.Clamp(fallbackPlacementDistance, minPlacementDistance, maxPlacementDistance);

        if (!TryGetPlacementPose(partData, screenPosition, false, out Vector3 worldPosition, out Quaternion worldRotation))
        {
            return;
        }

        dragPreviewInstance = Instantiate(partData.partPrefab, worldPosition, worldRotation);
        dragPreviewInstance.name = partData.partPrefab.name + " Preview";
        EnsureMeshAttachColliders(dragPreviewInstance, true);
        SetPartCollidersForInstallMode(dragPreviewInstance);
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
            return false;
        }

        if (!TryGetPlacementPose(installedPartData, screenPosition, true, out Vector3 worldPosition, out Quaternion worldRotation))
        {
            ClearDragPreview();
            return false;
        }

        dragPreviewInstance.transform.SetPositionAndRotation(worldPosition, worldRotation);

        if (!TryRequestInstallSelected(currentAttachSurface))
        {
            ClearDragPreview();
            return false;
        }

        dragPreviewInstance.name = installedPartData != null ? installedPartData.partName : dragPreviewInstance.name;
        AttachInstalledPartToSurface(dragPreviewInstance, currentAttachSurface);
        EnsureMeshAttachColliders(dragPreviewInstance, true);
        SetPartCollidersForInstallMode(dragPreviewInstance);
        EnsureAttachSurface(dragPreviewInstance, installedPartData);
        InitializePlacedPartDragHandler(dragPreviewInstance, installedPartData);
        dragPreviewInstance = null;
        return true;
    }

    private bool TryPlaceSelectedFromButton()
    {
        if (selectedPartData == null)
        {
            return false;
        }

        Vector2 screenPosition = lastPointerScreenPosition;

        if (screenPosition == Vector2.zero)
        {
            screenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        BeginDragPreview(selectedPartData, screenPosition);
        return FinishDragInstall(screenPosition);
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
        Vector3 freeWorldPosition = ray.GetPoint(dragPreviewDistance);

        if (TryGetAttachPoseFromPartCenter(
            freeWorldPosition,
            partData,
            out PartAttachSurface attachSurface,
            cameraToUse.transform.forward,
            out worldPosition,
            out worldRotation))
        {
            currentAttachSurface = attachSurface;
            return true;
        }

        currentAttachSurface = null;

        if (requireAttachTarget)
        {
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;
            return false;
        }

        worldPosition = freeWorldPosition;
        worldRotation = Quaternion.Euler(dragPreviewRotationOffset);
        return true;
    }

    private bool TryRequestInstallSelected(PartAttachSurface attachSurface)
    {
        if (localInventory == null || selectedPartData == null || selectedSlotData.IsEmpty || attachSurface == null)
        {
            return false;
        }

        if (!localInventory.ClientHasPart(selectedPartData.partID, 1))
        {
            return false;
        }

        Transform attachRoot = attachSurface.AttachRoot;
        Vector3 installedPosition = attachRoot != null && dragPreviewInstance != null
            ? attachRoot.InverseTransformPoint(dragPreviewInstance.transform.position)
            : Vector3.zero;
        Vector3 installedEulerAngles = attachRoot != null && dragPreviewInstance != null
            ? (Quaternion.Inverse(attachRoot.rotation) * dragPreviewInstance.transform.rotation).eulerAngles
            : Vector3.zero;
        Vector3 installedScale = dragPreviewInstance != null ? dragPreviewInstance.transform.localScale : Vector3.one;

        localInventory.CmdInstallPartFromBackpack(
            selectedPartData.partID,
            attachSurface.AttachPointID,
            installedPosition,
            installedEulerAngles,
            installedScale
        );

        return true;
    }

    private bool TryGetAttachPoseFromPartCenter(
        Vector3 partCenter,
        PartData partData,
        out PartAttachSurface attachSurface,
        Vector3 cameraForward,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        float nearestDistance = float.PositiveInfinity;
        attachSurface = null;
        Collider nearestCollider = null;
        worldPosition = Vector3.zero;
        worldRotation = Quaternion.identity;

        if (placementProbeRadius <= 0f)
        {
            return false;
        }

        Collider[] colliders = Physics.OverlapSphere(
            partCenter,
            placementProbeRadius,
            placementMask,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider hitCollider = colliders[i];

            if (IsIgnoredAttachCollider(hitCollider))
            {
                continue;
            }

            if (requireMeshColliderAttachSurface && hitCollider.GetComponent<MeshCollider>() == null)
            {
                continue;
            }

            PartAttachSurface surface = hitCollider.GetComponentInParent<PartAttachSurface>();

            if (surface == null || !surface.CanAttach(partData))
            {
                continue;
            }

            Vector3 closestPoint = hitCollider.ClosestPoint(partCenter);
            float sqrDistance = (partCenter - closestPoint).sqrMagnitude;

            if (sqrDistance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = sqrDistance;
            attachSurface = surface;
            nearestCollider = hitCollider;
        }

        if (attachSurface == null || nearestCollider == null)
        {
            return false;
        }

        attachSurface.BuildAttachPoseFromCenter(
            nearestCollider,
            partCenter,
            cameraForward,
            dragPreviewRotationOffset,
            out worldPosition,
            out worldRotation
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

        Transform attachRoot = attachSurface.AttachRoot;

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

    #region Button Events

    private void HandleEquipClicked()
    {
        PartData installedPartData = selectedPartData;

        if (!TryPlaceSelectedFromButton())
        {
            return;
        }

        Debug.Log($"[BackpackUI] Installed part: {installedPartData.partName}, PartID: {installedPartData.partID}");
    }

    #endregion

    #region Inventory Binding

    private void UnbindInventory()
    {
        RestoreLocalPlayerCapsuleColliders();

        if (localInventory == null)
        {
            return;
        }

        localInventory.OnInventoryChanged -= RefreshUI;
        localInventory = null;
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

    private void UnlockCursorForBackpack()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
