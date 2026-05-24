using UnityEngine;

public class PlacedPartDragHandler : MonoBehaviour
{
    #region Runtime Data

    private int partID;
    private PlayerInventoryNetwork inventory;
    private BackpackUIController backpackUIController;
    private Camera placementCamera;
    private LayerMask placementMask;
    private float placementRayDistance;
    private float placementProbeRadius;
    private float placementRotationSpeed;
    private float minPlacementDistance;
    private float maxPlacementDistance;
    private float placementDistanceScrollSpeed;
    private float dragFollowDistance;
    private Vector3 dragRotationOffset;
    private PartAttachSurface currentAttachSurface;
    private bool isInitialized;
    private bool isDragging;

    #endregion

    #region Public Methods

    /// <summary>
    /// Initializes dragging support for an installed part.
    /// </summary>
    public void Initialize(
        int initializedPartID,
        PlayerInventoryNetwork initializedInventory,
        BackpackUIController initializedBackpackUIController,
        Camera initializedPlacementCamera,
        LayerMask initializedPlacementMask,
        float initializedPlacementRayDistance,
        float initializedPlacementProbeRadius,
        float initializedPlacementRotationSpeed,
        float initializedMinPlacementDistance,
        float initializedMaxPlacementDistance,
        float initializedPlacementDistanceScrollSpeed)
    {
        partID = initializedPartID;
        inventory = initializedInventory;
        backpackUIController = initializedBackpackUIController;
        placementCamera = initializedPlacementCamera;
        placementMask = initializedPlacementMask;
        placementRayDistance = initializedPlacementRayDistance;
        placementProbeRadius = initializedPlacementProbeRadius;
        placementRotationSpeed = initializedPlacementRotationSpeed;
        minPlacementDistance = initializedMinPlacementDistance;
        maxPlacementDistance = Mathf.Max(initializedMaxPlacementDistance, minPlacementDistance);
        placementDistanceScrollSpeed = initializedPlacementDistanceScrollSpeed;
        isInitialized = partID > 0 && inventory != null && backpackUIController != null;

        EnsureDragCollider();
        SetCollidersForDragMode();
    }

    #endregion

    #region Mouse Events

    private void OnMouseDown()
    {
        if (!isInitialized || !backpackUIController.IsBackpackOpen())
        {
            return;
        }

        isDragging = true;
        backpackUIController.SetBackpackBlocksRaycasts(false);
        dragRotationOffset = Vector3.zero;
        dragFollowDistance = Mathf.Clamp(GetCurrentCameraDistance(), minPlacementDistance, maxPlacementDistance);
    }

    private void Update()
    {
        if (!isDragging)
        {
            return;
        }

        DragToScreenPosition(Input.mousePosition);
    }

    private void DragToScreenPosition(Vector2 screenPosition)
    {
        if (!isDragging)
        {
            return;
        }

        if (!backpackUIController.IsBackpackOpen())
        {
            isDragging = false;
            return;
        }

        UpdateRotationInput();

        if (TryGetPlacementPose(screenPosition, out Vector3 worldPosition, out Quaternion worldRotation))
        {
            transform.SetPositionAndRotation(worldPosition, worldRotation);
        }
    }

    private void OnMouseUp()
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        backpackUIController.SetBackpackBlocksRaycasts(true);

        if (!backpackUIController.IsScreenPositionOverBackpack(Input.mousePosition))
        {
            AttachToCurrentSurface();
            return;
        }

        inventory.CmdReturnInstalledPartToBackpack(partID);
        Destroy(gameObject);
    }

    #endregion

    #region Drag Logic

    private bool TryGetPlacementPose(Vector2 screenPosition, out Vector3 worldPosition, out Quaternion worldRotation)
    {
        Camera cameraToUse = placementCamera != null ? placementCamera : Camera.main;

        if (cameraToUse == null)
        {
            worldPosition = transform.position;
            worldRotation = transform.rotation;
            return false;
        }

        Ray ray = cameraToUse.ScreenPointToRay(screenPosition);

        if (TryGetAttachPoseFromScreenRay(
            ray,
            cameraToUse.transform.forward,
            out PartAttachSurface attachSurface,
            out worldPosition,
            out worldRotation))
        {
            currentAttachSurface = attachSurface;
            return true;
        }

        currentAttachSurface = null;
        Vector3 freeWorldPosition = ray.GetPoint(dragFollowDistance);
        worldPosition = freeWorldPosition;
        worldRotation = Quaternion.Euler(dragRotationOffset);
        return true;
    }

    private void UpdateRotationInput()
    {
        UpdateFollowDistanceInput();

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
            dragRotationOffset += direction * placementRotationSpeed * Time.deltaTime;
        }
    }

    private void UpdateFollowDistanceInput()
    {
        float scroll = Input.mouseScrollDelta.y;

        if (Mathf.Approximately(scroll, 0f))
        {
            return;
        }

        dragFollowDistance = Mathf.Clamp(
            dragFollowDistance + scroll * placementDistanceScrollSpeed,
            minPlacementDistance,
            maxPlacementDistance
        );
    }

    private float GetCurrentCameraDistance()
    {
        Camera cameraToUse = placementCamera != null ? placementCamera : Camera.main;

        if (cameraToUse == null)
        {
            return 3f;
        }

        float distance = Vector3.Dot(transform.position - cameraToUse.transform.position, cameraToUse.transform.forward);
        return Mathf.Max(0.5f, distance);
    }

    private void AttachToCurrentSurface()
    {
        if (currentAttachSurface == null)
        {
            return;
        }

        Transform attachRoot = currentAttachSurface.ResolveAttachRoot(transform.position);

        if (attachRoot == null)
        {
            return;
        }

        Transform oldParent = transform.parent;
        Transform attachPoint = CreateRuntimeAttachPoint(attachRoot);
        transform.SetParent(attachPoint, true);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        CleanupEmptyRuntimeAttachPoint(oldParent);
    }

    #endregion

    #region Attach Helpers

    private Transform CreateRuntimeAttachPoint(Transform attachRoot)
    {
        GameObject attachPointObject = new GameObject(name + " AttachPoint");
        Transform attachPoint = attachPointObject.transform;
        attachPoint.SetParent(attachRoot, false);
        attachPoint.SetPositionAndRotation(transform.position, transform.rotation);
        attachPoint.localScale = Vector3.one;
        return attachPoint;
    }

    private void CleanupEmptyRuntimeAttachPoint(Transform attachPoint)
    {
        if (attachPoint == null || attachPoint.childCount > 0 || !attachPoint.name.EndsWith(" AttachPoint"))
        {
            return;
        }

        Destroy(attachPoint.gameObject);
    }

    private void EnsureDragCollider()
    {
        Collider existingRootCollider = GetComponent<Collider>();

        if (existingRootCollider != null)
        {
            return;
        }

        BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
        boxCollider.center = Vector3.zero;
        boxCollider.size = Vector3.one;

        if (TryGetRendererLocalBounds(out Bounds localBounds))
        {
            boxCollider.center = localBounds.center;
            boxCollider.size = localBounds.size;
        }
    }

    private bool TryGetAttachPoseFromScreenRay(
        Ray ray,
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

            if (meshCollider == null || meshCollider.sharedMesh == null)
            {
                continue;
            }

            PartAttachSurface surface = hitCollider.GetComponentInParent<PartAttachSurface>();

            if (surface == null)
            {
                continue;
            }

            attachSurface = surface;
            attachSurface.BuildAttachPoseFromHit(
                hits[i],
                cameraForward,
                dragRotationOffset,
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

    private bool TryGetRendererLocalBounds(out Bounds localBounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            localBounds = default;
            return false;
        }

        Bounds worldBounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            worldBounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
        Vector3 localSize = transform.InverseTransformVector(worldBounds.size);
        localBounds = new Bounds(localCenter, Abs(localSize));
        return true;
    }

    private Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private bool IsIgnoredAttachCollider(Collider hitCollider)
    {
        return hitCollider == null
            || hitCollider is CapsuleCollider
            || hitCollider.transform.IsChildOf(transform);
    }

    private void SetCollidersForDragMode()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = true;
            colliders[i].isTrigger = true;
        }
    }

    #endregion
}
