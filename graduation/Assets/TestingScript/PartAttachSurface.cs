using UnityEngine;

public class PartAttachSurface : MonoBehaviour
{
    #region Settings

    [Header("Attach Rules")]
    [SerializeField] private string attachPointID;
    [SerializeField] private bool acceptAnyPartType = true;
    [SerializeField] private PartType acceptedPartType = PartType.Arm;

    [Header("Attach Transform")]
    [SerializeField] private Transform attachRoot;
    [SerializeField] private bool alignToSurfaceNormal = true;
    [SerializeField] private float surfaceOffset = 0.02f;

    #endregion

    #region Public Properties

    public Transform AttachRoot => attachRoot != null ? attachRoot : transform;
    public string AttachPointID => string.IsNullOrWhiteSpace(attachPointID) ? name : attachPointID;
    public bool AlignToSurfaceNormal => alignToSurfaceNormal;
    public float SurfaceOffset => surfaceOffset;

    #endregion

    #region Public Methods

    /// <summary>
    /// Checks whether this surface accepts the selected part.
    /// </summary>
    public bool CanAttach(PartData partData)
    {
        if (partData == null)
        {
            return false;
        }

        return acceptAnyPartType || partData.partType == acceptedPartType;
    }

    /// <summary>
    /// Builds the world-space pose at the pointer hit point.
    /// </summary>
    public void BuildAttachPose(
        RaycastHit hit,
        Vector3 cameraForward,
        Vector3 rotationOffset,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        Vector3 surfaceNormal = hit.normal.sqrMagnitude > 0f ? hit.normal.normalized : transform.up;
        worldPosition = hit.point + surfaceNormal * surfaceOffset;

        if (!alignToSurfaceNormal)
        {
            worldRotation = Quaternion.Euler(rotationOffset);
            return;
        }

        Vector3 forwardOnSurface = Vector3.ProjectOnPlane(cameraForward, surfaceNormal);

        if (forwardOnSurface.sqrMagnitude < 0.0001f)
        {
            forwardOnSurface = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
        }

        if (forwardOnSurface.sqrMagnitude < 0.0001f)
        {
            forwardOnSurface = Vector3.forward;
        }

        Quaternion surfaceRotation = Quaternion.LookRotation(forwardOnSurface.normalized, surfaceNormal);
        worldRotation = surfaceRotation * Quaternion.Euler(rotationOffset);
    }

    /// <summary>
    /// Builds the world-space pose from the dragged part center to a nearby attach collider.
    /// </summary>
    public void BuildAttachPoseFromCenter(
        Collider attachCollider,
        Vector3 partCenter,
        Vector3 cameraForward,
        Vector3 rotationOffset,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        Vector3 surfacePoint = attachCollider != null ? attachCollider.ClosestPoint(partCenter) : partCenter;
        Vector3 surfaceNormal = partCenter - surfacePoint;

        if (surfaceNormal.sqrMagnitude < 0.0001f)
        {
            surfaceNormal = transform.up;
        }
        else
        {
            surfaceNormal.Normalize();
        }

        worldPosition = surfacePoint + surfaceNormal * surfaceOffset;

        if (!alignToSurfaceNormal)
        {
            worldRotation = Quaternion.Euler(rotationOffset);
            return;
        }

        Vector3 forwardOnSurface = Vector3.ProjectOnPlane(cameraForward, surfaceNormal);

        if (forwardOnSurface.sqrMagnitude < 0.0001f)
        {
            forwardOnSurface = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
        }

        if (forwardOnSurface.sqrMagnitude < 0.0001f)
        {
            forwardOnSurface = Vector3.forward;
        }

        Quaternion surfaceRotation = Quaternion.LookRotation(forwardOnSurface.normalized, surfaceNormal);
        worldRotation = surfaceRotation * Quaternion.Euler(rotationOffset);
    }

    #endregion
}
