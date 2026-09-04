# 研发工作流

## 任务进入

Issue 必须写明玩家价值、非目标、可观察验收条件、依赖、性能/存档/输入影响和验证方式。高风险跨模块能力先提交 RFC；确认后的长期技术决策转为 ADR。

跨工种 AI 生产任务使用 [生产任务表单](../.github/ISSUE_TEMPLATE/production.yml)，按 [AI 交接协议](Production/AI-HANDOFF.md) 记录候选、来源、人工审批与逐项验收证据。表单填写完整不代表审批或生产能力验证已完成。

## 开发循环

1. 从最新 `main` 创建 `feature/*` 或 `fix/*`，目标两天内合并。
2. 先建立最小端到端切片和失败路径测试，再扩展内容数量。
3. 提交前运行 `pwsh Tools/Workflow.ps1 check`。
4. PR 标题使用 Conventional Commits，填写证据、影响、风险与回滚。
5. Squash Merge；`main` 上的每个提交都必须可构建、可游玩。

## 自动门禁

`check` 依次验证 Git/LFS 策略、任务证据工具、候选快照保真、EditMode、内容与项目结构、固定种子平衡门和 PlayMode Bootstrap。PR CI 额外生成 Windows Development Player；每日 CI 保存 30 天内部构建。

正式标签触发一次性 IL2CPP 构建、manifest 与永久 Draft GitHub Release。Steam 只晋级这一不可变制品，不在发布环节重建。

## 内容与存档

- 内容引用使用稳定 ID；显示名、路径和 Unity Instance ID 不进入存档。
- 使用关卡场景工作流时，新关卡必须有 `LevelProductionProfile`；视觉小说节点和卡牌遭遇使用结构化目录，不要求各自创建场景。所有内容稳定 ID 保持唯一，视听交接见 [内容规范](Production/Content-Pipeline.md)。
- AI 内容不得直接修改 approved 运行时源；先用 `New-ContentCandidate.ps1` 创建隔离 draft 包，执行 `Review-ContentCandidate.ps1`，再由非生成者提交哈希绑定的人工批准后运行晋级脚本。
- 存档 schema 逐版本迁移，不允许跳版本；旧版、损坏版和未来版均要有测试。
- 玩家可见文本进入 Localization 表，代码和 Prefab 不硬编码正式文案。

## 完成与回滚

完成要求以 `AGENTS.md` 和 PR Definition of Done 为准。热修复从发布标签创建，但必须先回到 `main` 并通过完整门禁；Steam 回滚切换到上一稳定 Build ID，不重新打包旧源码。
