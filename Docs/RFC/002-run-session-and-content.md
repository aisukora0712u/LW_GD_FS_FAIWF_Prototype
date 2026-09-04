# RFC-002：垂直切片 Run 会话与版本化内容目录

- 状态：Accepted for implementation
- Owner：Gameplay / Content Pipeline
- Reviewers：Design / Narrative / Engineering / QA
- 最后更新：2026-09-01
- 关联 RFC：RFC-001

## 背景与玩家价值

卡牌战斗与叙事状态机已经能独立运行，但玩家还不能经历一条连续流程，内容作者也没有可版本化入口。本 RFC 建立最小 Run 会话，使叙事命令可以启动遭遇、发放卡牌和改变资源，并让战斗胜利进入奖励再返回叙事或结束。

## 目标

- 用 Gameplay 层编排 `NarrativeStateMachine`、`CombatState`、牌组、资源和奖励。
- 建立带 `schemaVersion`、`contentVersion` 和审批状态的 JSON 内容目录。
- 在载入时验证 ID 唯一性、引用完整性、效果白名单、叙事目标和最小数值边界。
- 支持安全检查点的 JSON 往返与内容版本拒绝。
- 用一个小型内容包贯通“叙事选择 → 战斗 → 三选一奖励 → Run 完成”。

## 非目标

- 本轮不制作正式 UI、美术、音频、地图或最终文案。
- 不实现战斗逐动作存档；异常恢复回到战斗开始前的安全检查点。
- 不实现多个章节、商店、遗物、状态效果或 Meta 解锁。
- 内容包仅作为管线样例，不代表最终创意、数值或容量承诺。

## 会话状态

`NotStarted → Narrative → Combat → Reward → Narrative/Completed`，玩家死亡进入 `Defeat`。只有 `Narrative`、`Reward`、`Completed`、`Defeat` 可以生成检查点；`Combat` 返回明确拒绝。

Run 根种子派生 `combat.N` 与 `reward.N` 命名流。加载检查点后，相同遭遇序号仍产生相同战斗洗牌和奖励候选。

## 内容格式

源文件保存卡牌、敌人、遭遇、故事节点、初始牌组、奖励池和垂直切片入口。玩家可见文本只保存本地化键。`approved` 内容才能进入运行时目录；`draft` 允许校验和预览，但构建门应拒绝。

## 存档兼容

检查点包含 schema、内容版本、根种子、阶段、当前节点、叙事变量、牌组、生命、资源、奖励候选和遭遇序号。载入时要求 schema 与内容版本精确匹配；正式兼容迁移在格式稳定后另立 RFC。底层 `ISaveService` 继续负责 envelope、校验和、备份与原子提交。

## 验收标准

- [x] 样例内容可以解析为 Core 定义且所有引用有效。
- [x] 选择叙事选项后启动确定性战斗。
- [x] 战斗胜利生成确定性奖励，领取后更新牌组并完成 Run。
- [x] 战败进入 Defeat，不能继续执行叙事或领取奖励。
- [x] 非当前阶段命令拒绝且不改变状态。
- [x] 检查点 JSON 往返保留牌组、变量、资源、节点、奖励和种子。
- [x] 内容版本不一致和 Combat 中保存被明确拒绝。

## 验证

- EditMode：内容解析/非法引用、完整成功路径、战败路径、命令阶段保护、检查点往返和确定性。
- PlayMode：现有 Bootstrap 冒烟保持绿色；UI 接入后扩展玩家路径。
- 全量：`pwsh Tools/Workflow.ps1 check`。

## 风险与回滚

JSON DTO 是 v1 草案，只允许加法式演进。尚无发行存档，因此可以删除新增 Gameplay 文件和样例内容回滚。进入公开试玩前必须冻结 schema 并建立逐版本迁移测试。
