# 决竞球 Unity 迁移 → 6 大类统一核查 CheckList

## A 层 编译 (最高优先: 0 CS0xxx Error)
|ID|项目|接受标准|状态|
|---|---|---|---|
|A1|22 .cs 语法错=0|Console 0 CS0246/CS0103/CS1503/CS0115/CS0161|✅ 2026-09-07 MCP 核查 0 错 0 警(实际 54 .cs)|
|A2|BattleHud UnityEngine.UI 无缺失|UGUI/TMP 包导入后 0 CS0246|⬜|
|A3|跨命名空间引用齐全|AiManager 能引用 Battle.Core + Battle + Systems.*|⬜|
|A4|@onready 字段不空|Awake/Start 赋值；Play 时无 MissingReferenceException|⬜|

## B 层 单例/初始化
|B1|GameManager.Instance|非空; Preparation→Half1→Mid→Half2→Result; gameTime 递减|⬜|
|B2|DataManager.Instance|Characters.Count>=7; player1.name=猪猪侠|⬜|
|B3|BattleManager teamA/teamB|各3Transform非空 (TestBattle A_1/2/3 B_4/5/6)|⬜|
|B4|开球&回球权|每半场发球方底线开球; 进球→对方中场开球|⬜|

## C 层 球员/手感 (px÷100→m)
|C1|尺寸|player root=0.01, node_0=1 → Renderer.bounds≈(0.77,0.77,0.50)m|✅ Done|
|C2|WASD 移动|speed=5m/s; LeftShift 冲刺+2.5m/s|✅ 主人确认|
|C3|场地钳制|pos ∈ [-6.5,6.5]×[-3.9,3.9] (margin 0.3)|⬜|
|C4|持球跟随|ball.pos = owner.pos + (0,0.4,0); 抖动<1cm|⬜|
|C5|投球/射门|按 J 沿 +Z 飞 B 球门; 速度 6m/s|⬜|
|C6|冲刺+体力|体力100→Sprint每帧-8; <30%禁止; 冷却 2s|⬜|

## D 层 球物理
|D1|球尺寸|Renderer.bounds dia≈0.30m; SphereCollider radius=0.15|✅ Done|
|D2|飞行&最大距离|Shoot(+X,6,0)→飞 5m 停; isActive=false|✅ 反射调用通过|
|D3|出界回队|x>+6.5→队B最近; x<-6.5→队A最近|⬜|
|D4|命中停下|碰撞非发球者→isActive=false; OnBallHitPlayer触发1次|⬜|
|D5|CCD 不穿透|ContinuousDynamic; 8m/s 不穿透 0.3m 球员|⬜|

## E 层 AI 决策
|E1|决策节奏|200ms/次; 无每帧抖动|⬜|
|E2|6状态机|Idle/Chase/Support/Cover/GoHome/Shoot/Pass 切换; 卡死<5s|⬜|
|E3|角色分工|主攻射门; 辅助传; 防御站位|⬜|
|E4|分离力|inner=0.06m/outer=0.14m; 30帧无>0.3m穿模|⬜|
|E5|auto_simulate|auto_simulate=true 完整打一场(比分>0)|⬜|

## F 层 systems
|F1|Buff 6 层初始化|Buff/Status/Duration/Mult/Check/Label 字典非空; 100+标签加载|⬜|
|F2|元灵绑定|7角色 spirit.id 匹配 spirits.json; visual Prefab激活|⬜|
|F3|技能触发|持球+冷却+按键→SpiritSkillTrigger.OnTrigger→BuffManager堆叠标签|⬜|
|F4|元素克制|elements.json克制表 OnBallHitPlayer 处 ±25% 乘算|⬜|
|F5|技能三态|casting(0.3s)/cooldown(5s)/ready; UI灯对应|⬜|

## 危险项 (0 才交付)
- 比分僵死 0-0 → Fail
- AI 卡死 >200 帧 → Fail
- 传球率 <80% → Fail
- 全场零击中 <1 → Fail
- Console 任何 NRE → 修 A4 重跑
