using System;

public class ProtocolException : Exception
{
    public ProtocolException(string msg) : base(msg) { }
}

public struct Frame
{
    public ushort MsgId;
    public uint Seq;
    public byte[] Body;
}

public static class FrameCodec
{
    public const ushort Magic = 0x5344;
    public const int HeaderSize = 12;
    public const int MaxBodySize = 65536;

    public static byte[] Encode(ushort msgId, uint seq, byte[] body)
    {
        int bodyLen = body?.Length ?? 0;
        if (bodyLen > MaxBodySize)
            throw new ProtocolException("body too large"); // 与 Decoder 对称：超限拒绝，防自踢
        var b = new byte[HeaderSize + bodyLen];
        b[0] = (byte)(Magic >> 8); b[1] = (byte)(Magic & 0xFF);
        b[2] = (byte)(msgId >> 8); b[3] = (byte)msgId;
        b[4] = (byte)(seq >> 24); b[5] = (byte)(seq >> 16);
        b[6] = (byte)(seq >> 8); b[7] = (byte)seq;
        b[8] = (byte)(bodyLen >> 24); b[9] = (byte)(bodyLen >> 16);
        b[10] = (byte)(bodyLen >> 8); b[11] = (byte)bodyLen;
        if (bodyLen > 0) Buffer.BlockCopy(body, 0, b, HeaderSize, bodyLen);
        return b;
    }
}

public class FrameDecoder
{
    private byte[] _buf = new byte[8192];
    private int _len;

    public void Feed(byte[] data, int count, Action<Frame> onFrame)
    {
        EnsureCapacity(_len + count);
        Buffer.BlockCopy(data, 0, _buf, _len, count);
        _len += count;

        int offset = 0;
        while (_len - offset >= FrameCodec.HeaderSize)
        {
            int magic = (_buf[offset] << 8) | _buf[offset + 1];
            if (magic != FrameCodec.Magic)
                throw new ProtocolException("bad magic");
            int bodyLen = (_buf[offset + 8] << 24) | (_buf[offset + 9] << 16)
                        | (_buf[offset + 10] << 8) | _buf[offset + 11];
            if (bodyLen > FrameCodec.MaxBodySize)
                throw new ProtocolException("body too large");
            if (_len - offset < FrameCodec.HeaderSize + bodyLen) break; // 半包，等下次

            var f = new Frame
            {
                MsgId = (ushort)((_buf[offset + 2] << 8) | _buf[offset + 3]),
                Seq = (uint)((_buf[offset + 4] << 24) | (_buf[offset + 5] << 16)
                           | (_buf[offset + 6] << 8) | _buf[offset + 7]),
                Body = new byte[bodyLen],
            };
            Buffer.BlockCopy(_buf, offset + FrameCodec.HeaderSize, f.Body, 0, bodyLen);
            offset += FrameCodec.HeaderSize + bodyLen;
            onFrame(f);
        }
        if (offset > 0)
        {
            Buffer.BlockCopy(_buf, offset, _buf, 0, _len - offset);
            _len -= offset;
        }
    }

    private void EnsureCapacity(int need)
    {
        if (need <= _buf.Length) return;
        int cap = _buf.Length * 2;
        while (cap < need) cap *= 2;
        Array.Resize(ref _buf, cap);
    }
}
