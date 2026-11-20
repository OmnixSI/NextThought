# 手机伴侣：范围与生命期逐项对照（F12）

Mac 参照：`Sources/NextThought/CompanionServer.swift`、`CompanionRelay.swift`。

| 项目 | Mac 做法 | Windows 做法 | 结论 |
|---|---|---|---|
| 监听地址段 | 仅 RFC 1918（10/8、172.16/12、192.168/16）与本机回环；不接受 100.64/10 等运营商 NAT 段 | `CompanionServer.IsLanAddress` 相同：仅 RFC 1918 + 回环，100.64/10 已移除 | 相同 |
| 令牌传递 | 配对链接 fragment 内 base64url JSON（版本、LAN 地址、每次服务生命期轮换的随机令牌），不进初始页面请求与 Referrer | 相同：`PairingUrl = BaseUri + "#p=" + base64url({v:1,lan,token})`，每个服务实例重新生成 | 相同 |
| 服务生命期 | 启动后一直运行，直到用户停止或应用退出；没有固定时长自动停止 | 无 8 小时定时器；`DisposeAsync` 前一直 accept/SSE | 相同 |
| 配对链接内容 | `lan` 地址 + `token`（v1） | 相同字段与顺序 | 相同 |
| 中转通道 | 可选，端到端加密，超出局域网时使用 | `CompanionRelayClient` 相同接口：先 LAN，再按设置启用中转 | 相同（具体加密向量由 B22 CompanionCryptoTests 覆盖） |

Windows 证据：`Windows/src/NextThought.Platform/CompanionServer.cs`（IsLanAddress、PairingUrl、lifetime）、`AppState.Launcher.cs` 的启动/停止路径。
