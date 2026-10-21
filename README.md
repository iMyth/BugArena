# Bug Arena / 斗虫擂台

Unity 6000.4.9f1 · Universal Render Pipeline · Input System

## 开始试玩

1. 用 Unity Hub 打开本项目。
2. 菜单 **Bug Arena → Open Arena Scene**（场景：`Assets/BugArena/Scenes/BugArena.unity`）。
3. 点击 Play，再按 Enter 或点击 **Enter the Ring** 开始。
4. 如果 Game 视图被裁切，将顶部 Scale 调到最小；双击 Game 标签可最大化。

| 操作 | 按键 |
| --- | --- |
| 左右移动 | A / D 或方向键 |
| 蓄力 | 按住空格 |
| 向对手冲刺 | 松开空格 |
| 暂停 / 继续 | Esc |
| 重开本局 | R |

角色自动面向对手。失去应用焦点时自动暂停。

## 核心玩法

- 蓄力 0–1.45 秒，冲刺距离从 1.65 米增加到 6.8 米；达到上限后可以继续按住。
- 蓄力时的细线表示冲刺距离，金色区域表示能够触发末端命中的**对手中心位置范围**。
- 普通命中造成 10–16 点伤害；末端命中造成 23–34 点伤害并挑飞。
- 打中正在蓄力的对手会打断蓄力，并追加 4 点伤害。
- 双方冲刺相撞会弹开，不扣血。
- 空冲后有 0.43 秒破绽；短蓄力适合近身，长蓄力适合远距离。
- 双方各 100 点生命；本局结束后可无限重开，并显示本次运行中的胜负计数。

## 实现与验证

- `ArenaCombat.cs`：独立战斗状态与固定步长模拟；距离、伤害和蓄力参数在此调整。
- `ArenaGame.cs`：Input System 输入、回合流程、顿帧、镜头震动与合成音效。
- `ArenaVisuals.cs`：运行时构建甲虫、林间擂台、预判线与命中特效。
- `ArenaHUD.cs`：生命、蓄力、操作提示、暂停与结算界面。
- `Editor/ArenaSetup.cs`：场景创建和 **Run Combat Checks** 战斗检查入口。

美术为程序化占位模型；场景在进入 Play Mode 后生成，不依赖外部素材或付费包。当前是单人对 AI 的手感原型，尚未加入联网、角色选择、长期养成与正式动画。

`Assets`（含 `.meta`）、`Packages` 与 `ProjectSettings` 纳入 Git；Unity 缓存、日志、构建产物和 IDE 生成文件由 `.gitignore` 排除。
