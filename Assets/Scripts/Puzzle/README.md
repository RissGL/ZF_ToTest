# 场景解密的框架（Puzzle）

48h 原型第二块：**点场景里的东西 → 规则表决定发生什么 → 改全局状态 → 别的时代跟着变**。

核心不是"解密算法"，是**规则引擎 + 一处权威状态**。解密游戏真正难写的从来不是算法，是
「谁在什么条件下点了什么、世界该怎么变」这件事别写散。

---

## 一、怎么跑起来（演示）

先有四个时代窗口（`Tools/时代窗口/搭建四个时代窗口场景`），然后：

1. 菜单 **Tools → 谜题 → 搭建解密演示**
2. 按 Play

演示的谜题链就是「原始人钻木取火 → 现代人的壁炉跟着点着」：

| 步骤 | 操作 | 发生什么 |
| --- | --- | --- |
| 1 | 点石器时代窗口进去，点左上角的**岩壁** | 拿到「燧石」，`rock_searched = 1` |
| 2 | 按 `Tab` 把燧石拿在手里，点右边的**柴堆** | 燧石被消耗，`fire_lit = 1`，柴堆切到 `lit` |
| 3 | — | 谜题 `P_stone_fire`（主线）完成 → **石器时代窗口亮对勾** |
| 4 | `ESC` 回全景，进信息时代 | **壁炉自己烧起来了** ← 跨时代联动 |

清理：**Tools → 谜题 → 清掉解密演示**。
规则表想重置：**Tools → 谜题 → 重建演示规则表（覆盖）**（平时重跑搭建菜单不会覆盖你手改过的规则）。

操作：`左键点物体` = 交互，`Tab` = 换手里的道具（背包 UI 还没做，先看 Console）。

---

## 二、三条原则

1. **权威状态只有一份**（`PuzzleModel`），场景物体只是它的**视图**。
   「火点着了」是 `PuzzleModel` 里的一个 flag，不是石器时代那个 `SpriteRenderer` 上的开关 ——
   所以信息时代的壁炉能读到它。**这条是能不能跨时代联动的分水岭**，也是存档能一行 JSON 搞定的原因。
2. **物体只声明"我是谁、能被怎么点"**（`Interactable`），一个字剧情逻辑都不写。
3. **规则表只声明"点了之后发生什么"**（`PuzzleTableSO`），不管场景怎么摆。
   所以同一个物体能参与多个谜题，改谜题不用动场景，改场景不用动谜题。

---

## 三、六个概念

| 概念 | 代码 | 说明 |
| --- | --- | --- |
| 可交互物 | `Interactable`（场景组件） | id + 时代 + 状态组 + 状态规则 + 点击区域 |
| 道具 | `PuzzleModel` 的背包 | id 字符串，捡到/用掉都是改 Model |
| 世界状态 | `PuzzleModel` 的 flag | `Dictionary<string,float>`，**全局唯一命名空间**，bool 就是 0/1 |
| 条件 | `PuzzleCondition` | `[SerializeReference]` 多态，9 种实现，可 And/Or/Not 嵌套 |
| 效果 | `PuzzleEffect` | `[SerializeReference]` 多态，9 种实现，顺序执行，可带延迟 |
| 谜题 | `PuzzleDefinition` | 完成条件 + 完成效果；标 `isMainPuzzle` 就是"这个时代的主线" |

---

## 四、规则表长什么样（策划真正写的东西）

一条规则 = `目标物 + 动作 + 条件[] → 效果[] + 不满足时的提示`

**匹配顺序**：规则表从上往下读，**第一条「目标 + 动作匹配 且 条件全满足」的生效**。
所以写法永远是「特殊条件在前，通用兜底在后」。演示表里就是这个形状：

| # | 目标 | 动作 | 条件 | 效果 |
| --- | --- | --- | --- | --- |
| 1 | `rock` | Interact | `rock_searched == 0` | 给燧石、`rock_searched = 1`、提示"抠出一块燧石" |
| 2 | `rock` | Interact | （无） | 提示"什么都没有了" ← 兜底，必须排后面 |
| 3 | `woodpile` | UseItem `flint` | 持有 flint | 消耗燧石、`fire_lit = 1`、柴堆→`lit` |
| 4 | `woodpile` | Interact | （无） | 提示"得有个火种" |
| 5 | `hearth` | Interact | `fire_lit == 1` | `hearth_lit = 1`、提示"火种穿过了时间" |
| 6 | `hearth` | Interact | （无） | 提示"壁炉是冷的" |

第 5 条**没有任何"跨时代"的字眼** —— 它只是读了 `fire_lit` 这个全局 flag。
联动是"状态共享"自然产生的，不是特判出来的。

### 玩家侧的动词只有两个

界面上玩家只有「点」和「选中道具后点」两种输入，所以动词只有 `Interact` / `UseItem`（外加 `Any` 通配）。
"观察 / 打开 / 推"这一套靠**条件**区分，不靠动词 —— 这也是上面第 5/6 条能写成两条同目标规则的原因。

`Interact` 时会自动分流：手里拿着道具就先找 `UseItem` 规则，**一条都没匹配上**才退回空手点。

---

## 五、物体外观也是规则（跨时代表现联动）

`Interactable` 的外观 = 状态组。状态是**算出来的**，不是存起来的：

```
1. 按顺序看 visualRules，第一条条件全满足的 → 用它的状态
2. 都不满足 → 用它自己的「物体状态」（SetObjectState 效果写的）
3. 还没有 → 用 defaultState
```

所以壁炉只要写一条 `fire_lit == 1 → lit`，石器时代点火它自己就烧起来，
**两个时代互相不知道对方存在**。这是这套框架里最值钱的一条。

---

## 六、数据流

```
点击物体 → InteractCommand → PuzzleSystem
              ├─ 按顺序挑「目标 + 动作匹配」的规则
              ├─ 逐条判条件，第一条全过的执行效果
              └─ 都不满足 → 用第一条"匹配但条件不满足"的规则的 elseFeedback
                     ↓
              PuzzleModel（flags / 物体状态 / 背包 / 已解谜题）
                     ↓ Revision +1，所有 Interactable 重算外观
              PuzzleSolvedEvent ──► isMainPuzzle 的话 → CompleteEraCommand
                                                          ↓
                                              EraWindowSystem.TryComplete
                                                          ↓
                                              EraCompletedEvent（窗口亮对勾）
                                              ↑ 以后"窗口里的人迁到下一个时代"就挂这儿
```

和 EraGallery 是同一条链：`点击 → 命令 → System 改 Model → 视图监听`。

### 和时代窗口的衔接

- `Interactable` 挂在 `EraWindow` 底下。聚焦某个时代时 EraGallery 只保留那个窗口活跃
  → **只有当前时代的物体能被点到**，但**条件读的是全局状态**，联动天然成立。
- `PuzzlePointerController` 只在 `FocusState == Focused` 时响应，
  所以全景下点物体 = 进那个时代，不会顺手把物体也点了。

---

## 七、代码结构

```
Assets/Scripts/Game/GameApp.cs      全游戏唯一的架构入口（注册 EraGallery + Puzzle 的 Model/System）
Puzzle/Core/PuzzleTypes.cs          Verb / FlagOp / PuzzleStates / PuzzleOps / PuzzleContext
Puzzle/Core/PuzzleModel.cs          IPuzzleModel：flags / 物体状态 / 背包 / 已解谜题 / Revision / 存档 JSON
Puzzle/Core/PuzzleCondition.cs      [SerializeReference] 条件基类 + 9 种实现
Puzzle/Core/PuzzleEffect.cs         [SerializeReference] 效果基类 + 9 种实现
Puzzle/Core/PuzzleTableSO.cs        InteractionRule / VisualStateRule / PuzzleDefinition / 规则表资产
Puzzle/Core/PuzzleSystem.cs         IPuzzleSystem：规则匹配 + 条件判定 + 效果执行 + 谜题完成判定
Puzzle/Core/PuzzleCommands.cs       Interact / ExplicitInteract / SelectItem / CycleSelectedItem / ResetPuzzle
Puzzle/Core/PuzzleEvents.cs         Interaction / Feedback / Solved / Item / Selection
Puzzle/View/Interactable.cs         场景可交互物（只报 id、开关状态组、悬停高亮）
Puzzle/View/PuzzlePointerController.cs  谜题侧的鼠标：悬停 + 点击 + 切换道具
Puzzle/View/PuzzleBootstrap.cs      把规则表交给 PuzzleSystem
Puzzle/Editor/PuzzleDemoBuilder.cs  一键搭演示 + 生成规则表
```

### 为什么只有一个架构（`GameApp`）

谜题条件要读「玩家现在进到哪个时代里了」，谜题完成要触发「这个时代通关」——
两个功能是互相咬着的。分成两个 `GameArchitecture` 的话，Model/System 就割裂了，
只能互相 `OtherApp.Interface.XXX` 硬跳。**一个游戏一个架构**，功能靠命名空间和文件夹分。

事件总线（`EventBus`）本来就是全局静态的，跨功能监听事件不需要同架构 —— 需要同架构的只有 Model/System/Command。

---

## 八、几个设计取舍（都写在代码注释里了）

| 事 | 怎么处理的 | 为什么 |
| --- | --- | --- |
| 条件/效果怎么扩展 | `[SerializeReference]` 多态 + 抽象基类 | Inspector 里能加不同类型；加新类型 = 加一个类，不动老代码。类型安全，字段不冗余 |
| 效果要"等动画" | 效果基类有 `delayBefore`；有延迟时 `PuzzleSystem` 借框架的 `MonoManager.Instance.StartCoroutine` 跑 | System 是纯逻辑没有 Update；这样效果类本身保持同步、可测 |
| 谜题完成怎么判定 | 状态每变一次就重查所有未解谜题的完成条件 | 策划只写"什么状态下算完成"，不用手动在每条规则里塞 `SolvePuzzle` |
| 递归爆栈 | `RecheckPuzzles` 用「再跑一轮」代替递归 | 完成效果里可能又解开别的谜题，链长了递归会爆 |
| 忘了填条件的谜题 | 0 条条件的谜题**不会**自动完成，只能被 `SolvePuzzleEffect` 显式完成 | 否则手滑漏填条件 = 开局自动通关 |
| 状态名打错字 | `Interactable` 找不到对应状态组时退回默认状态并吼一声，不让物体整个消失 | 内容一多必然打错，静默消失最难查 |
| **重叠形状的绘制顺序** | 见下面「坑：重叠形状的层级」 | 这是已经踩过的坑，症状极像逻辑 bug，其实是渲染 |
| 存档 | `PuzzleModel.ToJson()/LoadJson()` | 状态全在一个 Model 里（3 个字典 + 1 个集合），`JsonUtility` 直接序列化 |

### 坑：重叠形状的层级（画出来时有时无）

`SpriteRenderer` 的绘制顺序是 **sortingOrder → 离相机距离**。两个形状如果**层级相同又互相重叠**，
第二个条件分不出胜负，**画的先后就是不确定的**（每次 Play 都可能不一样）。

症状特别像逻辑 bug：壁炉的「着火了」那一组里，本体(1.60×1.20) 把炉膛和两个火苗都包在里面，
四个形状层级都是 7 —— 本体有时候画在火苗上面，就表现成
**「石器时代点了火、壁炉却没火」「有时候又有火」「有时候只有下面一个火苗」**。
状态其实一直是切对了的（不然它永远不会亮），纯粹是画的顺序没定。

规矩：**同一个物体里，重叠的形状必须给递增的 `sortingOrder`**。

- 搭建脚本按形状先后依次 +1（起点 `PuzzleSortingOrder.ObjectBase` = 11，在窗口边框 10 之上、锁-对勾标记 20 之下）。
- 手工在 Inspector 里加形状懒得算层级，就把 `Interactable` 的**「自动排形状层级」打开**（默认开）——
  它按层级顺序（就是搭建/Inspector 里的先后，和 uGUI 的兄弟顺序一个道理）依次 +1。
  代价是 Inspector 里手填的 `sortingOrder` 会被它覆盖。
- 关掉自动排的话，`Interactable` 在 Awake 里会检查「层级相同又有重叠」的形状并报警告
  （互斥的状态组不会同时出现，不比较；"边贴边"也不算重叠）。

---

## 九、还没做的（都能后面加，不影响骨架）

- **道具栏 UI / 反馈气泡**：现在捡东西、反馈都只进 Console。有 UI 之后监听
  `PuzzleItemEvent` / `PuzzleFeedbackEvent` / `PuzzleSelectionEvent` 就行，框架不用改。
  （顺带：工程里还没有中文字体，见 EraGallery 的 README。）
- **人物**：场景里会有人，操控方式待策划定。设计上留的口子是——
  人先当「也是一种 `Interactable`」（能点、有 id、有时代）；
  等确定能操控，操控层是**独立的一层**，只往 flag 空间里写（"人在哪个时代"、"手里拿什么"），
  规则表不用改一行。这也是"状态和物体解耦"这条原则的收益。
- **道具组合**（A + B = C）：加一个 `CombineItemsCommand` + 一条组合表，条件/效果能直接复用。
- **提示系统**：给每个 `PuzzleDefinition` 配一条提示条件链即可。
- **多结局**：状态都在 flag 里，结局判定就是读 flag。

---

## 十、注意

- 搭建脚本用 `SerializedObject` 写组件的私有 `[SerializeField]`，用
  `SerializedProperty.managedReferenceValue` 往 `[SerializeReference]` 列表里塞条件/效果。
  **改 `Interactable` / `PuzzleBootstrap` 的字段名要同步改搭建脚本**，写错了会报「找不到字段」而不是静默失败。
- 演示规则表用的是**代码构造的对象**（直接 `new FlagCondition{...}` 赋给列表）。
  这条路对 `[SerializeReference]` 是成立的 —— Unity 在存资产时会给它们分配引用 id。
  同一个实例不要往两个地方塞，否则会变成同一个引用。
- 规则表里的 id 全是字符串，**打错字不会报错，只会"没规则命中"**。
  规则一多就该加一个校验工具（菜单里跑一遍，报"规则引用了不存在的物体 id / flag 拼写可疑"）。
