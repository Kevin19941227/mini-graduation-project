using UnityEngine;

public class PlacedPartDragHandler : MonoBehaviour
{
    private int partID;
    private PlayerInventoryNetwork inventory;
    private BackpackUIController backpackUIController;
    private Camera placementCamera;
    private LayerMask placementMask;
    private float placementRayDistance;
    private float fallbackPlacementDistance;
    private float placementRotationSpeed;
    private float dragYawOffset;
    private bool isInitialized;
    private bool isDragging;

    public void Initialize(
        int initializedPartID,
        PlayerInventoryNetwork initializedInventory,
        BackpackUIController initializedBackpackUIController,
        Camera initializedPlacementCamera,
        LayerMask initializedPlacementMask,
        float initializedPlacementRayDistance,
        float initializedFallbackPlacementDistance,
        float initializedPlacementRotationSpeed)
    {
        partID = initializedPartID;
        inventory = initializedInventory;
        backpackUIController = initializedBackpackUIController;
        placementCamera = initializedPlacementCamera;
        placementMask = initializedPlacementMask;
        placementRayDistance = initializedPlacementRayDistance;
        fallbackPlacementDistance = initializedFallbackPlacementDistance;
        placementRotationSpeed = initializedPlacementRotationSpeed;
        isInitialized = partID > 0 && inventory != null && backpackUIController != null;

        EnsureDragCollider();
    }

    private void OnMouseDown()
    {
        if (!isInitialized || !backpackUIController.IsBackpackOpen())
        {
            return;
        }

        isDragging = true;
        dragYawOffset = transform.eulerAngles.y;
    }

    private void OnMouseDrag()
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

        if (TryGetPlacementPose(Input.mousePosition, out Vector3 worldPosition, out Quaternion worldRotation))
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

        if (!backpackUIController.IsScreenPositionOverBackpack(Input.mousePosition))
        {
            return;
        }

        inventory.CmdReturnInstalledPartToBackpack(partID);
        Destroy(gameObject);
    }

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

        if (TryRaycastPlacement(ray, out RaycastHit hit))
        {
            worldPosition = hit.point;
            worldRotation = BuildPlacementRotation(cameraToUse.transform.forward, hit.normal);
            return true;
        }

        worldPosition = ray.GetPoint(fallbackPlacementDistance);
        worldRotation = Quaternion.Euler(0f, dragYawOffset, 0f);
        return true;
    }

    private void UpdateRotationInput()
    {
        float direction = 0f;

        if (Input.GetKey(KeyCode.Q))
        {
            direction -= 1f;
        }

        if (Input.GetKey(KeyCode.E))
        {
            direction += 1f;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(scroll, 0f))
        {
            direction += scroll;
        }

        if (!Mathf.Approximately(direction, 0f))
        {
            dragYawOffset += direction * placementRotationSpeed * Time.deltaTime;
        }
    }

    private Quaternion BuildPlacementRotation(Vector3 cameraForward, Vector3 surfaceNormal)
    {
        Vector3 forwardOnSurface = Vector3.ProjectOnPlane(cameraForward, surfaceNormal);

        if (forwardOnSurface.sqrMagnitude < 0.0001f)
        {
            forwardOnSurface = Vector3.ProjectOnPlane(Vector3.forward, surfaceNormal);
        }

        Quaternion surfaceRotation = Quaternion.LookRotation(forwardOnSurface.normalized, surfaceNormal);
        Quaternion yawRotation = Quaternion.AngleAxis(dragYawOffset, surfaceNormal);
        return yawRotation * surfaceRotation;
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

    private bool TryRaycastPlacement(Ray ray, out RaycastHit nearestHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, placementRayDistance, placementMask);
        float nearestDistance = float.PositiveInfinity;
        nearestHit = default;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null || hits[i].collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hits[i].distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = hits[i].distance;
            nearestHit = hits[i];
        }

        return nearestDistance < float.PositiveInfinity;
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
}
