using System;
using System.Collections.Generic;
using NUnit.Framework;

public class FrameCodecTests
{
    [Test]
    public void Encode_Layout_BigEndian()
    {
        var body = new byte[] { 0x61, 0x62, 0x63 };
        var b = FrameCodec.Encode(7, 42, body);
        Assert.AreEqual(FrameCodec.HeaderSize + 3, b.Length);
        Assert.AreEqual(0x53, b[0]); Assert.AreEqual(0x44, b[1]);          // magic
        Assert.AreEqual(0x00, b[2]); Assert.AreEqual(0x07, b[3]);          // msgId
        Assert.AreEqual(42, (b[4] << 24) | (b[5] << 16) | (b[6] << 8) | b[7]); // seq
        Assert.AreEqual(3, (b[8] << 24) | (b[9] << 16) | (b[10] << 8) | b[11]); // len
        Assert.AreEqual(0x61, b[12]);
    }

    [Test]
    public void Decoder_MultiFrame_And_Fragmented()
    {
        var f1 = FrameCodec.Encode(1, 1, new byte[] { 1, 2 });
        var f2 = FrameCodec.Encode(2, 2, new byte[] { 3 });
        var all = new byte[f1.Length + f2.Length];
        Buffer.BlockCopy(f1, 0, all, 0, f1.Length);
        Buffer.BlockCopy(f2, 0, all, f1.Length, f2.Length);

        var got = new List<Frame>();
        var dec = new FrameDecoder();
        // 一个字节一个字节喂，模拟极端拆包
        for (int i = 0; i < all.Length; i++)
            dec.Feed(new[] { all[i] }, 1, f => got.Add(f));

        Assert.AreEqual(2, got.Count);
        Assert.AreEqual(1, got[0].MsgId);
        Assert.AreEqual(2, got[0].Body.Length);
        Assert.AreEqual(2, got[1].MsgId);
        Assert.AreEqual(2u, got[1].Seq);
    }

    [Test]
    public void Decoder_BadMagic_Throws()
    {
        var raw = FrameCodec.Encode(1, 1, null);
        raw[0] = 0xFF;
        var dec = new FrameDecoder();
        Assert.Throws<ProtocolException>(() => dec.Feed(raw, raw.Length, _ => { }));
    }

    [Test]
    public void Encode_OversizedBody_Throws()
    {
        var big = new byte[FrameCodec.MaxBodySize + 1];
        Assert.Throws<ProtocolException>(() => FrameCodec.Encode(1, 1, big));
    }
}
