# 全栈工作流就绪审计（2026-09-04）

目标保持为支持《杀戮尖塔》相当规模、包含视觉小说和卡牌等类型的全栈 AI 游戏开发工作流。本文件是证据盘点，不是重新定义目标，也不是完成声明。

## 当前证据与缺口

| 能力 | 当前可证明范围 | 尚缺的完成证据 |
| --- | --- | --- |
| 任务/交接 | 生产表单、逐 AC 证据协议、本地哈希检查及失败测试 | 真实跨工种负责人使用记录、审批真实性与任务吞吐量 |
| 规则/运行时 | 分层代码、确定性战斗与叙事、路线/奖励/商店/敌人意图 | 正式设计确认、完整目标容量与系统覆盖审计 |
| 结构化内容 | draft 审阅、引用/本地化/叙事拓扑/模拟门、晋级代码测试 | 非程序作者从候选到人工批准再到实际集成的演练 |
| 美术/动画/音频 | 导入规范与交接要求 | 已批准方向、授权资产、实际导入/预览/回退、视听 QA |
| QA | 最近本地 Workflow：71/71 EditMode、1/1 PlayMode；200 种子 0 卡死 | 真机体验、无障碍、兼容矩阵、性能捕获与目标硬件预算 |
| 构建 | 本地 Windows Development 构建成功 | 干净机器构建、IL2CPP RC、安装与升级演练 |
| 协作 CI | 仓库内 workflow 文件、GitHub 远端和个人 CODEOWNER 已配置；首次框架同步于 2026-09-05 执行 | 组织团队、Runner、Ruleset、真实 CI 运行 |
| 发布 | 发布/Steam 晋级脚本与手册存在 | 平台身份/授权、环境审批、不可变制品验证、实际回滚演练 |

可重查来源：`Tools/Workflow.ps1`、`TestResults/`、`Artifacts/balance-report.json`、`Logs/build-Development.log`、`.github/workflows/`、`.github/CODEOWNERS`、`git log -1`、`git remote -v`。远端为 `https://github.com/aisukora0712u/LW_GD_FS_FAIWF_Prototype.git`。日志是当前工作区证据，不是永久发布证明。

## 本次独立任务：本地与 CI 门禁一致

目标：消除 PR/nightly/release 复制步骤导致的漂移。非目标：不创建远端、不配置身份/密钥、不触发发布、不升级 Actions 或 Unity。

验收：三个 workflow 均调用一次 `./Tools/Workflow.ps1 check`，不再分别列出 Unity 检查；原构建与制品步骤保留，PR/nightly 额外上传平衡报告。执行本地统一入口确认可用。远端调度与 YAML 在线验证仍须在真实仓库中完成。

原因证据：旧三个 CI 文件未调用 `Run-BalanceSimulation.ps1` 或 `Test-TaskEvidence.Tests.ps1`，与本地 `check` 不一致。统一入口使以后新增门禁无需同步复制三份步骤。

本次验证：三个文件的源码断言通过（统一入口各一次、无独立重复检查、保留构建调用）；`pwsh -NoProfile -File Tools/Workflow.ps1 check` 退出码 0，证据工具 10 项、EditMode 71/71、PlayMode 1/1 通过。200 种子模拟为 180 胜、20 败、0 卡死，当前门禁通过；这不是正式平衡性结论。未运行远端 CI、未验证 GitHub 在线 YAML 解析，本次也未重新执行 Windows 构建。

回滚：撤回三个 workflow 中统一调用与平衡报告上传改动。运行时、内容、存档不受影响。

## 后续优先次序

### 文档入口纠偏（2026-09-04）

目标：让新作者和 AI 从用户要求的视觉小说/卡牌等全栈工作流进入，而非误认为 3D 关卡制是产品前提。非目标：不改变引擎、运行时、构建平台、性能数值、素材或审批状态，不删除现有 3D 支持。

验收：README 明确目标与当前实现边界并链接两条内容管线；视听规范覆盖 VN、卡面/UI、动画和音频交接，把场景拆分限制到对应任务；源码证明关卡配置与导入默认值的适用范围；作者文档的内容 schema/策略版本与当前源一致；本地链接检查与 Workflow 通过。

回滚：撤回 README、WORKFLOW、两份内容手册和候选 README 的本次文案调整；不涉及运行时或存档。文档不是视听生产完成证据。

本次验证：18 个本地 Markdown 链接存在；内容源 schemaVersion 7、当前平衡报告 policyVersion greedy-v2-shop 与作者说明一致；已阅读 ProjectValidator.ValidateLevelProfiles 与 ProductionAssetPostprocessor 对照适用范围。全量 Workflow 退出码 0，EditMode 71/71、PlayMode 1/1，证据工具、候选保真与平衡门通过。未执行视听资产人工验收、远端 CI 或新 Player 构建。

### 待执行

1. 完成真实候选生产演练的本地技术准备，保留人工审批边界；不以更多样例玩法替代生产流程验证。
2. 为视听资产建立一个已批准参考到导入、预览、回滚的代表性交接，确认授权与风格后执行。
3. 由负责人配置组织团队、Runner、Ruleset 与发布目标后完成远端 CI、干净构建和发行演练；凭据通过产品安全入口配置，不写入文档。
4. 用实测制作成本、返工和性能数据确认容量；不得把自动测试数量当作中型项目生产能力证明。
