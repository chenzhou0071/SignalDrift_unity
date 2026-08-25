using System;
using NUnit.Framework;

// BattleCodec 测试：与 Go 端 codec_test.go 对拍（字节级一致）
public class BattleCodecTests
{
    // Go 端 TestInputGoldenVector 的真实输出（MoveX=-50, MoveY=100, FireLob, AimX=640, AimY=360）
    [Test]
    public void EncodeInput_MatchesGoGoldenVector()
    {
        var b = BattleCodec.EncodeInput(-50, 100, BattleCodec.BtnLob, 640, 360);
        var want = new byte[] { 0xCE, 0x64, 0x02, 0x02, 0x80, 0x01, 0x68 };
        Assert.AreEqual(want.Length, b.Length);
        for (int i = 0; i < want.Length; i++)
            Assert.AreEqual(want[i], b[i], $"byte {i}");
    }

    // 手工构造状态包字节：2 玩家 + 1 弹体 + dirty，逐字段断言布局
    [Test]
    public void DecodeState_Layout()
    {
        // 弹体段 22B：id u32 + kind u8 + owner u8 + x/y/targetX/targetY f32×4
        var b = new byte[4 + 2 * 10 + 1 + 22 + 2 + 2 + 1 + 2 + 2 + 2 + 2];
        int p = 0;
        PutU32(b, ref p, 123);                      // tick
        PutF32(b, ref p, 100f); PutF32(b, ref p, 200f);
        b[p++] = 80; b[p++] = 3;                    // 玩家0：ink=80 flags=3(slow+online)
        PutF32(b, ref p, 300f); PutF32(b, ref p, 400f);
        b[p++] = 20; b[p++] = 2;                    // 玩家1：ink=20 flags=2(online)
        b[p++] = 1;                                 // projCount
        PutU32(b, ref p, 7); b[p++] = 1; b[p++] = 0; // 弹体：id=7 kind=1(lob) owner=0
        PutF32(b, ref p, 500f); PutF32(b, ref p, 600f);
        PutF32(b, ref p, 640f); PutF32(b, ref p, 360f); // target（抛射落点）
        PutU16(b, ref p, 4000); PutU16(b, ref p, 3000); // cov0=40% cov1=30%
        b[p++] = 0xFF;                              // cdLeader 无
        PutU16(b, ref p, 0); PutU16(b, ref p, 4500);    // cdTicks=0 leftTicks=4500
        PutU16(b, ref p, 1);                        // dirtyCount
        PutU16(b, ref p, (ushort)(129 | (1 << 14)));    // dirty: idx=129 color=1

        var m = BattleCodec.DecodeState(b);
        Assert.AreEqual(123u, m.Tick);
        Assert.AreEqual(2, m.Players.Length);
        Assert.AreEqual(100f, m.Players[0].X, 0.001f);
        Assert.AreEqual(200f, m.Players[0].Y, 0.001f);
        Assert.AreEqual(80, m.Players[0].Ink);
        Assert.IsTrue(m.Players[0].Slow);
        Assert.IsTrue(m.Players[0].Online);
        Assert.AreEqual(300f, m.Players[1].X, 0.001f);
        Assert.AreEqual(20, m.Players[1].Ink);
        Assert.IsFalse(m.Players[1].Slow);
        Assert.AreEqual(1, m.Projs.Length);
        Assert.AreEqual(7u, m.Projs[0].Id);
        Assert.AreEqual(1, m.Projs[0].Kind);
        Assert.AreEqual(0, m.Projs[0].Owner);
        Assert.AreEqual(500f, m.Projs[0].X, 0.001f);
        Assert.AreEqual(640f, m.Projs[0].TargetX, 0.001f);
        Assert.AreEqual(360f, m.Projs[0].TargetY, 0.001f);
        Assert.AreEqual(4000, m.Cov0);
        Assert.AreEqual(3000, m.Cov1);
        Assert.AreEqual(0xFF, m.CdLeader);
        Assert.AreEqual(4500, m.LeftTicks);
        Assert.AreEqual(1, m.Dirty.Length);
        Assert.AreEqual(129, m.Dirty[0] & 0x3FFF);
        Assert.AreEqual(1, m.Dirty[0] >> 14);
    }

    [Test]
    public void DecodeState_TooShort_Throws()
    {
        Assert.Throws<ArgumentException>(() => BattleCodec.DecodeState(new byte[10]));
    }

    // Go 端 TestSnapshotHexDump 的真实输出对拍（涂 (15,15)=P0、(500,500)=P1 后的 RLE 快照）
    [Test]
    public void DecodeSnapshot_MatchesGoRealOutput()
    {
        var hex =
            "0081000001010e8400000401006c00000402000a00000801006800000802000700000a01006600000a020006" +
            "00000a01006600000a02000500000c01006400000c02000400000c01006400000c02000400000c0100640000" +
            "0c02000400000c01006400000c02000500000a01006600000a02000600000a01006600000a02000700000801" +
            "006800000802000a00000401006c000004020438000001020acd00";
        var rle = FromHex(hex);
        // 快照包：mySlot u8 | tick u32 | rleLen u16 | rle
        var b = new byte[7 + rle.Length];
        b[0] = 0; // mySlot
        b[1] = 0; b[2] = 0; b[3] = 0; b[4] = 0; // tick=0
        b[5] = (byte)(rle.Length >> 8); b[6] = (byte)rle.Length;
        Array.Copy(rle, 0, b, 7, rle.Length);

        var (slot, tick, colors) = BattleCodec.DecodeSnapshot(b);
        Assert.AreEqual(0, slot);
        Assert.AreEqual(0u, tick);
        Assert.AreEqual(128 * 72, colors.Length);
        // IdxAt(15,15) → idx = 1*128+1 = 129 应为 P0(1)
        Assert.AreEqual(1, colors[129]);
        // IdxAt(500,500) → idx = 50*128+50 = 6450 应为 P1(2)
        Assert.AreEqual(2, colors[6450]);
        // 塔区永久色：塔0 (80,360) 内格 P0
        Assert.AreEqual(1, colors[36 * 128 + 8]); // (80,360) → idx
    }

    [Test]
    public void DecodeSnapshot_Truncated_Throws()
    {
        Assert.Throws<ArgumentException>(() => BattleCodec.DecodeSnapshot(new byte[] { 0, 0, 0, 0, 0, 0, 9, 1, 1, 1 }));
    }

    private static byte[] FromHex(string hex)
    {
        var b = new byte[hex.Length / 2];
        for (int i = 0; i < b.Length; i++)
            b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return b;
    }

    private static void PutU32(byte[] b, ref int p, uint v)
    {
        b[p++] = (byte)(v >> 24); b[p++] = (byte)(v >> 16);
        b[p++] = (byte)(v >> 8); b[p++] = (byte)v;
    }

    private static void PutU16(byte[] b, ref int p, ushort v)
    {
        b[p++] = (byte)(v >> 8); b[p++] = (byte)v;
    }

    private static void PutF32(byte[] b, ref int p, float v)
    {
        var bits = BitConverter.SingleToInt32Bits(v);
        PutU32(b, ref p, (uint)bits);
    }
}
