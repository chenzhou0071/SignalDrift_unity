using UnityEditor;
using UnityEngine;

// BattleSceneBuilder — 一键生成战斗静态层：墙/反射墙/黑洞/双塔（坐标照 configs/map_01.json）
// 用法：菜单 Tools/Battle/生成静态层（在 Battle 场景中执行）
public static class BattleSceneBuilder
{
    [MenuItem("Tools/Battle/生成静态层")]
    public static void BuildStaticLayer()
    {
        var root = new GameObject("StaticLayer");

        // ---- 普通墙 #334155 ----
        BuildWall(root.transform, new Vector2(400, 130), new Vector2(130, 22), false);
        BuildWall(root.transform, new Vector2(750, 568), new Vector2(130, 22), false);
        BuildWall(root.transform, new Vector2(300, 300), new Vector2(22, 120), false);
        BuildWall(root.transform, new Vector2(958, 300), new Vector2(22, 120), false);

        // ---- 反射墙（#FBBF24 描边） ----
        BuildWall(root.transform, new Vector2(560, 80), new Vector2(90, 16), true);
        BuildWall(root.transform, new Vector2(630, 624), new Vector2(90, 16), true);

        // ---- 黑洞（黑圆 + 紫环） ----
        BuildBlackhole(root.transform, new Vector2(640, 360), 120f);

        // ---- 双塔（青/品红圆环，safe_radius=60） ----
        BuildTowerRing(root.transform, new Vector2(80, 360), 60f, Color.cyan);
        BuildTowerRing(root.transform, new Vector2(1200, 360), 60f, Color.magenta);

        Debug.Log("[Battle] static layer generated");
    }

    private static void BuildWall(Transform parent, Vector2 center, Vector2 size, bool reflect)
    {
        var go = new GameObject(reflect ? "ReflectWall" : "Wall");
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(center.x, center.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeSolidSprite();
        sr.color = new Color(0.39f, 0.45f, 0.52f); // #64748B（slate-500，明显亮于背景）
        sr.sortingOrder = 1;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        // 描边：普通墙亮灰，反射墙白色（世界坐标画，不受墙 scale 放大影响）
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        var mat = new Material(Shader.Find("Sprites/Default"));
        lr.material = mat;
        lr.startColor = reflect ? Color.white : new Color(0.80f, 0.85f, 0.92f);
        lr.endColor = lr.startColor;
        lr.startWidth = reflect ? 4f : 2f; // 反射墙白框加粗（明显）
        lr.endWidth = lr.startWidth;
        lr.positionCount = 5;
        var hw = size.x / 2f; var hh = size.y / 2f;
        var cx = center.x; var cy = center.y;
        lr.SetPosition(0, new Vector3(cx - hw, cy - hh, 0));
        lr.SetPosition(1, new Vector3(cx + hw, cy - hh, 0));
        lr.SetPosition(2, new Vector3(cx + hw, cy + hh, 0));
        lr.SetPosition(3, new Vector3(cx - hw, cy + hh, 0));
        lr.SetPosition(4, new Vector3(cx - hw, cy - hh, 0));
    }

    private static void BuildBlackhole(Transform parent, Vector2 center, float radius)
    {
        // 黑圆盘（禁涂区视觉）
        var go = new GameObject("Blackhole");
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(center.x, center.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite();
        sr.color = Color.black;
        sr.sortingOrder = 0;
        // 64 纹理 PPU=1 = 64 世界单位 → 直径 2r（240）→ scale = 2r/64
        go.transform.localScale = new Vector3(radius * 2f / 64f, radius * 2f / 64f, 1f);
        // 紫环
        var ring = new GameObject("Ring");
        ring.transform.SetParent(parent);
        ring.transform.position = new Vector3(center.x, center.y, 0f);
        var lr = ring.AddComponent<LineRenderer>();
        lr.useWorldSpace = false; // SetPosition 用相对黑洞中心的局部坐标
        var mat = new Material(Shader.Find("Sprites/Default"));
        lr.material = mat;
        lr.startColor = new Color(0.7f, 0.3f, 0.9f);
        lr.endColor = lr.startColor;
        lr.startWidth = 3f; lr.endWidth = 3f;
        const int seg = 48;
        lr.positionCount = seg + 1;
        for (int i = 0; i <= seg; i++)
        {
            float ang = i * Mathf.PI * 2f / seg;
            lr.SetPosition(i, new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * radius);
        }
    }

    private static void BuildTowerRing(Transform parent, Vector2 center, float radius, Color color)
    {
        var go = new GameObject(color == Color.cyan ? "TowerP0" : "TowerP1");
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(center.x, center.y, 0f);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false; // SetPosition 用相对塔中心的局部坐标
        var mat = new Material(Shader.Find("Sprites/Default"));
        lr.material = mat;
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = 4f; lr.endWidth = 4f;
        const int seg = 48;
        lr.positionCount = seg + 1;
        for (int i = 0; i <= seg; i++)
        {
            float ang = i * Mathf.PI * 2f / seg;
            lr.SetPosition(i, new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * radius);
        }
    }

    private static Sprite _solid;
    private static Sprite MakeSolidSprite()
    {
        if (_solid != null) return _solid;
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _solid = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f); // PPU=1：scale=世界尺寸
        return _solid;
    }

    private static Sprite _circle;
    private static Sprite MakeCircleSprite()
    {
        if (_circle != null) return _circle;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float dx = (x - S / 2f) / (S / 2f);
                float dy = (y - S / 2f) / (S / 2f);
                px[y * S + x] = dx * dx + dy * dy <= 1f ? Color.white : new Color32(0, 0, 0, 0);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        _circle = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 1f); // PPU=1：scale=世界尺寸
        return _circle;
    }
}
