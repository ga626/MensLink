# MensLink 当前状态

更新：2026-10-20。已完成S1工程骨架和本机文字闭环，并进入路线验证；没有可视原型或正式产品。[当前执行计划](../docs/plans/validation-prototype-github.md) 与 [实现合同](../docs/reference/implemented-contract.md) 区分规划和真实能力。

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

仓库为`ga626/MensLink`，公开、main、MIT、Issues与私密漏洞报告。Bootstrap首推只有.editorconfig、.gitattributes、.gitignore、LICENSE；治理PR建立公开入口、只读CI与保护，产品源码随后经独立PR接入。实时提交、PR及CI以GitHub和实际Git状态为准，不在本文虚构候选SHA或已合并状态。

## 未验与接续

Obsidian 1.14.4安装已定位。合成Vault与项目独立配置启动探针没有拿到插件通过回执，发现共享CLI管道冲突，任务测试窗口已关闭；日用会话保持。Node只读IPC成功不代替真实插件、管理配对、人改或离线重连。

V03–V06仍需真实插件通信、管理通道、人工编辑保护、持久任务和Codex原生接入。PDF/网页、语义质量、平台同步、安装升级、Win11/干净环境与用户体验仍未验；没有连接私人Vault、平台账号或改全局Codex配置。

下一步继续关键路线，再做Obsidian MD/TXT最小原型，给用户看效果后才扩大功能。
