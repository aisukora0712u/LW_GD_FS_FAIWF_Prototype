# ADR-002：版本化数据驱动 Run 内容与相邻迁移

- 状态：Accepted
- 日期：2026-09-01
- 决策人：Gameplay / Content / Engineering
- 关联：RFC-002、RFC-004、RFC-005、RFC-006、RFC-007

## 背景

路线、构筑节点和长期扩展会持续改变内容目录与安全存档。若把这些结构固化在场景、显示文本或 Unity 对象引用中，AI 批量生产、无头模拟、版本审阅和旧档诊断都无法可靠执行。

## 决策

Run 定义继续使用经 JSON Schema 校验、带审批状态和稳定 ID 的数据目录；规则状态只保存稳定 ID 和标量，不保存 Unity 实例、资源路径或本地化文本。内容格式每次破坏性结构变化提升 `schemaVersion`，实际设计修订提升 `contentVersion`。

存档只支持相邻 schema 逐版本迁移，迁移格式不猜测内容映射。恢复仍要求 `contentVersion` 精确匹配；跨内容版本兼容必须由显式映射 RFC 另行批准。Presentation 只能通过 Gameplay 命令投影/修改 Run，不拥有领域状态。

## 后果

- AI 或人工生成内容可在进入 Unity 资产前接受 schema、引用、本地化和模拟门禁。
- 回放与模拟可仅依赖 Core/Gameplay 定义和根种子。
- 新格式需要同步 schema、解析器、迁移测试、内容样例、文档和构建门，改动成本有意提高。
- 已发布内容 ID 不复用；删除或重定向必须提供显式迁移策略。

## 被否决方案

- ScriptableObject 作为唯一作者源：难以文本审阅、批量生成和独立校验。
- 任意脚本化效果：扩大安全面并破坏确定性与静态分析。
- 对旧档做最佳猜测：容易产生可载入但语义错误的 Run。

## 验证

内容解析/非法引用测试、存档逐版本迁移测试、双语覆盖、确定性模拟、`pwsh Tools/Workflow.ps1 check` 和 Windows Development 构建共同构成证据。
