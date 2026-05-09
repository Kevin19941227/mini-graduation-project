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
    private bool isInitialized;
    private bool isDragging;

    public void Initialize(
        int initializedPartID,
        PlayerInventoryNetwork initializedInventory,
        BackpackUIController initializedBackpackUIController,
        Camera initializedPlacementCamera,
        LayerMask initializedPlacementMask,
        float initializedPlacementRayDistance,
        float initializedFallbackPlacementDistance)
    {
        partID = initializedPartID;
        inventory = initializedInventory;
        backpackUIController = initializedBackpackUIController;
        placementCamera = initializedPlacementCamera;
        placementMask = initializedPlacementMask;
        placementRayDistance = initializedPlacementRayDistance;
        fallbackPlacementDistance = initializedFallbackPlacementDistance;
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
            worldRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(cameraToUse.transform.forward, hit.normal), hit.normal);
            return true;
        }

        worldPosition = ray.GetPoint(fallbackPlacementDistance);
        worldRotation = transform.rotation;
        return true;
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
