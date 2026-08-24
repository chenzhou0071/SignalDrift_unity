using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

// NetworkClient — TCP 客户端单例：后台接收线程 + 主线程消息泵 + 心跳 + 断线事件
// 铁律：后台线程只入队帧，MonoBehaviour 与 handler 只被主线程（Update 泵）触碰
public class NetworkClient : MonoBehaviour
{
    public static NetworkClient I { get; private set; }

    public bool Connected => _client?.Connected ?? false;
    public long Uid;              // 登录后由 LoginController 写入
    public string Nickname;       // 玩家显示名（登录/设名后写入，战斗场景读取）
    public string ReconnectToken; // 登录应答签发，断线重连用
    public MessageDispatcher Dispatcher { get; } = new();
    public event Action OnDisconnected; // 主线程触发

    private TcpClient _client;
    private NetworkStream _stream;
    private Thread _recvThread;
    private readonly ConcurrentQueue<Frame> _inbox = new();
    private readonly object _sendLock = new();
    private uint _seq;
    private volatile bool _running;
    private float _heartbeatTimer;
    private bool _disconnectPending;

    private void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Connect(string host, int port, Action<bool> onResult)
    {
        Disconnect();
        try
        {
            _client = new TcpClient();
            _client.NoDelay = true;
            _client.Connect(host, port);
            _stream = _client.GetStream();
            _running = true;
            _recvThread = new Thread(RecvLoop) { IsBackground = true };
            _recvThread.Start();
            onResult(true);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Net] connect failed: {e.Message}");
            onResult(false);
        }
    }

    private void RecvLoop()
    {
        var dec = new FrameDecoder();
        var buf = new byte[8192];
        try
        {
            while (_running)
            {
                int n = _stream.Read(buf, 0, buf.Length);
                if (n <= 0) break;
                dec.Feed(buf, n, f => _inbox.Enqueue(f));
            }
        }
        catch (Exception) { /* 连接中断，统一走断线流程 */ }
        _disconnectPending = true;
    }

    public void Send<T>(ushort msgId, T body) => SendRaw(msgId, Json.Ser(body));
    public void SendEmpty(ushort msgId) => SendRaw(msgId, null);

    private void SendRaw(ushort msgId, byte[] body)
    {
        if (!Connected) return;
        var seq = ++_seq;
        var raw = FrameCodec.Encode(msgId, seq, body);
        lock (_sendLock)
        {
            try { _stream.Write(raw, 0, raw.Length); }
            catch (Exception e) { Debug.LogError($"[Net] send: {e.Message}"); }
        }
    }

    private void Update()
    {
        // 主线程泵消息
        while (_inbox.TryDequeue(out var f))
        {
            if (f.MsgId == MsgId.HeartbeatAck) continue;
            Dispatcher.Dispatch(f.MsgId, f.Body);
        }
        // 心跳（5 秒，与服务端 configs 一致）
        if (Connected)
        {
            _heartbeatTimer += Time.unscaledDeltaTime;
            if (_heartbeatTimer >= 5f)
            {
                _heartbeatTimer = 0f;
                SendEmpty(MsgId.Heartbeat);
            }
        }
        if (_disconnectPending)
        {
            _disconnectPending = false;
            Disconnect();
            OnDisconnected?.Invoke();
        }
    }

    public void Disconnect()
    {
        _running = false;
        _stream?.Close();
        _client?.Close();
        _stream = null;
        _client = null;
    }

    private void OnDestroy() => Disconnect();
}
