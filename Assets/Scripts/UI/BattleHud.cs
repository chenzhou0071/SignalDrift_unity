using TMPro;
using UnityEngine;
using UnityEngine.UI;

// BattleHud — 战斗 HUD：昵称/占比条/剩余时间/能量条/倒计时大字/受伤红闪/终局演出
// 数据来自 BattleController.LatestState（State 包）与 Settle（终局演出触发）
public class BattleHud : MonoBehaviour
{
    [Header("顶部")]
    [SerializeField] private TMP_Text leftNick;   // 我（青）
    [SerializeField] private TMP_Text rightNick;  // 对手（品红）
    [SerializeField] private Image bar0;          // P0 占比条（青）
    [SerializeField] private Image bar1;          // P1 占比条（品红）
    [SerializeField] private TMP_Text covText;    // "42% - 58%"
    [SerializeField] private TMP_Text timeText;   // 剩余 mm:ss

    [Header("左下")]
    [SerializeField] private Image inkBar;        // 能量条（fillAmount = ink/100）
    [SerializeField] private TMP_Text inkText;

    [Header("中央")]
    [SerializeField] private TMP_Text countdownText; // 倒计时大字（cdLeader 激活时显示）

    [Header("反馈")]
    [SerializeField] private Image hurtOverlay;   // 全屏边缘泛红（命中减速时 0.3s）
    [SerializeField] private Image winFlash;      // 终局演出：全屏染色从塔位涌满

    private float _hurtTimer;
    private int _lastCdLeader = -1;
    private bool _namesSet; // 名字是否已按快照 slot 设置（等 MySlot 就绪，防竞态）

    private void Start()
    {
        if (hurtOverlay != null) hurtOverlay.color = new Color(1f, 0f, 0f, 0f);
        if (winFlash != null) winFlash.gameObject.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 等快照到达（MySlot 就绪）后一次性设置左右昵称：左=青方(P0) 右=品红方(P1)，两个窗口一致
        if (!_namesSet && BattleController.SnapshotColors != null)
        {
            _namesSet = true;
            ApplyNames();
        }
        var st = BattleController.LatestState;
        if (st.Players == null) return;

        // 占比条 + 数字
        if (bar0 != null) bar0.fillAmount = st.Cov0 / 10000f;
        if (bar1 != null) bar1.fillAmount = st.Cov1 / 10000f;
        if (covText != null) covText.text = $"{st.Cov0 / 100f:F0}% - {st.Cov1 / 100f:F0}%";
        // 剩余时间
        if (timeText != null)
        {
            int left = st.LeftTicks / 30;
            timeText.text = $"{left / 60}:{left % 60:D2}";
        }
        // 能量条（我所在 slot）
        var me = st.Players[BattleController.MySlot < 2 ? BattleController.MySlot : 0];
        if (inkBar != null) inkBar.fillAmount = Mathf.Clamp01(me.Ink / 100f);
        if (inkText != null) inkText.text = $"{me.Ink:F0}";
        // 倒计时大字（cdLeader != 0xFF）
        int cd = st.CdLeader;
        if (cd != 0xFF && countdownText != null)
        {
            int remain = Mathf.CeilToInt((BattleController.LatestState.CdTicks) / 30f);
            bool opponentWinning = cd != BattleController.MySlot;
            countdownText.gameObject.SetActive(true);
            countdownText.text = opponentWinning ? $"Opponent winning! {remain}" : $"Victory in {remain}!";
        }
        else if (countdownText != null && _lastCdLeader != cd)
        {
            countdownText.gameObject.SetActive(false);
        }
        _lastCdLeader = cd;
        // 受伤红闪（我被命中减速）
        if (me.Slow)
        {
            _hurtTimer = 0.3f;
        }
        if (_hurtTimer > 0f && hurtOverlay != null)
        {
            _hurtTimer -= Time.deltaTime;
            hurtOverlay.color = new Color(1f, 0f, 0f, Mathf.Clamp01(_hurtTimer / 0.3f) * 0.35f);
        }
    }

    // 左右昵称：左=青方(P0) 右=品红方(P1)——两个客户端窗口显示一致
    private void ApplyNames()
    {
        if (BattleController.MySlot == 0)
        {
            leftNick.text = BattleContext.MyNickname;
            rightNick.text = BattleContext.OpponentNickname;
            leftNick.color = Color.cyan;
            rightNick.color = Color.magenta;
        }
        else
        {
            leftNick.text = BattleContext.OpponentNickname;
            rightNick.text = BattleContext.MyNickname;
            leftNick.color = Color.cyan;   // 左永远是青方
            rightNick.color = Color.magenta; // 右永远是品红方
        }
    }

    // 终局演出：胜方颜色从胜方塔位涌满全屏（0.8s）→ 完成后回调
    public void PlayWinFlash(long winnerUid, System.Action onDone)
    {
        if (winFlash == null) { onDone?.Invoke(); return; }
        bool iWin = winnerUid == NetworkClient.I.Uid;
        // 赢家颜色与塔位按 slot 归属（我不是固定青色！）
        bool meIsP0 = BattleController.MySlot == 0;
        var myColor = meIsP0 ? Color.cyan : Color.magenta;
        var oppColor = meIsP0 ? Color.magenta : Color.cyan;
        var myTower = meIsP0 ? new Vector2(80, 360) : new Vector2(1200, 360);
        var oppTower = meIsP0 ? new Vector2(1200, 360) : new Vector2(80, 360);
        var color = iWin ? myColor : oppColor;
        Vector2 from = iWin ? myTower : oppTower;
        // 世界 → 画布坐标（1280×720 世界 → 1920×1080 画布，按窗口缩放）
        from *= Screen.width / 1280f;

        var rt = winFlash.rectTransform;
        winFlash.gameObject.SetActive(true);
        winFlash.color = color;
        rt.position = from;
        rt.localScale = Vector3.one * 0.01f;
        var anim = winFlash.gameObject.AddComponent<WinFlashAnim>();
        anim.Setup(rt, 0.01f, 60f, color, 0.8f, onDone);
    }
}

// 终局演出动画：0.8s 从塔位放大 + 渐隐（独立小组件，避免 tween 依赖）
public class WinFlashAnim : MonoBehaviour
{
    private RectTransform _rt;
    private float _fromScale, _toScale, _duration, _start;
    private Color _color;
    private System.Action _onDone;
    private Image _img;

    public void Setup(RectTransform rt, float fromScale, float toScale, Color color, float duration, System.Action onDone)
    {
        _rt = rt; _fromScale = fromScale; _toScale = toScale;
        _color = color; _duration = duration; _onDone = onDone;
        _start = Time.time;
        _img = rt.GetComponent<Image>();
    }

    private void Update()
    {
        float t = Mathf.Clamp01((Time.time - _start) / _duration);
        _rt.localScale = Vector3.one * Mathf.Lerp(_fromScale, _toScale, t);
        if (_img != null) _img.color = new Color(_color.r, _color.g, _color.b, 1f - t);
        if (t >= 1f)
        {
            Destroy(this);
            _onDone?.Invoke();
        }
    }
}
