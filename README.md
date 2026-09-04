# Project WorkFlow

面向《杀戮尖塔》相当规模、覆盖视觉小说、卡牌等内容驱动游戏的全栈 AI 开发工作流。当前以 Unity `6000.3.18f1` 和视觉小说＋卡牌样例验证分层规则、内容生产、自动测试与构建交接；并不限定产品必须是 3D 关卡制。

仓库已有 Windows 构建和 Steam 发布配置，这是当前工程实现路径，不是已确认的发行范围或已验证的发布能力。正式玩法容量、美术方向、平台与性能预算仍需负责人确认。当前能力与缺口见 [就绪审计](Docs/Production/READINESS-AUDIT.md)。

## 从任务到交付

按 [研发工作流](Docs/WORKFLOW.md) 填写目标、非目标和验收，遵守 [AI 交接协议](Docs/Production/AI-HANDOFF.md)：权威输入 → 隔离候选 → 自动验证 → 人工评审 → 集成预览 → 回归/构建 → 证据交付。AI 可以起草、实现和检查，不能替负责人批准玩法、叙事、视听方向或授权。

- 卡牌、敌人、分支事件、本地化：从 [结构化内容管线](Docs/Production/CONTENT_PIPELINE.md) 和 [候选操作指南](Content/Candidates/README.md) 开始。
- 立绘、卡面、背景、动画、音效：从 [视听内容交接](Docs/Production/Content-Pipeline.md) 开始；3D 场景规范仅按需采用。
- 工程、QA、构建与发行：从下方架构入口、[质量策略](Docs/Quality-Strategy.md) 和 [发布手册](Docs/Release/Release-Runbook.md) 开始。配置存在不代表已实测通过。

## 快速开始

前置条件：

- Unity `6000.3.18f1`，包含 Windows Build Support（IL2CPP）。
- Git 与 Git LFS。
- PowerShell 7（CI 与本地统一使用 `pwsh`）。

克隆后运行：

```powershell
./Tools/Bootstrap.ps1
```

常用命令：

```powershell
./Tools/Workflow.ps1 check
./Tools/Run-UnityTests.ps1 -Platform EditMode
./Tools/Run-UnityTests.ps1 -Platform PlayMode
./Tools/Validate-Project.ps1
./Tools/Build-Windows.ps1 -Configuration Development
```

在 Unity 中打开并运行 `Assets/_Game/Scenes/Bootstrap.unity` 即可体验当前双语垂直切片：路线地图、视觉小说分支、带可预告敌人意图的多场卡牌战斗、力量/虚弱/易伤组合、剧情授予遗物、奖励、休整升级、确定性商店与最终 Boss 使用同一套版本化内容和 Gameplay 状态。安全存档采用 schema v4，并保留路线位置、访问轨迹、牌组、遗物、商店库存与删牌状态、生命、资源及剧情变量。

系统/内容设计可运行 `pwsh Tools/Run-BalanceSimulation.ps1 -Samples 1000`，用固定策略和连续种子生成 `Artifacts/balance-report.json`，在人工试玩前发现不可通关或数值漂移内容。

AI 结构化内容不得直接写入 approved 运行时源。使用 `Tools/New-ContentCandidate.ps1` 创建隔离 draft 快照，`Tools/Review-ContentCandidate.ps1` 生成来源/本地化/引用/模拟与 SHA-256 审阅报告；只有独立人工批准文件才能交给 `Tools/Promote-ContentCandidate.ps1` 原子晋级。

如果 Unity 不在标准路径，设置 `UNITY_EDITOR_PATH` 指向 `Unity.exe`。不要用其他 Unity 补丁版本打开并保存项目。

## 架构入口

- `Assets/_Game/Core`：不依赖 UnityEngine 的纯 C# 基础类型。
- `Assets/_Game/Foundation`：稳定服务契约、配置和关卡生产预算。
- `Assets/_Game/Infrastructure`：存档、场景、输入、平台、Addressables 与构建信息实现。
- `Assets/_Game/Gameplay`：按玩法功能继续拆分程序集。
- `Assets/_Game/Presentation`：UI、音频、动画与视觉表现。
- `Assets/_Game/Editor`：项目初始化、导入规则、质量门禁与构建入口。
- `Assets/_Game/Tests`：EditMode 与 PlayMode 测试。

详细规范见 [工程手册](Docs/Engineering-Handbook.md)、[内容生产](Docs/Production/Content-Pipeline.md)和[发布运行手册](Docs/Release/Release-Runbook.md)。

## 当前集成边界

默认运行 `OfflinePlatformServices`，确保无 Steam 或离线时仍可进入游戏。接入真实 Steamworks SDK 前必须完成 [Steam 集成清单](Docs/Steam-Integration.md)，不得提交 App 密钥、Steam 登录配置或第三方闭源二进制来源不明的插件。
