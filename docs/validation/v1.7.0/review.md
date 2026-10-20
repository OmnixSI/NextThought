# 独立审查与修复记录

独立只读审查检查了源码、任务隔离和生命周期；没有以静态审查替代真实运行。

首轮确认并修复：App 资源打包；HTML 长表完整行组与 rowspan；Office 取消后的进程终止与恢复；XLSX 展开预算；Office 图片的正文／slide／sheet 关联；Codex PDF 全页覆盖；无运行包 TXT 导入；共享 Markdown 引用的删除保护。

次轮确认并修复：login/version 的 Foundation waitUntilExit 竞争；MCP 图片 ID 与位置关联；运行包最后 suspension point 后检查取消；取消回答后后台联网决策的新请求。

最终复核确认以上修复落地，没有发现新的有具体证据的管线阻塞。另指出 workflow_dispatch 时 update.json 应记录实际 checkout 的 SHA，已将来源改为 git rev-parse HEAD。

实际运行证据另见主验证报告、各 selftest 日志与原始模型输出。
