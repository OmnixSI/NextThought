# 资料库：Mac 3.4.0 图对照（task-48，Windows 专有页面）

- Windows 最新批次：`validation/screens/current/library-light.png` / `library-dark.png`（`--demo --screenshots`，未入库）。
- Mac 最接近的参考：[series-light](../../mac-screens/series/series-light.png) / [series-dark](../../mac-screens/series/series-dark.png)（Mac 3.4 的「系列」页是左系列列表+右详情；Mac 没有独立的「资料库」导入/解析页）。

## 逐元素对照

| Windows 资料库（`library-*.png`） | Mac 对照 | 结论 |
|---|---|---|
| 页标题「资料库」+ 说明「导入 PDF、Word、PPT、Excel、Markdown 与图片；解析在本机完成，检索时把相关摘录发给回答服务。」 | Mac 系列页无此页（资料挂在系列/资料区块） | Windows 专有页面，Mac 无直接对应 |
| 左栏「筛选资料名称」输入框 + 资料行（文件名 + 类型 + 段数） | Mac 系列页左栏是系列列表 + 搜索系列或资料 | 结构类似但对象不同：Windows 列资料，Mac 列系列 |
| 右栏「解析结果」（按资料原有的页/幻灯片或工作表定位 + 打开文件位置）+ 空态说明 | Mac 系列详情右栏是记录/资料/经验积累标签 | 不同 |
| 右上「导入资料」、底部「数据目录」 | Mac 系列页有 + 新系列 / 配置 / 开下一场 | 不同（Windows 资料导入是本页唯一主动作） |

## 未解决/差异

- 页面本身是 Windows 专有信息架构（Mac 3.4 把资料放在系列详情里），不做像素级对齐；本轮只把 Mac 系列图与实际截图索引起来，供以后若 Mac 新增资料库页时对照。
- 资料行的段数/文件类型文案、解析结果定位格式沿用 E23「资料组件」的规则；与本页截图一致性由 `library-*.png` 留证（未提交）。

## 自动验证

- `tests/NextThought.Tests/MacScreensIndexTests.cs` 校验本文件与 README 索引存在。

---

## §5 结论（2026-11-21，task-77，decisions §3.5 library）

**结论：逐条一致（页面已按决议移除）。**

Mac 3.4.0 没有独立的资料页，资料在系列详情里。Windows 的「资料库」页因此不再可达：
- 左侧导航删除「资料库」入口（`MainWindow.axaml`）；
- `AppSection.Library` 路由到系列页（`ShowSection`），设置自查清单里的入口改为「系列」；
- 资料库视图模型里的打开动作改为进入系列页；
- 资料内容保留在系列详情「资料」标签（E20 实现），导入/移除在系列编辑器「资料」分区。
旧的 library 截图不再适用（§5：页面已不存在）。