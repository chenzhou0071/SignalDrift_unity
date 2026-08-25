using System;

// BattleCodec — 战斗二进制编解码（与 Go 端 internal/battle/codec.go 字节级对齐，全大端）
// 纯 C# 协议层：不依赖 UnityEngine（SignalDrift.Protocol 程序集强制）
public static class BattleCodec
{
    public const byte BtnStraight = 1; // 直射
    public const byte BtnLob = 2;      // 抛射

    // ---------- 输入包 7B：moveX i8 | moveY i8 | buttons u8 | aimX u16 | aimY u16 ----------
    public static byte[] EncodeInput(sbyte moveX, sbyte moveY, byte buttons, ushort aimX, ushort aimY)
    {
        var b = new byte[7];
        b[0] = (byte)moveX;
        b[1] = (byte)moveY;
        b[2] = buttons;
        b[3] = (byte)(aimX >> 8); b[4] = (byte)aimX;
        b[5] = (byte)(aimY >> 8); b[6] = (byte)aimY;
        return b;
    }

    // ---------- 状态包（镜像 Go EncodeState 布局） ----------
    public struct PlayerState
    {
        public float X, Y;
        public byte Ink;
        public byte Flags;
        public bool Slow => (Flags & 1) != 0;   // 命中减速中
        public bool Online => (Flags & 2) != 0;
    }

    public struct ProjState
    {
        public uint Id;
        public byte Kind;   // 0=直射 1=抛射
        public byte Owner;  // 0/1
        public float X, Y;
        public float TargetX, TargetY; // 抛射落点（直射填当前位置）
    }

    public struct StateMsg
    {
        public uint Tick;
        public PlayerState[] Players; // 恒 2
        public ProjState[] Projs;
        public ushort Cov0, Cov1;     // 万分比（/10000 得占比）
        public byte CdLeader;         // 0xFF=无倒计时
        public ushort CdTicks;        // 已累计倒计时 tick
        public ushort LeftTicks;      // 剩余 tick
        public ushort[] Dirty;        // 脏格：低 14 位 idx + 高 2 位颜色
    }

    public static StateMsg DecodeState(byte[] b)
    {
        if (b == null || b.Length < 36) throw new ArgumentException("state too short");
        int p = 0;
        var m = new StateMsg();
        m.Tick = ReadU32(b, ref p);
        m.Players = new PlayerState[2];
        for (int i = 0; i < 2; i++)
        {
            m.Players[i] = new PlayerState
            {
                X = ReadF32(b, ref p),
                Y = ReadF32(b, ref p),
                Ink = b[p++],
                Flags = b[p++],
            };
        }
        int projN = b[p++];
        m.Projs = new ProjState[projN];
        for (int i = 0; i < projN; i++)
        {
            m.Projs[i] = new ProjState
            {
                Id = ReadU32(b, ref p),
                Kind = b[p++],
                Owner = b[p++],
                X = ReadF32(b, ref p),
                Y = ReadF32(b, ref p),
                TargetX = ReadF32(b, ref p),
                TargetY = ReadF32(b, ref p),
            };
        }
        m.Cov0 = ReadU16(b, ref p);
        m.Cov1 = ReadU16(b, ref p);
        m.CdLeader = b[p++];
        m.CdTicks = ReadU16(b, ref p);
        m.LeftTicks = ReadU16(b, ref p);
        int dirtyN = ReadU16(b, ref p);
        m.Dirty = new ushort[dirtyN];
        for (int i = 0; i < dirtyN; i++) m.Dirty[i] = ReadU16(b, ref p);
        return m;
    }

    // ---------- 快照包：mySlot u8 | tick u32 | rleLen u16 | rle（C# RLE 解码） ----------
    public static (byte mySlot, uint tick, byte[] colors) DecodeSnapshot(byte[] b)
    {
        if (b == null || b.Length < 7) throw new ArgumentException("snapshot too short");
        int p = 0;
        byte mySlot = b[p++];
        uint tick = ReadU32(b, ref p);
        int rleLen = ReadU16(b, ref p);
        if (b.Length < p + rleLen) throw new ArgumentException("snapshot truncated");
        var colors = new byte[128 * 72];
        int pos = 0;
        int end = p + rleLen;
        while (p < end)
        {
            int run = (b[p] << 8) | b[p + 1];
            byte c = b[p + 2];
            p += 3;
            if (pos + run > colors.Length) throw new ArgumentException("snapshot run overflow");
            for (int i = 0; i < run; i++) colors[pos + i] = c;
            pos += run;
        }
        if (pos != colors.Length)
            throw new ArgumentException($"snapshot cells {pos} != {colors.Length}");
        return (mySlot, tick, colors);
    }

    private static uint ReadU32(byte[] b, ref int p)
    {
        uint v = (uint)((b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3]);
        p += 4;
        return v;
    }

    private static ushort ReadU16(byte[] b, ref int p)
    {
        ushort v = (ushort)((b[p] << 8) | b[p + 1]);
        p += 2;
        return v;
    }

    private static float ReadF32(byte[] b, ref int p)
    {
        uint bits = ReadU32(b, ref p);
        return BitConverter.Int32BitsToSingle((int)bits);
    }
}
