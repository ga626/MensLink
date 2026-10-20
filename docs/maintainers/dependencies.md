# 依赖、工具链与维护

2026-10-20 第一阶段：版本来自当次实际读取的官方 release metadata 与 NuGet index；原始响应在 `local/foundation/2026-10-20/`。产品分发审查未完成。

| 依赖 | 锁定 | 用途 | 维护/退出边界 |
|---|---|---|---|
| .NET SDK | 10.0.402；`global.json`、`config/toolchain.json` | 仅开发，项目私有目录 | 官方 SHA-512；不改系统 PATH；不是普通用户前提 |
| .NET 运行目标 | net10.0；本机实际 patch 记录在检查回执 | C# 程序 | 正式 self-contained 包另锁 patch/摘要；上游支持与本产品实测分开 |
| Microsoft.Data.Sqlite | 10.0.13 | 台账和 FTS5 基线 | `packages.lock.json` 记录传递依赖；status 读实际 native SQLite |
| ModelContextProtocol | 2.2.0 | 官方 stdio 服务及检查客户端 | Apache-2.0 官方资料，正式分发仍核冻结包 LICENSE/NOTICE |
| Microsoft.Extensions.Hosting | 10.0.13 | MCP 进程生命周期及 stderr 日志 | 核锁文件和分发义务，不借宿主认证 |
| Python | 本机现有；非产品依赖 | 文档/结构检查和本轮证据整理 | 标准库，无运行时导入；普通用户产品不携带 |

没有引入 QMD、Node/Python 产品运行时、OCR/ASR、浏览器扩展或云模型。开发检查不调用付费 API。

首次公开前应由实际包/二进制生成 SBOM，核源代码与原生传递组件、运行时、安装器的 LICENSE/NOTICE 和漏洞记录。当前 NuGet 锁文件只证明依赖解析身份，不等于已完成安全或再分发审计。不要复制其他项目的依赖清单或以自有 MIT 覆盖第三方许可。

更新依赖应明确解决的问题、受影响合同、原生版本、lock 变化及最小验证；不要无目的追最新。引入 WAL 或外部检索引擎前重新核 SQLite 修复与并发合同。没有产品安装包时，不编造 artifact digest、签名或安装成功回执。
