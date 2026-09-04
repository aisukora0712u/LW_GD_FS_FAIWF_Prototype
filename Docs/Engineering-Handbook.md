# 工程手册

## 不可变基线

- Unity 固定为 `6000.3.18f1`；升级独立 PR，必须通过 EditMode、PlayMode、内容校验和 Windows 构建。
- `manifest.json` 与 `packages-lock.json` 同时提交；包升级不得夹带功能修改。
- Release 使用 IL2CPP；Development 使用 Mono 并开启调试。
- Addressables v1 只能使用本地加载路径，不允许远程内容替换。

## 模块边界

依赖方向为 `Core <- Foundation <- Infrastructure / Gameplay / Presentation`。Gameplay 不得直接引用 Steamworks、文件系统或 Addressables；通过 `IPlatformServices`、`ISaveService`、`ISceneFlowService` 和 `IAssetProvider` 工作。

`CompositionRoot` 是唯一组合入口，但不是全局单例。需要服务的场景对象通过初始化方法、序列化引用或场景级上下文显式获得 `GameContext`。禁止 `static Instance`、Service Locator 和全局可写事件总线。

新增跨模块依赖前先回答：

1. 这是稳定能力边界还是单个玩法细节？
2. 能否放在调用方模块内部？
3. 是否需要可替换实现或离线实现？
4. 是否引入生命周期、释放或主线程约束？

## 存档规则

- 业务层负责把状态序列化为 JSON；存档服务负责版本、校验、原子提交和备份。
- 每次 schema 变化必须递增版本并提供逐版本迁移测试，禁止跳过未知版本。
- `.tmp` 文件永不进入 Steam 云；只有成功提交的 `.json` 和必要备份可同步。
- 不将加密当作防作弊手段。若未来需要防篡改，另立 RFC 定义密钥、离线行为和失败策略。

## 日常开发

- `main` 始终可构建；使用 `feature/*`、`fix/*`，分支目标寿命不超过两个工作日。
- PR 使用 Squash Merge，标题遵循 `type(scope): summary`。
- 任务控制在 0.5–2 天，需求必须有玩家价值、验收条件、依赖和测试计划。
- 核心架构、存档、构建、平台和包版本变更由技术负责人批准。
- 提交前运行 EditMode 测试和项目校验；场景或玩家流程变化再运行 PlayMode。

## Definition of Done

- 玩家可观察验收条件全部满足。
- 自动测试覆盖正常路径与主要失败路径。
- 没有丢失引用、远程 Addressables、未登记依赖或新编译警告。
- 存档、输入、性能、内存、包体与平台影响已记录。
- 相关文档、关卡预算与回滚方式已更新。
- Windows Player 构建验证通过。
