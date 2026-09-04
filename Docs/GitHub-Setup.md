# GitHub 仓库设置

远端仓库为 `aisukora0712u/LW_GD_FS_FAIWF_Prototype`。代码无法自行确认或完成 GitHub 管理面设置；首次推送后由仓库管理员执行：

1. 当前 `.github/CODEOWNERS` 由 `@aisukora0712u` 负责；建立组织团队后再将各路径替换为真实团队。
2. 为 `main` 创建 Ruleset：禁止直接推送，要求 PR、至少一名批准、Code Owner 批准、解决全部对话、线性历史和成功质量门禁。
3. 要求检查：`validate-title` 与 `unity-quality-gates`。
4. 禁止强推、删除标签和绕过规则；Release 标签限制给发布负责人。
5. 注册自托管标签 `Windows`、`Unity`，设置仓库变量 `UNITY_EDITOR_PATH`。
6. 创建 `steam-internal`、`steam-external`、`steam-default` Environments；external/default 配置人工审批。
7. 在 Steam Environments 中配置 `STEAMCMD_PATH`、`STEAM_APP_ID`、`STEAM_DEPOT_ID` 和 `STEAM_USERNAME`，并安全恢复授权后的 `config.vdf`。

Ruleset 未启用前，不得将仓库视为商业发布源。
