using UnityEngine;

// PaintRenderer — 涂色层：128×72 纹理贴 1280×720 Sprite
// 快照到达全量 SetPixels32；每个 State 包只对 dirty 格 SetPixel，帧末 Apply(false)
public class PaintRenderer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer target; // 场景中的涂色层 Sprite（1280×720）

    private const int W = 128;
    private const int H = 72;
    private static readonly Color32 ColorNone = new(0, 0, 0, 0);          // 透明
    private static readonly Color32 ColorP0 = new(34, 211, 238, 140);     // #22D3EE 55% alpha（青）
    private static readonly Color32 ColorP1 = new(244, 114, 182, 140);    // #F472B6 55% alpha（品红）

    private Texture2D _tex;
    private Color32[] _pixels;
    private uint _appliedSnapshotTick; // 已应用的全量快照 tick
    private uint _appliedStateTick;    // 已应用的增量 tick（只消费更新的 dirty）

    // 格子颜色 → 渲染色（静态，供测试）
    public static Color32 CellToColor(byte c)
    {
        switch (c)
        {
            case 1: return ColorP0;
            case 2: return ColorP1;
            default: return ColorNone;
        }
    }

    private void Awake()
    {
        // 兜底：Target 槽没拖 / 物体缺 SpriteRenderer 时自动补齐
        if (target == null) target = GetComponent<SpriteRenderer>();
        if (target == null) target = gameObject.AddComponent<SpriteRenderer>();
        _tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point, // 像素风：格子边缘锐利
        };
        _pixels = new Color32[W * H];
        for (int i = 0; i < _pixels.Length; i++) _pixels[i] = ColorNone;
        _tex.SetPixels32(_pixels);
        _tex.Apply(false);
        if (target != null)
        {
            // PPU=1 → 128×72 世界单位；scale×10 → 1280×720；位置对齐世界中心（0..1280 / 0..720）
            target.sprite = Sprite.Create(_tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 1f);
            target.transform.position = new Vector3(640f, 360f, 0f);
            target.transform.localScale = new Vector3(10f, 10f, 1f);
        }
    }

    private void Update()
    {
        if (!BattleController.InBattle && BattleController.SnapshotColors == null) return;

        // 新快照 → 全量
        if (BattleController.SnapshotTick != _appliedSnapshotTick && BattleController.SnapshotColors != null)
        {
            var colors = BattleController.SnapshotColors;
            for (int i = 0; i < _pixels.Length && i < colors.Length; i++)
                _pixels[i] = CellToColor(colors[i]);
            _appliedSnapshotTick = BattleController.SnapshotTick;
            _appliedStateTick = 0; // 快照后增量从 0 重来（快照已含全部状态）
        }

        // 增量：只应用比上次更新的 State 的 dirty 格
        var st = BattleController.LatestState;
        if (st.Players != null && st.Tick != _appliedStateTick)
        {
            foreach (var packed in st.Dirty)
            {
                int idx = packed & 0x3FFF;
                byte color = (byte)(packed >> 14);
                if (idx >= 0 && idx < _pixels.Length)
                    _pixels[idx] = CellToColor(color);
            }
            _appliedStateTick = st.Tick;
        }

        _tex.SetPixels32(_pixels);
        _tex.Apply(false);
    }
}
