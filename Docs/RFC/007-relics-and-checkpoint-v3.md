# RFC-007：遗物战斗触发器与 Run checkpoint v3

- 状态：Accepted for implementation
- Owner：Gameplay / Core / Systems Design
- Reviewers：Narrative / Content / QA
- 最后更新：2026-09-02
- 关联 ADR：ADR-002

## 玩家价值

遗物让一次 Run 的选择持续改变后续战斗，是类《杀戮尖塔》构筑身份的重要来源。本切片让剧情选择授予遗物，并在后续 Boss 战开始时产生可见、可重放的规则效果。

## 目标

- 内容以稳定 ID 定义遗物、CombatStart 触发器和白名单效果。
- 剧情通过 `GrantRelic` 命令授予遗物；同一 Run 中遗物唯一且按获得顺序保存。
- Core 在第一回合建立后按遗物顺序执行战斗开始效果，并产生 `RelicTriggered` 及具体状态/格挡事件。
- 首批效果限定为 GainBlock 和 ApplyStatus；不执行任意脚本。
- Run checkpoint 升级至 v3，保存遗物 ID，并严格执行 v1→v2→v3 相邻迁移。
- UI 双语显示当前遗物，模拟器自然包含遗物效果。

## 非目标

- 本轮不实现遗物选择奖励、商店、稀有度、装备槽或触发栈。
- 不实现受伤、出牌、回合结束等反应式触发器；它们需要独立的优先级和循环预算 RFC。
- 样例遗物数值不代表正式平衡。

## 触发顺序

战斗执行 `CombatStarted → 洗牌 → 第一玩家回合/抽牌 → 按获得顺序触发遗物`。每件遗物先写 `RelicTriggered`，再按内容数组顺序写 BlockGained 或 StatusChanged。遗物效果全部完成后才接受玩家命令。

## 存档迁移

v3 新增 `relics`。v1 先补空地图字段成为 v2，再由 v2 补空遗物列表成为 v3；反序列化器接受历史 v1/v2 和当前 v3，拒绝未来版本。内容版本仍需精确匹配。

## 验收标准

- [x] 剧情命令授予存在的遗物，重复授予不产生重复实例。
- [x] 遗物在后续战斗开始按获得顺序触发，并改变实际战斗状态。
- [x] 缺失遗物/状态引用、非法触发器或效果阻断内容导入。
- [x] checkpoint v3 往返保留遗物；v1、v2 逐步迁移，未来版本被拒绝。
- [x] 双语 UI 显示遗物，确定性模拟仍为 0 卡死并通过宽门。
- [x] EditMode、内容门、PlayMode、完整 Workflow 和 Windows Development 构建通过。

## 回滚

移除遗物目录、GrantRelic、战斗开始修正器和 UI 投影，恢复 content schema v4 / vertical-slice.4；checkpoint v3 制品不能静默降为 v2。
