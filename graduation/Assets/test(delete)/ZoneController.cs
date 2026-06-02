using UnityEngine;
using Mirror;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ZoneController : NetworkBehaviour
{
    [Header("縮圈設定")]
    public float initialRadius = 100f;       // 初始半徑
    public float finalRadius = 5f;           // 最終半徑
    public float shrinkDelay = 5f;          // 幾秒後開始縮
    public float shrinkDuration = 5f;       // 縮完需要幾秒
    public float damageInterval = 1f;        // 幾秒扣一次血
    public int damagePerTick = 1;            // 每次扣幾滴

    [Header("視覺設定")]
    public int lineSegments = 64;            // 圓圈的線段數，越多越圓
    public Color zoneColor = Color.blue;
    public float lineWidth = 0.5f;
    [SerializeField, Min(0f)] private float zoneLineHeight = 500f;
    [SerializeField] private Color outsideZoneColor = new Color(0f, 0.25f, 1f, 0.35f);
    [SerializeField, Min(0f)] private float outsideZoneHeightOffset = 1f;
    [SerializeField, Min(1f)] private float fallbackOutsideRadiusMultiplier = 3f;
    [SerializeField] private string gameplaySceneName = "gamescene";
    [SerializeField, Min(1f)] private float zoneWallHeight = 800f;
    [SerializeField] private float zoneWallBottomOffset = 0f;

    [Header("Poison Screen")]
    [SerializeField] private GameObject poisonScreenPanel;
    [SerializeField] private Color poisonScreenColor = new Color(0f, 0.25f, 1f, 0.28f);
    [SerializeField] private int poisonScreenSortingOrder = 200;

    [SyncVar]
    private float _currentRadius;

    [SyncVar]
    private Vector3 _zoneCenter;

    private LineRenderer _lineRenderer;
    private MeshFilter _outsideZoneMeshFilter;
    private MeshRenderer _outsideZoneMeshRenderer;
    private Mesh _outsideZoneMesh;
    private GameObject _zoneWallObject;
    private MeshFilter _zoneWallMeshFilter;
    private MeshRenderer _zoneWallMeshRenderer;
    private Mesh _zoneWallMesh;
    private Vector3[] _outsideZoneVertices;
    private int[] _outsideZoneTriangles;
    private Vector3[] _zoneWallVertices;
    private int[] _zoneWallTriangles;
    private int _cachedOutsideZoneSegments;
    private int _cachedZoneWallSegments;
    private MapGenerator _mapGenerator;
    private float _mapGeneratorLookupTimer;
    private float _damageTimer;
    private float _lastVisualRadius = -1f;
    private float _lastOuterRadius = -1f;
    private Vector3 _lastVisualCenter = new Vector3(float.NaN, float.NaN, float.NaN);
    private float _lastWallRadius = -1f;
    private Vector3 _lastWallCenter = new Vector3(float.NaN, float.NaN, float.NaN);
    private float _lastWallHeight = -1f;
    private bool _isVisualEnabled;
    private Canvas _runtimePoisonCanvas;
    private Image _poisonScreenImage;
    private const float MAP_GENERATOR_LOOKUP_INTERVAL = 1f;
    private const float VISUAL_UPDATE_EPSILON = 0.01f;

    void Awake()
    {
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.loop = true;
        _lineRenderer.positionCount = lineSegments;
        _lineRenderer.startWidth = lineWidth;
        _lineRenderer.endWidth = lineWidth;
        _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _lineRenderer.startColor = zoneColor;
        _lineRenderer.endColor = zoneColor;

        _outsideZoneMesh = new Mesh();
        _outsideZoneMesh.name = "Outside Zone Overlay";
        _outsideZoneMesh.MarkDynamic();
        _outsideZoneMeshFilter = gameObject.AddComponent<MeshFilter>();
        _outsideZoneMeshRenderer = gameObject.AddComponent<MeshRenderer>();
        _outsideZoneMeshFilter.sharedMesh = _outsideZoneMesh;
        _outsideZoneMeshRenderer.material = CreateOutsideZoneMaterial();

        _zoneWallObject = new GameObject("Zone Cylinder Wall");
        _zoneWallObject.transform.SetParent(transform, false);
        _zoneWallMesh = new Mesh();
        _zoneWallMesh.name = "Zone Cylinder Wall";
        _zoneWallMesh.MarkDynamic();
        _zoneWallMeshFilter = _zoneWallObject.AddComponent<MeshFilter>();
        _zoneWallMeshRenderer = _zoneWallObject.AddComponent<MeshRenderer>();
        _zoneWallMeshFilter.sharedMesh = _zoneWallMesh;
        _zoneWallMeshRenderer.material = CreateOutsideZoneMaterial();

        SetZoneVisualEnabled(false);
        SetPoisonScreenVisible(false);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!IsGameplayScene())
        {
            SetZoneVisualEnabled(false);
            SetPoisonScreenVisible(false);
            return;
        }

        _currentRadius = initialRadius;
        _zoneCenter = transform.position;

        if (MapGenerator.IsNavMeshReady)
            InitZoneCenter();
        else
            MapGenerator.OnNavMeshReady += InitZoneCenter;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsGameplayScene())
        {
            SetZoneVisualEnabled(false);
            SetPoisonScreenVisible(false);
            return;
        }

        TryResolveMapGenerator(true);
    }

    private void OnDestroy()
    {
        MapGenerator.OnNavMeshReady -= InitZoneCenter;

        if (_runtimePoisonCanvas != null)
            Destroy(_runtimePoisonCanvas.gameObject);
    }

    private void InitZoneCenter()
    {
        MapGenerator.OnNavMeshReady -= InitZoneCenter;

        if (TryResolveMapGenerator(true))
        {
            _zoneCenter = _mapGenerator.GetRandomTileCenter();
        }

        StartCoroutine(ZoneRoutine());
    }

    void Update()
    {
        if (!IsGameplayScene() || !MapGenerator.IsNavMeshReady)
        {
            SetZoneVisualEnabled(false);
            SetPoisonScreenVisible(false);
            return;
        }

        SetZoneVisualEnabled(true);

        // 所有 Client 都畫圈
        DrawZone();
        DrawOutsideZone();
        DrawZoneWall();
        UpdatePoisonScreen();

        // 只有 Server 處理傷害
        if (!isServer) return;

        _damageTimer += Time.deltaTime;
        if (_damageTimer >= damageInterval)
        {
            _damageTimer = 0f;
            DamagePlayersOutside();
        }
    }


    private IEnumerator ZoneRoutine()
    {
        // 等待倒數
        yield return new WaitForSeconds(shrinkDelay);

        // 開始縮圈
        float elapsed = 0f;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkDuration;
            _currentRadius = Mathf.Lerp(initialRadius, finalRadius, t);
            yield return null;
        }

        _currentRadius = finalRadius;
    }

    [Server]
    private void DamagePlayersOutside()
    {
        PlayCol[] players = FindObjectsOfType<PlayCol>();
        foreach (var player in players)
        {
            float dist = Vector3.Distance(
                new Vector3(player.transform.position.x, 0, player.transform.position.z),
                new Vector3(_zoneCenter.x, 0, _zoneCenter.z)
            );

            if (dist > _currentRadius)
                player.TakeZoneDamage(damagePerTick);
        }
    }

    private void DrawZone()
    {
        for (int i = 0; i < lineSegments; i++)
        {
            float angle = (float)i / lineSegments * Mathf.PI * 2f;
            float x = _zoneCenter.x + Mathf.Cos(angle) * _currentRadius;
            float z = _zoneCenter.z + Mathf.Sin(angle) * _currentRadius;
            float lineY = _zoneCenter.y + Mathf.Max(zoneLineHeight, zoneWallBottomOffset + zoneWallHeight);
            _lineRenderer.SetPosition(i, new Vector3(x, lineY, z));
        }
    }

    private void DrawOutsideZone()
    {
        if (_outsideZoneMesh == null) return;

        EnsureOutsideZoneMeshBuffers();

        float outerRadius = GetOutsideZoneRadius();
        if (!ShouldUpdateOutsideZoneVisual(outerRadius))
            return;

        float y = _zoneCenter.y + outsideZoneHeightOffset;

        for (int i = 0; i < lineSegments; i++)
        {
            float angle = (float)i / lineSegments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            int innerIndex = i * 2;
            int outerIndex = innerIndex + 1;

            _outsideZoneVertices[innerIndex] = new Vector3(
                _zoneCenter.x + cos * _currentRadius,
                y,
                _zoneCenter.z + sin * _currentRadius);
            _outsideZoneVertices[outerIndex] = new Vector3(
                _zoneCenter.x + cos * outerRadius,
                y,
                _zoneCenter.z + sin * outerRadius);
        }

        _outsideZoneMesh.Clear();
        _outsideZoneMesh.vertices = _outsideZoneVertices;
        _outsideZoneMesh.triangles = _outsideZoneTriangles;
        _outsideZoneMesh.RecalculateBounds();

        _lastVisualRadius = _currentRadius;
        _lastOuterRadius = outerRadius;
        _lastVisualCenter = _zoneCenter;
    }

    private void DrawZoneWall()
    {
        if (_zoneWallMesh == null) return;

        EnsureZoneWallMeshBuffers();

        if (!ShouldUpdateZoneWallVisual())
            return;

        float bottomY = _zoneCenter.y + zoneWallBottomOffset;
        float topY = bottomY + zoneWallHeight;

        for (int i = 0; i < lineSegments; i++)
        {
            float angle = (float)i / lineSegments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            int bottomIndex = i * 2;
            int topIndex = bottomIndex + 1;
            float x = _zoneCenter.x + cos * _currentRadius;
            float z = _zoneCenter.z + sin * _currentRadius;

            _zoneWallVertices[bottomIndex] = new Vector3(x, bottomY, z);
            _zoneWallVertices[topIndex] = new Vector3(x, topY, z);
        }

        _zoneWallMesh.Clear();
        _zoneWallMesh.vertices = _zoneWallVertices;
        _zoneWallMesh.triangles = _zoneWallTriangles;
        _zoneWallMesh.RecalculateBounds();

        _lastWallRadius = _currentRadius;
        _lastWallCenter = _zoneCenter;
        _lastWallHeight = zoneWallHeight;
    }

    private void EnsureOutsideZoneMeshBuffers()
    {
        if (_cachedOutsideZoneSegments == lineSegments
            && _outsideZoneVertices != null
            && _outsideZoneTriangles != null)
        {
            return;
        }

        _cachedOutsideZoneSegments = lineSegments;
        _outsideZoneVertices = new Vector3[lineSegments * 2];
        _outsideZoneTriangles = new int[lineSegments * 6];
        _lastVisualRadius = -1f;

        for (int i = 0; i < lineSegments; i++)
        {
            int innerIndex = i * 2;
            int outerIndex = innerIndex + 1;
            int nextInnerIndex = ((i + 1) % lineSegments) * 2;
            int nextOuterIndex = nextInnerIndex + 1;
            int triangleIndex = i * 6;

            _outsideZoneTriangles[triangleIndex] = innerIndex;
            _outsideZoneTriangles[triangleIndex + 1] = nextOuterIndex;
            _outsideZoneTriangles[triangleIndex + 2] = outerIndex;
            _outsideZoneTriangles[triangleIndex + 3] = innerIndex;
            _outsideZoneTriangles[triangleIndex + 4] = nextInnerIndex;
            _outsideZoneTriangles[triangleIndex + 5] = nextOuterIndex;
        }
    }

    private void EnsureZoneWallMeshBuffers()
    {
        if (_cachedZoneWallSegments == lineSegments
            && _zoneWallVertices != null
            && _zoneWallTriangles != null)
        {
            return;
        }

        _cachedZoneWallSegments = lineSegments;
        _zoneWallVertices = new Vector3[lineSegments * 2];
        _zoneWallTriangles = new int[lineSegments * 12];
        _lastWallRadius = -1f;

        for (int i = 0; i < lineSegments; i++)
        {
            int bottomIndex = i * 2;
            int topIndex = bottomIndex + 1;
            int nextBottomIndex = ((i + 1) % lineSegments) * 2;
            int nextTopIndex = nextBottomIndex + 1;
            int triangleIndex = i * 12;

            _zoneWallTriangles[triangleIndex] = bottomIndex;
            _zoneWallTriangles[triangleIndex + 1] = topIndex;
            _zoneWallTriangles[triangleIndex + 2] = nextTopIndex;
            _zoneWallTriangles[triangleIndex + 3] = bottomIndex;
            _zoneWallTriangles[triangleIndex + 4] = nextTopIndex;
            _zoneWallTriangles[triangleIndex + 5] = nextBottomIndex;

            _zoneWallTriangles[triangleIndex + 6] = bottomIndex;
            _zoneWallTriangles[triangleIndex + 7] = nextTopIndex;
            _zoneWallTriangles[triangleIndex + 8] = topIndex;
            _zoneWallTriangles[triangleIndex + 9] = bottomIndex;
            _zoneWallTriangles[triangleIndex + 10] = nextBottomIndex;
            _zoneWallTriangles[triangleIndex + 11] = nextTopIndex;
        }
    }

    private bool ShouldUpdateZoneWallVisual()
    {
        if (_zoneWallVertices == null || _zoneWallTriangles == null)
            return true;

        if (Mathf.Abs(_lastWallRadius - _currentRadius) > VISUAL_UPDATE_EPSILON)
            return true;

        if (Mathf.Abs(_lastWallHeight - zoneWallHeight) > VISUAL_UPDATE_EPSILON)
            return true;

        return (_lastWallCenter - _zoneCenter).sqrMagnitude > VISUAL_UPDATE_EPSILON * VISUAL_UPDATE_EPSILON;
    }

    private bool ShouldUpdateOutsideZoneVisual(float outerRadius)
    {
        if (_outsideZoneVertices == null || _outsideZoneTriangles == null)
            return true;

        if (Mathf.Abs(_lastVisualRadius - _currentRadius) > VISUAL_UPDATE_EPSILON)
            return true;

        if (Mathf.Abs(_lastOuterRadius - outerRadius) > VISUAL_UPDATE_EPSILON)
            return true;

        return (_lastVisualCenter - _zoneCenter).sqrMagnitude > VISUAL_UPDATE_EPSILON * VISUAL_UPDATE_EPSILON;
    }

    private float GetOutsideZoneRadius()
    {
        if (!TryResolveMapGenerator())
            return Mathf.Max(initialRadius, _currentRadius) * fallbackOutsideRadiusMultiplier;

        Vector3 mapCenter = _mapGenerator.GetMapCenter();
        float halfWidth = _mapGenerator.gridWidth * _mapGenerator.tileSize * 0.5f;
        float halfHeight = _mapGenerator.gridHeight * _mapGenerator.tileSize * 0.5f;
        float maxX = Mathf.Abs(_zoneCenter.x - mapCenter.x) + halfWidth;
        float maxZ = Mathf.Abs(_zoneCenter.z - mapCenter.z) + halfHeight;
        float padding = _mapGenerator.tileSize * 0.1f;

        return Mathf.Max(_currentRadius, Mathf.Sqrt(maxX * maxX + maxZ * maxZ) + padding);
    }

    private bool TryResolveMapGenerator(bool forceLookup = false)
    {
        if (_mapGenerator != null)
            return true;

        if (!forceLookup)
        {
            _mapGeneratorLookupTimer -= Time.deltaTime;
            if (_mapGeneratorLookupTimer > 0f)
                return false;

            _mapGeneratorLookupTimer = MAP_GENERATOR_LOOKUP_INTERVAL;
        }

        _mapGenerator = FindObjectOfType<MapGenerator>();
        return _mapGenerator != null;
    }

    private Material CreateOutsideZoneMaterial()
    {
        Material material = new Material(Shader.Find("Sprites/Default"));
        material.color = outsideZoneColor;
        material.renderQueue = 3000;
        return material;
    }

    private bool IsGameplayScene()
    {
        if (string.IsNullOrEmpty(gameplaySceneName))
            return true;

        string activeSceneName = SceneManager.GetActiveScene().name;
        return activeSceneName == gameplaySceneName
            || activeSceneName.StartsWith(gameplaySceneName + " ");
    }

    private void SetZoneVisualEnabled(bool enabledValue)
    {
        if (_isVisualEnabled == enabledValue)
            return;

        _isVisualEnabled = enabledValue;

        if (_lineRenderer != null)
            _lineRenderer.enabled = enabledValue;

        if (_outsideZoneMeshRenderer != null)
            _outsideZoneMeshRenderer.enabled = enabledValue;

        if (_zoneWallMeshRenderer != null)
            _zoneWallMeshRenderer.enabled = enabledValue;
    }

    private void UpdatePoisonScreen()
    {
        if (!NetworkClient.active || NetworkClient.localPlayer == null)
        {
            SetPoisonScreenVisible(false);
            return;
        }

        Transform localPlayerTransform = NetworkClient.localPlayer.transform;
        Vector3 playerPosition = localPlayerTransform.position;
        Vector2 playerXZ = new Vector2(playerPosition.x, playerPosition.z);
        Vector2 zoneCenterXZ = new Vector2(_zoneCenter.x, _zoneCenter.z);
        bool isOutsideZone = Vector2.Distance(playerXZ, zoneCenterXZ) > _currentRadius;

        SetPoisonScreenVisible(isOutsideZone);
    }

    private void SetPoisonScreenVisible(bool visible)
    {
        if (!visible && poisonScreenPanel == null)
            return;

        EnsurePoisonScreenPanel();

        if (poisonScreenPanel != null && poisonScreenPanel.activeSelf != visible)
            poisonScreenPanel.SetActive(visible);
    }

    private void EnsurePoisonScreenPanel()
    {
        if (poisonScreenPanel != null)
        {
            ConfigurePoisonScreenPanel(poisonScreenPanel);
            return;
        }

        GameObject canvasObject = new GameObject("Runtime_PoisonScreenCanvas");
        _runtimePoisonCanvas = canvasObject.AddComponent<Canvas>();
        _runtimePoisonCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _runtimePoisonCanvas.sortingOrder = poisonScreenSortingOrder;
        canvasObject.AddComponent<CanvasScaler>();

        poisonScreenPanel = new GameObject("PoisonScreen_Panel");
        poisonScreenPanel.transform.SetParent(canvasObject.transform, false);
        _poisonScreenImage = poisonScreenPanel.AddComponent<Image>();
        _poisonScreenImage.color = poisonScreenColor;
        ConfigurePoisonScreenPanel(poisonScreenPanel);
    }

    private void ConfigurePoisonScreenPanel(GameObject panelObject)
    {
        if (panelObject == null) return;

        RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        Image panelImage = panelObject.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = poisonScreenColor;
            panelImage.raycastTarget = false;
        }

        CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = panelObject.AddComponent<CanvasGroup>();

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }
}
