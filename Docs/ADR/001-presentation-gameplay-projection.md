# ADR-001：Presentation 直接投影视图所需的 Gameplay/Core 只读状态

- 状态：Accepted
- 日期：2026-09-01
- 决策者：Engineering
- 关联 RFC：RFC-001、RFC-002、RFC-003

## 背景

垂直切片界面需要读取 Run 阶段、叙事节点、手牌、敌人、奖励和稳定内容 ID，并把按钮操作转换为 Gameplay 命令。若通过 Foundation 复制一套 UI DTO，会在规则仍快速迭代的阶段制造重复模型；若把视图状态放进 MonoBehaviour，则破坏领域状态唯一来源。

## 决策

`Game.Presentation` 可以直接引用 `Game.Gameplay` 与 `Game.Core`，但只允许读取公开状态并调用命令方法。Presentation 不得修改集合、持有第二份可写领域状态或绕过 RunSession 调用 Infrastructure。运行时内容、本地化和存档仍由 Gameplay/Infrastructure 负责。

当前使用程序化 uGUI 构建原型界面，避免垂直切片阶段维护大量易冲突的 Prefab。进入视觉定稿前可以把视图替换为 Prefab/UI Toolkit，只要保持 Presenter 的单向投影边界。

## 候选方案

- Foundation UI DTO：层次最严格，但当前每次规则变化都需要同步 DTO 与映射，暂不采用。
- 全局事件总线：耦合隐蔽且生命周期难验证，拒绝。
- Presentation 自行维护战斗/剧情状态：产生双重真相，拒绝。

## 后果

收益是端到端接入快、状态一致、可用 PlayMode 直接验收。代价是 Presentation 对领域 API 的编译依赖；领域 API 变化会明确触发 UI 编译失败。未来若存在多个前端或远程客户端，再评估稳定查询 DTO。

## 验证与复审

PlayMode 必须证明 Bootstrap 可创建 Presenter、本地化切换和叙事到战斗转换。若 Presentation 开始出现规则分支、存档调用或大量映射重复，则重新评估 DTO 边界。
