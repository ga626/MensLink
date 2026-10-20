# 工程结构

本文件解释职责；机器检查入口为 [结构清单](config/project-structure.manifest.json)。仅创建实际使用的目录，规划中的目录不放空壳。

| 路径 | 职责与权威 |
|---|---|
| `modules/MensLink.Core/` | 文字读取/范围核查、Unicode 定位、预算与内部 IPC 合同 |
| `adapters/MensLink.Sqlite/` | 样机台账、不可变修订、FTS5、权限和发布过滤 |
| `apps/MensLink.Cli/` | 开发管理命令及只读管道服务；不承担完整插件界面 |
| `integrations/codex/MensLink.Mcp/` | 官方 SDK stdio 桥；只提供三个读取工具 |
| `tests/` | 核心与协议检查程序；`fixtures/` 只放合成公开样本 |
| `config/` | 工具链、结构及阶段任务清单；不含认证 |
| `scripts/` | 本项目实际可运行的引导、构建、演示与结构检查入口 |
| `docs/` | 公开产品范围、架构/ADR、开发/维护、验证计划和发布准备；历史研究仅本地保留 |
| `.github/` | Issues/PR 模板、只读权限 CI；实际分支保护在 GitHub 配置 |
| `project_status/current.md` | 当前实现、检查结果与下一步的单一入口 |
| `local/` | 忽略的 SDK、NuGet 缓存、研究原始数据、备份、演示库和回执 |

依赖方向：CLI → SQLite → Core；MCP → Core → 内部读管道 → CLI 读服务 → SQLite。Core 不知道 UI 或 SQLite；MCP 进程不打开数据库，不加载模型。

`bin/obj` 是生成物；包锁文件是源码审阅材料。Obsidian 插件、PDF/网页适配器、安装器和平台扩展尚未创建，待有实际实现再加入结构清单。程序安装根、正式内部数据根与 Vault 是未来运行时目录，不在源码树创建私人资料。

`scripts/check_repository.py` 审核受 Git 跟踪的公开文件；`scripts/check_project.py` 检查结构、链接、SDK 和依赖方向。`docs/adr/0003-lexical-boundaries.md` 记录跨块检索、中文输出预算及旧库增补合同。
