using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// SettlePanel — 结算面板：胜负大字/最终占比/占比曲线/我的统计/ELO/返回大厅
// 触发链：BattleController 收到 MsgBattleSettle → 终局演出 → Show()
public class SettlePanel : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text resultText;    // "XXX Wins!" / "Draw"
    [SerializeField] private TMP_Text covText;       // "42.3% - 57.7%"
    [SerializeField] private RawImage curveImage;    // 占比曲线（200×80 纹理）
    [SerializeField] private TMP_Text statsText;     // 我的统计
    [SerializeField] private TMP_Text eloText;       // ELO 变化（"ELO 1200 (+15)" / "Calculating..."）
    [SerializeField] private Button backButton;

    private Texture2D _curveTex;
    private SettlePayload _payload;
    private int _myElo;

    private void Awake()
    {
        // 注意：这里【不能】主动 panel.SetActive(false)——
        // 面板初始 inactive 由场景未勾选保证；若在此关闭，
        // Show() 激活面板的瞬间 Awake 会把它再次关掉（结算框永不显示）
        if (backButton != null) backButton.onClick.AddListener(() => SceneManager.LoadScene(1)); // Lobby
    }

    // 由 BattleController 在结算时调用
    public void Show(SettlePayload p)
    {
        _payload = p;
        _myElo = 0;
        if (panel == null) panel = gameObject; // 兜底：Panel 槽没拖时用自身
        panel.SetActive(true);
        // 胜负（字段判空防 NRE 中断）
        bool iWin = p.winner_uid == NetworkClient.I.Uid;
        if (resultText != null)
        {
            resultText.text = p.draw ? "Draw"
                : iWin ? $"{NetworkClient.I.Nickname} Wins!"
                : $"{BattleContext.OpponentNickname} Wins!";
            resultText.color = p.draw ? Color.white : (iWin ? Color.cyan : Color.magenta);
        }
        // 最终占比
        if (covText != null && p.cov != null && p.cov.Length >= 2)
            covText.text = $"{p.cov[0] * 100f:F1}% - {p.cov[1] * 100f:F1}%";
        // 占比曲线
        if (curveImage != null) DrawCurve(p.cov_history);
        // 我的统计（按 slot 归属）
        if (statsText != null)
        {
            var me = BattleController.MySlot == 0 ? p.stats_a : p.stats_b;
            if (me != null)
            {
                statsText.text = $"Painted: {me.painted_cells}\n" +
                                 $"Straight: {me.straight_shots}  Lob: {me.lob_shots}\n" +
                                 $"Hits: {me.hits}  Blackholed: {me.blackhole_lost}\n" +
                                 $"Reflects: {me.reflects}";
            }
        }
        // ELO（等推送）
        if (eloText != null) eloText.text = "Calculating...";
    }

    // EloUpdate 推送更新（BattleController 转发）
    public void OnEloUpdate(int newElo)
    {
        _myElo = newElo;
        eloText.text = $"ELO {newElo}";
    }

    // 占比曲线：青/品红两条折线绘制到 200×80 纹理
    private void DrawCurve(CovPoint[] history)
    {
        if (_curveTex == null)
            _curveTex = new Texture2D(200, 80, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color32[200 * 80];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(10, 12, 20, 255); // 深底
        if (history != null && history.Length > 1)
        {
            for (int i = 0; i < history.Length - 1; i++)
            {
                DrawLine(px, 200, 80,
                    (float)i / (history.Length - 1) * 199f, (1f - history[i].a) * 79f,
                    (float)(i + 1) / (history.Length - 1) * 199f, (1f - history[i + 1].a) * 79f,
                    new Color32(34, 211, 238, 255));
                DrawLine(px, 200, 80,
                    (float)i / (history.Length - 1) * 199f, (1f - history[i].b) * 79f,
                    (float)(i + 1) / (history.Length - 1) * 199f, (1f - history[i + 1].b) * 79f,
                    new Color32(244, 114, 182, 255));
            }
        }
        _curveTex.SetPixels32(px);
        _curveTex.Apply(false);
        curveImage.texture = _curveTex;
    }

    private static void DrawLine(Color32[] px, int w, int h, float x0, float y0, float x1, float y1, Color32 c)
    {
        int steps = Mathf.Max(2, (int)Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)));
        for (int s = 0; s <= steps; s++)
        {
            float t = (float)s / steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            if (x >= 0 && x < w && y >= 0 && y < h)
            {
                // 加粗 3×3
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx >= 0 && xx < w && yy >= 0 && yy < h)
                            px[yy * w + xx] = c;
                    }
            }
        }
    }
}
