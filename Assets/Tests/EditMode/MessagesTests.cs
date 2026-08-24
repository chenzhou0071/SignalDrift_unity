using NUnit.Framework;
using System.Text;

public class MessagesTests
{
    [Test]
    public void LoginResp_Roundtrip_GoJson()
    {
        // Go 端真实输出样例
        var goJson = "{\"code\":0,\"uid\":7,\"elo\":1000,\"token\":\"7.99.abc\"}";
        var v = Json.De<LoginResp>(Encoding.UTF8.GetBytes(goJson));
        Assert.AreEqual(0, v.code);
        Assert.AreEqual(7, v.uid);
        Assert.AreEqual(1000, v.elo);
        Assert.AreEqual("7.99.abc", v.token);
    }

    [Test]
    public void RegisterReq_Serialize()
    {
        var raw = Json.Ser(new RegisterReq { username = "alice", password = "123456" });
        var s = Encoding.UTF8.GetString(raw);
        StringAssert.Contains("\"username\":\"alice\"", s);
        StringAssert.Contains("\"password\":\"123456\"", s);
    }

    [Test]
    public void MatchFound_Deserialize()
    {
        var goJson = "{\"room_id\":33,\"opp_uid\":9,\"opp_elo\":1080}";
        var v = Json.De<MatchFoundPush>(Encoding.UTF8.GetBytes(goJson));
        Assert.AreEqual(33, v.room_id);
        Assert.AreEqual(9, v.opp_uid);
    }
}
