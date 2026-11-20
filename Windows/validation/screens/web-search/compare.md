# E14 联网搜索收尾：Mac ↔ Windows 对照

对照代码：`Sources/NextThought/WebSearch.swift`（272 行全文：WebSearchProvider/TavilyClient/WebLookup/toggleWebAnswer/lookUpDisplayedAnswer/WebSourcesList 相关）；`Sources/NextThought/SettingsView.swift` 的 `WebSearchSettings`（联网模式 off/auto/always）与高级页「搜索方式与 Tavily」；`Sources/NextThought/SearchAssessment.swift`（模型复核判断是否联网）
对照基准：Mac v3.3.0（`5ddb9e5`）；Mac 无对应截图，按源码逐元素对照
Windows 代码：`Platform/WebSearch.cs`、`AppState.Lifecycle.cs`（provider 解析/密钥状态/缓存持有）、`AppState.Live.cs`（自动联网与「联网查一下」区）、`AppState.SettingsActions.cs`（密钥保存）、`ViewModels/SettingsViewModel.cs`、`Views/SettingsView.axaml`（联网区）、`NextThought.Core/WebVerificationCache.cs`（新增）、`NextThought.Core/WebSearchNeed.cs`
Windows 截图：待 verify 阶段补跑（本轮按 Lead 要求暂停应用启动/截图）

## 1. 设置页联网区（WebSearchSettings / 高级「搜索方式与 Tavily」）

| Mac | Windows | 结论 |
|---|---|---|
| 主设置「联网」：关 / 自动判断 / 总是（webSearchMode，写入 UserDefaults） | 「允许联网核实（需 TAVILY_API_KEY）」勾选（`Settings.WebSearch`）+ 自动判断（`WebSearchNeed.Assess` 规则）；无「总是」与「关」的三态语义差异：关=不勾选，总是≈每次规则判断不通过时仍不查（见未解决） | 部分 |
| 高级「搜索方式」：Codex 联网搜索 / Tavily 搜索 API（webSearchProvider） | 无 Codex 搜索实现：支持 Tavily 一种 provider（`TavilySearchProvider`，interface 带 Provider id） | 部分（Mac 双 provider，Windows 只有 Tavily） |
| Tavily Key 输入（SecureField，存系统钥匙串；获取 Key 链接；说明「问题文本会发送给 Tavily…」） | 密码框 `Settings.TavilyKey` + 「保存密钥」（`SettingsStore.SaveTavilyKey`，当前 Windows 用户加密）+ 说明文案；Key 状态行 `Settings.TavilyKeyStatus` | 一致（存储由钥匙串改为当前用户加密，见未解决） |
| 密钥状态回显 | E14 补：`IsWebSearchConfigured` / `WebSearchProviderLabel` / `HasStoredWebSearchKey` / `WebSearchAvailabilityText`（已配置显示「Tavily 密钥已保存… · tavily」，未配置显示「联网未配置：先在设置里保存 Tavily 密钥。」）；保存/清空密钥即时刷新并同步 `Settings.TavilyKeyStatus` | 已补 |
| 密钥保存后立即生效（provider 重建） | 保存时 `webSearchResolved=false; webSearchProvider=null`，下次查询重新解析 | 一致 |
| 环境变量兼容 | `TAVILY_API_KEY` + `NEXTTHOUGHT_WEB_SEARCH=0/off/false` 可关；设置密钥优先 | 一致（Windows 额外兼容） |

## 2. 自动联网（后台查证）

| Mac | Windows | 结论 |
|---|---|---|
| `assessWebSearch`：off/always → 规则 → 模型复核（3 秒超时；失败沿用规则，记录 fallback 原因） | `Settings.WebSearch` + `WebSearchNeed.Assess(question).NeedsSearch` 规则判断；无模型复核（Windows 无 Codex 决策服务） | 部分（无模型复核） |
| 后台 lookup 按回答 id 保存 `WebLookup`（searching/done/failed、question/answer/sources/seconds/provider/startedAt） | `WebResults` 集合 + `WebStatusText`/`HasWebResults`；无按回答 id 的 `WebLookup` 状态机 | 部分（状态粒度更粗） |
| `showsWebAnswer`/`toggleWebAnswer`：联网版替换快速回答，用户可切回原回答并记 `showOriginalAnswer` | 「联网版」由来源区展示，无「原回答/联网版」切换语义（LiveView 由 d-parity 在途，不得改动） | 未做（记未解决） |
| 缓存 | **E14 新增 `WebVerificationCache`**：自动联网先查缓存（key=规范化问题+provider，30 分钟有效，过期即删）；命中则复用来源并在状态显示「正在联网核实（使用 N 分钟前的查证结果）…」，不再请求 Tavily；空结果不缓存（临时失败下次重试）；最多 40 条按时间淘汰 | 已补 |
| 失效条件 | 30 分钟过期；provider 变化（key 含 provider）；保存新 Tavily 密钥时 `Invalidate()`（旧结果可能属于另一个账号）；手动 `ClearWebVerificationCacheCommand` | 已补 |

## 3. 对已显示回答查证与来源列表

| Mac | Windows | 结论 |
|---|---|---|
| `lookUpDisplayedAnswer`：拿当前显示回答的问题 + 回答文本联网重新查证，结果替换来源列表 | `LookUpDisplayedAnswerAsync`：问题取 `DetectedQuestion`（空则「核对上一条回答的事实」）+ AnswerText 前 300 字；手动查证总是新请求，结果写入 `WebVerificationCache`（供同问题自动联网复用）；状态「查证完成：N 条联网来源。」 | 一致（Windows 手动查证不读缓存，符合「已显示回答查证」要新信息） |
| `WebSourcesList`：来源标题/域名/摘要/发布时间，打开链接 | `LiveWebSources`（`WebSourceRow(序号, WebResult)`）+ LiveView 已有来源列表与 `OpenWebSource`；本轮未改动 LiveView（d-parity E5 在途） | 一致（既有） |
| 来源写回回答提示（`WebSearchNeed`/`WebLookupPromptBuilder`） | `BuildAnswerPrompt(..., web)` + `WebLookupPromptBuilder`（Core 既有） | 一致 |
| 失败/未配置提示 | 「联网未配置：先在设置里保存 Tavily 密钥。」/`webResponse.Error` 状态；无密钥时保持离线 | 一致 |

## 4. 新增测试

`Windows/tests/NextThought.Tests/WebVerificationCacheTests.cs`（6 个，全部通过）：
- 30 分钟内命中、31 分钟过期且过期条目即时删除；
- key 用「规范化问题（小写/折叠空白/截断 400）+ provider」：问法不同/provider 不同 miss；
- `Invalidate()`（保存新密钥/手动清空）后不再命中；
- 空结果不缓存（失败下次重试）；
- 容量 40 条按 FetchedAt 淘汰最旧；
- `AgeLabel`（刚刚/N 分钟前/N 小时前）。

## 5. 自检点按记录（`web-search`，隔离数据目录）

自检 `RunWebSearchCheckAsync` 已加入：无密钥→未配置；保存假密钥→加密落盘、解析 provider=tavily、状态行刷新；缓存命中/区分 provider/过期；保存新密钥后缓存清空；手动清空缓存；设置页「联网」区块截图；清空密钥回到未配置。

**本轮运行状态（如实记录）**：按 Lead 要求本轮结束前暂停应用启动与截图，**自检未执行取证**；E14 现在为「已实现、未验证」，缓存语义由上述单元测试覆盖，key 状态/provider 解析为编译级验证。

## 6. 未解决 / 待补

1. 无 Mac 的「联网模式」三态（关/自动判断/总是）与 Codex 搜索 provider；Windows 只有开关 + 规则自动判断 + Tavily。
2. 无 Mac 的模型复核（assessWebSearch 3 秒分类器，失败 fallback 记录）；Windows 直接按规则判断。
3. 无「原回答 ↔ 联网版」切换（`toggleWebAnswer`/`showOriginalAnswer`）与按回答 id 的 `WebLookup` 状态机；LiveView 由 d-parity 在途，本轮未改。
4. 密钥存储从 Mac 系统钥匙串改为 Windows 当前用户加密文件（DPAPI）；备份排除清单已含 `websearch.tavily`。
5. 设置页缺少 Mac 的「搜索方式/Tavily Key」高级分组视觉（当前「联网」区在回答服务卡片内）；本轮 SettingsView 有其它队友在途修改，未追加布局改动。
6. 缓存目前只服务自动联网；手动「联网查一下」总是新请求（Mac 语义如此），无 UI 显示缓存年龄（状态行有文字，LiveView 未改）。
7. 真实 Tavily 联网未在自动化中执行（无有效密钥/网络），需 verify 阶段实机点按。
