using System;
using System.Collections.Generic;

// MessageDispatcher — msgID → handler 注册与分发（覆盖式注册，主线程消费）
public class MessageDispatcher
{
    private readonly Dictionary<ushort, Action<byte[]>> _handlers = new();

    public void On(ushort msgId, Action<byte[]> handler) => _handlers[msgId] = handler;
    public void Off(ushort msgId) => _handlers.Remove(msgId);

    public void Dispatch(ushort msgId, byte[] body)
    {
        if (_handlers.TryGetValue(msgId, out var h)) h(body);
        else UnityEngine.Debug.LogWarning($"[Net] no handler for msgId={msgId}");
    }
}
