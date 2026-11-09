# 用自己的 Mac 当构建机

仓库的构建、测试、自检、截图和发布（`.github/workflows/` 里的 Verify、Release、Screenshots）默认在**你自己的 Mac** 上运行（GitHub 叫 self-hosted runner），不再消耗 GitHub 的 macOS 计费分钟（私有仓库按 10 倍计算）。

## 一次性设置（约 5 分钟）

前提：Apple Silicon Mac，已安装 **Xcode 16 或更新**（App Store 安装后打开一次、同意许可），并且你是这个仓库的管理员。

在仓库目录里运行：

```sh
./scripts/setup-ci-runner.sh
```

脚本会依次：

1. 检查 Xcode / Swift 6，缺 GitHub CLI 时用 Homebrew 安装 `gh`，没登录就打开浏览器让你登录；
2. 用 `gh` 自动获取注册令牌（没有管理员权限时，提示你从 仓库 Settings → Actions → Runners → New self-hosted runner 页面复制令牌）；
3. 下载 GitHub 官方的 Actions Runner 到 `~/actions-runner-nextthought`；
4. 以“这台电脑名-nextthought”注册到仓库；
5. 设为登录后自动在后台运行（launchd 用户服务）。

完成后打开 仓库 Settings → Actions → Runners，应能看到这台 Mac 显示 **Idle**（空闲）。之前排队的任务会自动开始。

## 平时怎么用

不用管它。推送代码、合并 PR、发版时，任务会自动在这台 Mac 上跑：

| 工作流 | 什么时候 | 大约耗时 |
| --- | --- | --- |
| Verify | 推送代码（只改文档的提交跳过） | 构建 + 测试 3–6 分钟，自检 3–8 分钟（首次下载识别模型约 270 MB） |
| Release | 提交说明含 `[release vX.Y.Z]`，或合并到 main（main 最新构建） | 5–10 分钟 |
| Screenshots | 提交说明含 `[screenshots]` | 2–3 分钟，期间屏幕上会闪过应用窗口 |

常用命令：

```sh
./scripts/setup-ci-runner.sh --status   # 是否在运行
./scripts/setup-ci-runner.sh --stop     # 暂停（新任务会排队，start 后继续）
./scripts/setup-ci-runner.sh --start
./scripts/setup-ci-runner.sh --remove   # 注销并删除
```

**睡眠**：Mac 睡眠或关机时任务会排队等待，醒来后继续（排队超过 24 小时会被 GitHub 取消，重新推送即可）。插电时可以在 系统设置 → 电池 → 选项 里打开“显示器关闭时防止自动睡眠”。

## 会不会影响我自己的 NextThought？

不会。工作流专门做了隔离：

- 测试用的应用使用独立身份 `com.nextthought.app.ci`：设置（UserDefaults）和钥匙串条目与你安装的 NextThought 完全分开，不会读写你的 API Key、场景或记录。
- 所有测试数据（场景、记录、下载的识别模型、测试用签名证书）都在 `~/.nextthought-ci`，可以随时删除。
- 签名测试会临时把测试钥匙串加入钥匙串搜索列表，任务结束时自动移除；发布用的临时钥匙串也会在结束时删除。不会使用 `sudo`，不会改动系统钥匙串。
- 来自 fork 的 PR **不会**在你的 Mac 上运行（只在 GitHub 的机器上运行）。如果以后把仓库改成公开，建议改回 GitHub 的机器（见下）。

占用空间：Runner 本身约 200 MB，构建缓存 1–3 GB（在 `~/actions-runner-nextthought/_work`），识别模型约 270 MB。

## 截图工作流需要屏幕录制权限

截图会启动演示模式的应用并用 `screencapture` 截取窗口。第一次运行时 macOS 可能弹出权限请求；如果截图没有生成（日志里出现 `window capture failed`），到 系统设置 → 隐私与安全性 → 屏幕与系统音频录制，点 ＋ 添加 `~/actions-runner-nextthought/bin/Runner.Worker` 和 `Runner.Listener`，然后 `--stop` 再 `--start`。只跑构建、测试和发布不需要这个权限。

## 改回 GitHub 的机器

在 仓库 Settings → Secrets and variables → Actions → **Variables** 新建变量 `MAC_RUNNER`，值填 `github`。所有工作流会改回 `macos-latest`（消耗 GitHub 分钟数）。删除这个变量就回到自己的 Mac。

## 常见问题

- **任务一直显示 Queued / Waiting for a runner**：Runner 没在运行。运行 `--status` 看看；Mac 在睡眠或已注销时也会这样。
- **`swift: error: ... requires Xcode`**：安装完整的 Xcode 并运行 `sudo xcode-select -s /Applications/Xcode.app`。
- **发布时 `gh: command not found`**：Runner 服务启动时的 PATH 里没有 Homebrew。运行 `--stop`，在 `~/actions-runner-nextthought/.path` 末尾加上 `:/opt/homebrew/bin`，再 `--start`。

## v1.7.0 临时 runner

本轮仅使用仓库 `.local/actions-runner` 内的前台 runner，不安装登录服务。运行结束通过 `config.sh remove` 注销；不把私钥写入 GitHub secrets。`verify.yml` 已恢复同仓 PR 检查，并在自检前后完整保存／恢复原 keychain 搜索列表。构建自检使用独立 `.ci` identifier 和 runner 临时数据目录。

发布工作流以 tag 或显式 dispatch 触发，先获取 App 内固定哈希的 Python 运行包，再构建签名 App／DMG／ZIP；所有资产上传到 draft 并重新下载验证全部 SHA-256 后才正式发布。手工准备的运行包也必须匹配受版本控制的 manifest，不会在 CI 临时解析一个“最新”依赖集合。
