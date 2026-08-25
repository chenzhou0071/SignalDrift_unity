// 消息 ID 常量：数值与 Go 端 server/internal/protocol/msgid.go 逐一对齐
public static class MsgId
{
    public const ushort Heartbeat = 1;
    public const ushort HeartbeatAck = 2;
    public const ushort RegisterReq = 200;
    public const ushort RegisterResp = 201;
    public const ushort LoginReq = 202;
    public const ushort LoginResp = 203;
    public const ushort MatchReq = 210;
    public const ushort MatchResp = 211;
    public const ushort MatchCancel = 212;
    public const ushort MatchCancelOK = 213;
    public const ushort MatchFound = 215;
    public const ushort FriendAdd = 220;
    public const ushort FriendAddOK = 221;
    public const ushort FriendDel = 222;
    public const ushort FriendDelOK = 223;
    public const ushort FriendList = 224;
    public const ushort FriendListOK = 225;
    public const ushort ProfileReq = 230;
    public const ushort ProfileResp = 231;
    public const ushort SetNickname = 232;
    public const ushort SetNicknameOK = 233;
    public const ushort EloUpdate = 234;
    public const ushort ErrorResp = 299;
    // 战斗 300-349（与 Go 端 roomd.go 消息号对齐）
    public const ushort RoomJoin = 300;
    public const ushort RoomJoinOK = 301;
    public const ushort BattleSnapshot = 303;
    public const ushort BattleInput = 310;
    public const ushort BattleState = 320;
    public const ushort BattleSettle = 340;
    public const ushort RoomErr = 349;
}
