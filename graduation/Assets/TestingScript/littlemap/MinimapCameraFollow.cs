using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class MinimapCameraFollow : MonoBehaviour
{
    private enum MarkerKind
    {
        LocalPlayer,
        RemotePlayer,
        Monster
    }

    [SerializeField] private float cameraHeight = 3000f;
    [SerializeField] private float followSmoothTime = 0.1f;
    [SerializeField] private float targetLookupInterval = 0.5f;
    [SerializeField] private bool logLookupStatus = true;

    [Header("Minimap Markers")]
    [SerializeField] private bool generateMarkers = true;
    [SerializeField] private float markerRefreshInterval = 2f;
    [SerializeField] private float markerDistanceBelowCamera = 10f;
    [SerializeField] private float playerMarkerSize = 5f;
    [SerializeField] private float monsterMarkerSize = 5f;
    [SerializeField] private Color localPlayerMarkerColor = Color.green;
    [SerializeField] private Color remotePlayerMarkerColor = Color.blue;
    [SerializeField] private Color monsterMarkerColor = Color.red;

    [Header("Zone Display")]
    [SerializeField] private bool showZone = true;
    [SerializeField] private int zoneLineSegments = 64;
    [SerializeField] private float zoneLineWidth = 8f;
    [SerializeField] private float directionLineWidth = 10f;
    [SerializeField] private float directionLineLength = 120f;
    [SerializeField] private Color zoneLineColor = Color.cyan;
    [SerializeField] private Color outsideZoneDirectionColor = Color.yellow;

    [Header("Performance")]
    [SerializeField] private int renderEveryNFrames = 4;

    private Camera minimapCamera;
    private Transform target;
    private Vector3 velocity;
    private float targetLookupTimer;
    private float markerRefreshTimer;
    private ZoneController zoneController;
    private LineRenderer zoneLineRenderer;
    private LineRenderer directionLineRenderer;
    private Mesh markerMesh;
    private Material localPlayerMarkerMaterial;
    private Material remotePlayerMarkerMaterial;
    private Material monsterMarkerMaterial;
    private readonly Dictionary<Transform, GameObject> markerObjects = new Dictionary<Transform, GameObject>();

    private float lastZoneRadius = -1f;
    private Vector3 lastZoneCenter;
    private float lastMarkerY = float.MinValue;

    private void Awake()
    {
        minimapCamera = GetComponent<Camera>();
        if (minimapCamera != null)
            minimapCamera.enabled = false;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            TryFindLocalPlayer();
        }

        if (target != null)
            FollowTarget();

        UpdateMarkers();
        UpdateZoneDisplay();

        if (minimapCamera != null && Time.frameCount % renderEveryNFrames == 0)
            minimapCamera.Render();
    }

    private void OnDestroy()
    {
        foreach (GameObject markerObject in markerObjects.Values)
        {
            if (markerObject != null)
                Destroy(markerObject);
        }

        markerObjects.Clear();

        if (zoneLineRenderer != null)
            Destroy(zoneLineRenderer.gameObject);

        if (directionLineRenderer != null)
            Destroy(directionLineRenderer.gameObject);
    }

    private void FollowTarget()
    {
        Vector3 targetPosition = new Vector3(
            target.position.x,
            target.position.y + cameraHeight,
            target.position.z);
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            followSmoothTime);

        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void TryFindLocalPlayer()
    {
        targetLookupTimer -= Time.deltaTime;
        if (targetLookupTimer > 0f)
            return;

        targetLookupTimer = targetLookupInterval;

        if (TryBindLocalPlayCol())
            return;

        if (TryBindLocalFastNetworkController())
            return;

        if (TryBindMirrorLocalPlayer())
            return;

        if (logLookupStatus)
            Debug.Log("[MinimapCameraFollow] Local gameplay player not found yet.");
    }

    private bool TryBindLocalPlayCol()
    {
        PlayCol[] players = FindObjectsOfType<PlayCol>();
        for (int i = 0; i < players.Length; i++)
        {
            PlayCol player = players[i];
            if (player == null || !player.isLocalPlayer)
                continue;

            target = player.transform;
            LogTargetFound("PlayCol.isLocalPlayer");
            return true;
        }

        return false;
    }

    private bool TryBindLocalFastNetworkController()
    {
        PlayerFastNetworkController[] players = FindObjectsOfType<PlayerFastNetworkController>();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerFastNetworkController player = players[i];
            if (player == null || !player.isLocalPlayer)
                continue;

            target = player.transform;
            LogTargetFound("PlayerFastNetworkController.isLocalPlayer");
            return true;
        }

        return false;
    }

    private bool TryBindMirrorLocalPlayer()
    {
        NetworkIdentity localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null)
            return false;

        if (localPlayer.GetComponent<PlayCol>() == null
            && localPlayer.GetComponent<PlayerFastNetworkController>() == null)
        {
            if (logLookupStatus)
                Debug.Log($"[MinimapCameraFollow] Ignored NetworkClient.localPlayer because it is not a gameplay player: {localPlayer.name}");

            return false;
        }

        target = localPlayer.transform;
        LogTargetFound("NetworkClient.localPlayer");
        return true;
    }

    private void LogTargetFound(string source)
    {
        if (!logLookupStatus)
            return;

        Debug.Log($"[MinimapCameraFollow] Bound target from {source}: {target.name}");
    }

    private void UpdateMarkers()
    {
        if (!generateMarkers)
            return;

        markerRefreshTimer -= Time.deltaTime;
        if (markerRefreshTimer <= 0f)
        {
            markerRefreshTimer = markerRefreshInterval;
            RefreshMarkerTargets();
        }

        float markerY = transform.position.y - markerDistanceBelowCamera;
        foreach (KeyValuePair<Transform, GameObject> markerPair in markerObjects)
        {
            Transform markerTarget = markerPair.Key;
            GameObject markerObject = markerPair.Value;
            if (markerTarget == null || markerObject == null)
                continue;

            markerObject.transform.position = new Vector3(markerTarget.position.x, markerY, markerTarget.position.z);
            markerObject.transform.rotation = Quaternion.identity;
        }
    }

    private void RefreshMarkerTargets()
    {
        RegisterPlayColMarkers();
        RegisterFastNetworkControllerMarkers();
        RegisterMonsterMarkers();
        RemoveMissingMarkers();
    }

    private void RegisterPlayColMarkers()
    {
        PlayCol[] players = FindObjectsOfType<PlayCol>();
        for (int i = 0; i < players.Length; i++)
        {
            PlayCol player = players[i];
            if (player == null)
                continue;

            MarkerKind markerKind = player.isLocalPlayer ? MarkerKind.LocalPlayer : MarkerKind.RemotePlayer;
            EnsureMarker(player.transform, markerKind);
        }
    }

    private void RegisterFastNetworkControllerMarkers()
    {
        PlayerFastNetworkController[] players = FindObjectsOfType<PlayerFastNetworkController>();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerFastNetworkController player = players[i];
            if (player == null || markerObjects.ContainsKey(player.transform))
                continue;

            MarkerKind markerKind = player.isLocalPlayer ? MarkerKind.LocalPlayer : MarkerKind.RemotePlayer;
            EnsureMarker(player.transform, markerKind);
        }
    }

    private void RegisterMonsterMarkers()
    {
        MonsterAI[] legacyMonsters = FindObjectsOfType<MonsterAI>();
        for (int i = 0; i < legacyMonsters.Length; i++)
        {
            MonsterAI monster = legacyMonsters[i];
            if (monster == null)
                continue;

            EnsureMarker(monster.transform, MarkerKind.Monster);
        }

        MonsterNavMeshAIController[] navMeshMonsters = FindObjectsOfType<MonsterNavMeshAIController>();
        for (int i = 0; i < navMeshMonsters.Length; i++)
        {
            MonsterNavMeshAIController monster = navMeshMonsters[i];
            if (monster == null || markerObjects.ContainsKey(monster.transform))
                continue;

            EnsureMarker(monster.transform, MarkerKind.Monster);
        }
    }

    private void EnsureMarker(Transform markerTarget, MarkerKind markerKind)
    {
        if (markerTarget == null || markerObjects.ContainsKey(markerTarget))
            return;

        EnsureMarkerAssets();

        GameObject markerObject = new GameObject($"MinimapMarker_{markerKind}_{markerTarget.name}");
        MeshFilter meshFilter = markerObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = markerObject.AddComponent<MeshRenderer>();
        meshFilter.sharedMesh = markerMesh;
        meshRenderer.sharedMaterial = GetMarkerMaterial(markerKind);

        float markerSize = markerKind == MarkerKind.Monster ? monsterMarkerSize : playerMarkerSize;
        markerObject.transform.localScale = new Vector3(markerSize, markerSize, markerSize);
        markerObjects.Add(markerTarget, markerObject);
    }

    private void EnsureMarkerAssets()
    {
        if (markerMesh == null)
            markerMesh = CreateCircleMesh();

        if (localPlayerMarkerMaterial == null)
            localPlayerMarkerMaterial = CreateMarkerMaterial(localPlayerMarkerColor);

        if (remotePlayerMarkerMaterial == null)
            remotePlayerMarkerMaterial = CreateMarkerMaterial(remotePlayerMarkerColor);

        if (monsterMarkerMaterial == null)
            monsterMarkerMaterial = CreateMarkerMaterial(monsterMarkerColor);
    }

    private Material GetMarkerMaterial(MarkerKind markerKind)
    {
        switch (markerKind)
        {
            case MarkerKind.LocalPlayer:
                return localPlayerMarkerMaterial;
            case MarkerKind.RemotePlayer:
                return remotePlayerMarkerMaterial;
            default:
                return monsterMarkerMaterial;
        }
    }

    private Material CreateMarkerMaterial(Color color)
    {
        Material material = new Material(Shader.Find("Sprites/Default"));
        material.color = color;
        material.renderQueue = 4000;
        return material;
    }

    private void UpdateZoneDisplay()
    {
        if (!showZone)
        {
            SetZoneDisplayEnabled(false);
            return;
        }

        if (!TryResolveZoneController())
        {
            SetZoneDisplayEnabled(false);
            return;
        }

        EnsureZoneDisplayObjects();
        DrawZoneLine();
        DrawDirectionLine();
    }

    private bool TryResolveZoneController()
    {
        if (zoneController != null)
            return true;

        zoneController = FindObjectOfType<ZoneController>();
        return zoneController != null;
    }

    private void EnsureZoneDisplayObjects()
    {
        if (zoneLineRenderer == null)
        {
            GameObject zoneObject = new GameObject("Minimap_ZoneLine");
            zoneLineRenderer = zoneObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(zoneLineRenderer, zoneLineColor, zoneLineWidth, true);
        }

        if (directionLineRenderer == null)
        {
            GameObject directionObject = new GameObject("Minimap_ZoneDirection");
            directionLineRenderer = directionObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(directionLineRenderer, outsideZoneDirectionColor, directionLineWidth, false);
        }
    }

    private void ConfigureLineRenderer(LineRenderer lineRenderer, Color color, float width, bool loop)
    {
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = loop;
        lineRenderer.material = CreateMarkerMaterial(color);
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.positionCount = loop ? Mathf.Max(3, zoneLineSegments) : 2;
    }

    private void DrawZoneLine()
    {
        int safeSegments = Mathf.Max(3, zoneLineSegments);
        if (zoneLineRenderer.positionCount != safeSegments)
            zoneLineRenderer.positionCount = safeSegments;

        zoneLineRenderer.enabled = true;
        zoneLineRenderer.startWidth = zoneLineWidth;
        zoneLineRenderer.endWidth = zoneLineWidth;

        Vector3 zoneCenter = zoneController.ZoneCenter;
        float radius = zoneController.CurrentRadius;
        float markerY = transform.position.y - markerDistanceBelowCamera;

        if (radius == lastZoneRadius && zoneCenter == lastZoneCenter && markerY == lastMarkerY)
            return;

        lastZoneRadius = radius;
        lastZoneCenter = zoneCenter;
        lastMarkerY = markerY;

        for (int i = 0; i < safeSegments; i++)
        {
            float angle = (float)i / safeSegments * Mathf.PI * 2f;
            float x = zoneCenter.x + Mathf.Cos(angle) * radius;
            float z = zoneCenter.z + Mathf.Sin(angle) * radius;
            zoneLineRenderer.SetPosition(i, new Vector3(x, markerY, z));
        }
    }

    private void DrawDirectionLine()
    {
        if (target == null)
        {
            directionLineRenderer.enabled = false;
            return;
        }

        Vector3 zoneCenter = zoneController.ZoneCenter;
        Vector3 playerPosition = target.position;
        Vector2 playerXZ = new Vector2(playerPosition.x, playerPosition.z);
        Vector2 zoneCenterXZ = new Vector2(zoneCenter.x, zoneCenter.z);
        Vector2 toZoneCenter = zoneCenterXZ - playerXZ;

        if (toZoneCenter.sqrMagnitude <= zoneController.CurrentRadius * zoneController.CurrentRadius)
        {
            directionLineRenderer.enabled = false;
            return;
        }

        Vector2 direction = toZoneCenter.normalized;
        float markerY = transform.position.y - markerDistanceBelowCamera;
        Vector3 startPosition = new Vector3(playerPosition.x, markerY, playerPosition.z);
        Vector3 endPosition = startPosition + new Vector3(direction.x, 0f, direction.y) * directionLineLength;

        directionLineRenderer.enabled = true;
        directionLineRenderer.startWidth = directionLineWidth;
        directionLineRenderer.endWidth = 0f;
        directionLineRenderer.SetPosition(0, startPosition);
        directionLineRenderer.SetPosition(1, endPosition);
    }

    private void SetZoneDisplayEnabled(bool enabledValue)
    {
        if (zoneLineRenderer != null)
            zoneLineRenderer.enabled = enabledValue;

        if (directionLineRenderer != null)
            directionLineRenderer.enabled = enabledValue;
    }

    private Mesh CreateCircleMesh()
    {
        const int SegmentCount = 24;
        Vector3[] vertices = new Vector3[SegmentCount + 1];
        int[] triangles = new int[SegmentCount * 3];

        vertices[0] = Vector3.zero;
        for (int i = 0; i < SegmentCount; i++)
        {
            float angle = (float)i / SegmentCount * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);
        }

        for (int i = 0; i < SegmentCount; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i == SegmentCount - 1 ? 1 : i + 2;
            triangles[triangleIndex + 2] = i + 1;
        }

        Mesh mesh = new Mesh();
        mesh.name = "Runtime Minimap Circle Marker";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);
        return mesh;
    }

    private void RemoveMissingMarkers()
    {
        List<Transform> missingTargets = null;
        foreach (KeyValuePair<Transform, GameObject> markerPair in markerObjects)
        {
            if (markerPair.Key != null && markerPair.Value != null)
                continue;

            if (missingTargets == null)
                missingTargets = new List<Transform>();

            missingTargets.Add(markerPair.Key);
        }

        if (missingTargets == null)
            return;

        for (int i = 0; i < missingTargets.Count; i++)
        {
            Transform missingTarget = missingTargets[i];
            if (markerObjects.TryGetValue(missingTarget, out GameObject markerObject) && markerObject != null)
                Destroy(markerObject);

            markerObjects.Remove(missingTarget);
        }
    }
}
