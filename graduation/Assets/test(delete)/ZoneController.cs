using UnityEngine;
using Mirror;
using System.Collections;

public class ZoneController : NetworkBehaviour
{
    [Header("縮圈設定")]
    public float initialRadius = 100f;       // 初始半徑
    public float finalRadius = 5f;           // 最終半徑
    public float shrinkDelay = 30f;          // 幾秒後開始縮
    public float shrinkDuration = 60f;       // 縮完需要幾秒
    public float damageInterval = 1f;        // 幾秒扣一次血
    public int damagePerTick = 1;            // 每次扣幾滴

    [Header("視覺設定")]
    public int lineSegments = 64;            // 圓圈的線段數，越多越圓
    public Color zoneColor = Color.blue;
    public float lineWidth = 0.5f;

    [SyncVar]
    private float _currentRadius;

    [SyncVar]
    private Vector3 _zoneCenter;

    private LineRenderer _lineRenderer;
    private float _damageTimer;

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
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentRadius = initialRadius;
        _zoneCenter = transform.position;
        StartCoroutine(ZoneRoutine());
    }

    void Update()
    {
        // 所有 Client 都畫圈
        DrawZone();

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
            _lineRenderer.SetPosition(i, new Vector3(x, transform.position.y, z));
        }
    }
}