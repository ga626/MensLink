# 定向验证与证据

当前只有开发构建，没有 Release、安装或宿主就绪声明。实际结果统一登记在 [当前状态](../../project_status/current.md)。

| 改动影响 | 最小检查 | 证明及限制 |
|---|---|---|
| 结构/规范/计划 | `python scripts/check_project.py` | JSON、路径、文档链接、任务依赖、基线 SHA；不证明运行能力 |
| C# 或依赖 | `scripts/dev.ps1 build` | 全部工程实际编译；包锁和依赖问题不能以代码截图代替 |
| 文字/存储/权限/查询 | `scripts/dev.ps1 test` | 合成输入的可重复断言；不推出中文总体召回或安全保证 |
| IPC/MCP | `scripts/dev.ps1 mcp-test` | 官方 SDK 客户端到 stdio 桥、管道、查询网关的真实调用；不是当前 Desktop 工具卡证据 |
| 用户命令调用链 | `scripts/dev.ps1 demo` | 从实际 CLI 完成预览、提交、查询和固定版本取回；不是完整插件/安装体验 |

依赖首次准备或有意修改锁文件后需要 restore；日常构建 `--no-restore`，检查程序 `--no-build`。不在每个提交钩子重建全部产品。PR Actions 的 `repository` 检查公开文件与结构，`foundation` 在 Windows runner 上执行锁定 restore、build、核心与 MCP 检查；不代表 Win11 客户端或真实宿主验收。

回执写入唯一 `local/checks/`、`local/mcp-checks/` 或 `local/demo/` 子目录，包含实际环境、通过/失败和未验范围。修复后标明前次失败和改变，不能覆盖旧失败为成功。交付前核对回执确实存在，并绑定当前源码/锁文件哈希；冻结后记录实际 Git SHA。公开 checkout 缺少本机历史台账时明确跳过，不伪造历史验收。

后续插件验收要真实宿主加载、版本/配对、用户编辑竞争和错误状态；PDF 要逐页定位；发布要同制品真实环境与远端摘要。设备暂缺可以记录待验，不能用 Windows Server CI 或模拟替代客户端兼容。
