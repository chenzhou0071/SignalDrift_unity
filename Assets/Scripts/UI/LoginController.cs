using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// LoginController — 登录/注册界面：连接服务端、注册登录、首次登录设名、跳转大厅
public class LoginController : MonoBehaviour
{
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject nicknamePanel;       // 首次登录设名面板（默认隐藏）
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button confirmNicknameButton;

    [SerializeField] private string host = "127.0.0.1"; // 默认值，Resources/network_config.json 可覆盖
    [SerializeField] private int port = 8080;

    [Serializable]
    public class NetConfig
    {
        public string host;
        public int port;
    }

    private void Awake()
    {
        // 外部配置优先：Resources/network_config.json；缺失时用 Inspector 默认值
        var cfg = Resources.Load<TextAsset>("network_config");
        if (cfg != null)
        {
            var nc = JsonUtility.FromJson<NetConfig>(cfg.text);
            if (!string.IsNullOrEmpty(nc.host)) host = nc.host;
            if (nc.port > 0) port = nc.port;
            Debug.Log($"[Net] config host={host} port={port}");
        }
    }

    private void Start()
    {
        loginButton.onClick.AddListener(() => Submit(isRegister: false));
        registerButton.onClick.AddListener(() => Submit(isRegister: true));

        var d = NetworkClient.I.Dispatcher;
        d.On(MsgId.RegisterResp, body =>
        {
            var r = Json.De<RegisterResp>(body);
            statusText.text = r.code == 0 ? $"注册成功 UID={r.uid}，请登录"
                : r.code == 409 ? "用户名已存在" : $"注册失败({r.code})";
        });
        d.On(MsgId.LoginResp, body =>
        {
            var r = Json.De<LoginResp>(body);
            if (r.code != 0) { statusText.text = $"登录失败({r.code})"; return; }
            NetworkClient.I.Uid = r.uid;
            NetworkClient.I.Nickname = r.nickname;
            NetworkClient.I.ReconnectToken = r.token;
            if (string.IsNullOrEmpty(r.nickname))
                nicknamePanel.SetActive(true); // 首次登录未设名：弹设名面板
            else
                SceneManager.LoadScene("Lobby");
        });
        d.On(MsgId.SetNicknameOK, body =>
        {
            var r = Json.De<SetNicknameResp>(body);
            if (r.code != 0) { statusText.text = "昵称需 1-16 字符"; return; }
            NetworkClient.I.Nickname = r.nickname;
            SceneManager.LoadScene("Lobby");
        });
        confirmNicknameButton.onClick.AddListener(() =>
        {
            var nick = nicknameInput.text.Trim();
            if (nick.Length < 1) { statusText.text = "请输入昵称"; return; }
            NetworkClient.I.Send(MsgId.SetNickname, new SetNicknameReq { nickname = nick });
        });
    }

    private void Submit(bool isRegister)
    {
        var u = usernameInput.text.Trim();
        var p = passwordInput.text;
        if (u.Length < 3 || p.Length < 6) { statusText.text = "用户名≥3字符，密码≥6字符"; return; }

        void DoSend()
        {
            if (isRegister) NetworkClient.I.Send(MsgId.RegisterReq, new RegisterReq { username = u, password = p });
            else NetworkClient.I.Send(MsgId.LoginReq, new LoginReq { username = u, password = p });
        }

        if (!NetworkClient.I.Connected)
        {
            statusText.text = "连接中...";
            NetworkClient.I.Connect(host, port, ok =>
            {
                statusText.text = ok ? "" : "无法连接服务器";
                if (ok) DoSend();
            });
        }
        else DoSend();
    }

    private void OnDestroy()
    {
        var d = NetworkClient.I?.Dispatcher;
        d?.Off(MsgId.RegisterResp);
        d?.Off(MsgId.LoginResp);
        d?.Off(MsgId.SetNicknameOK);
    }
}
