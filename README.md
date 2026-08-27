# SignalDrift — 实时涂色对战 客户端

2D 实时涂色对战游戏《信号漂流》的 Unity 客户端（Unity 6 / URP 2D）。配合服务端仓库 `chenzhou0071/SignalDrift`（Go 双进程）实现完整对战：登录 → 大厅匹配 → 实时涂色对战 → 结算 ELO。

## 技术栈

- Unity 6（URP 2D 渲染管线）、新 Input System（`Keyboard.current` / `Mouse.current`）
- 程序集分层：`SignalDrift.Protocol`（纯 C#，`noEngineReferences` 强制无引擎依赖——编解码可脱离 Unity 测试）/ `SignalDrift.Net` / `SignalDrift.Tests`
- 坐标系：世界 1280×720（128×72 格 ×10px），正交相机 Size=360 @ (640,360,-10)

## 场景与流程

| 场景 | 内容 |
|---|---|
| `Login`（0） | 注册/登录（自动保存凭据，断线重连用） |
| `Lobby`（1） | 好友/档案/匹配（ELO 近邻撮合） |
| `Battle`（2） | 实时对战：涂色渲染/实体插值/HUD/结算面板 |

```
登录 → 大厅 → 匹配成功 → Battle：
  RoomJoin → 快照（RLE 全量）+ 30Hz State 流（脏格子增量）
  → 输入上报（30Hz：WASD/瞄准/左右键）→ 对局 → 结算（演出 + 面板）→ ELO
```

## 目录结构

```
Assets/
├── Scripts/
│   ├── Net/Protocol/      纯 C#：FrameCodec/BattleCodec/MsgId（golden vector 对拍）
│   ├── Net/               NetworkClient（后台线程+主线程泵）/MessageDispatcher/Messages
│   ├── Game/              BattleController（入房/输入/重连）/PaintRenderer（涂色层）
│   │                       /EntityView（实体插值）/BattleContext
│   ├── UI/                Login/Lobby/BattleHud/SettlePanel/UiTheme
│   └── Editor/            BattleSceneBuilder（一键生成战斗静态层：墙/塔/黑洞）
├── Scenes/                Login / Lobby / Battle
├── StreamingAssets/       network_config.json（服务器地址，打包后可改）
└── Tests/                 EditMode 测试（编解码对拍等）
```

## 快速开始

```text
1. 服务端启动：见服务端仓库 README（MySQL + gateway + roomd）
2. 联机地址：Assets/StreamingAssets/network_config.json
   { "host": "127.0.0.1", "port": 8080 }
   （异地联机：改成服务端机器的 IP/域名，重新打开即可，无需重新打包）
3. Unity 打开项目 → 打开 Login 场景 → Play
4. 注册 → 登录 → 大厅点 Match → 双开/和朋友联机对战
```

## 打包

```text
Build Profiles → Windows → Build
注意：打包后 network_config.json 在 SignalDrift_Data/StreamingAssets/ 下，改它即可换服务器
```

## 战斗玩法

- 涂色：直射沿弹道拖墨线、抛射越墙溅射；覆盖率 75% + 倒计时 = 胜利（3 分钟时限）
- 弹道：直射/抛射双弹道、黑洞弯轨、反射墙镜像、命中减速
- 能量：直射耗墨 3 / 抛射 30，己方地盘回墨 9/s、敌方 2.4/s
- 断线重连：自动重连（2s×15 次）→ 重登 → 重入房，30 秒宽限内可回场

## 配套文档

- 服务端仓库：https://github.com/chenzhou0071/SignalDrift（架构/配置/压测）
- 开发日志六篇：`docs/blog/`（服务端仓库内）
