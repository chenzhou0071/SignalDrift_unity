using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// BattleController — 战斗场景总控：入房/30Hz 输入流/State 环形缓冲/快照存储/断线自动重连
// Task 11 的 PaintRenderer/EntityView 从这里取数据
public class BattleController : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;     // 状态/重连遮罩文本（可空）
    [SerializeField] private TMP_Text inputDebugText; // 输入调试显示（可空）：每 0.3s 刷新当前按键状态

    // 对战数据（Task 11 渲染消费）
    public static byte[] SnapshotColors { get; private set; } // 9216 格（快照全量）
    public static byte MySlot { get; private set; }
    public static uint SnapshotTick { get; private set; }
    public static bool InBattle { get; private set; } // 收到首个 State 后 true

    private static readonly List<BattleCodec.StateMsg> StateRing = new(64); // 插值缓冲（Task 11 用）

    private string _baseStatus = ""; // 状态行（Connected/重连）
    private string _inputDebug = ""; // 输入行（实时刷新）

    private bool _reconnecting;
    private float _reconnectTimer;
    private int _reconnectAttempts;
    private bool _inputLogged; // 诊断：SendInput 首次执行标记
    private const int MaxReconnectAttempts = 15; // ≈30s 重连窗口
    private const float ReconnectInterval = 2f;

    private void Start()
    {
        StateRing.Clear();
        InBattle = false;
        var d = NetworkClient.I.Dispatcher;
        d.On(MsgId.RoomJoinOK, _ => { });
        d.On(MsgId.BattleSnapshot, OnSnapshot);
        d.On(MsgId.BattleState, OnState);
        d.On(MsgId.LoginResp, OnReloginResp); // 重连流程的重新登录应答
        NetworkClient.I.OnDisconnected += OnDisconnected;
        SendJoin();
        InvokeRepeating(nameof(SendInput), 0f, 1f / 30f);
    }

    // ---------- 入房 ----------
    private void SendJoin()
    {
        if (NetworkClient.I == null || !NetworkClient.I.Connected) return;
        NetworkClient.I.Send(MsgId.RoomJoin, new RoomJoinReq
        {
            room_id = BattleContext.RoomId,
            token = NetworkClient.I.ReconnectToken,
        });
        SetStatus("Joining room...");
    }

    private void OnSnapshot(byte[] body)
    {
        var (slot, tick, colors) = BattleCodec.DecodeSnapshot(body);
        MySlot = slot;
        SnapshotTick = tick;
        SnapshotColors = colors;
        SetStatus("Connected");
    }

    // ---------- State 流（环形缓冲，Task 11 插值用） ----------
    private void OnState(byte[] body)
    {
        var m = BattleCodec.DecodeState(body);
        StateRing.Add(m);
        if (StateRing.Count > 64) StateRing.RemoveAt(0);
        InBattle = true;
    }
    // ---------- 30Hz 输入流：WASD + 鼠标瞄准 + 左键直射/右键抛射（新 Input System API） ----------
    private void SendInput()
    {
        if (!_inputLogged)
        {
            _inputLogged = true;
            Debug.Log($"[Battle] SendInput running InBattle={InBattle} Connected={NetworkClient.I?.Connected}");
        }
        if (!InBattle || NetworkClient.I == null || !NetworkClient.I.Connected) return;
        sbyte mx = 0, my = 0;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) my = 100;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) my = -100;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) mx = -100;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) mx = 100;
        }
        byte buttons = 0;
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.isPressed) buttons |= BattleCodec.BtnStraight;
            if (mouse.rightButton.isPressed) buttons |= BattleCodec.BtnLob;
        }
        // 鼠标瞄准：屏幕坐标 → 世界坐标（与游戏世界 1280×720 对应）
        var aim = Vector2.zero;
        var sp = Vector2.zero;
        if (mouse != null && Camera.main != null)
        {
            sp = mouse.position.ReadValue();
            aim = (Vector2)Camera.main.ScreenToWorldPoint(new Vector3(sp.x, sp.y, 0f));
        }
        var aimX = (ushort)Mathf.Clamp(aim.x, 0, 65535);
        var aimY = (ushort)Mathf.Clamp(aim.y, 0, 65535);
        NetworkClient.I.SendBytes(MsgId.BattleInput, BattleCodec.EncodeInput(mx, my, buttons, aimX, aimY));
        // 输入调试：屏幕坐标 + 世界坐标对照（World 应 0-1280 / 0-720）
        var keys = new List<string>();
        if (kb != null)
        {
            if (kb.wKey.isPressed) keys.Add("w");
            if (kb.aKey.isPressed) keys.Add("a");
            if (kb.sKey.isPressed) keys.Add("s");
            if (kb.dKey.isPressed) keys.Add("d");
        }
        if (mouse != null)
        {
            if (mouse.leftButton.isPressed) keys.Add("LMB");
            if (mouse.rightButton.isPressed) keys.Add("RMB");
        }
        _inputDebug = $"Screen:({sp.x:F0},{sp.y:F0}) World:({aimX},{aimY}) Keys:[{string.Join("][", keys)}]";
        RefreshStatus(); // 每帧刷新（调试期实时性优先）
        if (inputDebugText != null) inputDebugText.text = _inputDebug;
    }

    // ---------- 断线自动重连：遮罩 → 每 2s Connect（≤15 次）→ 重新登录 → 重新入房 ----------
    private void OnDisconnected()
    {
        if (_reconnecting) return;
        _reconnecting = true;
        _reconnectAttempts = 0;
        _reconnectTimer = 0f;
        SetStatus("Connection lost. Reconnecting...");
    }

    private void Update()
    {
        if (!_reconnecting || NetworkClient.I == null || NetworkClient.I.Connected) return;
        _reconnectTimer += Time.unscaledDeltaTime;
        if (_reconnectTimer < ReconnectInterval) return;
        _reconnectTimer = 0f;
        _reconnectAttempts++;
        if (_reconnectAttempts > MaxReconnectAttempts)
        {
            SetStatus("Reconnect failed. Back to login.");
            SceneManager.LoadScene(0); // 超重连窗口：回登录
            return;
        }
        NetworkClient.I.Connect(NetworkClient.I.Host, NetworkClient.I.Port, ok =>
        {
            if (!ok) return; // 下轮再试
            // 重新登录（复用内存凭据）
            NetworkClient.I.Send(MsgId.LoginReq, new LoginReq
            {
                username = NetworkClient.I.LastUsername,
                password = NetworkClient.I.LastPassword,
            });
        });
    }

    private void OnReloginResp(byte[] body)
    {
        if (!_reconnecting) return;
        var r = Json.De<LoginResp>(body);
        if (r.code != 0)
        {
            SetStatus("Relogin failed. Back to login.");
            SceneManager.LoadScene(0);
            return;
        }
        NetworkClient.I.Uid = r.uid;
        NetworkClient.I.ReconnectToken = r.token;
        _reconnecting = false; // 恢复后隐藏遮罩由收到新 Snapshot 触发
        SetStatus("Reconnected. Joining...");
        SendJoin();
    }

    private void OnDestroy()
    {
        if (NetworkClient.I != null) NetworkClient.I.OnDisconnected -= OnDisconnected;
    }

    private void SetStatus(string s)
    {
        _baseStatus = s;
        RefreshStatus();
    }

    // 状态文本两行：状态 + 输入调试（输入行可空）
    private void RefreshStatus()
    {
        if (statusText == null) return;
        statusText.text = string.IsNullOrEmpty(_inputDebug) ? _baseStatus : $"{_baseStatus}\n{_inputDebug}";
    }
}
