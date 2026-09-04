# Windows / Steam 发布运行手册

## RC 准入

- 标签提交来自已通过保护规则的 `main`。
- 功能、内容、本地化和第三方许可证冻结。
- EditMode、PlayMode、Windows Player、旧存档与兼容矩阵通过。
- 商店素材、年龄评级、隐私说明、最低配置和支持渠道就绪。

## 构建与验证

1. 在 `main` 创建符合 SemVer 的不可变标签，如 `v1.0.0`。
2. `release.yml` 在干净 Runner 运行测试并生成一次 IL2CPP Player。
3. 检查 Draft GitHub Release 中的 ZIP、manifest、commit 和校验和。
4. QA 只验证该 ZIP，不接受本地重建包。
5. 通过 `steam-publish.yml` 依次晋级 internal、external、default；每个 Environment 设置指定审批人。

## 回滚

- 在 Steamworks 保留上一稳定 Build ID，不覆盖或删除。
- 发现阻断问题时先将默认分支切回上一 Build ID，再评估补丁。
- 补丁从发布标签创建 `hotfix/*`，修复先合入 `main`，通过完整门禁后发布新的 Patch 标签。
- 禁止用回滚版本写入旧客户端无法理解的新存档；必要时暂停云同步并发布支持公告。

## 发布后

按崩溃率、存档故障、退款原因和社区高频问题排序。每日记录版本健康状态、已知问题、负责人和下次判断时间。
