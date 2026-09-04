# RFC-003：Unity 双语视觉小说与卡牌垂直切片界面

- 状态：Accepted for implementation
- Owner：Presentation / Localization
- Reviewers：Design / Narrative / Engineering / QA
- 最后更新：2026-09-01
- 关联 RFC：RFC-001、RFC-002
- 关联 ADR：ADR-001

## 背景与玩家价值

已有纯 C# 流程无法被玩家直接操作。本 RFC 把版本化内容目录投影为 Unity 运行时界面，使玩家能阅读剧情、选择分支、打牌、结束回合、选择奖励并切换语言。

## 目标

- Bootstrap 自动创建一套可操作的 uGUI 垂直切片界面。
- 支持英文与简体中文，并对内容键和 UI 键执行完整覆盖检查。
- 视觉小说、战斗、奖励、完成和战败阶段使用同一个 RunSession。
- 鼠标、键盘和手柄通过 Input System UI module 工作。
- 脚手架可幂等重建场景引用，质量门能发现 Presenter 或内容引用缺失。

## 非目标

- 当前布局是功能性原型，不是最终视觉、美术、动画或音频质量。
- 本轮不实现地图、多遭遇、设置菜单、字体打包策略或正式无障碍审计。
- 不把玩家可写领域状态迁移到 Presentation。

## 本地化契约

`Content/Schema/localization-v1.schema.json` 定义源格式。运行时要求 `en` 与 `zh-Hans` 各有唯一、已批准、版本匹配的目录，并覆盖内容目录声明的全部键及垂直切片 UI 键。缺键、重复键、空文本或版本漂移阻断构建。

## 验收标准

- [x] Bootstrap 中只有一个 Presenter，三份源资产引用完整。
- [x] 默认简体中文可渲染视觉小说节点与两个选项。
- [x] 切换英文后不改变 Run 状态。
- [x] 点击迎战选项进入 Combat，并显示玩家、敌人、手牌与结束回合操作。
- [x] 两个 locale 对所有内容/UI 键覆盖完整且版本一致。
- [x] EditMode、内容质量门与 PlayMode Bootstrap 全部通过。

## 验证与回滚

执行 `pwsh Tools/Workflow.ps1 check`。回滚时移除 Presenter 组件、Presentation 脚本、本地化源以及 Game.Editor/Game.Tests.PlayMode 对 Presentation 的引用；Core、Gameplay 与存档不受影响。
