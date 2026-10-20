# ADR 0002：.NET 10 与单库词法基线

状态：接受为第一阶段试验基线；正式运行时、分发包和产品 schema 待验。

本机有 .NET 10.0.13 运行时，没有 SDK。官方元数据给出 10.0.402 SDK，使用项目私有安装并按元数据 SHA-512 核对。开发依赖锁定 Microsoft.Data.Sqlite 10.0.13、ModelContextProtocol 2.2.0 与 Microsoft.Extensions.Hosting 10.0.13；实际传递依赖由 `packages.lock.json` 记录。

| 方案 | 接口与依赖 | 测试、迁移和代价 |
|---|---|---|
| A：C# 核心＋SQLite＋官方 C# MCP SDK | CLI 管理；同用户只读管道桥接。与 v0.3 优先路线一致；本机补一个 SDK | 单事务可测版本与权限。需要维护 C#，产品打包需核原生 DLL 与运行时闭包 |
| B：TypeScript 服务＋Node 内置 SQLite／另装 SQLite 包 | 可与未来插件共享类型，但产品需要私有 Node；不能依赖用户当前 Node | 初始编译工具较少，最终仍需协议、原生依赖与安装维护；与已推荐 C# 路线切换没有已测收益 |

选择 A。先完成最小闭环，再用具体失败决定是否引入 QMD、PDF.js 或其他路线。避免为了对照长期维护两套样机。官方 MCP SDK 负责协议，内部 IPC 仅承载产品三种只读请求。

存储选择单 SQLite 事务，P1 使用默认 rollback journal，**不启用 WAL**；台账/索引拆分和 outbox 延到引入异步任务或外部索引时实现。实际 SQLite 版本在 status 和验证报告里读取；NuGet 包版本不能替代原生版本。此取舍规避当前样机不需要的 WAL 前提，不代表完成 SQLite 安全/分发审计。

FTS5 trigram 用于至少三字符字面匹配；一、二字符使用有限规模的精确子串基线。当前没有中文分词器、RRF、embedding 或相关性排名；繁简/别名仍是缺口。固定分块的跨界漏召回已按 [ADR 0003](0003-lexical-boundaries.md) 改为段落优先与有限重叠，结果仅覆盖所测字面场景。超过 1 万片段的短词请求拒绝，避免无界扫描。

回退接口是来源/修订/片段 ID、code point 定位和只读结果，不是底层 FTS rowid。未来更换索引时保留这些身份；当前数据库为实验 schema，未承诺跨版本迁移，不能导入私人长期资料。
