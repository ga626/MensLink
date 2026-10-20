# MensLink 当前状态

更新：2026-10-20。已完成S1工程骨架、本机文字闭环与若干路线探针，新增三栏只读开发预览；没有正式产品。[当前执行计划](../docs/plans/validation-prototype-github.md) 与 [实现合同](../docs/reference/implemented-contract.md) 区分规划和真实能力。

## 已验证

| 范围 | 结果与限制 |
|---|---|
| 开发环境 | Windows10 Pro 22H2 x64/build19045；项目私有.NET SDK10.0.402，runtime10.0.13；SQLite实际3.53.3/delete journal |
| 编译 | 六工程0警告0错误；NuGet版本与锁文件固定 |
| 核心 | 26/26：中文/代码、Unicode定位、修订、权限、预算、范围、旧引用、撤销、跨块及旧库补索引 |
| 真值集 | 首轮15项中3项跨块漏召回；修复后15/15。固定小样本，不宣称总体质量或性能 |
| MCP | 官方SDK客户端到stdio桥、管道与SQLite的8项检查通过；不是当前Codex Desktop工具卡证据 |
| CLI闭包 | 自包含技术样机约82.9MB/201文件，排除SDK命令PATH后启动；hostfxr/hostpolicy/coreclr/SQLite DLL均从样机目录加载 |
| 文档考古 | 原181文件保留；项目规则从1978字符压缩为约1100字符，详细流程移到对应文档 |

自包含样机绑定修改检索前的源码快照；它验证运行时闭包，不充当检索修复版本的制品身份。开发样机不是可安装候选，不发布、不激活。所有失败、命令、源码摘要和运行回执保留在忽略的本机目录；公开仓库以可复现检查说明范围。

## GitHub与分支

仓库为`ga626/MensLink`，公开、main、MIT、Issues与私密漏洞报告。Bootstrap首推只有.editorconfig、.gitattributes、.gitignore、LICENSE；治理PR建立公开入口、只读CI与保护，治理PR #1与产品源码PR #2已合并，实际保护要求repository与foundation。实时提交、PR及CI以GitHub和实际Git状态为准，不在本文虚构候选SHA或已合并状态。

## 未验与接续

Obsidian 1.14.4 的项目独立 profile 与合成 Vault 已完成真实插件加载、库/schema握手、search→fetch固定引用、错库拒绝、服务离线与重启重连。早期未启用社区插件的失败保留，不再将共享CLI管道冲突误作插件不可用结论。

真实宿主的安全存储探针通过合成凭据写入、无明文检查、应用重启后解密与删除；`vault.process` 的人改基线冲突与独立人工笔记保留也通过。以上为技术探针，管理配对协议及完整插件尚未实现。

SQLite持久任务模型7项通过，含取消/发布竞争、真实子进程提交前退出回滚、提交后退出幂等与worker租约；尚未接入产品Catalog真实导入。路线选择及落实顺序见 [ADR 0004](../docs/adr/0004-prototype-routes.md)。

V04–V06仍需落实管理配对、产品持久队列与Codex原生接入，不能写“所有路线已通过”。PDF/网页、语义质量、平台同步、安装升级、Win11/干净环境与用户体验未验；没有连接私人Vault、平台账号或改全局Codex配置。

用户已选择“三栏资料工作台”：左侧合成资料来源、中间真实命中、右侧固定版本片段及引用，继承 Obsidian 主题。新增原生 CommonJS 插件，无 npm 运行依赖；通过只读 CLI 调用 status/search/fetch，不扫描 Vault、不直写数据库。合成导入由显式开发启动器完成；配对与持久任务没有伪装成界面功能。见 [预览说明](../docs/user/development-preview.md)。

预览候选必须从干净 Git 身份生成，宿主加载与交互结果绑定本机回执和确切源码 SHA；不能只凭源码存在声称通过。先展示这个范围的效果并等待用户评审，期间只修当前缺陷，不扩大到 PDF、平台同步或安装。完整 V04–V06 仍按后续计划完成。
