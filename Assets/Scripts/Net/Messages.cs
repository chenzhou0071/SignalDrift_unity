using System;
using System.Text;
using UnityEngine;

// 消息 DTO：字段名与 Go 端 lobby/dto.go 的 JSON tag 完全一致（小写下划线）
[Serializable] public class ErrorResp { public int code; public string msg; }
[Serializable] public class RegisterReq { public string username; public string password; }
[Serializable] public class RegisterResp { public int code; public long uid; }
[Serializable] public class LoginReq { public string username; public string password; }
[Serializable] public class LoginResp { public int code; public long uid; public string nickname; public int elo; public string token; public long exp; }
[Serializable] public class SetNicknameReq { public string nickname; }
[Serializable] public class SetNicknameResp { public int code; public string nickname; }
[Serializable] public class MatchFoundPush { public long room_id; public long opp_uid; public string opp_nickname; public int opp_elo; }
[Serializable] public class FriendAddReq { public long friend_uid; }
[Serializable] public class FriendDelReq { public long friend_uid; }
[Serializable] public class FriendInfo { public long uid; public string nickname; public int elo; public bool online; }
[Serializable] public class FriendListResp { public int code; public FriendInfo[] friends; }
[Serializable] public class ProfileResp { public int code; public long uid; public string nickname; public int elo; public int max_elo; public int wins; public int losses; }
[Serializable] public class RoomJoinReq { public long room_id; public string token; }
// 结算 JSON（与 Go protocol.SettlePayload 对齐；cov_history 对象数组——JsonUtility 不支持嵌套数组）
[Serializable] public class SettlePayload
{
    public long room_id;
    public long winner_uid;
    public long loser_uid;
    public bool draw;
    public float[] cov;        // [0]=P0 [1]=P1
    public CovPoint[] cov_history;
    public StatsPayload stats_a;
    public StatsPayload stats_b;
    public int duration;
}
[Serializable] public class CovPoint { public float a; public float b; }
[Serializable] public class StatsPayload
{
    public int painted_cells;
    public int straight_shots;
    public int lob_shots;
    public int hits;
    public int blackhole_lost;
    public int reflects;
}
[Serializable] public class EloUpdatePush { public int elo; }

// 统一序列化入口：UTF-8 + JsonUtility
public static class Json
{
    public static byte[] Ser<T>(T v) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(v));
    public static T De<T>(byte[] body) => JsonUtility.FromJson<T>(Encoding.UTF8.GetString(body));
}
