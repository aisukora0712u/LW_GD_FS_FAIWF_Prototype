# 内容生产管线

## 单一来源

卡牌、敌人、遭遇、事件、对白和本地化都应有明确的单一来源。建议作者使用可审阅的表格或结构化文本，Unity 资产是导入产物；不要同时手改来源和生成资产。

```text
作者源 → Schema 校验 → ID/引用校验 → 导入 → Unity 预览
      → 规则测试 → 平衡模拟 → 本地化覆盖 → 可发布内容清单
```

当前内容 schema v7 / 本地化 schema v1 契约（视听资产另见 [视听内容交接](Content-Pipeline.md)）：

- JSON Schema：`Content/Schema/game-content-v7.schema.json`
- 本地化 Schema：`Content/Schema/localization-v1.schema.json`
- Unity 运行时源：`Assets/_Game/Content/Source/*.json`
- 双语源：`Assets/_Game/Content/Localization/*.json`
- 可执行样例：`Assets/_Game/Content/Source/vertical-slice.json`
- 运行时解析：`ContentCatalogJson.ParseApproved`
- 构建门：`ProjectValidator.ValidateContentCatalogs`

`schemaVersion`、`contentVersion`、`status` 由目录声明并由其中所有内容继承；每项内容仍必须携带 `metadata.owner`、`metadata.source` 和唯一非空 `metadata.tags`。卡牌、敌人、遭遇和路线节点还必须提供 `nameKey` 与 `descriptionKey`。路线节点以稳定 ID 引用剧情或遭遇，非最终节点声明至少一个后继，最终节点不得有后继；导入时会验证入口可达性与全部跨引用。

Run 安全存档当前为 schema v4，包含路线当前位置、访问轨迹、遗物列表及活动商店库存/删牌状态。JSON 反序列化器按相邻版本迁移 v1→v2→v3→v4；`contentVersion` 仍必须精确匹配，禁止把格式迁移误当作内容兼容。

当前发行门要求 `en` 与 `zh-Hans` 各有且仅有一个版本匹配的批准目录。内容键和 UI 键必须 100% 覆盖；缺键、空文本、重复键和本地化版本漂移都会让 Unity 项目校验失败。新增语言时先加入必需 locale 清单，再补齐全部键，避免部分翻译被误发。

提交内容前运行 `pwsh Tools/Workflow.ps1 check`。快速的 JSON 语法检查由仓库策略执行，ID、枚举、数值与跨引用校验由 Unity 内容门执行。

规则或数值变更还应运行：

```powershell
pwsh Tools/Run-BalanceSimulation.ps1 -Samples 1000 -StartSeed 1 -Output Artifacts/balance-report.json -EnforceGate
```

报告记录 `policyVersion`（当前 `greedy-v2-shop`）、胜率、剩余生命、战斗回合、路线选择和卡牌使用次数。默认工程探针要求 0 卡死且胜率在 100–900‰；阈值用于同策略、同种子区间的回归比较和极端离群筛查，不代表真实玩家胜率。AI 生成内容即使通过模拟也仍保持 `draft`，必须完成人工设计/叙事/授权审核后才能改为 `approved`。

## 最低公共字段

每个内容项至少包含：

- `id`：稳定 ID，发布后不复用。
- `schemaVersion`：格式版本。
- `contentVersion`：内容修订版本。
- `status`：draft/review/approved/deprecated。
- `tags`：检索、规则和分析标签。
- `nameKey`/`descriptionKey`：本地化键，不直接存显示文本。
- `owner`：负责角色或团队。
- `source`：原创、委托、生成或授权来源与许可记录。

## AI 生成规则

- 生成物默认是 `draft`，不能自动进入发行清单。
- 提示词、模型/工具、日期、输入素材与人工修改责任人应可追踪。
- 叙事草稿进行正典、角色声音、逻辑连贯、敏感内容和本地化可行性审查。
- 卡牌/敌人草稿进行 schema、引用、效果白名单、状态 ID、数值边界、无限循环和可终止性检查。
- 卡牌升级只引用稳定目标 ID；不得自引用，目标缺失会阻断导入。
- 状态效果只允许 `status.strength`、`status.weak`、`status.vulnerable` 等目录中已注册的稳定 ID；结算/衰减顺序由 RFC-006 固定，内容不能覆盖或插入任意脚本触发器。
- 遗物只使用白名单触发器/效果并引用已注册状态 ID。当前仅允许 `CombatStart` 配合 `GainBlock` 或 `ApplyStatus`；新增触发时机必须先定义顺序和循环预算。
- 图像/音频必须记录授权；风格参考不能替代使用权。

结构化内容使用 `Content/Candidates` 隔离工作流：创建完整 draft 快照 → 自动审阅并生成 SHA-256 绑定报告 → 设计/叙事/本地化/授权负责人复核 → 独立人工批准 → 原子晋级。候选不能被运行时或构建读取，自动审阅通过也不会自动修改 approved 源。具体命令与文件拓扑见 `Content/Candidates/README.md` 和 RFC-008。

## 平衡闭环

静态规则先拦截非法内容，确定性模拟发现离群值，人工试玩判断体验。最少跟踪卡牌选择率、使用率、胜率贡献、伤害/资源效率、敌人致死率、事件选择分布和 Run 时长。指标用于提出问题，不直接替代设计判断。

## 叙事与玩法联动

叙事节点只能调用注册过的领域命令，例如获得卡牌、改变资源、设置故事变量、进入遭遇。条件表达式必须可静态解析并能报告不存在的变量、不可达分支和无出口循环。每个关键故事状态至少有一条自动遍历测试。
