# D1/D6/D8 截图对照

本目录由当前 Windows 构建生成：

```powershell
# 1. 启动演示模式并截图（中文/英文 × 浅色/深色，31 张）
NextThought.exe --demo --screenshots=<临时目录>

# 2. 把截图按视图复制到本目录，并生成 Mac/Windows 并排图
#    对照源：docs/screenshots/v3/home.png、docs/screenshots/v3/home-dark.png、
#            docs/screenshots/history-light.png、materials-light.png、settings-simple-light.png、
#            docs/validation/v2.4.1/live-session-ui/synthetic-component-samples-light-1200.png
```

- `<view>/light.png`、`dark.png`、`en-light.png`、`en-dark.png`：Windows 当前截图
- `compare-home-light.png`、`compare-home-dark.png`：Mac v3 首页与 Windows 并排
- `compare-controls-light.png`：Mac 组件样张与 Windows 组件样张并排
- 其余 compare-*.png 为可直接对应的页面

**现状**：已生成 37 张证据；首页/历史/资料库/设置/组件样张已有并排图，但差异明显（标题行、首页场景选择器、卡片布局），D1/D6/D8 仍未完成。
