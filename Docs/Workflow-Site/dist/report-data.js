globalThis.WORKFLOW_REPORT = {
  baseline: '2b20b25',
  date: '2026-09-15',
  sources: {
    overview: { title: '项目定位与样例边界', path: 'README.md', excerpt: '目标是支撑视觉小说、卡牌等内容驱动游戏。Windows / Steam 是当前工程路径；正式发行范围仍待确认。' },
    workflow: { title: '研发工作流', path: 'Docs/WORKFLOW.md', excerpt: '任务写明玩家价值、非目标、可观察验收与验证方式。最小端到端切片、统一 check、PR 证据、Squash Merge 和可构建主线构成开发循环。' },
    handoff: { title: 'AI 全栈生产交接协议', path: 'Docs/Production/AI-HANDOFF.md', excerpt: '权威输入 → 隔离候选 → 自动验证 → 人工评审 → 集成预览 → 回归/构建 → 证据交付。逐项 AC 绑定版本、方法、日期、结果和证据路径。' },
    audit: { title: '就绪审计与真实缺口', path: 'Docs/Production/READINESS-AUDIT.md', excerpt: '2026-09-04 审计及 09-05 同步记录：本地基础已建立；真实跨工种生产、视听验收、远端 CI、干净构建与发布演练仍缺证据。' },
    roadmap: { title: '路线图与容量护栏', path: 'Docs/ROADMAP.md', excerpt: '当前在阶段 2：垂直切片。阶段 3 要求非程序角色新增一张卡、一个敌人和一个分支事件，用实际工时修订容量。容量数字属于待确认规划。' },
    architecture: { title: '架构与确定性', path: 'Docs/ARCHITECTURE.md', excerpt: 'Core 保持无 Unity 引用；内容使用稳定 ID；Run 使用根种子和命名随机流；规则状态、作者定义与表现分离。' },
    projection: { title: 'ADR-001：表现层状态投影', path: 'Docs/ADR/001-presentation-gameplay-projection.md', excerpt: 'Presentation 可引用 Gameplay/Core 的公开状态并调用命令；不得建立第二份可写领域状态或绕过 RunSession 调用基础设施。' },
    data: { title: 'ADR-002：版本化内容与迁移', path: 'Docs/ADR/002-versioned-data-driven-run-content.md', excerpt: '存档按相邻 schema 逐版本迁移。恢复要求 contentVersion 精确匹配；格式迁移不能代替内容兼容策略。' },
    content: { title: '结构化内容管线', path: 'Docs/Production/CONTENT_PIPELINE.md', excerpt: '内容 schema v7，本地化 schema v1，安全存档 schema v4。en 与 zh-Hans 必需键完全覆盖。固定策略模拟是工程回归探针。' },
    media: { title: '视听内容交接', path: 'Docs/Production/Content-Pipeline.md', excerpt: '立绘、卡面、动画和音频需保留来源许可、源文件、事件映射、实际预览和人工验收。现有 JSON 候选工具不导入图片或音频。' },
    candidate: { title: '候选区操作指南', path: 'Content/Candidates/README.md', excerpt: '候选由 content.json、两份本地化和 provenance.json 构成；创建、审阅和独立人工批准后晋级，失败恢复旧内容。' },
    governance: { title: 'RFC-008：候选治理', path: 'Docs/RFC/008-ai-content-candidate-governance.md', excerpt: 'SHA-256 绑定源文件；候选发生改变会使旧批准失效。晋级重新审阅并检查基础版本。完整快照暂不支持语义合并。' },
    fidelity: { title: 'RFC-012：候选快照保真', path: 'Docs/RFC/012-candidate-copy-fidelity.md', excerpt: '转换仅改变顶层版本和 status，保留叙事、来源、ID 和嵌套数据；拒绝非 approved 基线、空版本及重复顶层字段。' },
    gate: { title: '统一门禁的实际执行顺序', path: 'Tools/Workflow.ps1', excerpt: '仓库策略 → 任务证据工具 → 候选保真 → EditMode → 项目校验 → 200 种子平衡门 → PlayMode。check 自身不构建 Player。' },
    quality: { title: '质量与性能策略', path: 'Docs/Quality-Strategy.md', excerpt: '自动测试、Player 冒烟、真机兼容和发布回归是不同层级。性能结论需要目标硬件和 Profiler 捕获。' },
    pr: { title: 'PR CI 配置', path: '.github/workflows/pull-request.yml', excerpt: 'PR 在自托管 Windows / Unity Runner 调用统一 check，额外构建 Windows Development Player，并上传诊断资料。' },
    release: { title: '发布与回滚运行手册', path: 'Docs/Release/Release-Runbook.md', excerpt: '正式标签构建一次，QA 验证该 ZIP，再经环境审批晋级 Steam 分支；回滚切换上一稳定 Build ID。' },
    releaseci: { title: '正式发布 CI 配置', path: '.github/workflows/release.yml', excerpt: '标签触发统一门禁、Release 构建、manifest、ZIP 和 Draft GitHub Release。配置文件不能证明一次实际发布成功。' },
    steam: { title: 'Steam 制品晋级配置', path: '.github/workflows/steam-publish.yml', excerpt: '下载指定 Release 的既有 ZIP，解压后调用发布脚本；目标环境为 steam-internal / external / default。' },
    evidence: { title: '任务证据完整性检查器', path: 'Tools/Test-TaskEvidence.ps1', excerpt: '校验逐 AC 结果、版本、负责人、证据路径和 SHA-256；仅检查记录的一致性，不鉴定审批身份或证据语义。' },
    nightly: { title: 'Nightly CI 配置', path: '.github/workflows/nightly.yml', excerpt: '调用统一 check，生成 Windows Development Player，构建和诊断制品保留 30 天。' },
    core: { title: 'Core 程序集边界', path: 'Assets/_Game/Core/Game.Core.asmdef', excerpt: 'references 为空，noEngineReferences 为 true，证明当前 Core 程序集不引用引擎。' },
    localcheck: { title: '本次本地验证摘要', path: 'Docs/Workflow-Site/verification.json', excerpt: '2026-09-15 完整 Workflow 通过：EditMode 71/71、PlayMode 1/1、200 种子 180 胜 / 20 败 / 0 卡死；同时记录网页验证范围与原始测试文件哈希。' }
  },
  stages: [
    { name: '权威输入', role: '负责人 / 设计 / 工程', tag: '先定义完成', intro: '把需求变成可以验收的任务契约。', input: '玩家价值、已有 RFC / ADR、内容基线和依赖。', action: '明确目标、非目标、允许修改范围、负责人、AC 编号、验证方法与回滚。高风险跨模块能力先写 RFC。', output: '有版本与验收条件的任务卡；长期技术决策进入 ADR。', stop: '创意、数值、许可或发行决定尚未确认时，保留待审状态。', sources: ['workflow', 'handoff'] },
    { name: '隔离候选', role: 'AI / 内容作者', tag: '默认 draft', intro: '让生成和试错发生在运行时之外。', input: '已批准内容基线、目标版本、模型与来源许可记录。', action: '创建完整内容和双语快照；只改变顶层版本及状态。AI 结构化产物写入 Content/Candidates。', output: 'content.json、两份 localization JSON 与 provenance.json。', stop: '候选不进入运行时或构建；当前快照工具不处理多人语义合并或视听导入。', sources: ['candidate', 'governance', 'fidelity'] },
    { name: '自动验证', role: '工具 / QA', tag: '技术可接受', intro: '先发现非法数据、断裂引用与回归。', input: 'draft / review 候选、schema、本地化和固定策略种子。', action: '验证来源、ID、引用、数值边界、本地化、叙事拓扑、基础版本与模拟结果。', output: '审阅报告及绑定完整候选的 SHA-256 sourceDigest。', stop: '潜在路径可达不等于条件一定可满足；通过模拟不等于真实玩家体验良好。', sources: ['content', 'governance', 'handoff'] },
    { name: '人工评审', role: '独立负责人', tag: '必须留证', intro: '对自动检查无法判断的内容负责。', input: '候选原文 / 资产、审阅报告、来源许可与哈希。', action: '审核设计、叙事、语言与授权。独立审核人提供实际批准文件，AI 不代填 reviewer 或 approved。', output: '与候选哈希绑定的 approval.json；视听资产另留人工验收。', stop: '候选任一字节变化需重新审核。字段齐全不能认证真人身份或许可真实性。', sources: ['candidate', 'handoff', 'media'] },
    { name: '集成预览', role: '工程 / Presentation / 作者', tag: '看实际效果', intro: '让已批准内容在真实玩法情境中运行。', input: '独立批准、版本匹配的候选，以及上一版可回退源。', action: '晋级前重新审阅，校验 approved 包并原子替换三份源；视听资产按稳定 ID / 事件接入，在目标画面或播放情境中验收。', output: 'Unity 预览、集成记录及保留 GUID 的回退材料。', stop: '晋级失败恢复旧源；JSON 审阅不能充当画面、循环音频或混音验收。', sources: ['candidate', 'media'] },
    { name: '回归 / 构建', role: '工程 / QA / CI', tag: '统一 check', intro: '把局部成功放回整个项目中检验。', input: '已集成内容、版本化规则、旧存档与关键玩家路径。', action: '运行七道统一门禁。PR CI 额外构建 Development Player；正式标签按发布流程生成一次 IL2CPP 制品。', output: '测试 XML、平衡 JSON、构建日志，以及发布时的制品与 manifest。', stop: '本地通过不证明远端 Runner 可用；check 通过也不代表 Player 已构建。', sources: ['gate', 'pr', 'releaseci'] },
    { name: '证据交付', role: '交付者 / 负责人', tag: '逐项 AC', intro: '让每个“完成”都有可重新检查的依据。', input: '输入版本、实际命令 / 人工步骤、执行时间、结果与文件。', action: '逐项对照验收，保存路径与 SHA-256，列出事实、推断、未验证项、风险及回滚；必要验收缺失时保持未完成。', output: '可审阅的 PR / 交付记录，以及下一阶段明确的接收人。', stop: '证据工具检查文件完整性，不替代人工批准、证据语义或真实生产能力验证。', sources: ['handoff', 'evidence'] }
  ]
};
