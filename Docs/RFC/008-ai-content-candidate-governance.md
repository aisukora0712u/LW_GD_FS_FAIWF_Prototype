# RFC-008：AI 内容候选隔离、审阅与人工晋级

- 状态：Accepted for implementation
- Owner：Content Operations / Design / QA
- Reviewers：Security / Localization / Engineering
- 最后更新：2026-09-02
- 关联 ADR：ADR-002

## 目标

- AI 产物只能进入 `Content/Candidates/<id>`，不能直接进入运行时 approved 目录。
- 候选必须提供完整双语包和最小来源记录：生成器、模型、UTC 时间、提示词摘要、来源、许可和负责人。
- 自动审阅验证 draft/review 状态、schema、稳定 ID、引用、本地化覆盖、基础版本和固定种子平衡门。
- 报告以 SHA-256 绑定候选全部源文件；修改任一字节使旧批准失效。
- 晋级必须有独立人工批准文件，且会重新审阅、转换 approved 状态、同步校验并原子替换；失败恢复旧内容。

## 非目标

- 工具不调用外部模型，也不保存原始提示词或密钥。
- 自动分数不能代替创意、叙事、授权、敏感内容或发行负责人判断。
- 本轮候选是完整目录快照，不实现语义合并或多人冲突解决。

## 验收标准

- [x] 运行时解析器拒绝 draft/review，隔离审阅器拒绝 approved 候选。
- [x] 缺来源字段、缺本地化、引用错误、基础版本漂移或平衡门失败会生成失败报告。
- [x] 相同候选源得到相同 sourceDigest；源变更使批准失效。
- [x] pending/伪造/空 reviewer/非 UTC/哈希不匹配批准均被拒绝。
- [x] 晋级前重新审阅并验证 approved 包，写入失败可恢复三份旧源。
- [x] 文档、EditMode、完整 Workflow 和 Windows Development 构建通过。

## 回滚

删除候选治理脚本、Editor 批处理入口、治理 DTO、两份 schema 与候选文档；运行时内容格式和 checkpoint 不受影响。
