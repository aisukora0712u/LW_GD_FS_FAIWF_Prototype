# 技术架构

## 设计目标

架构服务于三件事：规则可测试、内容可扩展、表现可替换。卡牌战斗和叙事都应能在不启动完整 Unity 场景的情况下验证核心状态转换。

## 模块拓扑

```text
Presentation ───────→ Foundation ←────── Infrastructure
      │                    ↑                    ↑
      └──────→ Gameplay ───┘                    │
                    │                           │
                    └────────→ Core ←───────────┘
```

- **Core**：值对象、实体、规则、确定性随机、战斗/叙事状态机、命令与事件。保持无 Unity 引用。
- **Foundation**：跨模块接口、配置契约、稳定 ID 和结果类型。
- **Gameplay**：运行会话、战斗、牌组、地图、事件、对话等用例的编排。
- **Presentation**：UI Toolkit/uGUI、动画、音效、镜头和可访问性呈现。
- **Infrastructure**：存档、平台、资源、场景、遥测和外部工具适配器。
- **Content**：只保存声明式内容与导入结果，不承载运行时规则代码。

## 玩法边界

建议按可组合 feature 划分，而不是按“场景”堆脚本：

| Feature | 输入 | 输出 |
| --- | --- | --- |
| Run | 新游戏/读档/种子 | RunState、路线进度 |
| Combat | 队伍、牌组、遭遇、种子 | 战斗结果、事件日志 |
| Cards | CardDefinition、目标、费用 | 领域命令与效果 |
| Narrative | StoryNode、条件、选择 | 变量变化、后续节点 |
| Rewards | 战斗/事件结果、经济规则 | 候选奖励与选择结果 |
| Meta | 账号进度、解锁条件 | 可用内容集合 |

Feature 之间通过命令、查询和领域事件通信，不直接持有彼此的 MonoBehaviour。

## 数据与身份

- 所有可引用内容使用稳定、不可复用的字符串 ID，例如 `card.iron_strike`。
- 定义数据与运行时状态分离。定义可缓存共享，状态必须拥有独立生命周期。
- 存档保存 ID、数值状态、内容版本、schema 版本和随机状态，不序列化 Unity 对象图。
- 内容删除先弃用至少一个兼容周期；迁移器负责旧 ID 映射。
- 对话条件和卡牌效果优先使用受限的声明式操作集合，避免在内容中执行任意代码。

## 确定性与可调试性

每个 Run 记录根种子；地图、战斗、奖励等使用命名随机流。所有会改变领域状态的操作生成结构化事件日志。Bug 报告应能携带构建号、内容版本、种子、节点/遭遇 ID 和最近事件，以便重放。

当前纯 C# 领域入口：

- `ContentId`：发布后稳定的小写分段身份。
- `NamedRandomStreams`：从 Run 根种子派生命名流，流之间互不消耗状态。
- `CombatState`：声明式卡牌定义驱动的战斗命令、状态与事件。
- `NarrativeStateMachine`：节点、条件选择、故事变量与受限跨玩法命令。
- `ContentCatalogJson`：把版本化、已批准的 JSON 内容转换为 Core 定义，并拒绝非法引用与缺失溯源信息。
- `RunSession`：在 Gameplay 层编排叙事、战斗、奖励、牌组和资源。
- `RunSaveCoordinator`：将安全阶段检查点交给 `ISaveService`，保持业务序列化与物理存储解耦。
- `LocalizationCatalogJson`：验证批准状态、版本、重复键以及必需 locale 的完整覆盖。
- `VerticalSlicePresenter`：把 RunSession 只读状态投影到 uGUI，并把输入转换为 Run 命令；不拥有领域状态。

Core 不负责加载资源或切换场景。Gameplay 层消费 `StartCombat`、`GrantCard`、`ChangeResource` 等叙事命令，并把对应结果重新交给表现层。

RunSession 的存档边界是叙事、奖励、完成与战败。战斗中拒绝生成新检查点，调用方保留 `LastSafeCheckpoint`，恢复后以同一 Run 种子重新进入安全边界；逐动作战斗恢复不属于 v1。

## 性能预算

在垂直切片阶段建立基线，而不是最后优化：目标平台、帧率、内存、加载时长、存档时长与包体预算写入对应 RFC。任何 Addressables 分组策略变化都需要测量冷启动、峰值内存和失败恢复。

## 架构决策

满足以下任一条件时创建 ADR：新增包或服务、改变模块依赖、改变内容格式、改变存档兼容策略、引入代码生成、改变场景/资源加载模型。使用 `Docs/ADR/000-template.md`。

战斗效果使用 Core 白名单与整数规则。状态结算顺序固定为基础伤害与力量、虚弱、易伤、格挡、生命；应用和衰减进入结构化事件日志。内容只能引用注册过的稳定状态 ID，不得提供动态脚本。
