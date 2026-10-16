#!/bin/bash
# Registers this Mac as the GitHub Actions build machine (self-hosted runner) for
# InterviewHacker, so builds, tests and releases run here instead of on GitHub's
# metered macOS runners. See docs/SELF-HOSTED-RUNNER.md.
#
#   ./scripts/setup-ci-runner.sh            install and start
#   ./scripts/setup-ci-runner.sh --status   show whether it is running
#   ./scripts/setup-ci-runner.sh --stop     stop (jobs wait until you start it again)
#   ./scripts/setup-ci-runner.sh --start    start again
#   ./scripts/setup-ci-runner.sh --remove   stop, unregister and delete it
set -euo pipefail

REPO="${INTERVIEWHACKER_REPO:-OmnixSI/InterviewHacker}"
DIR="${RUNNER_DIR:-$HOME/actions-runner-interviewhacker}"
NAME="${RUNNER_NAME:-$(scutil --get LocalHostName 2>/dev/null || hostname -s)-interviewhacker}"

say() { printf '\n\033[1m%s\033[0m\n' "$*"; }
fail() { printf '\n\033[31m%s\033[0m\n' "$*" >&2; exit 1; }

service() {
  [[ -x "$DIR/svc.sh" ]] || fail "还没有安装（先运行 $0）"
  (cd "$DIR" && ./svc.sh "$1")
}

case "${1:-}" in
  --status) service status; exit 0 ;;
  --stop) service stop; exit 0 ;;
  --start) service start; exit 0 ;;
  --remove)
    [[ -d "$DIR" ]] || fail "没有找到 $DIR"
    cd "$DIR"
    ./svc.sh stop >/dev/null 2>&1 || true
    ./svc.sh uninstall >/dev/null 2>&1 || true
    if [[ -f .runner ]]; then
      token=$(gh api -X POST "repos/$REPO/actions/runners/remove-token" -q .token 2>/dev/null || true)
      [[ -n "$token" ]] || read -r -p "粘贴移除令牌（仓库 Settings → Actions → Runners → 这台机器 → Remove 里显示的 --token 后那串）: " token
      ./config.sh remove --token "$token"
    fi
    cd "$HOME" && rm -rf "$DIR"
    say "已移除。测试数据在 ~/.interviewhacker-ci，可以手动删除。"
    exit 0 ;;
  "") ;;
  *) fail "未知参数：$1" ;;
esac

[[ "$(uname -s)" == Darwin && "$(uname -m)" == arm64 ]] || fail "需要 Apple Silicon（M 系列）Mac。"

say "1/5 检查工具"
xcode-select -p >/dev/null 2>&1 || fail "没有找到 Xcode。请从 App Store 安装 Xcode（16 或更新），打开一次同意许可，再重新运行。"
swift_version=$(swift --version 2>&1 | head -1)
echo "$swift_version"
[[ "$swift_version" =~ Swift\ version\ ([6-9]|[1-9][0-9])\. ]] || fail "需要 Swift 6（Xcode 16 或更新）。"
if ! command -v gh >/dev/null 2>&1; then
  if command -v brew >/dev/null 2>&1; then
    brew install gh
  else
    fail "发布需要 GitHub CLI：先安装 Homebrew（https://brew.sh），再运行 brew install gh；或从 https://cli.github.com 下载安装。"
  fi
fi
gh auth status >/dev/null 2>&1 || gh auth login --hostname github.com --git-protocol https --web

say "2/5 获取注册令牌"
token=$(gh api -X POST "repos/$REPO/actions/runners/registration-token" -q .token 2>/dev/null || true)
if [[ -z "$token" ]]; then
  echo "无法自动获取（需要这个仓库的管理员权限）。"
  echo "打开 https://github.com/$REPO/settings/actions/runners/new ，复制页面里 --token 后面那一串："
  read -r -p "令牌: " token
  [[ -n "$token" ]] || fail "没有令牌，已停止。"
fi

say "3/5 下载 GitHub Actions Runner"
mkdir -p "$DIR"
cd "$DIR"
if [[ ! -x ./config.sh ]]; then
  version=$(curl -fsSL https://api.github.com/repos/actions/runner/releases/latest | /usr/bin/python3 -c 'import json, sys; print(json.load(sys.stdin)["tag_name"].lstrip("v"))')
  echo "版本 $version"
  curl -fL --progress-bar -o runner.tar.gz "https://github.com/actions/runner/releases/download/v$version/actions-runner-osx-arm64-$version.tar.gz"
  tar xzf runner.tar.gz
  rm runner.tar.gz
else
  echo "已下载，跳过"
fi

say "4/5 注册到 $REPO（名称 $NAME）"
if [[ -f .runner ]]; then
  echo "已注册，跳过（要重新注册，先运行 $0 --remove）"
else
  ./config.sh --url "https://github.com/$REPO" --token "$token" --name "$NAME" \
    --labels interviewhacker --work _work --unattended --replace
fi

say "5/5 设为登录后自动运行"
./svc.sh install >/dev/null 2>&1 || true
./svc.sh start
mkdir -p "$HOME/.interviewhacker-ci"

say "完成"
cat <<EOF
这台 Mac 已经是 $REPO 的构建机，登录后自动在后台运行。
· 查看状态：$0 --status      暂停：$0 --stop      移除：$0 --remove
· 构建、测试和发布会在这里跑；测试数据在 ~/.interviewhacker-ci，不会动你已安装的 InterviewHacker。
· 合上盖子或睡眠时任务会排队等待，醒来后继续。插电时可在 系统设置 → 电池 → 选项 里打开“显示器关闭时防止自动睡眠”。
· 详细说明：docs/SELF-HOSTED-RUNNER.md
EOF
