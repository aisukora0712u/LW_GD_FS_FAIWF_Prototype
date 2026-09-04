# RFC-006：战斗状态效果与确定性触发顺序

- 状态：Accepted for implementation
- Owner：Core Gameplay / Systems Design
- Reviewers：Content / UI / QA
- 最后更新：2026-09-01
- 关联 ADR：ADR-002

## 玩家价值

只有直接伤害、格挡和抽牌不足以支撑接近《杀戮尖塔》的牌组组合。Strength、Weak 与 Vulnerable 提供最小的跨卡牌协同语言，同时保持规则可读、可预测和可批量模拟。

## 目标

- 内容用稳定 status ID 声明状态定义，卡牌效果只能引用已注册状态。
- Strength 永久叠加并增加来源的每次伤害。
- Weak 令来源伤害变为 75%（向下取整）；Vulnerable 令目标受到伤害变为 150%（向下取整）。
- 修正顺序固定为：基础伤害 + Strength → Weak → Vulnerable → Block → Health。
- 玩家回合结束时衰减玩家 Weak 与敌人 Vulnerable；敌方阶段结束时衰减敌人 Weak 与玩家 Vulnerable。Strength 不自动衰减。
- 应用和衰减均产生结构化事件；UI 双语显示非零状态层数。

## 非目标

- 本轮不实现毒、能力牌、遗物触发器、反应式效果栈或敌人技能牌。
- 不支持内容自定义触发时机或任意脚本。
- 数值是垂直切片回归样例，不是正式平衡决定。

## 确定性与安全预算

单张牌按 JSON 中效果顺序执行。状态层数必须为正，叠加使用 checked arithmetic；所有修正只做整数运算。状态不能递归发出新效果，因此当前不存在触发无限循环。未来加入触发栈前必须另立 RFC，规定优先级、深度和事件预算。

## 内容与存档

内容目录升为 schema v4，新增 `statuses`，卡牌效果新增 `ApplyStatus` 和 `statusId`。状态本身是战斗瞬时状态，因此本 RFC 当时未改变 checkpoint；后续 RFC-007 因持久遗物把 Run checkpoint 升为 v3。

## 验收标准

- [x] Strength、Weak、Vulnerable 按规定顺序改变伤害与格挡结算。
- [x] 状态叠加、阶段衰减和事件顺序确定且有规则测试。
- [x] 非法、缺失或不匹配 status ID 阻断内容导入。
- [x] 至少一张双语样例牌在可玩流程中应用状态，UI 显示层数。
- [x] 相同种子模拟报告一致，平衡门没有卡死。
- [x] EditMode、内容门、PlayMode、完整 Workflow 和 Windows Development 构建通过。

## 回滚

移除状态定义、ApplyStatus 分支、样例牌及 UI 投影，恢复 content schema v3 与 vertical-slice.3；存档 schema 无需回滚。
