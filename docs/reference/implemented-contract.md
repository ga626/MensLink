# 第一阶段实现合同

协议标识 `menslink-p1/v1`，数据库标识 `p1-v1`。这是当前程序的实现子集；[完整产品范围](../product.md) 仍有未实现内容，不将规划字段当作现行合同。

## CLI

统一入口为 `scripts/dev.ps1 cli <command> ...`，所有命令显式指定 `--data-root`。

| 命令 | 额外参数 | 当前行为 |
|---|---|---|
| `init` | 无 | 只在空数据目录建实验库；已有文件就拒绝 |
| `preview` | `--root <获准根> --file <单文件>` | 只返回路径、大小和 mtime，不读正文；仅 MD/TXT |
| `import` | `--manifest <预览JSON>`；可选 `--allow-agent` | 复核范围与版本，严格 UTF-8，规范化 LF，原子保存修订、片段、索引和指针 |
| `status` | 无 | 本地库状态、实际 SQLite、能力与 journal 模式 |
| `search` | `--query <字面查询>`；可选 `--max-chars` | 查询当前已发布修订；本地 CLI 可读该库 local_only 来源 |
| `fetch` | `--source-id --revision-id --chunk-id`；可选 `--max-chars` | 按完整身份取固定修订；缺失不替换成新版本 |
| `withdraw` | `--source-id` | 立即撤销可见性并提高策略/索引代际；不删原件 |
| `serve` | 无 | 启动指定库的同用户只读 Named Pipe；Ctrl+C 停止 |

限制：单文件最多 8MiB；UTF-8 BOM 可读，UTF-16/错误 UTF-8 及空白正文拒绝。路径/祖先目录 reparse point 拒绝；读文件期间只共享读取，并核对 Windows 最终句柄落点。预览不隐含目录订阅。再导入不会解除撤销或扩大既有授权，变更授权的 UI/合同尚未实现。

新片段按段落/换行优先，最多 1200 code points，重叠 499，以覆盖最多 500 code point 的跨块字面查询。旧库再次确认导入时只补缺少范围，不删除旧片段或改变已有引用；有新增片段才增加索引代际。旧来源不自动扫描升级；相同正文重复导入已补齐后保持幂等。见 [ADR 0003](../adr/0003-lexical-boundaries.md)。

## MCP 与内部读取

`menslink-mcp` 使用官方 SDK stdio；stdout 仅协议，日志到 stderr。只桥接已启动读服务，不自动启动服务、索引、模型或扫描。管道命名由合法库 UUID 构成，限制同用户并设置 5 秒超时及帧大小上限；不监听网络端口。

| 工具 | 当前参数 |
|---|---|
| `status` | `library_id` |
| `search` | `library_id, query, limit=8, max_chars=6000` |
| `fetch` | `library_id, source_id, revision_id, chunk_id, max_chars=8000` |

与完整产品合同的差异：未实现 `scope_ids/mode/required_layers/include_history/cursor` 及 locator 联合输入；没有能力协商、配对 UI 或完整错误分类。只支持本阶段工具 schema，不能把未声明字段传给下层当作有效过滤。未知内部 IPC 字段拒绝；外层通过官方 SDK 请求上下文检查参数名，未知参数返回 `invalid_argument`。

搜索 limit 为 1–20，query 为 1–500 code points；search 预算 1024–20000，fetch 预算 1024–24000。紧凑 JSON 使用支持 Unicode 的安全 encoder，按实际序列化字符数核算，UTF-8 字节不超过预算四倍；放不下完整命中返回 `budget_too_small`。第一阶段减少命中数而不删除出处；尚无分页续取，`truncated=true` 表示需收窄查询或增大预算。

命中绑定 `source_id/content_version_id/revision_id/artifact_id/chunk_id`，含 `source_uri/captured_at/artifact_sha256/locator/text_kind/coverage/citation`。locator 的字符区间零起、半开、按 Unicode code point；行号一起；指向规范化不可变 artifact，不是原件字节。结果为候选证据，分数、摘要可信度和整体覆盖不能由是否命中推导。

典型错误：`outside_scope`、`links_not_allowed`、`changed_since_preview`、`encoding_not_supported`、`empty_text`、`invalid_argument`、`policy_change_requires_review`、`source_withdrawn`、`not_authorized`、`version_not_found_or_not_authorized`、`budget_too_small`、`service_unavailable`、`index_unavailable`。

本阶段未提供正式安装包、Codex 自动注册或私人库接入说明。开发演示与协议探针的真实回执见 [当前状态](../../project_status/current.md)。

## Obsidian 开发预览

`integrations/obsidian` 在独立合成 Vault 中提供三栏工作台，通过 framework-dependent CLI 执行人的 `status/search/fetch`。不改变 MCP 的 Agent 权限过滤；不接入管理写通道。来源清单来自启动器的合成导入回执，来源筛选只作用于当前已返回命中，详情为固定版本的单个片段。复制前重新 fetch，资料正文只写入 textContent。启动、制品摘要及验收边界见 [开发预览](../user/development-preview.md)。
