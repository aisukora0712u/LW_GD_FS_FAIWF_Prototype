# RFC-012：候选快照内容保真

状态：技术修复，本地验证通过（2026-09-04）；无玩法或创意决策。

目标：创建 draft 快照仅改变顶层版本字段及 status，不得改动剧情、来源、稳定 ID 或嵌套数据中的同名字段/版本字符串。现有脚本全文 Replace 会误改这些值，且目标版本含引号时可能破坏 JSON。

非目标：不新增内容，不晋级，不提供人工批准，不改变 schema、存档、依赖包或完整快照模式。JSON 缩进和转义形式允许变化，数据值必须保持。

验收与验证：

- 内容及本地化分别仅改 contentVersion/localizationVersion 与顶层 status。
- 保留同版本的正文、嵌套 status、数字、布尔、数组和 Unicode；带引号目标版本仍为合法 JSON。
- 拒绝非对象、缺失/重复顶层字段、源版本不匹配、非 approved 源及空/相同目标版本。
- 独立 PowerShell 回归覆盖以上条件和当前三份正式源；不写正式源、不模拟真人批准。测试接入 Workflow，全量 check 通过。

风险：快照格式会重新排版；新候选哈希按实际生成文件计算，既有候选不修改。版本命名的业务合法性仍由候选审阅负责。

验证证据：独立测试通过合成内容保真、10 类拒绝与三份正式源只读语义比较；实际 `New-ContentCandidate.ps1` 冒烟生成四份合法 JSON，正式源 SHA-256 前后不变。夹具位于 `Artifacts/candidate-copy-smoke-ae64f0d4cb75442eb280ed147d7603a4`，来源与负责人明确为 synthetic，不是批准或生产演练。四个相关 PowerShell 文件语法解析通过。`pwsh -NoProfile -File Tools/Workflow.ps1 check` 退出码 0，EditMode 71/71、PlayMode 1/1、证据测试及平衡门通过。未执行人工评审/晋级、远端 CI 或新 Player 构建；本次不改变 Unity 代码。

回滚：撤回转换函数、测试和创建脚本调用；移除 Workflow 对测试的调用。正式内容和存档不受影响。
