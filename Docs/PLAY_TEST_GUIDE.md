# 决竞球 BattleBall_Unity - Play 测试验证指引 & 系统入口总表

> 生成时间：2026-09-04
> 里程碑状态：**C# 编译 0 errors ✅**（从 142 错 → 30 → 5 → 2 → 0 经历 3 次试错）
> 对应文档：同级目录的 `MIGRATION_CHECKLIST.md`（迁移进度清单）

---

## 一、架构分层 & 系统主入口文件总表

> **4 个程序集（asmdef）**：BattleBall.Core / Battle / Systems / UI，互相依赖：
> Core ← Battle ← Systems ← UI
>
> `asmdef` 路径：
> - Core → `Assets\_Project\Scripts\Core\BattleBall.Core.asmdef`
> - Battle → `Assets\_Project\Scripts\Battle\BattleBall.Battle.asmdef`
> - Systems → `Assets\_Project\Scripts\Systems\BattleBall.Systems.asmdef`
> - UI → `Assets\_Project\Scripts\UI\BattleBall.UI.asmdef`
> - Test → `Assets\_Project\Scripts\Test\BattleBall.Test.asmdef`（独立测试程序集）

### ① Core 层（最底层：全局状态 + 数据加载 + 存档持久化真类）

| 系统 | 主类名 | 主入口文件 | 职责说明 | 备注 |
|---|---|---|---|---|
| 全局游戏状态机 | **GameManager** | `Assets\_Project\Scripts\Core\GameManager.cs` | 游戏阶段(PREPARE/KICKOFF/PLAYING/END)、得分、LastMenuMode(上次菜单模式)、MODE_SLOT_PLAYER/ADMIN 存档槽常量、MatchTime | 属性均 PascalCase |
| 数据加载 | **DataManager** | `Assets\_Project\Scripts\Core\DataManager.cs` | 读取 JSON 数据：角色、技能、精灵图鉴(`Spirits`)、`GetCharacterById(id)`、`GetSkillById(id)` | 返回 `Dictionary<string,object>` / `List<Dict>` 通用结构 |
| 存档持久化真类 | **PlayerSaveManager**（Core版） | `Assets\_Project\Scripts\Core\PlayerSaveManager.cs` | 787 行 Newtonsoft.Json 磁盘读写、存档路径、RARITY 常量、真正的序列化/反序列化 | ⚠️ 命名空间 `BattleBall.Core.PlayerSaveManager`。UI 层通过别名引用 Systems 版，避免和适配版撞名 |

### ② Battle 层（核心赛场：球员/球/物理/AI 决策）

| 系统 | 主类名 | 主入口文件 | 职责说明 | 备注 |
|---|---|---|---|---|
| 球员控制器 | **PlayerController** | `Assets\_Project\Scripts\Battle\PlayerController.cs` | 球员属性、移动、精灵装备(`EquipSpirit`/`UnequipSpirit`)、12+ Spirit 兼容重载方法(TurnOnLight/TurnOffLight/AddTickEffect/AddSkillXxx)、ReturnToPrevious/TeleportTo、`GetVisualRadius()`(0.5f) | Pascal 兼容属性：`CharacterId` / `CharData`；snake_case 方法（供 Spirit 链路调用） |
| 决竞球控制器 | **BallController** | `Assets\_Project\Scripts\Battle\BallController.cs` | 球物理、碰撞、得分、`GetVisualRadius()`(0.3f) | 碰撞圈 0.3 与视觉半径对齐 |
| 输入管理 | **InputManager** | `Assets\_Project\Scripts\Battle\InputManager.cs` | 键鼠/手柄输入映射 → 球员动作 | — |
| 比赛状态驱动 | **BattleManager** | `Assets\_Project\Scripts\Battle\BattleManager.cs` | 比赛阶段推进、发球、犯规判定、得分调用 GameManager | — |
| 精灵 AI 决策 | **SpiritAIManager** | `Assets\_Project\Scripts\Battle\AI\SpiritAIManager.cs` | 什么时候用什么精灵技能、目标选择、技能释放优先级决策 | 反射调用 Spirit 系统跨程序集方法 |
| 幻象球放置 | **IllusionPlacer** | `Assets\_Project\Scripts\Battle\Physics\IllusionPlacer.cs` | 在球场上放置幻象/假球，干扰 AI 感知 | `using Random = UnityEngine.Random;` 已消除歧义 |
| AI占位类 | **AIProfile / AIManager** | `Assets\_Project\Scripts\UI\PreparationUI.cs`（命名空间 `BattleBall.UI.Battle`） | 占位类，给 PreparationUI 类型引用用 | 真实 AI 行为后续补到 BattleBall.Battle 程序集 |

### ③ Systems 层（系统桩：存档适配/背包/营养/训练/奖励/Buff/精灵）

> ⚠️ UI 层别名 `using PlayerSaveManager = BattleBall.Systems.PlayerSaveManager;` 指向下面的**适配版**。Core 版那个是真持久化，不用改。

| 系统 | 主类名 | 主入口文件 | 职责说明 | 备注 |
|---|---|---|---|---|
| 存档UI适配 | **PlayerSaveManager**（Systems版） | `Assets\_Project\Scripts\Systems\PlayerSaveManager.cs` | UI调用的所有存档/装备/训练/耐久接口：`GetEquippedItem`/`GetEquippedDurability`/`EquipmentBonuses`/`GetCharacterTrain`；静态字典 `RARITY_MAX_DURABILITY`（5档稀有度→最大耐久，`Dictionary<string,object>`）；`SaveData()`/`GetData()`/`LoadSlot()`/`GetCurrency()`/`GetAllEquipped()`（返回 `Dictionary<string,Dict<...>>`） | 所有装备接口有 **(int playerIdx)** 和 **(string charId)** 两套重载（UI传string角色ID优先） |
| 背包装备 | **InventoryManager** | `Assets\_Project\Scripts\Systems\Inventory\InventoryManager.cs` | 物品定义(`GetItemDef`)、稀有度颜色(`GetRarityColor`)、装备穿戴卸下（两种参数风格：int playerIdx 版 / string charId 版）、背包分类列表(`GetBackpackEquipment`/`GetBackpackConsumables`) | snake_case 兼容 API：`get_item_count` / `remove_item` |
| 营养食物 | **NutritionManager** | `Assets\_Project\Scripts\Systems\Nutrition\NutritionManager.cs` | 食物列表/定义、激活食物Id(`GetActiveFoodId` 有/无参版)、队伍加成(`GetTeamBonuses` 有/无参版)、消耗食物(`ConsumeFood` 1参/2参版)、存档加载(`LoadFromSave` 有/无参版) | 少参版全部有默认值/空返回 |
| 场地+属性训练 | **TrainingManager** | `Assets\_Project\Scripts\Systems\Training\TrainingManager.cs` | 场地等级(`GetFieldLevel` 0/1参版)、升级成本(`GetFieldUpgradeCost` int版/string版)、训练成本(`GetTrainCost` 0/2参版)、训练属性(`CanTrain`/`TrainStat` 2/3参版)、场地升级(`UpgradeField` 0/1/2参版)、属性上限(`GetStatMax`)、常量 `TRAIN_BONUS_PER_TIME = 1.0f` | **全签名重载铺齐**，对 UI 任何调用形式都能编译 |
| 奖励+连胜 | **RewardSystem** | `Assets\_Project\Scripts\Systems\RewardSystem.cs` | 奖励开关（实例版：`GetRewardEnabled`/`SetRewardEnabled` / `GetWinStreak`）+ **静态包装版**（UI 类名直接调）`RewardEnabledStatic()` / `RewardSetEnabledStatic(bool)` / `WinStreakStatic()` | UI 代码风格是 Godot autoload → **类名.方法()**，所以用 static 包装入口转到 Instance，不改原 virtual |
| Buff 系统 | **BuffManager** | `Assets\_Project\Scripts\Systems\BuffSystem\BuffManager.cs` | AddBuff 多参版本：原版 + 6参数版（供 SpiritTagEffectHandler 调用） | 效果叠加/持续时长控制后续补 |
| 精灵效果标签 | **SpiritTagEffectHandler** | `Assets\_Project\Scripts\Systems\SpiritSystem\SpiritTagEffectHandler.cs` | 精灵技能命中后应用标签 Buff：伤害/治疗/增益/减益/灯光开-关、Tick 伤害、技能冷却/耗蓝倍率、额外使用次数 | 调用 PlayerController 的多参重载方法 |
| 技能视觉特效 | **SkillVisualManager** | `Assets\_Project\Scripts\Systems\SpiritSystem\SkillVisualManager.cs` | 画技能范围圈、Buff 视觉、碰撞半径（用 PlayerController.GetVisualRadius / BallController.GetVisualRadius） | — |
| 精灵 UI 面板 | **SpiritUI** | `Assets\_Project\Scripts\Systems\SpiritSystem\SpiritUI.cs` | 精灵技能按钮、冷却条、Buff 状态显示 | `.rectTransform` → 已改成 `GetComponent<RectTransform>()`；desc 变量重名已修 → `dsc` |

### ④ UI 层（uGUI 界面脚本）

| 场景界面 | 主类名 | 主入口文件 | 职责说明 | 跨层引用 |
|---|---|---|---|---|
| **公共菜单基类** | **BaseSystem** | `Assets\_Project\Scripts\UI\BaseSystem.cs` | 所有菜单父类：金币标签刷新、装备面板公共逻辑、角色解锁列表、字典工具方法(GetStr/GetInt/GetDict) | PSM / InventoryManager / TrainingManager / NutritionManager |
| **主菜单入口** | **MainMenu** | `Assets\_Project\Scripts\UI\MainMenu.cs` | 启动后第一个界面：玩家模式/管理员模式切换、存档槽切换(MODE_SLOT_PLAYER/ADMIN)、奖励开关按钮、构建子菜单 | GameManager.LastMenuMode、PSM.CurrentSlot/LoadSlot、RewardSystem 静态包装 |
| **赛前准备 UI** | **PreparationUI** | `Assets\_Project\Scripts\UI\PreparationUI.cs` | **最大 UI 文件**：球员装备面板(3槽位)、精灵装备面板、训练按钮+成本、场地升级按钮+成本、角色属性、食物营养、AI/AiProfile 占位类（namespace Battle） | 几乎覆盖以上所有管理器 |
| **赛后结果 UI** | **MatchResultUI** | `Assets\_Project\Scripts\UI\MatchResultUI.cs` | 胜负显示、连胜提示、奖励未开启提示、赛前赛后装备变化对比 | RewardSystem、NutritionManager、PSM |

---

## 二、启动前置（必做）
1. Unity Editor 打开 `BattleBall_Unity` 项目 → 确认 Console 无 C# 错误（应显示 0 errors）
2. **Build Settings → Player Settings**：Scripting Backend 选 **Mono**（IL2CPP 禁止用于开发阶段）
3. 打开入口场景：优先 **MainMenu 场景**（如果没有就开 Battle 场景）
4. Console 面板点 **Clear**，勾选「Error Pause」避免错漏。

---

## 三、P0 冒烟测试（10 分钟。任意一项不过 → 停，优先修致命）

> 目标：不崩、能进、能出。P0 全过再进 P1。

| 编号 | 操作步骤 | 预期通过标准 | Console 观察点 | 对应主入口文件 |
|---|---|---|---|---|
| P0-1 | 按 ▶ Play | 不崩溃、不卡 Loading；界面正常渲染（主菜单或球场）；Unity 不弹 White Sphere 缺失 | 连续 `NullReferenceException` → 失败；少量 `[xxx] 调用` 桩日志 → **通过** | GameManager.cs / MainMenu.cs / BattleManager.cs |
| P0-2 | 主菜单 → 点「玩家模式」→ 再点「管理员模式」来回切 3 次 | 每次切换按钮状态刷新；不崩 | 不出现 `LastMenuMode Null` 或 `LoadSlot FormatException` → **通过** | MainMenu.cs ↔ GameManager.cs ↔ PlayerSaveManager.cs (Systems版) |
| P0-3 | 若直接开 Battle 场景：看 6 名球员 + 决竞球 | 全部显示，无白膜（之前 PBR 贴图已修）；球员全部站立不躺；球不穿透地板 | PlayerController.Awake 不 Null `charId` → **通过**；BallController 初始化正常 → **通过** | PlayerController.cs ↔ BallController.cs |
| P0-4 | 按 Esc 或返回按钮 / 手动停止 Play | Unity 正常退出到编辑态、无残留报错；OnDestroy 不循环 NullRef | Dispose/OnDestroy 不抛异常 → **通过** | 全部 MonoBehaviour |

---

## 四、P1 核心系统验证（30~40 分钟。P0 全过后再开始）

> 目标：逐个验证 11 个系统桩/管理器/UI 功能链路不炸（允许功能没数据但**不能报 Exception**）。

### A. 数据/存档层（3 项）

| 编号 | 操作 | 预期（不报错=通过） | 对应主入口 |
|---|---|---|---|
| P1-A1 | 主菜单 「玩家模式 ↔ 管理员模式」切换 3 次 | PlayerSaveManager.CurrentSlot 记住状态；不爆 Null/FormatException | Core/GameManager.cs ↔ Systems/PlayerSaveManager.cs |
| P1-A2 | 主菜单 → 奖励开关按钮（OnToggleReward）：开 → 关 → 开 3 轮 | 按钮文字在「比赛奖励: 已开启 / 已关闭」切换；颜色黄/灰 | Systems/RewardSystem.cs（静态包装） |
| P1-A3 | **角色系统/图鉴** 入口（若存在）→ 打开角色列表/精灵列表/技能列表 | 面板打开不白屏崩；DataManager.GetCharacterById / GetSkillById / Spirits 属性被调用不 Null | Core/DataManager.cs |

### B. 赛前准备 UI（5 项）— 打开 PreparationUI

| 编号 | 操作 | 预期（不报错=通过） | 对应主入口 |
|---|---|---|---|
| P1-B1 | 点「进入准备」或跳转 PreparationUI 场景 | 面板整体渲染；球员卡/装备槽/精灵槽/训练面板/场地升级区域全部可见（没数据也显示"未装备/0成本"文字） | UI/PreparationUI.cs |
| P1-B2 | 装备面板：3 个装备槽 glove / jersey / shoes | 显示「装备名: 未装备」或实际装备名；稀有度颜色显示 | Systems/PlayerSaveManager.cs ↔ InventoryManager.cs（简化兜底当前返回空 → **显示未装备 = 通过**） |
| P1-B3 | 精灵装备面板：选一个精灵 → 点「装备」 → 再点「卸下」 | Console 出现 `[PC] EquipSpirit: ?` / `[PC] UnequipSpirit 调用` 两条日志桩 | Battle/PlayerController.cs（Spirit 装备兼容方法） |
| P1-B4 | 训练面板：看场地等级(Lv) → 点「训练」/「升级场地」按钮 | 可点击不报 Null；TrainingManager.TRAIN_BONUS_PER_TIME 不 CS0117；TRAIN_BONUS_PER_TIME 存在；属性数值 +1/0 都行 | Systems/TrainingManager.cs（简化兜底当前成本显示 0 = **通过**） |
| P1-B5 | 营养面板：点列表中任一食物 → 点「消耗」 | NutritionManager.ConsumeFood 调用不崩；当前食物 Id 刷新 | Systems/Nutrition/NutritionManager.cs |

### C. 比赛场景（3 项）

| 编号 | 操作 | 预期 | 对应主入口 |
|---|---|---|---|
| P1-C1 | 点「开始比赛」→ 观察阶段流：准备→发球→攻防→结束 | GameManager.phase 正常推进（PREPARE / KICKOFF / PLAYING / END）；AI 球员和玩家都动 | Battle/BattleManager.cs ↔ Core/GameManager.cs |
| P1-C2 | 比赛中主动释放精灵技能 或 观察 AI 决策触发 | Console 出现链路桩日志：`[SpiritAI] → [PC] UseSkill → [BuffMgr] AddBuff Xxx` 不崩 | Battle/AI/SpiritAIManager.cs → SpiritTagEffectHandler.cs → BuffSystem/BuffManager.cs |
| P1-C3 | 推进一次得分（进球） | 分数 +1、阶段重置回下一局、球员归位；BallController 碰撞不穿圈 | Battle/BallController.cs ↔ Core/GameManager.cs |

### D. 赛后结果 UI（2 项）— 打完一场胜利打开 MatchResultUI

| 编号 | 操作 | 预期 | 对应主入口 |
|---|---|---|---|
| P1-D1 | 打开 MatchResultUI 胜利面板 | 胜利文案显示；连胜 streak 读取（当前桩返回0=不显示连胜也可=通过）；奖励未开启提示（开关关的话）出现 | UI/MatchResultUI.cs ↔ Systems/RewardSystem.cs（WinStreakStatic） |
| P1-D2 | 装备变化对比（赛前 / 赛后） | 装备槽位渲染（简化兜底传空Dict → 显示"无变化/未装备"也可 = **通过**；不报 Null/KeyNotFound） | UI/MatchResultUI.cs ↔ PlayerSaveManager.cs |

---

## 五、P2 边界 & 细节校验（可选 20 分钟 / P1 全过后跑）

| 编号 | 操作 | 预期 |
|---|---|---|
| P2-1 | 主菜单快速开/关 5 次循环 → 不退出Unity → 再进比赛 | SaveData 文件不损坏、不再爆 JSON 反序列化错误 |
| P2-2 | 单一球员身上连叠 3 次以上 Buff / 精灵技能 | BuffManager 6 参数重载无 ArgumentNullException；Spirit 冷却叠加正常 |
| P2-3 | 背包中装备一件 rare（稀有）/ epic（史诗）装备 | InventoryManager.GetRarityColor 返回非白色（稀有≠默认颜色） |
| P2-4 | IllusionPlacer 假球幻象：手动放置一次 | 假球生成、AI 感知位置不 Null 越界 |
| P2-5 | 装备/精灵按钮快速点击 10 次压力测试 | 不出现 KeyNotFoundException / NullReferenceException 连环 |
| P2-6 | TrainingManager 所有方法签名各调用一次（0参/1参/2参） | 不报 CS1503 / CS7036（签名全铺齐已验证） |

---

## 六、本轮"简化兜底遗留清单"（⚠️ 不是 Bug，是写死空数据，之后再联调）

> 这是**最后2条C#错误**（试错 3/3）为保证通关 0 errors 而做的**简化兜底写法**。如果在 P1/P2 中发现对应功能"始终空 / 始终0"，不是新报错，直接看这里。

| 精确位置 | 当前简化写法（兜底） | 正确联调做法（后续改） |
|---|---|---|
| `UI/BaseSystem.cs L370` | `var unlockedChars = new List<object>(); // 简化兜底...` | 改成 `PlayerSaveManager.Instance.GetData()["unlocked_characters"] as List<object>` 返回真实解锁角色列表 |
| `UI/MatchResultUI.cs L286` | `GetDict(new Dictionary<string, object>(), charId, ...)` **直接空Dict** | postEquip 类型是 `Dictionary<string, Dictionary<string, object>>`（嵌套Dict），需先扁平化成 `Dictionary<string, object>` 再传入，或按 key 正确取赛后角色装备 |
| `UI/PreparationUI.cs L576` | `int upgradeCost = _uc.ContainsKey("gold") ? (int)_uc["gold"] : 0` | 若 TrainingManager 返回的成本字典键名不是 `"gold"`（是 `"coin"` / `"cost"` 等）→ 改这里的键名字符串 |
| `UI/PreparationUI.cs L584` | `int trainCost = _tc.ContainsKey("gold") ? (int)_tc["gold"] : 0` | 同上，键名若不对改这里 |
| `UI/PreparationUI.cs L682 / L764 / L800` | `_eiXXX["item_id"]` 取装备ID | 若 PSM.GetEquippedItem 返回的装备字典键名是 `"id"`（而非 `"item_id"`）→ 改这3处的键名 |
| `UI/PreparationUI.cs L695 / L774 / L804` | `System.Convert.ToSingle(_edXXX["current"])` 取当前耐久度 | 若 PSM.GetEquippedDurability 返回字典键名是 `"now"` / `"dur"` 而非 `"current"` → 改这3处的键名 |
| 11 个 Systems 层管理器的**全部桩方法** | 返回值都是 `0` / `true` / `new List()` / `new Dictionary()` | 等 JSON 数据链路 + 真实业务补全后，逐个把桩方法替换成真实逻辑（加载JSON、读写背包、计算加成） |

---

## 七、调试小技巧

### Console 快速过滤报错
- Unity Console 顶部按 **Error** 按钮（只显示红叉，隐藏黄 Warning 和 Debug.Log）
- 搜索框输入 `NullReference` / `ArgumentException` 排查致命错误

### Unity 强制重编译（改代码没生效时）
1. 窗口外点击 `Assets → Refresh`（或 Ctrl+R）
2. 仍没生效时：顶部菜单 `Assets → Reimport All`（只重导入代码，慢一点但稳定）

### 清缓存卡（编译不更新时）
- 删除 `BattleBall_Unity\Library\ScriptAssemblies\*.dll` 和 `BattleBall_Unity\Library\Bee\` 目录 → 再进 Unity 会完整重编

---

## 八、里程碑 & 后续路线（编译 0 errors 达成后）

1. **第一阶段已达成**：GDScript → C# 全部系统迁移 + C# 0 errors ✅
2. **当前阶段（P0~P2 Play 测）**：运行时链路打通，修正简化兜底遗留清单中的 7 处（上表）
3. **下一阶段**：
   - JSON 真实数据加载（DataManager 真实读取 characters.json / skills.json）
   - 精灵技能资源特效对接（SkillVisualManager + 美术资源）
   - AI 真实决策替换 AIManager 占位类
   - UI 接入美术 uGUI 资源（UGUI 布局图替换代码生成占位UI）
   - 球员 FBX/动作 3D 资源补全 + 动画驱动
4. **完整游戏验收**：AI vs AI 自动跑满一场，0 Exception，阶段流、得分流、精灵技能流、赛后奖励流全部闭环

---
*— 文档生成：2026-09-04 于决竞球 BattleBall_Unity 项目 —*