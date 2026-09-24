# ZGameFramework 使用说明

本框架包含：架构层（IOC + Model/System/Utility/Command）、全局事件总线、对象池、资源加载（引用计数）、UI 框架（Panel/Window 分层 + 动画 + 窗口队列）、以及寻路/音频/网格等模块。

---

## 一、环境依赖

- Unity **2022.3**（URP）
- `Assets/Plugins` 下需有 **DOTween** 与 **UniTask**（资源异步加载、UI 动画要用）
- Cinemachine **2.x**（`ScreenShake` 用；3.x 需改 `Unity.Cinemachine`）
- 资源加载基于 `UnityEngine.Resources`，**不是 Addressables**

---

## 二、架构层

```csharp
using ZGameFramework;

// 1) 定义架构（全局唯一入口）
public class MyArch : GameArchitecture<MyArch>
{
    protected override void OnInit()
    {
        RegisterModel(new BagModel());
        RegisterSystem(new BattleSystem());
        RegisterUtility(new TimeUtility());
    }
}

// 2) 就地实现模块
public class BagModel : AbstractModel
{
    protected override void OnInit() { }      // 初始化数据
    protected override void OnDeInit() { }    // 释放
}

public class BattleSystem : AbstractSystem
{
    protected override void OnInit()
    {
        this.RegisterEvent<PlayerDeadEvent>(OnDead).UnregisterOnDestroyTrigger(this);
    }
    private void OnDead(PlayerDeadEvent e) { }
}

public class TimeUtility : IUtility { }       // 工具类只需实现空标记接口

// 3) 命令
public class AddGoldCommand : AbstractCommand
{
    protected override void OnExecute() { }
}

// 4) 访问
MyArch.Interface.GetModel<BagModel>();
this.GetSystem<BattleSystem>();               // 能力接口扩展方法（System/Model/Command/Controller 内可用）
this.SendCommand(new AddGoldCommand());
```

初始化顺序：首次访问 `Interface` → `OnInit()`（你的注册点）→ 先初始化所有 `IModel`，再初始化所有 `ISystem`。
`Deinit()` 顺序相反，并会清空容器。

### 注意

- **容器以「注册时用的泛型类型」为键**，必须用具体类型注册/获取。
  `RegisterSystem(new BattleSystem())` 之后要 `GetSystem<BattleSystem>()`；
  写成 `RegisterSystem<ISystem>(x)` 会导致取不到，并打印 `"{类型名}未注册但尝试获取"`、返回 `null`。
- `IUtility` 没有生命周期，也不会被注入架构。
- 容器非线程安全，只在主线程使用。

---

## 三、事件系统

强烈不建议直接使用EventBus 应统一走this.Register,Unregiest,SendEvent

Regiester会返回一个IUnregister,直接链式调用UnregiterOndestroyed就行，省去忘记在OnDestroyed取消订阅的麻烦

事件必须继承 `GameEvent`，分「带参事件」和「信号事件」两种形态。

```csharp
using ZGameFramework.Core;

public class PlayerDeadEvent : GameEvent { public int PlayerId; public string Cause; }
public class StartGameEvent : GameEvent { }

// 发布
EventBus.Publish(new PlayerDeadEvent { PlayerId = 1, Cause = "Lava" });
EventBus.PublishSignal<StartGameEvent>();

// 订阅（返回 IUnregister）
EventBus.Register<PlayerDeadEvent>(e => Debug.Log(e.PlayerId));
EventBus.Register<StartGameEvent>(() => Debug.Log("开始"));

// 在 System/Model/Controller 内可简写：
this.SendEvent(new PlayerDeadEvent { PlayerId = 1 });
this.SendEvent<StartGameEvent>();
this.RegisterEvent<PlayerDeadEvent>(OnDead).UnregisterOnDestroyTrigger(this);

// 自动反注册：UnregisterOnDestroyTrigger / UnregisterOnDisableTrigger / UnregisterWhenCurrentSceneUnloaded
```

### 注意

- **发布使用监听列表快照**，派发过程中注册/注销不影响本轮（已快照的监听本轮仍会被调用一次）。
- 监听列表无锁，非线程安全。
- `Publish` 不会回收事件对象，也不检查 `null`。

---

## 四、单例与主线程

```csharp
public class MyManager : Singleton<MyManager>
{
    private MyManager() { }        // 必须：非 public 无参构造
}
MyManager.Instance.DoSomething();

// MonoBehaviour 单例：MonoSingleton<T>（不跨场景） / PersistentMonoSingleton<T>（跨场景，会自动建物体）

// 非 MonoBehaviour 也能拿到 Update 与协程
MonoManager.Instance.AddUpdateListener(OnUpdate);
MonoManager.Instance.RemoveUpdateListener(OnUpdate);
MonoManager.Instance.StartCoroutine(Routine());
```

### 注意

- `Singleton<T>` 靠**反射查找非 public 无参构造函数**，缺失会抛异常；首次访问必须在主线程。
- `MonoSingleton<T>` 重复实例会 `Destroy(gameObject)`；`PersistentMonoSingleton<T>` 首次访问会自动创建 `{T}AutoCreator` 物体。

---

## 五、对象池

```csharp
// 引用类型
var data = ClassPool<MyData>.Get();
ClassPool<MyData>.Recycle(data);

// 集合
var list = ListPool<int>.Get();
ListPool<int>.Recycle(list);
var dict = DictionaryPool<string, int>.Get();
DictionaryPool<string, int>.Recycle(dict);

// GameObject（按名字分桶）
var go = PoolManager.Instance.GetGameObject(prefab, parent);
PoolManager.Instance.PushGameObject(go);

// 可选：实现 IPoolable 在出入池时清理/重置
public class MyData : IPoolable
{
    public void OnGet() { }
    public void OnRecycled() { /* 清空旧数据 */ }
}
```

### 注意

- **`ListPool` / `DictionaryPool` 必须手动归还**，否则泄漏；`Recycle` 会先 `Clear()`，**归还后原数据即失效**。
- `ClassPool<T>.Recycle` 先回调 `OnRecycled()` 再判容量，**池满（默认 200）时对象被静默丢弃**。
- **`PoolManager` 以 `GameObject.name` 为池键**，不同预制体重名会**串池**，请保证命名唯一。

---

## 六、资源加载

```csharp
using ZGameFramework.Core;

// 方式 A：直连加载（简单，但不计数，可能被 UnloadUnused 清掉）
var sprite = ResManager.Load<Sprite>("UI/Icons/Coin");
var sp2 = await ResManager.LoadAsync<Sprite>("UI/Icons/Coin");

// 方式 B：ResLoader 引用计数（推荐）
var loader = ResManager.GetResLoader();
try
{
    var cfg = loader.LoadSync<TextAsset>("Configs/Level");
    var prefab = await loader.LoadAsync<GameObject>("Prefabs/Enemy", token);
}
finally
{
    loader.ReleaseAllAndRecycle();     // 释放全部引用并归还池
}

// 方式 C：绑定到宿主，宿主销毁时自动释放（最省心）
var loader2 = ResManager.GetResLoader().BindTo(this);

// 实例化（内部走 PoolManager）
var go = ResManager.Instantiate("Prefabs/Enemy", parent);
ResManager.Recycle(go);

// 预热 / 卸载
await ResManager.PreloadAsync(paths, p => Debug.Log(p), token);
await ResManager.UnloadUnused();
```

### 注意

- 路径是 **`Resources` 目录下的相对路径且不带扩展名**。
- 缓存键 = `路径 + "_" + 类型名`，**同一路径用不同类型加载会各占一条缓存**；`GetRefCount<T>` 必须用与加载时相同的 `T`。
- `ResManager.Load/LoadAsync` **不增加引用计数**，需要稳定持有时请用方式 B / C。
- `PreloadAsync` 不写入缓存表、不产生引用计数。
- 释放或回收后再调用 `Load*` 会抛 `InvalidOperationException`；不调用 `ReleaseAll` 会造成引用计数泄漏。

---

## 七、UI 框架

### 分层

```
UIFrame (Canvas + GraphicRaycaster, 挂在 UI 层)
├── PanelLayer            → PanelUILayer
├── WindowLayer           → WindowUILayer
├── PriorityPanelLayer    → PanelPriority.Priority
├── PriorityWindowLayer   → WindowParaLayer（含弹窗遮罩 DarkenBG）
└── TutorialPanelLayer    → PanelPriority.Tutorial
```

`PanelPriority`：`None / Priority / Tutorial / Blocker`。
`WindowPriority`：`ForceForeground`（直接显示）/ `Enqueue`（排队，等当前窗口关闭后再显示）。

### 打开 / 关闭

```csharp
ui.OpenWindow("PlayerWindow");
ui.OpenWindow("PlayerWindow", new PlayerWindowProperties(data));   // 带属性
ui.CloseWindow("PlayerWindow");
ui.CloseCurrentWindow();

ui.ShowPanel("ToastPanel");
ui.HidePanel("ToastPanel");

ui.ShowScreen(id);      // 自动判断是 Panel 还是 Window
ui.HideAll();
ui.IsScreenRegistered(id);
ui.IsPanelOpen(id);
```

### 控制器

```csharp
// 面板
public class ToastPanelController : PanelController
{
    protected override void OnPropertiesSet() { /* 每次 Show 都会调用，用 Properties 刷界面 */ }
}

// 窗口（需要传参时自定义属性类）
[Serializable]
public class PlayerWindowProperties : WindowProperties
{
    public readonly List<PlayerDataEntry> PlayerData;
    public PlayerWindowProperties(List<PlayerDataEntry> data) { PlayerData = data; }
}

public class PlayerWindowController : WindowController<PlayerWindowProperties>
{
    protected override void AddListener()     { /* Awake 中执行一次：绑按钮、注册事件 */ }
    protected override void OnPropertiesSet() { Refresh(Properties.PlayerData); }
    protected override void WhileHiding()     { /* 开始关闭时 */ }
    // 关闭按钮直接绑 WindowController.UI_CloseWindow()
}
```

生命周期：
`Awake → AddListener()`；
`Show(props) → 校验类型 → SetProperties → HierarchyFixOnShow → OnPropertiesSet → 播 animIn → IsVisible=true → InTransitionFinished`；
`Hide(animate) → 播 animOut → WhileHiding() → IsVisible=false → SetActive(false) → OutTransitionFinished`。

### 动画

继承 `AniComponent` 实现 `Animate(Transform target, Action callWhenFinished)`，挂到控制器的 `animIn` / `animOut`。
框架已提供 `AnimationView`、`FadeInAni`（DOTween 淡入淡出）、`ScaleScreenAni`、`SlideScreenAni`。

### 接入一个新界面

1. 工程里先有名为 **`UI`** 的 Layer。
2. 菜单 `Assets/Create/UI/UI Frame Prefab`（或 `UI Frame in Scene`）生成 UI 根节点。
3. 写控制器（继承 `PanelController` 或 `WindowController`），挂在**界面预制体根节点**上。
4. 预制体放进 `UISetting` 的 `screenToRegister`（资产由 `Assets/Create/ZGameFramework/UI/UISetting` 创建）。
5. 运行时 `uiSetting.CreateUIInstance(MyArch.Interface)` 一次性实例化并注册所有界面。
6. 之后用 `ui.OpenWindow(id)` / `ui.ShowPanel(id)` 打开。

### 注意

- **界面 ID 就是预制体名**：注册用的是 `screen.name`，而 ID 生成器会去掉空格，所以**预制体名不要带空格**，且要与打开时传入的 ID 完全一致。
- 预制体**根节点必须挂 `IScreenController`**，否则 `UISetting.OnValidate` 会报错并把它剔除。
- `UISetting` 的「实例化时停用」要保持勾选：只有对象是 inactive，首次 `Show()` 才会播放入场动画。
- **面板优先级在注册时就固定了**（决定挂到哪个层），`Show` 时改不了，必须在预制体上提前配好。
- **窗口属性默认「预制体优先」**：`SuppressPrefabProperties == false`（默认）时，会用预制体上配置的 `HideOnForegroundLost / IsPopup / WindowQueuePriority` 覆盖传入属性；想用传入值必须设为 `true`。
- `Show(props)` 传入的 `props` 类型必须与控制器声明的 `TProps` 一致，否则只 `LogError` 并直接返回（界面不显示，容易误判为「打不开」）。
- 自定义动画**必须调用 `callWhenFinished`**，否则 `IsVisible` 不会更新，且窗口队列与输入遮罩会卡死。
- `HideScreen` 只对当前窗口生效，关闭非当前窗口会报错；同一时刻只有一个前台窗口。
- 一键生成的层级里 **`PanelPriority.Blocker` 没有映射**，会回退挂到 `PanelLayer`。

---

## 八、功能模块  //不重要

```csharp
// 网格
var grid = new GridSystem<MyCell>(width, height, cellSize, floorAmount, floorHeight, GridPlane.XZ, factory);
var cell = grid.GetGridObject(new GridPosition(x, z, floor));
var pos  = grid.GetWorldPosition(gridPosition);

// 寻路
public class MyGraph : IPathGraph<MyNode> { /* GetNeighbors / GetMoveCost / GetHeuristicCost */ }
var path = AStarPathfinder.FindPath(graph, start, end, out int cost);   // 找不到路返回 null

// 音频
AudioVolumeManager.Instance.SetVolume(AudioChannel.BGM, 0.5f);
AudioVolumeManager.Instance.SetMuted(true);
var src = AudioSourceManager.Instance.GetAudioSource(AudioChannel.SFX);
AudioSourceManager.Instance.ReturnAudioSource(src, 2f);         // 受 timeScale 影响
AudioSourceManager.Instance.ReturnAudioSourceRealtime(src, 2f); // 不受影响
AudioSourceManager.Instance.PlayBGM(clip);

// 其他组件
[Label("移动速度")] public float moveSpeed = 5f;   // Inspector 显示中文标签
// LookAtCamera：按模式让物体朝向相机；ScreenShake.Shake(intensity)：Cinemachine 震屏
// BindableProperty<T>：带变更通知的属性
var hp = new BindableProperty<int>(100);
hp.RegisterWithInitValue(v => Debug.Log(v));
hp.Value = 80;
```

### 注意

- `AStarPathfinder` 返回的**邻居列表会被算法回收**，`GetNeighbors` 不要返回内部共享列表。
- 网格：`GridModel.GetGridPosition()` 返回的 `floor` 恒为构造时的楼层；`GetGridObject()` **不做越界校验**，越界会抛异常。
- `ScreenShake` 没有 `[RequireComponent]`，物体上缺 `CinemachineImpulseSource` 时 `Shake()` 会空引用。
- `AudioSourceManager` 若 Inspector 上没配音源预制体，会回退加载 `Resources/AudioSourcePrefab`，该资源不存在会空引用。

---

## 九、编辑器工具

| 菜单                                                       | 用途                                 |
| -------------------------------------------------------- | ---------------------------------- |
| `ZGame/Resource Debugger`                                | 查看资源引用计数、加载中/未使用/缺失状态与 ResLoader 池 |
| `Assets/Create/UI/UI Frame Prefab` / `UI Frame in Scene` | 一键生成 UI 根节点                        |
| `Assets/Create/UI/Re-generate UI ScreenIds`              | 扫描界面预制体生成 ID 常量类                   |
| `Assets/Create/ZGameFramework/UI/UISetting`              | 创建 UI 配置资产                         |
| `Assets/Create/BaseSoundEffExampleSO`                    | 创建音效资产                             |

`ScreenIdProcessor` 的扫描目录、输出脚本路径都是**硬编码常量**（默认 `Assets/UIFrameworkExamples/...`），使用前需按你的工程结构修改，且目标 `ScreenIds` 脚本文件必须已存在。

---

## 十、其他注意事项
1. 编辑器工具依赖反射读写私有字段（`ResTable.table`、`ResLoader.resList`、`ResLoaderRecycler.resLoaders`、`ClassPool<ResLoader>.pool`、`WindowUILayer.priorityParaLayer` 等），**改字段名会让工具失效**。
2. `EventBus` 与 `IOCContainer` 均非线程安全，仅在主线程使用；`ClassPool`/`ListPool`/`DictionaryPool`/`CachePool` 内部有锁。
