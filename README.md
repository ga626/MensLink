# MensLink

为人和 Agent 提供有来源、版本和定位的本地知识。产品以 Obsidian 为主界面，以 Codex 为首个 Agent 接入方；用户先自行安装两个宿主，MensLink 不捆绑宿主。

**当前是第一阶段开发骨架，没有安装包或正式版本。** 本机已搭建的范围与实际检查见 [当前状态](project_status/current.md)，执行顺序见 [验证与原型计划](docs/plans/validation-prototype-github.md)。Windows 10 22H2 x64 与 Windows 11 x64 是产品目标；支持情况只按真实回执填写。

## 本阶段可以做什么

- 对单个 MD/TXT 文件做元数据预览，再确认导入；当前只支持严格 UTF-8。
- 保存独立来源和不可变文字修订，进行中文短词/字面检索，取回带字符及行号定位的文字。
- 用只读管道服务和官方 SDK 的 MCP 桥提供 `search/fetch/status`；默认私人来源不返回给 Agent。
- 对重复导入、版本变化、范围、预算及撤销进行本机合成样本检查。

当前没有 Obsidian 插件 UI、PDF/网页解析、语义检索、平台同步、安装升级、正式记忆与规则模块。它们仍在产品范围中，按后续阶段实现。完整计划中的接口字段不代表当前程序已经支持。

## 本机开发

开发机需要 Windows x64、PowerShell 和 .NET SDK。SDK 可装在本项目 `local/`；普通用户未来安装产品不需要 SDK、Python、Node 或 KnowledgeRadar。

```powershell
# 本机已经准备好项目 SDK 时跳过此步；首次下载可传 -ProxyUrl。
.\scripts\bootstrap.ps1
.\scripts\dev.ps1 restore
.\scripts\dev.ps1 build
.\scripts\dev.ps1 test
.\scripts\dev.ps1 mcp-test
.\scripts\dev.ps1 demo
```

`restore` 使用锁文件。演示只读取 [合成资料](tests/fixtures/knowledge.md)，每次生成独立 `local/demo/` 测试库，不使用个人 Vault 或账号。`mcp-test` 使用官方 SDK 客户端测试本产品桥与读服务；不自动配置 Codex，也不证明 Codex Desktop 原生接入已验。

具体命令与未支持字段见 [实现合同](docs/reference/implemented-contract.md)。工程规则见 [AGENTS.md](AGENTS.md)，协作说明见 [CONTRIBUTING.md](CONTRIBUTING.md)，架构与取舍见 [文档索引](docs/README.md)。

## 数据与许可

源文件不移动、不改写。运行数据只进入显式指定的数据根；开发演示数据位于忽略的 `local/`。将片段交给远端 Agent 也属于外发，CLI 导入默认 `local_only`，只有显式 `--allow-agent` 的来源才经 MCP 返回。[隐私与安全边界](docs/user/privacy.md)。

自有代码采用 [MIT](LICENSE)。第三方包及运行时有各自许可证，锁定版本与分发审查见 [维护台账](docs/maintainers/dependencies.md)。历史研究与本机证据留在本地。问题反馈见 [支持入口](SUPPORT.md)，漏洞使用 [安全入口](SECURITY.md)。当前没有正式下载或 Release。
