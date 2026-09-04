# RFC-011：叙事拓扑内容门禁

- 状态：技术实现及本地验证完成；不新增叙事设定或玩法数值
- 关联：ADR-002、RFC-008

## 目标与非目标

批量生成的视觉小说内容必须从声明入口可达，并且每个节点在结构上存在通向终止节点的路径。当前只校验跳转目标存在，不能发现孤立分支、空的非终止节点或无出口循环。

新增纯 C# 图检查，接入共享 ContentCatalog 构造链，使运行时导入和 draft 候选审阅使用相同规则。入口为 startNodeId 与所有 Story 路线的 payloadId；多个入口合法。允许具有出口的循环，不禁止条件分支。

非目标：不求解变量条件，不证明所有选择可满足、不自动改写故事、不保证玩家必然离开有出口循环。拓扑通过不能代替变量路径测试和人工通读。

## 验收

- AC-01：报告不可达节点和无终止路径节点的稳定 ID。
- AC-02：多入口和有出口循环被接受；封闭循环、空非终止节点被拒绝。
- AC-03：approved 导入与 draft 审阅都拒绝同一非法拓扑。
- AC-04：使用迭代遍历，避免长篇叙事的递归栈风险；结果排序稳定。
- AC-05：EditMode、完整 Workflow 与 Windows Development 构建验证。

## 兼容与回滚

不改变 JSON 字段、内容版本或存档格式；此前可导入的坏拓扑会被拒绝，作者需要修复或显式声明入口。回滚仅撤回图检查、目录接入及其测试，不自动迁移或删除内容。

## 验证证据（2026-09-04）

AC-01/02：`Topology_AcceptsMultipleEntriesAndCycleWithExit` 与 `Topology_RejectsClosedCycleAndEmptyNonterminalDeterministically` 验证稳定错误、合法多入口/循环和非法死路。

AC-03：`NarrativeTopology_IsEnforcedForApprovedAndReviewContent` 将实际样例跳转改为循环，确认两种解析入口都拒绝由此孤立的故事节点。

AC-04：`Topology_LongNarrativeUsesIterativeTraversal` 实跑一万节点；倒序输入对照验证错误排序稳定。算法以正向与反向迭代遍历计算可达性，不递归、不执行剧情命令。

AC-05：`pwsh Tools/Workflow.ps1 check` 通过（EditMode 71/71、PlayMode 1/1、证据工具测试、内容校验、200 种子 0 卡死）；Windows Development 构建成功。原始证据位于 `TestResults/`、`Artifacts/balance-report.json`、`Logs/build-Development.log`。这只证明本切片的本地门禁，不证明全栈生产目标或语义分支覆盖已完成。
