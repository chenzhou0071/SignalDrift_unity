using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// LobbyController — 大厅界面：档案/匹配/取消/好友；收到 MatchFound 后写入 BattleContext 并跳转战斗
public class LobbyController : MonoBehaviour
{
    [SerializeField] private TMP_Text profileText;
    [SerializeField] private Button matchButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text matchStatusText;
    [SerializeField] private TMP_InputField friendUidInput;
    [SerializeField] private Button friendAddButton;
    [SerializeField] private TMP_Text friendListText; // 一期用整块文本渲染列表，够用

    private float _matchTimer = -1f;

    private void Start()
    {
        matchButton.onClick.AddListener(() =>
        {
            NetworkClient.I.SendEmpty(MsgId.MatchReq);
        });
        cancelButton.onClick.AddListener(() =>
        {
            NetworkClient.I.SendEmpty(MsgId.MatchCancel);
        });
        friendAddButton.onClick.AddListener(() =>
        {
            if (long.TryParse(friendUidInput.text, out var fuid))
                NetworkClient.I.Send(MsgId.FriendAdd, new FriendAddReq { friend_uid = fuid });
        });

        var d = NetworkClient.I.Dispatcher;
        d.On(MsgId.ProfileResp, body =>
        {
            var p = Json.De<ProfileResp>(body);
            profileText.text = $"{p.nickname}\nELO {p.elo}  (max {p.max_elo})\n{p.wins}W {p.losses}L";
        });
        d.On(MsgId.MatchResp, body =>
        {
            var r = Json.De<ErrorResp>(body);
            if (r.code == 0) { _matchTimer = 0f; SetMatching(true); }
            else matchStatusText.text = $"Match request failed ({r.code})";
        });
        d.On(MsgId.MatchCancelOK, _ => { _matchTimer = -1f; SetMatching(false); });
        d.On(MsgId.MatchFound, body =>
        {
            var mf = Json.De<MatchFoundPush>(body);
            BattleContext.RoomId = mf.room_id;
            BattleContext.OpponentUid = mf.opp_uid;
            BattleContext.OpponentNickname = mf.opp_nickname;
            BattleContext.OpponentElo = mf.opp_elo;
            BattleContext.MyNickname = NetworkClient.I.Nickname;
            SceneManager.LoadScene(2); // Battle
        });
        d.On(MsgId.FriendAddOK, _ => NetworkClient.I.SendEmpty(MsgId.FriendList));
        d.On(MsgId.FriendListOK, body =>
        {
            var fl = Json.De<FriendListResp>(body);
            var sb = new StringBuilder();
            if (fl.friends != null)
                foreach (var f in fl.friends)
                    sb.AppendLine($"{(f.online ? "●" : "○")} {f.nickname}  ELO {f.elo}");
            friendListText.text = sb.Length > 0 ? sb.ToString() : "No friends yet";
        });

        NetworkClient.I.SendEmpty(MsgId.ProfileReq);
        NetworkClient.I.SendEmpty(MsgId.FriendList);
        SetMatching(false);
    }

    private void Update()
    {
        if (_matchTimer >= 0f)
        {
            _matchTimer += Time.deltaTime;
            matchStatusText.text = $"Matching... {Mathf.FloorToInt(_matchTimer)}s";
        }
    }

    private void SetMatching(bool matching)
    {
        matchButton.gameObject.SetActive(!matching);
        cancelButton.gameObject.SetActive(matching);
        if (!matching) matchStatusText.text = "";
    }

    private void OnDestroy()
    {
        var d = NetworkClient.I?.Dispatcher;
        if (d == null) return;
        d.Off(MsgId.ProfileResp); d.Off(MsgId.MatchResp); d.Off(MsgId.MatchCancelOK);
        d.Off(MsgId.MatchFound); d.Off(MsgId.FriendAddOK); d.Off(MsgId.FriendListOK);
    }
}
