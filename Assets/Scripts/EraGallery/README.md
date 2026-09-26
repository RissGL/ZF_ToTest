# 四个时代窗口（EraGallery）

48h 原型的第一块：**世界里摆四个窗口代表四个时代，相机能平移 + 缩放「推进」某个窗口里**。
四个时代**一开始就都能直接操作**（没有先后门禁）。通关会给窗口亮个对勾，
「一个时代完成后窗口里的人迁到下一个时代」就挂在这个通关事件上。

> 一关一关解那条链**留了接口但默认没启用** —— 见下面「解锁规则」。

---

## 一、怎么跑起来

1. 菜单 **Tools → 时代窗口 → 搭建四个时代窗口场景**
   （会生成占位方块图、`EraWorld` 根节点、2x2 的四个时代窗口，以及主相机上的相机执行器）
2. 按 Play

| 操作 | 效果 |
| --- | --- |
| 左键点窗口 | 相机推进并放大，进入这个时代 |
| 右键 / `ESC` | 退回全景（四个窗口一起拉回来） |
| 数字键 `1`~`4` | 直接进入第 N 个时代 |
| `C` | 把当前所在的时代标记成**通关**（窗口亮对勾，发 `EraCompletedEvent`） |

四个时代默认全是开的，点哪个进哪个。

清理：菜单 **Tools → 时代窗口 → 清掉四个时代窗口场景**。

---

## 二、场景里有什么

```
Main Camera                 Camera + EraCameraRig（只会「挪到某个矩形并缩放框住」）
EraWorld                    EraWorldController（总控）
├── EraWindow_0_Stone        石器时代   左上
├── EraWindow_1_Steam        蒸汽时代   右上
├── EraWindow_2_Electric     电气时代   左下
└── EraWindow_3_Information  信息时代   右下
```

每个 `EraWindow` 下面（全部是**一张 1x1 世界单位的白色方块图缩放 + 染色**拼的，没有别的美术资源）：

```
Backdrop      进到这个时代时打开的大遮光板（保证任何屏幕比例下都不会从边上漏出邻居）
Content       时代主题色的内容块
Ground        底部压暗的地景横条
Sig_*         时代专属几何剪影（金字塔 / 烟囱+齿轮 / 闪电+灯 / 节点网格）
Order_0..3    内容左上角一排小点，亮几个就是第几个时代
FrameTop/Bottom/Left/Right   四条边拼的边框
Mark_Locked   窗口下方的锁（✕ 两根斜杠拼的）
Mark_Done     窗口下方的对勾（✓ 两根斜杠拼的）
```

### 关于文字

**这个工程里目前一个字体资源都没有**（没有 `Assets/TextMesh Pro`、没有 TMP_FontAsset、没有 ttf），
所以窗口上暂时不画时代名，只用「主题色 + 几何剪影 + 序号点 + 锁定/通关标记」区分，
时代名和年代打在 Console 里。

等工程里导入中文字体（TMP Essential Resources + 一份带中文的 TTF/SDF）之后，
在 `EraWindow` 上加一个 `TextMeshPro` 引用、在 `ApplyVisual` 里刷文字就行，
`EraWindow` 上的 `title` / `timeline` 已经把数据备好了，窗口下方预留了标记那一行位置。

---

## 三、代码结构（全部在 ZGameFramework 的架构下）

```
（架构入口在 Assets/Scripts/Game/GameApp.cs —— 全游戏一个架构，见「谜题」那套的 README）
Core/EraTypes.cs              EraId / EraFocusState / EraVisualState / EraUnlockRule / EraPresentation / EraCatalog / 排序层级
Core/EraGalleryEvents.cs      EraFocusChangedEvent / EraFocusRejectedEvent / EraCompletedEvent
Core/EraWindowModel.cs        IEraWindowModel：聚焦序号、取景状态、通关/解锁（纯数据，不碰 MonoBehaviour）
Core/EraWindowSystem.cs       IEraWindowSystem：能不能进、进出终态、通关解锁的**规则**
Core/EraGalleryCommands.cs    FocusEraCommand / ExitEraFocusCommand / EraTransitionFinishedCommand / CompleteEraCommand
View/EraWindow.cs             单个窗口：长什么样 + 自己的取景框多大
View/EraCameraRig.cs          正交相机执行器：SnapTo / PlayTo（DOTween），不懂时代逻辑
View/EraWorldController.cs    总控（IController）：输入 → 命令；Model → 相机 + 窗口外观
Editor/EraWindowSceneBuilder.cs  一键搭场景 / 清场景
```

### 数据流（单向）

```
点击 / 按键
   ↓  SendCommand
FocusEraCommand ──► EraWindowSystem.TryFocus   （规则校验：忙不忙、解锁没解锁）
   ↓
EraWindowModel.FocusedIndex / FocusState       （BindableProperty 变更）
   ↓  监听
EraWorldController
   ├─ 只留目标窗口、打开它的遮光板、刷四个窗口的外观
   └─ EraCameraRig.PlayTo(窗口取景框)   ──补间结束──► EraTransitionFinishedCommand
                                                          ↓
                                              EraWindowSystem.NotifyTransitionFinished
                                                          ↓
                                                  Focused / Overview 终态
```

**关键约定**：相机只认 `FocusState` 这一个信号源。所以在任何地方改 Model（剧情、调试、存档读档）
都能得到正确的进出表现，不需要绕过控制器。

### 状态机

```
Overview ──FocusEraCommand──► Entering ──补间结束──► Focused
   ▲                                                    │
   └────── Overview ◄──补间结束── Leaving ◄──ExitEraFocusCommand
```

`IsBusy`（= Entering / Leaving）期间不接受新的进入请求，避免两个补间打架。

### 解锁规则（默认全开，接口留着）

`EraWindowModel.IsUnlocked(i)` 的判定顺序：

1. 序号越界 → 锁
2. `ForceLock(i)` 过 → 锁
3. `ForceUnlock(i)` 过 → 开
4. `UnlockRule == AllOpen`（**默认**）→ 开
5. `UnlockRule == PreviousCompleted` → `i == 0` 永远开着，否则要 `IsCompleted(i - 1)`

想改成一关一关解，只改 `EraWorldController` 上的「解锁规则」这一个枚举即可，
`RefreshVisuals` 会自动给还没解锁的窗口刷成 `EraVisualState.Locked`（压暗 + 亮锁标记），
点它会被 `TryFocus` 挡下并发 `EraFocusRejectedEvent`（窗口抖一下）。

`TryComplete(i)` 发的 `EraCompletedEvent.UnlockedIndex` 是
**「因为这次通关而新解锁的下一个时代」**：默认全开规则下永远是 `-1`，
切到 `PreviousCompleted` 才会变成 `i + 1`。所以监听者不用关心当前用的是哪条规则。

---

## 四、想改的地方

| 想改什么 | 改哪儿 |
| --- | --- |
| 四个时代的名字 / 年代 / 配色 | `EraCatalog.All`（改完重新跑一次搭建菜单即可覆盖场景；不重跑就只影响新搭的） |
| 解锁规则（全开 ↔ 一关一关解） | `EraWorldController` 的 `unlockRule`（枚举，不用改代码） |
| 窗口大小 / 间距 / 聚焦框 | `EraWindowSceneBuilder` 顶部那几个常量（`ContentWidth`、`PitchX`、`FocusWidth`…） |
| 相机手感（时长、缓动、起步预备） | 场景里 Main Camera 上的 `EraCameraRig` |
| 全景/进入的留白 | `EraWorldController` 的 `overviewPadding` / `focusPadding` |
| 时代剪影的画法 | `EraWindowSceneBuilder.BuildEraSignature` |
| 窗口外观随状态怎么变 | `EraWindow.ApplyVisual` |
| 通关后干什么（下一步：人物迁移） | 监听 `EraCompletedEvent`，或改 `EraWindowSystem.TryComplete` |

## 五、下一步接什么

- **窗口里的人迁到下一个时代**：`EraCompletedEvent` 带上了 `Index`（谁通关了）和
  `UnlockedIndex`（因此新解锁了谁，默认规则下是 `-1`），角色系统监听它就能知道该把谁搬到哪个窗口。
- **一关一关解**：把 `unlockRule` 换成 `PreviousCompleted` 就生效，其余代码不用动。
- **窗口里的谜题**：`EraWindow` 只用了一个 trigger 的 `BoxCollider2D` 做点选，
  且相机聚焦时只保留当前窗口活跃 —— 以后谜题物件直接挂在这个窗口下面即可，
  不在这个时代的物件天然不会被点到 / 不会被渲染。
- **时代名文字**：见上面「关于文字」。

## 六、注意

- 搭建菜单是**幂等**的：重复点会先清掉旧的 `EraWorld` 再重建（不会出现两份）。
- 搭建脚本用 `SerializedObject` 写组件上的私有 `[SerializeField]`。
  **改 `EraWindow` / `EraWorldController` 的字段名要同步改搭建脚本**，
  写错了会在 Console 报「找不到字段「xxx」」而不是静默失败。
- 窗口里的图案是**旋转过的矩形**，所以父节点必须等比缩放（窗口根节点 scale 恒为 1），
  不然斜杠会被拉斜。
- 相机取景用的是 `EraWindow.focusSize / focusOffset` 这两个序列化值，不依赖渲染器 ——
  窗口被隐藏（聚焦时其它三个会 SetActive(false)）也照样算得准。
- 画面比例变了（拖窗口 / 换分辨率）会自动重新贴合一次取景。
