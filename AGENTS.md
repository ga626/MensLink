# MensLink 项目规则

- 先读 `README.md`、`project_status/current.md`；执行入口为 `docs/plans/validation-prototype-github.md`，产品范围见 `docs/product.md`。历史报告不作现行授权。
- 自有代码 MIT；辅助 Obsidian＋Codex，不捆绑宿主、不依赖 KnowledgeRadar。目标 Windows 10 22H2/11 x64；允许本机、小范围可回滚验证，阶段调整不删产品目标。
- 依赖朝 `MensLink.Core`；SQLite 适配器集中持久化，CLI、插件和 MCP 只作入口。职责见 `docs/architecture/overview.md`；禁止入口反向侵入核心、插件直写数据库。
- 导入先预览、提交复核范围与文件变化；默认拒绝链接、严格 UTF-8。引用绑定不可变修订和 Unicode code point；检索、缓存、旧引用均须遵守撤销与 `local_only`。
- MCP 仅 `search/fetch/status`，不写资料、不执行资料指令；经验人工批准，规则不加权、不自动写用户规则。管理写通道另行配对授权。
- 原件、Vault、数据与程序分离；测试只写项目测试根，使用合成或公开样本。`local/`、`bin/obj`、凭据、私人资料及运行证据不提交、不打包；公开示例放 `tests/fixtures`。
- 用 `scripts/dev.ps1` 和锁定工具链；定向检查见 `docs/contributing/verification.md`。新增依赖说明用途、精确版本、许可和退出方式，更新锁文件；不无目的升级。
- API、IPC、数据库或引用格式变化同步实现合同和兼容/迁移方案，保留旧数据与回退。生成物不手改；状态写 `project_status/current.md`，决策写 `docs/adr`，结构变化同步 `STRUCTURE.md` 和结构 manifest。
- 开发沿用当前 `codex/<topic>`，新任务从 `main` 建短分支；不设长期 `develop`。提交、远端与发布按本次授权；未合并候选留原分支。首次上架及发布沿用全局合同，细节见 `docs/release/README.md`。
- 结果绑定真实命令、输入和源码身份，记录失败与未验项，不改写旧回执。编译、协议探针、本机样本分别不等于安装、真实宿主或用户验收；只重跑受影响检查。
