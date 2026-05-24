using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class PlayerAttachColliderController : NetworkBehaviour
{
    #region Settings

    private const string BoneAttachSurfaceID = "BoneAttach";
    private const char AttachPointSeparator = '|';

    [Header("Bone Attach Fallback")]
    [SerializeField] private bool enableBoneAttachFallback = true;
    [SerializeField] private float maxBonePickScreenDistance = 80f;
    [SerializeField] private string[] boneNameFilters =
    {
        "spine",
        "chest",
        "head",
        "hand",
        "forearm",
        "arm",
        "leg",
        "foot",
        "thigh",
        "thumb",
        "index",
        "middle",
        "ring",
        "pinky"
    };

    #endregion

    #region Runtime Data

    private readonly List<Transform> cachedAttachBones = new List<Transform>();

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        CacheAttachBones();
        SetAttachMeshCollidersEnabled(false);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Enables or disables this player's assembly-only attach surface MeshColliders.
    /// </summary>
    public void SetAttachMeshCollidersEnabled(bool isEnabled)
    {
        MeshCollider[] meshColliders = GetComponentsInChildren<MeshCollider>(true);

        for (int i = 0; i < meshColliders.Length; i++)
        {
            MeshCollider meshCollider = meshColliders[i];

            if (meshCollider == null || meshCollider.GetComponentInParent<PartAttachSurface>() == null)
            {
                continue;
            }

            meshCollider.enabled = isEnabled && isLocalPlayer;
        }
    }

    /// <summary>
    /// Resolves the nearest allowed bone to a screen position for collider-free assembly placement.
    /// </summary>
    public bool TryResolveBoneAttachPose(
        Camera cameraToUse,
        Vector2 screenPosition,
        Vector3 rotationOffsetEuler,
        out Transform attachRoot,
        out string attachPointID,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        attachRoot = null;
        attachPointID = string.Empty;
        worldPosition = Vector3.zero;
        worldRotation = Quaternion.identity;

        if (!enableBoneAttachFallback || !isLocalPlayer || cameraToUse == null)
        {
            return false;
        }

        Transform nearestBone = FindNearestBoneToScreenPosition(cameraToUse, screenPosition);

        if (nearestBone == null)
        {
            return false;
        }

        attachRoot = nearestBone;
        attachPointID = BuildBoneAttachPointID(nearestBone);
        worldPosition = nearestBone.position;
        worldRotation = nearestBone.rotation * Quaternion.Euler(rotationOffsetEuler);
        return true;
    }

    #endregion

    #region Bone Resolution

    private Transform FindNearestBoneToScreenPosition(Camera cameraToUse, Vector2 screenPosition)
    {
        Transform nearestBone = null;
        float nearestScreenSqrDistance = maxBonePickScreenDistance * maxBonePickScreenDistance;

        for (int boneIndex = 0; boneIndex < cachedAttachBones.Count; boneIndex++)
        {
            Transform bone = cachedAttachBones[boneIndex];

            if (bone == null)
            {
                continue;
            }

            Vector3 boneScreenPosition = cameraToUse.WorldToScreenPoint(bone.position);

            if (boneScreenPosition.z <= 0f)
            {
                continue;
            }

            float sqrDistance = ((Vector2)boneScreenPosition - screenPosition).sqrMagnitude;

            if (sqrDistance >= nearestScreenSqrDistance)
            {
                continue;
            }

            nearestScreenSqrDistance = sqrDistance;
            nearestBone = bone;
        }

        return nearestBone;
    }

    private void CacheAttachBones()
    {
        cachedAttachBones.Clear();

        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);

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

                if (bone == null || !IsAllowedBone(bone) || cachedAttachBones.Contains(bone))
                {
                    continue;
                }

                cachedAttachBones.Add(bone);
            }
        }
    }

    private bool IsAllowedBone(Transform bone)
    {
        if (boneNameFilters == null || boneNameFilters.Length == 0)
        {
            return true;
        }

        string boneName = bone.name.ToLowerInvariant();

        for (int i = 0; i < boneNameFilters.Length; i++)
        {
            string filter = boneNameFilters[i];

            if (!string.IsNullOrWhiteSpace(filter) && boneName.Contains(filter.ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    private string BuildBoneAttachPointID(Transform bone)
    {
        return bone == null ? string.Empty : $"{BoneAttachSurfaceID}{AttachPointSeparator}{bone.name}";
    }

    #endregion
}
