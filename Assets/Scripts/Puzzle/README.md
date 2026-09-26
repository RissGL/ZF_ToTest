# 场景解密的框架（Puzzle）

48h 原型第二块：**点场景里的东西 → 规则表决定发生什么 → 改全局状态 → 别的时代跟着变**。
人物（每个时代都有人，人能跨窗口搬）也是这一块的一部分。

核心不是"解密算法"，是**规则引擎 + 一处权威状态**。解密游戏真正难写的从来不是算法，是
「谁在什么条件下点了什么、世界该怎么变、人该怎么走」这件事别写散。

---

## 一、怎么跑起来（演示）

先有四个时代窗口（`Tools/时代窗口/搭建四个时代窗口场景`），然后：

1. 菜单 **Tools → 谜题 → 搭建解密演示**
2. 按 Play

搭出来是：每个时代有**人物**（石器时代故意放**两个**：阿岩、阿石；其余每代一个：老铜/小灯/零）
\+ 每个时代一个**时间裂隙**，石器时代有岩壁和柴堆，信息时代有壁炉。

演示的链条把几件事都串上了：

| 步骤 | 操作 | 发生什么 |
| --- | --- | --- |
| 1 | 进石器时代，点**岩壁** | 拿到「燧石」 |
| 2 | `Tab` 拿在手里，点**柴堆** | 燧石消耗，`fire_lit = 1`，柴堆长出火苗 |
| 3 | — | 谜题 `P_stone_fire`（主线）完成 → **石器时代窗口亮对勾**；四个时代的裂隙同时张开 |
| 4 | 【**单独送一个**】点**阿岩**（点名，身上亮框）→ 点**时间裂隙** | **只有阿岩一个人**过去，阿石留在石器时代 |
| 5 | 【**整个时代一起走**】再点一次阿岩取消点名 → 点**时间裂隙** | 石器时代的人**一起**过去 |
| 6 | 进蒸汽时代看 | 阿岩/阿石站在老铜旁边，**三个人自动分开站好**（站位是算出来的） |
| 7 | — | 谜题 `P_two_together`（蒸汽时代 ≥ 2 人）自动完成 ← 「两个人凑一起才能解」 |
| 8 | `ESC` 回全景，进信息时代 | **壁炉自己烧起来了** ← 跨时代联动 |

**点人物 = 点名/取消点名**（也会说一句台词），点名的那个就是"单独送走"的对象。

清理：**Tools → 谜题 → 清掉解密演示**。
规则表想重置：**Tools → 谜题 → 重建演示规则表（覆盖）**（平时重跑搭建菜单不会覆盖你手改过的规则；
但表是旧版本的话搭建菜单会自己重建并告诉你）。

操作：`左键点东西` = 交互，`Tab` = 换手里的道具（背包 UI 还没做，先看 Console）。

---

## 二、三条原则

1. **权威状态只有一份**（`PuzzleModel` + `CharacterModel`），场景里的东西只是它们的**视图**。
   「火点着了」是 `PuzzleModel` 里的一个 flag，不是石器时代那个 `SpriteRenderer` 上的开关 ——
   所以信息时代的壁炉能读到它。「阿岩在蒸汽时代」是 `CharacterModel` 里的一条记录，不是他挂在哪个
   Transform 下面 —— 所以他才能真正跨窗口搬。**这条是能不能跨时代联动/迁移的分水岭**，
   也是存档能一行 JSON 搞定的原因。
2. **东西只声明"我是谁、能被怎么点"**（`Interactable` / `CharacterView`），一个字剧情逻辑都不写。
3. **规则表只声明"点了之后发生什么"**（`PuzzleTableSO`），不管场景怎么摆。
   所以同一个东西能参与多个谜题，改谜题不用动场景，改场景不用动谜题。

---

## 三、七个概念

| 概念 | 代码 | 说明 |
| --- | --- | --- |
| 可交互物 | `Interactable`（场景组件） | id + 时代 + 状态组 + 状态规则 + 点击区域 |
| 道具 | `PuzzleModel` 的背包 | id 字符串，捡到/用掉都是改 Model |
| 世界状态 | `PuzzleModel` 的 flag | `Dictionary<string,float>`，**全局唯一命名空间**，bool 就是 0/1 |
| 条件 | `PuzzleCondition` | `[SerializeReference]` 多态，11 种实现，可 And/Or/Not 嵌套 |
| 效果 | `PuzzleEffect` | `[SerializeReference]` 多态，11 种实现，顺序执行，可带延迟 |
| 谜题 | `PuzzleDefinition` | 完成条件 + 完成效果；标 `isMainPuzzle` 就是"这个时代的主线" |
| 人物 | `CharacterModel` + `CharacterView` | 一句话：**人在哪个时代是模型里的一条记录**，角色是视图。见第六节 |

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

## 六、人物（每个时代都有人，人能跨窗口搬）

**一句话：人物在哪个时代是 `CharacterModel` 里的一条记录，场景里的角色只是视图。**

```
CharacterModel
   ayan → Stone       ← 权威状态
   tong → Steam
   deng → Electric
   ling → Information
        ↓ 谁要搬到哪，就改这一条（CharacterSystem.TryMove）
CharacterView（挂在时代窗口下面的角色）
   照着模型的记录，把自己 SetParent 到那个时代的窗口上
```

于是三件事全是白送的：

| 想要的效果 | 怎么来的 |
| --- | --- |
| 进哪个时代才看得见那个人 | 他的父节点是那个时代的窗口；窗口不活跃，他自然不显示。**一行代码没写** |
| 人物跨窗口搬 | 改一条 Model 记录 + 换个父节点。场景怎么摆、有几层嵌套，都不用管 |
| 存档记住谁在哪 | `CharacterModel.ToJson()` —— 就一张 id→时代 的表 |

### 「通过一定方式移动」的方式是什么

**方式就是规则表里的一条规则。** 框架不规定方式，只给效果：

| 效果 | 作用 |
| --- | --- |
| `MoveCharacterEffect` | 把**指定的某一个人**搬到某个时代（写死 id，适合"剧情安排他过去"） |
| `MoveSelectedCharacterEffect` | 把**当前点名的那个人**搬过去 ← 玩家自己选谁走 |
| `MoveEraCharactersEffect` | 把**一个时代里的人整体**搬走 ←「这个时代通关了，人一起走」 |
| `SelectCharacterEffect` | 点名 / 取消点名（点名的那个就是上面第 2 条的对象） |

配合两个条件做分支：

| 条件 | 作用 |
| --- | --- |
| `SelectedCharacterCondition` | 点名的是不是某个人（characterId 留空 = 「有没有点中任何人」） |
| `SelectedCharacterInEraCondition` | 点名的那个人在不在这个时代 |
| `CharacterInEraCondition` | 某个人在不在这个时代 |
| `CharacterCountInEraCondition` | 这个时代里有几个人 ← **「两个人凑到一起才能一起解密」** |

演示里方式是一个**时间裂隙**，规则按「特殊 → 通用」从上往下排：

| 顺序 | 条件 | 效果 | 玩家看到 |
| --- | --- | --- | --- |
| ① | 裂隙开着 **且** 点名的人在这个时代 | `MoveSelectedCharacterEffect` | **只有他一个人**过去 |
| ② | 裂隙开着 **且** 这个时代有人 | `MoveEraCharactersEffect` | 这个时代的人**一起**过去 |
| ③ | 裂隙开着 | （不搬） | "这个时代里已经没有人了" |
| ④ | （无） | （不搬） | "裂隙还闭着" ← 兜底，必须排最后 |

所以玩家侧的规则很好记：**点人物 = 点名他，点裂隙 = 送他一个人；不点人直接点裂隙 = 整个时代一起走。**

换成"解开通关谜题才开"、"要拿着某个道具"、"只能送某一个人（写死 id）"，
都只是改条件 / 换效果，代码一行不用动。

### 站位是算出来的

`CharacterView` 每次刷新都会问模型"这个时代现在有几个人"，按自己在那份名单里的位置自动左右排开
（一个人站正中，两个人左右分开）。所以**谁搬走、谁搬来，剩下的人会自己重新站好，永远不会叠在一起** ——
不需要策划手动摆位置。要精确摆位，就在时代窗口里放一排 Seat 子节点让他认领（TODO）。

点名的角色会把高亮框**钉亮**（复用悬停框，不额外占一套形状），所以一眼看得出点的是谁。

配套的条件有两个：

| 条件 | 作用 |
| --- | --- |
| `CharacterInEraCondition` | 某个人现在在不在这个时代（"他得先过去"） |
| `CharacterCountInEraCondition` | 这个时代里有几个人 ← **「两个人凑到一起才能一起解密」就是它** |

演示里 `P_two_together`（蒸汽时代里 ≥ 2 人）就是用它判的：把阿岩从石器时代送过去，
这个谜题自己就完成了。

### 人物和物件是同一个写法

`Interactable`（物件）和 `CharacterView`（人物）都继承 `StateVisualBehaviour`，
所以状态组、状态规则、悬停高亮、点击区域、形状层级，两边在 Inspector 里长得一模一样。
人物也是**可点的**：点他就能触发规则（演示里是他说一句台词）。

唯一的差别：人物的默认形状层级更高（`CharacterBase`），人才会站在炉子/箱子前面；
还有人物的**高亮框挂在人物自己身上**（物件挂在窗口上）—— 因为人会搬，挂在窗口上就不会跟着走。

### 操控呢

还没做，但口子留好了：**操控层只需要往 `CharacterModel` 里写**（"人现在在这个时代"、"手里拿什么"），
`CharacterView` 和规则表一行都不用改。这也是"状态和物体解耦"这条原则的收益。

---

## 七、数据流

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
                                              ↑ 「这个时代的人一起搬到下一个时代」挂这儿
```

和 EraGallery 是同一条链：`点击 → 命令 → System 改 Model → 视图监听`。

### 和时代窗口的衔接

- `Interactable` / `CharacterView` 挂在 `EraWindow` 底下。聚焦某个时代时 EraGallery 只保留那个窗口活跃
  → **只有当前时代的东西能被点到**，但**条件读的是全局状态**，联动天然成立。
- `PuzzlePointerController` 只在 `FocusState == Focused` 时响应，
  所以全景下点东西 = 进那个时代，不会顺手把东西也点了。
- 人物要跨窗口搬，得知道"目标时代的窗口在哪" → `EraWindowRegistry`（窗口自己在 Awake 里登记，
  谁想找某个时代的窗口就问它）。这样人物系统不用反过来依赖 `EraWorldController`。

---

## 八、代码结构

```
Assets/Scripts/Game/GameApp.cs      全游戏唯一的架构入口（注册 EraGallery + Puzzle + 人物 的 Model/System）
Puzzle/Core/PuzzleTypes.cs          Verb / FlagOp / PuzzleStates / PuzzleSortingOrder / PuzzleOps / StateGroup / PuzzleContext
Puzzle/Core/PuzzleModel.cs          IPuzzleModel：flags / 物体状态 / 背包 / 已解谜题 / Revision / 存档 JSON
Puzzle/Core/PuzzleCondition.cs      [SerializeReference] 条件基类 + 11 种实现（含 2 种人物条件）
Puzzle/Core/PuzzleEffect.cs         [SerializeReference] 效果基类 + 11 种实现（含 2 种搬人效果）
Puzzle/Core/PuzzleTableSO.cs        InteractionRule / VisualStateRule / PuzzleDefinition / 规则表资产
Puzzle/Core/PuzzleSystem.cs         IPuzzleSystem：规则匹配 + 条件判定 + 效果执行 + 谜题完成判定
Puzzle/Core/PuzzleCommands.cs       Interact / ExplicitInteract / SelectItem / CycleSelectedItem / ResetPuzzle
Puzzle/Core/PuzzleEvents.cs         Interaction / Feedback / Solved / Item / Selection
Puzzle/Core/CharacterModel.cs       ICharacterModel：谁现在在哪个时代（权威状态 + 存档）
Puzzle/Core/CharacterSystem.cs      ICharacterSystem：搬一个人 / 搬一个时代的人
Puzzle/Core/CharacterEvents.cs      CharacterMovedEvent
Puzzle/View/StateVisualBehaviour.cs 物件和人物的公共基类：状态组 + 状态规则 + 高亮 + 点击区域 + 形状层级
Puzzle/View/Interactable.cs         场景里的东西（只报 id，其余全在基类）
Puzzle/View/CharacterView.cs        场景里的人：按模型记录把自己挂到对应时代的窗口下面
Puzzle/View/PuzzlePointerController.cs  鼠标：悬停 + 点击（物件和人物一视同仁）+ 切换道具
Puzzle/View/PuzzleBootstrap.cs      把规则表交给 PuzzleSystem
Puzzle/Editor/PuzzleDemoBuilder.cs  一键搭演示 + 生成规则表
Puzzle/Editor/PuzzleTableEditorWindow.cs  规则表编辑器（表格 + 校验 + 试跑）
Puzzle/Editor/PuzzleDependencyWindow.cs   依赖关系图（GraphView，只读）
Puzzle/Editor/PuzzleEditorScan.cs         扫描场景 id / flag 读写 / 出校验问题（两个窗口共用）
Puzzle/Editor/PuzzleSimulation.cs         试跑用的假 Model/System（不启动游戏）
Puzzle/Core/PuzzleRuleMatcher.cs     规则匹配的**唯一**实现（运行时和试跑共用同一套）
EraGallery/View/EraWindowRegistry.cs  「时代 → 窗口」登记处（人物搬家要找目标窗口）
```

### 编辑器工具（不用按 Play 就能查谜题）

| 菜单 | 干什么 |
| --- | --- |
| `Tools/谜题/规则表编辑器` | 把规则表变成**能读的表**：一行一条规则（目标 · 动作 · 条件 → 效果），拖拽排序，点一行看完整字段 |
| `Tools/谜题/依赖关系图` | 只读关系图：谁写了哪个 flag、谁又读了它、哪个谜题靠它完成、跨时代联动长什么样 |

**规则表编辑器**补的正是 Inspector 看不到的三件事：

1. **顺序 = 优先级**。表格一行一条，谁遮蔽了谁一眼可见 —— 规则表最常见的坑就是**兜底规则写在了前面**，
   编辑器会直接标 ❌「永远轮不到它」。条件完全重复的也会标出来。
2. **校验**：
   - 规则/条件/效果里引用的物体或人物 id，场景里到底有没有（打错字不会报错，只会"点了没反应"）
   - 条件要用的道具，有没有任何 `GiveItemEffect` 给过它
   - flag **只读不写**（条件永远不成立）/ **只写不读**（写了没人用）
   - 引用了不存在的谜题 id；谜题 id 重复；谜题没写完成条件（它不会自己完成）
3. **试跑**：在面板上摆好「flag 值 / 背包 / 每个人在哪个时代 / 点了谁 / 手里拿什么 / 玩家进在第几个时代」，
   点「跑一次」—— 它**走的是运行时那套匹配逻辑**（`PuzzleRuleMatcher`，不是另写一套），
   逐条告诉你：这条为什么没中（哪条条件断了）、这条为什么命中了、执行了哪些效果、人物被搬到哪了。

**依赖关系图**是**只读**的（能拖节点摆位置、能缩放，但拖不出新连线）——
因为谜题规则本质是「顺序查表」，画成可编辑的节点图反而表达不了顺序。
它的价值在**看关系**：跨时代的那条链（石器时代的火 → 信息时代的壁炉）、
死锁（flag 只写不读 / 只读不写）、主线谜题 → 时代通关 → 人物迁移。

> `PuzzleRuleMatcher` 这个抽出来的公共实现很关键：挖出它之前，运行时的匹配逻辑写在 `PuzzleSystem` 里，
> 编辑器要"试跑"就只能复制一份 —— 而复制的那份迟早和真跑的跑偏，工具也就没意义了。

### 为什么只有一个架构（`GameApp`）

时代窗口、谜题、人物三块是互相咬着的：谜题条件要读「玩家进到哪个时代了」「谁现在在哪个时代」，
谜题完成要触发「这个时代通关」，人物搬家又要让谜题条件重算。
分成多个 `GameArchitecture` 的话，Model/System 就割裂了，只能互相 `OtherApp.Interface.XXX` 硬跳。
**一个游戏一个架构**，功能靠命名空间和文件夹分。

事件总线（`EventBus`）本来就是全局静态的，跨功能监听事件不需要同架构 —— 需要同架构的只有 Model/System/Command。

> 一个小坑：框架里 `ISystem` 只带 `ICanGetModel`，**不带 `ICanGetSystem`**，
> 所以 System 里想拿另一个 System 得写 `this.GetArchitecture().GetSystem<T>()`。

---

## 九、几个设计取舍（都写在代码注释里了）

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

规矩：**同一个东西里，重叠的形状必须给递增的 `sortingOrder`**。

- 搭建脚本按形状先后依次 +1（物件起点 `PuzzleSortingOrder.ObjectBase` = 11，人物起点 `CharacterBase` = 21）。
- 手工在 Inspector 里加形状懒得算层级，就把 `StateVisualBehaviour`（`Interactable` / `CharacterView`）的
  **「自动排形状层级」打开**（默认开）—— 它按层级顺序（就是搭建/Inspector 里的先后，和 uGUI 的兄弟顺序
  一个道理）依次 +1。代价是 Inspector 里手填的 `sortingOrder` 会被它覆盖。
- 关掉自动排的话，它在 Awake 里会检查「层级相同又有重叠」的形状并报警告
  （互斥的状态组不会同时出现，不比较；"边贴边"也不算重叠）。

---

## 十、还没做的（都能后面加，不影响骨架）

- **道具栏 UI / 反馈气泡**：现在捡东西、反馈都只进 Console。有 UI 之后监听
  `PuzzleItemEvent` / `PuzzleFeedbackEvent` / `PuzzleSelectionEvent` 就行，框架不用改。
  （顺带：工程里还没有中文字体，见 EraGallery 的 README。）
- **操控人物**：位置已经归 `CharacterModel` 管了，操控层只要往它里面写
  （"人现在在这个时代"、"手里拿什么"），`CharacterView` 和规则表一行都不用改。
  要"走一段路 / 穿门"的演出，改 `CharacterView.PlayArriveFeedback`。
- **人物站位**：现在是"站位槽 × 间距"自动排（同一时代多个人会左右错开）。
  想让策划精确摆位，就在时代窗口里放一排 Seat 子节点，让 `CharacterView` 去认领。
- **道具组合**（A + B = C）：加一个 `CombineItemsCommand` + 一条组合表，条件/效果能直接复用。
- **提示系统**：给每个 `PuzzleDefinition` 配一条提示条件链即可。
- **多结局**：状态都在 flag 里，结局判定就是读 flag。
- **规则表校验工具**：跑一遍规则表，报"引用了不存在的物体/人物 id"、"flag 拼写可疑"。

---

## 十一、注意

- 搭建脚本用 `SerializedObject` 写组件的私有 `[SerializeField]`，用
  `SerializedProperty.managedReferenceValue` 往 `[SerializeReference]` 列表里塞条件/效果。
  **改 `StateVisualBehaviour` / `Interactable` / `CharacterView` / `PuzzleBootstrap` 的字段名要同步改搭建脚本**，
  写错了会报「找不到字段」而不是静默失败。
- 演示规则表用的是**代码构造的对象**（直接 `new FlagCondition{...}` 赋给列表）。
  这条路对 `[SerializeReference]` 是成立的 —— Unity 在存资产时会给它们分配引用 id。
  同一个实例不要往两个地方塞，否则会变成同一个引用。
- 规则表里的 id 全是字符串，**打错字不会报错，只会"没规则命中"**。
  规则一多就该加一个校验工具（见上面「还没做的」）。
- **`Interactable` 的字段被搬到了基类 `StateVisualBehaviour`**（为了让物件和人物共用）。
  Unity 按字段名序列化，所以搬基类不会丢数据 —— 老场景不用重搭。
- **坑：基类里的 Unity 生命周期方法**。`StateVisualBehaviour.Awake` 为了量准点击区域会先把所有状态组
  打开、量完再全关掉，所以它**结尾必须自己 `Refresh()` 一次**把正确的状态组摆回来。
  踩过一次：把 `Interactable` 精简成继承基类时漏了 `Start`（订阅 + 首次 Refresh），
  结果可交互物件一运行就被关掉、再也不显示；`CharacterView` 自己有 `Start` 所以没事。
  现在基类自己在 Awake 结尾兜了一次，子类就算覆盖/漏了 `Start` 也不会隐身。
  **给这类基类加生命周期方法时记住：`protected virtual`，子类一律 `override` + `base.Xxx()`。**
