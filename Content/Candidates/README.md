# AI 内容候选隔离区

每个候选目录必须包含：

- `content.json`：完整 content schema v7 目录，`status` 只能是 `draft` 或 `review`。
- `localization.en.json`、`localization.zh-Hans.json`：版本匹配的完整本地化目录，同样不得为 `approved`。
- `provenance.json`：符合 `content-candidate-provenance-v1.schema.json` 的来源记录，只保存提示词 SHA-256，不保存可能含敏感信息的原始提示词。

从当前 approved 基线创建隔离副本：

```powershell
pwsh Tools/New-ContentCandidate.ps1 -CandidateId candidate.example_001 -TargetContentVersion candidate.example.1 -Generator codex -Model '<model-id>' -PromptDigest '<sha256>' -Source original -License internal -Owner '<owner>' -OutputDirectory Content/Candidates/candidate.example_001
```

候选目录不会被运行时、Addressables 或构建读取。执行：

```powershell
pwsh Tools/Review-ContentCandidate.ps1 -CandidateDirectory Content/Candidates/<candidate-id> -Samples 200
```

审阅通过后，负责人根据 `review-report.json` 的 `sourceDigest` 创建独立 `approval.json`。只有明确的 `decision: approved`、UTC 时间和非空 reviewer 才能执行：

```powershell
pwsh Tools/Promote-ContentCandidate.ps1 -CandidateDirectory Content/Candidates/<candidate-id> -Approval Content/Candidates/<candidate-id>/approval.json
```

晋级会重新执行全部解析、本地化和模拟门，验证基础版本未漂移，再原子替换三份运行时源；失败时恢复旧内容。AI 代理不得自行填写 reviewer 或把 decision 改为 approved。
