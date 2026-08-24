// BattleContext — 跨场景对战上下文：匹配成功后由 LobbyController 写入，战斗场景读取
public static class BattleContext
{
    public static long RoomId;
    public static long OpponentUid;
    public static string OpponentNickname;
    public static int OpponentElo;
    public static string MyNickname;   // = NetworkClient.I.Nickname，战斗 HUD 显示自己名字
}
