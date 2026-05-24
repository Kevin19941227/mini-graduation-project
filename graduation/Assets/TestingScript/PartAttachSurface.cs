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
    [SerializeField] private Transform[] attachRootCandidates;
    [SerializeField] private bool autoCollectSkinnedMeshBones = true;
    [SerializeField] private string[] autoBoneNameFilters =
    {
        "spine",
        "chest",
        "head",
        "hand",
        "forearm",
        "arm",
        "leg",
        "foot",
        "thigh"
    };
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
    /// Resolves the bone or transform that should own an installed part at the given world position.
    /// </summary>
    public Transform ResolveAttachRoot(Vector3 worldPosition)
    {
        if (attachRoot != null)
        {
            return attachRoot;
        }

        Transform nearestCandidate = FindNearestAttachRootCandidate(worldPosition);

        if (nearestCandidate != null)
        {
            return nearestCandidate;
        }

        return transform;
    }

    /// <summary>
    /// Builds the world-space pose from a raycast hit on an attach MeshCollider.
    /// </summary>
    public void BuildAttachPoseFromHit(
        RaycastHit hit,
        Vector3 cameraForward,
        Vector3 rotationOffsetEuler,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        Vector3 surfaceNormal = hit.normal;

        if (surfaceNormal == Vector3.zero)
        {
            surfaceNormal = -cameraForward.normalized;
        }

        if (surfaceNormal == Vector3.zero)
        {
            surfaceNormal = transform.forward;
        }

        Quaternion offsetRotation = Quaternion.Euler(rotationOffsetEuler);
        worldPosition = hit.point + surfaceNormal.normalized * surfaceOffset;

        if (!alignToSurfaceNormal)
        {
            worldRotation = offsetRotation;
            return;
        }

        Quaternion surfaceRotation = Quaternion.LookRotation(surfaceNormal.normalized, Vector3.up);
        worldRotation = surfaceRotation * offsetRotation;
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
        BuildAttachPoseFromHit(hit, cameraForward, rotationOffset, out worldPosition, out worldRotation);
    }

    #endregion

    #region Attach Root Resolution

    private Transform FindNearestAttachRootCandidate(Vector3 worldPosition)
    {
        Transform nearestCandidate = FindNearestExplicitCandidate(worldPosition);

        if (nearestCandidate != null || !autoCollectSkinnedMeshBones)
        {
            return nearestCandidate;
        }

        return FindNearestSkinnedMeshBone(worldPosition);
    }

    private Transform FindNearestExplicitCandidate(Vector3 worldPosition)
    {
        if (attachRootCandidates == null || attachRootCandidates.Length == 0)
        {
            return null;
        }

        Transform nearestCandidate = null;
        float nearestSqrDistance = float.PositiveInfinity;

        for (int i = 0; i < attachRootCandidates.Length; i++)
        {
            Transform candidate = attachRootCandidates[i];

            if (candidate == null)
            {
                continue;
            }

            float sqrDistance = (candidate.position - worldPosition).sqrMagnitude;

            if (sqrDistance >= nearestSqrDistance)
            {
                continue;
            }

            nearestSqrDistance = sqrDistance;
            nearestCandidate = candidate;
        }

        return nearestCandidate;
    }

    private Transform FindNearestSkinnedMeshBone(Vector3 worldPosition)
    {
        Transform root = transform.root != null ? transform.root : transform;
        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Transform nearestBone = null;
        float nearestSqrDistance = float.PositiveInfinity;

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Transform[] bones = renderers[rendererIndex].bones;

            if (bones == null)
            {
                continue;
            }

            for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
            {
                Transform bone = bones[boneIndex];

                if (bone == null || !IsAllowedAutoBone(bone))
                {
                    continue;
                }

                float sqrDistance = (bone.position - worldPosition).sqrMagnitude;

                if (sqrDistance >= nearestSqrDistance)
                {
                    continue;
                }

                nearestSqrDistance = sqrDistance;
                nearestBone = bone;
            }
        }

        return nearestBone;
    }

    private bool IsAllowedAutoBone(Transform bone)
    {
        if (autoBoneNameFilters == null || autoBoneNameFilters.Length == 0)
        {
            return true;
        }

        string boneName = bone.name.ToLowerInvariant();

        for (int i = 0; i < autoBoneNameFilters.Length; i++)
        {
            string filter = autoBoneNameFilters[i];

            if (!string.IsNullOrWhiteSpace(filter) && boneName.Contains(filter.ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}
