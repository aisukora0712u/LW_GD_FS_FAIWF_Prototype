# Steam 集成清单

工程默认使用 `OfflinePlatformServices`。真实 Steam 实现必须作为 `IPlatformServices` 的适配器存在，Gameplay 不得引用 SDK 类型。

## 接入步骤

1. 选择并登记 Steamworks C# 封装、版本、来源与许可证。
2. 在独立程序集内实现初始化、回调、成就、Overlay 和用户标识。
3. 使用编译符号隔离 Steam 适配器；无 SDK 构建仍必须编译并可离线游玩。
4. App ID 仅通过本地忽略文件或 CI Environment 注入，禁止提交真实凭据。
5. 云存档只匹配已原子提交的文件；在 Steamworks 后台配置冲突策略和容量上限。
6. 对初始化失败、客户端未运行、离线、用户切换、云冲突和回调超时编写测试。
7. SteamCMD Runner 使用受保护 Environment 和预先恢复的 `config.vdf`；密码不得出现在命令行或日志中。

发布晋级顺序为 `internal -> external -> default`。每次晋级都从 GitHub Release 下载同一不可变 ZIP，不重新构建。
