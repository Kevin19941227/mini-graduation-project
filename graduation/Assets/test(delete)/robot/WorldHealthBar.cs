using UnityEngine;
using UnityEngine.UI;

public class WorldHealthBar : MonoBehaviour
{
    private const string RuntimeRootName = "Runtime_HP_Bar";
    private const string BackgroundName = "Runtime_HP_Background";
    private const string FillName = "Runtime_HP_Fill";
    private const string HpTextName = "HPValue_Text";
    private const float TargetCanvasScale = 0.02f;
    private const float MinScale = 0.0001f;

    private static readonly Vector2 BarSize = new Vector2(170f, 24f);
    private static readonly Vector2 BarWorldOffset = new Vector2(0f, -0.45f);
    private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.65f);

    [SerializeField] private Image hpFillImage;
    [SerializeField] private Text hpValueText;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, 0f);

    private Transform _target;
    private Camera _mainCamera;
    private RectTransform _barRoot;
    private Image _hpBackgroundImage;
    private int _lastCurrentHp = 100;
    private int _lastMaxHp = 100;

    private void Awake()
    {
        ConfigureLayout();
        ApplyHpVisuals(_lastCurrentHp, _lastMaxHp);
    }

    public void Init(Transform target)
    {
        _target = target;
        _mainCamera = Camera.main;
        ConfigureLayout();
        ApplyHpVisuals(_lastCurrentHp, _lastMaxHp);
    }

    public void UpdateHP(int current, int max)
    {
        ConfigureLayout();

        int safeMax = Mathf.Max(1, max);
        int safeCurrent = Mathf.Clamp(current, 0, safeMax);
        _lastCurrentHp = safeCurrent;
        _lastMaxHp = safeMax;

        ApplyHpVisuals(safeCurrent, safeMax);
    }

    private void ApplyHpVisuals(int current, int max)
    {
        int safeMax = Mathf.Max(1, max);
        int safeCurrent = Mathf.Clamp(current, 0, safeMax);
        float ratio = Mathf.Clamp01((float)safeCurrent / safeMax);

        if (hpFillImage != null)
        {
            hpFillImage.type = Image.Type.Filled;
            hpFillImage.fillMethod = Image.FillMethod.Horizontal;
            hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            hpFillImage.fillAmount = ratio;
            hpFillImage.color = Color.Lerp(Color.red, Color.green, ratio);
        }

        if (hpValueText != null)
            hpValueText.text = $"{safeCurrent} / {safeMax}";
    }

    private void LateUpdate()
    {
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        if (_target == null || _mainCamera == null) return;

        transform.position = _target.position + offset;
        transform.forward = _mainCamera.transform.forward;
    }

    private void ConfigureLayout()
    {
        HidePrefabImages();

        _barRoot = EnsureRectTransform(transform, RuntimeRootName);
        ConfigureRuntimeRoot(_barRoot);

        _hpBackgroundImage = EnsureImage(_barRoot, BackgroundName);
        ConfigureStretchRect(_hpBackgroundImage.rectTransform);
        _hpBackgroundImage.color = BackgroundColor;
        _hpBackgroundImage.raycastTarget = false;

        hpFillImage = EnsureImage(_barRoot, FillName);
        ConfigureStretchRect(hpFillImage.rectTransform);
        hpFillImage.type = Image.Type.Filled;
        hpFillImage.fillMethod = Image.FillMethod.Horizontal;
        hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        hpFillImage.raycastTarget = false;

        hpValueText = EnsureHpValueText(_barRoot);

        _hpBackgroundImage.transform.SetAsFirstSibling();
        hpFillImage.transform.SetSiblingIndex(1);
        hpValueText.transform.SetAsLastSibling();
    }

    private void HidePrefabImages()
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (IsRuntimeTransform(image.transform))
                continue;

            image.enabled = false;
        }
    }

    private bool IsRuntimeTransform(Transform target)
    {
        while (target != null && target != transform)
        {
            if (target.name == RuntimeRootName)
                return true;

            target = target.parent;
        }

        return false;
    }

    private RectTransform EnsureRectTransform(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        GameObject childObject = existing != null ? existing.gameObject : new GameObject(objectName);
        childObject.transform.SetParent(parent, false);
        childObject.layer = gameObject.layer;

        RectTransform rectTransform = childObject.GetComponent<RectTransform>();
        if (rectTransform == null)
            rectTransform = childObject.AddComponent<RectTransform>();

        return rectTransform;
    }

    private Image EnsureImage(RectTransform parent, string objectName)
    {
        RectTransform rectTransform = EnsureRectTransform(parent, objectName);
        Image image = rectTransform.GetComponent<Image>();
        if (image == null)
            image = rectTransform.gameObject.AddComponent<Image>();

        image.enabled = true;
        return image;
    }

    private Text EnsureHpValueText(RectTransform parentRect)
    {
        RectTransform textRect = EnsureRectTransform(parentRect, HpTextName);

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;
        textRect.localScale = Vector3.one;

        Text text = textRect.GetComponent<Text>();
        if (text == null)
            text = textRect.gameObject.AddComponent<Text>();

        text.text = "100 / 100";
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 16;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 8;
        text.resizeTextMaxSize = 18;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Outline outline = textRect.GetComponent<Outline>();
        if (outline == null)
            outline = textRect.gameObject.AddComponent<Outline>();

        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, -1f);

        return text;
    }

    private void ConfigureRuntimeRoot(RectTransform rectTransform)
    {
        Vector3 scale = transform.lossyScale;
        float safeX = Mathf.Max(Mathf.Abs(scale.x), MinScale);
        float safeY = Mathf.Max(Mathf.Abs(scale.y), MinScale);
        float safeZ = Mathf.Max(Mathf.Abs(scale.z), MinScale);

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(BarWorldOffset.x / safeX, BarWorldOffset.y / safeY);
        rectTransform.sizeDelta = BarSize;
        rectTransform.localScale = new Vector3(TargetCanvasScale / safeX, TargetCanvasScale / safeY, TargetCanvasScale / safeZ);
    }

    private void ConfigureStretchRect(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }
}
