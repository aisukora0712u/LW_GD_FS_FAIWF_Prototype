# 新成员入组

## 第一天检查表

1. 获取 GitHub 团队、项目看板和 LFS 权限。
2. 安装指定 Unity 版本及 Windows IL2CPP 模块。
3. 克隆仓库后运行 `./Tools/Bootstrap.ps1`。
4. 在 Unity 中打开 `Assets/_Game/Scenes/Bootstrap.unity`，确认无 Console 错误。
5. 创建一个不改变行为的文档 PR，验证分支、评审和 CI 权限。
6. 阅读工程手册、内容生产规范、安全规范和当前里程碑退出条件。

Bootstrap 失败时，先查看 `Logs/bootstrap.log` 与 `TestResults`，不要删除锁文件或换 Unity 版本规避问题。

## 本地 Git 配置

Unity Smart Merge 由 Unity 安装提供。团队可在全局 Git 配置中注册 `unityyamlmerge`，但不得覆盖仓库的 `.gitattributes`。Scene、Prefab 和 Material 仍应通过拆场景、小 Prefab 与资产锁减少冲突，Smart Merge 只是最后防线。
