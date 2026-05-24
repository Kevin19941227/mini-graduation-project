using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public static class HUDCreator
{
    [MenuItem("Tools/建立玩家HUD")]
    public static void CreateHUD()
    {
        var canvasGo = new GameObject("PlayerHUD_Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var hud = canvasGo.AddComponent<PlayerHUD>();

        // ── 血量條（左下角）──
        var hpContainer = CreateRect(canvasGo.transform, "HP_Container",
            anchorMin: new Vector2(0, 0), anchorMax: new Vector2(0, 0),
            pivot: new Vector2(0, 0),
            pos: new Vector2(30, 30), size: new Vector2(350, 35));
        AddImage(hpContainer, new Color(0, 0, 0, 0.6f));

        var hpBg = CreateRect(hpContainer.transform, "HP_BG",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        AddImage(hpBg, new Color(0.2f, 0.05f, 0.05f, 1f));

        var hpFillGo = CreateRect(hpContainer.transform, "HP_Fill",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var hpImg = hpFillGo.AddComponent<Image>();
        hpImg.color = Color.green;
        hpImg.type = Image.Type.Filled;
        hpImg.fillMethod = Image.FillMethod.Horizontal;
        hpImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        hud.hpFillImage = hpImg;

        AddOutlineText(hpContainer.transform, "HP_Label", "HP",
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(5, 0), new Vector2(60, 35), 20, TextAnchor.MiddleLeft);

        // ── 蓄力條（下方中央）──
        var chargeContainer = CreateRect(canvasGo.transform, "Charge_Container",
            anchorMin: new Vector2(0.5f, 0), anchorMax: new Vector2(0.5f, 0),
            pivot: new Vector2(0.5f, 0),
            pos: new Vector2(0, 30), size: new Vector2(300, 22));
        AddImage(chargeContainer, new Color(0, 0, 0, 0.6f));

        var chargeBgGo = CreateRect(chargeContainer.transform, "Charge_BG",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        AddImage(chargeBgGo, new Color(0.15f, 0.1f, 0f, 1f));

        var chargeFillGo = CreateRect(chargeContainer.transform, "Charge_Fill",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var chargeImg = chargeFillGo.AddComponent<Image>();
        chargeImg.color = new Color(1f, 0.85f, 0f);
        chargeImg.type = Image.Type.Filled;
        chargeImg.fillMethod = Image.FillMethod.Horizontal;
        chargeImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        hud.chargeFillImage = chargeImg;

        var chargeGroup = chargeContainer.AddComponent<CanvasGroup>();
        chargeGroup.alpha = 0f;
        chargeGroup.blocksRaycasts = false;
        hud.chargeGroup = chargeGroup;

        AddOutlineText(chargeContainer.transform, "Charge_Label", "蓄力",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 22), 14, TextAnchor.MiddleCenter);

        // ── 遊戲結束面板 ──
        var gameOverGo = CreateRect(canvasGo.transform, "GameOver_Panel",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        AddImage(gameOverGo, new Color(0f, 0f, 0f, 0.75f));

        var titleText = CreateRect(gameOverGo.transform, "GameOver_Text",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(500, 120));
        var t = titleText.AddComponent<Text>();
        t.text = "遊戲結束";
        t.fontSize = 72;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var hintText = CreateRect(gameOverGo.transform, "Hint_Text",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(400, 60));
        var h = hintText.AddComponent<Text>();
        h.text = "你已陣亡";
        h.fontSize = 36;
        h.alignment = TextAnchor.MiddleCenter;
        h.color = new Color(1f, 0.4f, 0.4f);
        h.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        gameOverGo.SetActive(false);
        hud.gameOverPanel = gameOverGo;

        Undo.RegisterCreatedObjectUndo(canvasGo, "Create PlayerHUD");
        Selection.activeGameObject = canvasGo;
        Debug.Log("PlayerHUD 建立完成！請把 PlayerHUD_Canvas 上的 PlayerHUD 組件拖到 PlayCol 的 hud 欄位。");
    }

    // ── 工具方法 ──

    static GameObject CreateRect(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return go;
    }

    static void AddImage(GameObject go, Color color)
    {
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    static void AddOutlineText(Transform parent, string name, string content,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size,
        int fontSize, TextAnchor alignment)
    {
        var go = CreateRect(parent, name, anchorMin, anchorMax,
            new Vector2(0.5f, 0.5f), pos, size);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = fontSize;
        t.alignment = alignment;
        t.color = Color.white;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1, -1);
    }
}
