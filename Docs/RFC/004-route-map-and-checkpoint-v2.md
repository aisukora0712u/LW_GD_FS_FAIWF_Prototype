# RFC-004：路线地图、多遭遇与存档 v2

- 状态：Accepted for implementation
- Owner：Gameplay / Content / QA
- Reviewers：Design / Narrative / Engineering
- 最后更新：2026-09-01
- 关联 RFC：RFC-001、RFC-002、RFC-003

## 背景与玩家价值

当前垂直切片只有单段剧情和单场战斗，无法验证类 Roguelike 的路线决策、连续遭遇、Boss 收束及跨节点资源延续。Run 安全存档也没有路线位置，不能在扩展后可靠恢复。

## 目标

- 用稳定 ID 定义有向路线图，节点类型为 Story、Combat 或 Boss。
- 玩家只能选择当前节点声明的后继节点；完成节点后返回地图，最终节点完成 Run。
- 剧情变量、牌组、生命、资源和确定性随机流跨节点延续。
- 样例内容提供分支、汇合、多场普通遭遇和最终 Boss。
- Run checkpoint 升级到 schema v2，保存当前路线节点与已访问节点，并逐版本迁移 v1 JSON。
- 内容目录升级到 schema v2，显式加入路线入口和节点集合；本地化格式保持 v1。

## 非目标

- 本轮不实现程序化地图生成、地图动画、营火、商店、遗物或正式平衡。
- 不承诺 contentVersion 不同的存档可恢复；schema 迁移与内容兼容是两个独立门禁。
- 地图布局坐标暂不进入规则层，Presentation 只投影可选后继节点。

## 领域与内容契约

`RouteNodeDefinition` 属于 Gameplay 内容模型。Story 节点引用 story ID，Combat/Boss 节点引用 encounter ID；所有后继必须存在，非最终节点必须至少有一个后继，最终节点不得有后继。目录必须存在唯一入口，且所有路线节点从入口可达。

Run 的路线状态由 `CurrentMapNodeId`、`VisitedMapNodeIds` 和目录拓扑共同决定。随机流继续使用 `combat.N` 与 `reward.N` 命名，保证相同根种子和相同路线产生相同结果。

## 存档迁移

schema v2 新增 `currentMapNodeId` 与 `visitedMapNodeIds`。反序列化器只执行相邻版本 v1→v2：旧字段原样保留，新路线字段为空。恢复阶段仍要求 `contentVersion` 与当前目录一致；因此旧垂直切片内容版本的存档会被明确拒绝，而不是猜测映射。

## 验收标准

- [x] 启动 Run 进入地图并只显示入口节点。
- [x] 完成故事或领取战斗奖励后解锁声明的后继节点。
- [x] 分支可汇合，最终 Boss 奖励后进入 Completed。
- [x] 越权、重复或未知地图节点选择被拒绝。
- [x] schema v2 往返恢复路线状态；v1 JSON 可迁移且未来版本被拒绝。
- [x] 双语本地化、内容引用校验、EditMode、PlayMode 和 Windows Development 构建通过。

## 风险与回滚

主要风险是状态迁移和节点完成边界。通过仅在 Map、Narrative、Reward、Completed、Defeat 保存，以及覆盖分支/恢复/非法选择的测试控制风险。回滚时恢复 v1 内容版本与旧 RunSession/Checkpoint；已产生的 v2 存档需由产品明确提示不兼容，不允许静默降级。
