# 三栏资料工作台开发预览

这是给原型评审使用的只读切片，尚无 Release 或普通用户安装包。插件清单的 0.1.0 对应当前 0.1.0-dev 开发系列，不表示已公开发版。仅用于 Windows 桌面版 Obsidian 的合成测试 Vault，不接入日用 Vault。

## 操作范围

- 左栏：本次合成导入回执中的来源。筛选只作用于当前返回的命中，清单不是实时全库枚举。
- 中栏：真实 SQLite 字面检索，最多返回 8 个片段；超预算/数量时明确提示部分结果。
- 右栏：通过完整 source/revision/chunk 身份重新读取的固定版本片段、行号、Unicode 字符范围、来源及摘要；不是整篇资料阅读器。
- 复制：复制完整 `menslink:` 引用前再取一次同一固定版本，拒绝已撤销资料；不自动插入或修改用户笔记。

不提供导入、授权、撤销或任务按钮。合成样本由开发启动器显式预览并确认导入，全部默认 local_only；没有后台扫描或向 Agent 发片段。管理配对、产品持久队列、人工笔记写入与 Codex 原生接入仍待实现。

## 准备与启动

开发机需已安装 Obsidian、Python 3.12+，并按 README 准备项目 SDK 与 NuGet 缓存。界面本身使用宿主的 Node API，无需 npm 安装。插件没有新增第三方包或外部资产。

在已提交且受跟踪文件干净的分支执行：

```powershell
python scripts/package_preview.py --label workbench-review-1
python scripts/start_preview.py --candidate local/obsidian-preview/candidates/workbench-review-1 --obsidian '<已安装的 Obsidian.exe>'
```

第一步只从该提交导出，锁定离线 restore 并构建 framework-dependent CLI；不改项目锁。仅本次关闭 NuGet 在线漏洞审计，不形成漏洞审计结论。输出 candidate.json 记录确切 Git SHA 与全部制品 SHA256。第二步逐文件核验后在 `local/obsidian-preview/sessions/` 建新的 profile、Vault 和数据库，记录启动参数与 PID。不要把这些运行目录提交或分发。

在新测试 Vault 允许社区插件后，从命令面板执行“MensLink 开发预览：打开资料工作台”。可以检索“引用”“版本”“本地”，切换来源，打开命中并复制引用。关闭该测试实例即可结束；退出不影响日用 Obsidian。省略 `--obsidian` 只准备文件。`--debug-port` 与 `--hidden` 仅供本机自动验收 helper，不用于日用实例。

插件启动会复核自身与 CLI 的制品摘要、Vault 路径、协议及库身份；读取操作逐字传参，`shell=false`，超时与输出有上限。插件关闭时取消自己的子进程，不结束其他宿主或服务。CLI 在复制期间再次取回，但操作系统剪贴板与数据库无法构成单一事务；不能承诺复制后发生的撤销会收回已经交给人的内容。

## 验收与停止点

真实宿主检查绑定 candidate manifest、源码 SHA、测试 profile/Vault 和进程。覆盖加载、查询、固定版本、复制、无结果、Unicode 上限、撤销、错误恢复和窄窗口；命令/截图/回执保存在本机。测试只读取刚写入的合成剪贴板值，不收集先前用户剪贴板。

本机 Windows 10 检查不证明 Win11、全主题兼容、干净安装或完整产品安全。静态截图不证明用户审美通过。先让用户评审这个范围的界面，再决定功能调整；正式发布仍遵守 [发布合同](../release/README.md)。
