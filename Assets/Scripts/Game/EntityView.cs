using System.Collections.Generic;
using UnityEngine;

// EntityView — 实体视图：玩家三角/弹体圆点（按 id 建销）/抛射落点预警圈
// 插值：渲染时间 = 最新服务器 Tick 时间 −100ms，两帧间 Lerp（StateRing 缓冲）
public class EntityView : MonoBehaviour
{
    private readonly Dictionary<uint, GameObject> _projs = new();
    private GameObject[] _players;
    private Sprite _triP0, _triP1;
    private Sprite _dot;
    private Material _dotMat;

    private void Awake()
    {
        _triP0 = MakeTriangleSprite(new Color(0.13f, 0.83f, 0.93f)); // 青（P0）
        _triP1 = MakeTriangleSprite(new Color(0.96f, 0.45f, 0.71f)); // 品红（P1）
        _dotMat = new Material(Shader.Find("Sprites/Default"));
        _dot = MakeDotSprite(); // 自绘圆点（Unity 6 内置 Knob 资源路径已失效）
        _players = new GameObject[2];
        for (int i = 0; i < 2; i++)
        {
            _players[i] = new GameObject($"Player{i}");
            var sr = _players[i].AddComponent<SpriteRenderer>();
            sr.sprite = i == 0 ? _triP0 : _triP1;
            sr.sortingOrder = 2;
            // 16 纹理 PPU=1 = 16 世界 → 缩放到 ~22 世界（略大于玩家半径 15，视觉合适）
            _players[i].transform.localScale = Vector3.one * 1.4f;
        }
    }

    private void Update()
    {
        if (!BattleController.InBattle) return;
        var st = BattleController.LatestState;
        if (st.Players == null) return;

        float renderTime = st.Tick / 30f - 0.1f; // 渲染时间 = 最新 tick 时间 −100ms
        // 玩家：两帧间插值
        for (int slot = 0; slot < 2 && slot < _players.Length; slot++)
            _players[slot].transform.position = InterpPlayer(slot, renderTime);
        // 弹体：按 id 建/销/更新
        SyncProjs(st.Projs, renderTime);
    }

    // ---------- 插值 ----------
    // 在 StateRing 里找渲染时间所在的两帧 a/b 与比例 t（纯函数，供测试）
    public static bool FindInterpFrames(IReadOnlyList<BattleCodec.StateMsg> ring, float renderTime,
        out BattleCodec.StateMsg a, out BattleCodec.StateMsg b, out float t)
    {
        a = default; b = default; t = 0f;
        if (ring == null || ring.Count < 2) return false;
        for (int i = 0; i < ring.Count - 1; i++)
        {
            float ta = ring[i].Tick / 30f;
            float tb = ring[i + 1].Tick / 30f;
            if (ta <= renderTime && renderTime < tb)
            {
                a = ring[i]; b = ring[i + 1];
                t = (renderTime - ta) / (tb - ta);
                return true;
            }
        }
        return false;
    }

    private Vector2 InterpPlayer(int slot, float renderTime)
    {
        var ring = BattleController.StateRingView;
        if (FindInterpFrames(ring, renderTime, out var a, out var b, out var t))
        {
            return Vector2.Lerp(
                new Vector2(a.Players[slot].X, a.Players[slot].Y),
                new Vector2(b.Players[slot].X, b.Players[slot].Y), t);
        }
        var latest = BattleController.LatestState.Players[slot];
        return new Vector2(latest.X, latest.Y);
    }

    // ---------- 弹体同步：按 id 建/更新/销毁 ----------
    private void SyncProjs(BattleCodec.ProjState[] projs, float renderTime)
    {
        var alive = new HashSet<uint>();
        if (projs != null)
        {
            foreach (var p in projs)
            {
                alive.Add(p.Id);
                if (!_projs.TryGetValue(p.Id, out var go))
                {
                    go = new GameObject($"Proj{p.Id}");
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = _dot;
                    sr.material = _dotMat;
                    sr.color = p.Owner == 0 ? Color.cyan : Color.magenta;
                    sr.sortingOrder = 3;
                    // 自绘 16 纹理 PPU=1 = 16 世界；弹体半径 4-6 → 缩到 ~10 世界
                    go.transform.localScale = Vector3.one * 0.6f;
                    var tr = go.AddComponent<TrailRenderer>();
                    tr.time = 0.25f;
                    tr.startWidth = 2f; tr.endWidth = 0f;
                    tr.material = _dotMat;
                    _projs[p.Id] = go;
                    // 抛射落点预警圈
                    if (p.Kind == 1)
                        go.AddComponent<LineRenderer>();
                }
                go.transform.position = new Vector3(p.X, p.Y, 0f);
                // 预警圈更新
                if (p.Kind == 1 && go.TryGetComponent<LineRenderer>(out var lr))
                    DrawRing(lr, new Vector3(p.TargetX, p.TargetY, 0f), 8f, p.Owner == 0 ? Color.cyan : Color.magenta);
            }
        }
        // 销毁已消失的弹体
        var dead = new List<uint>();
        foreach (var kv in _projs)
            if (!alive.Contains(kv.Key)) dead.Add(kv.Key);
        foreach (var id in dead)
        {
            Destroy(_projs[id]);
            _projs.Remove(id);
        }
    }

    private static void DrawRing(LineRenderer lr, Vector3 center, float radius, Color color)
    {
        const int seg = 32;
        lr.positionCount = seg + 1;
        lr.startWidth = 1f; lr.endWidth = 1f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color; lr.endColor = color;
        for (int i = 0; i <= seg; i++)
        {
            float ang = i * Mathf.PI * 2f / seg;
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * radius);
        }
    }

    // ---------- 圆点精灵（代码生成 16×16 实心圆） ----------
    private static Sprite MakeDotSprite()
    {
        const int S = 16;
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
        tex.Apply(false);
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 1f); // PPU=1
    }

    // ---------- 三角精灵（代码生成 16×16 纹理） ----------
    private static Sprite MakeTriangleSprite(Color color)
    {
        const int S = 16;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                // 三角：顶点 (8,15)，底边 y=0 从 (0,0) 到 (15,0)
                bool inside = Mathf.Abs(x - 8) <= y * 8f / 15f;
                px[y * S + x] = inside ? (Color32)color : new Color32(0, 0, 0, 0);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false);
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.08f), 1f); // PPU=1
        // pivot 底部中心：三角指向 +Y，底部贴地（玩家中心）
    }
}
