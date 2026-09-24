# ZGameFramework 使用文档

Unity 通用游戏框架，包含：**架构层（IOC + Model/System/Utility/Command）**、**全局事件总线**、**对象池**、**资源加载（引用计数）**、**UI 框架（Panel/Window 分层 + 动画 + 窗口队列/回退）**，以及寻路、音频、网格等功能模块。

---

## 目录

1. [环境与依赖](#一环境与依赖)
2. [目录结构](#二目录结构)
3. [命名空间](#三命名空间)
4. [架构层](#四架构层)
5. [事件系统](#五事件系统)
6. [单例与主线程驱动](#六单例与主线程驱动)
7. [对象池](#七对象池)
8. [资源模块](#八资源模块)
9. [UI 框架](#九ui-框架)
10. [功能模块](#十功能模块)
11. [编辑器工具](#十一编辑器工具)
12. [已知问题与注意事项](#十二已知问题与注意事项)

---

## 一、环境与依赖

| 项目 | 版本 / 说明 |
| --- | --- |
| Unity | **2022.3.62f1c1**（URP 14.0.12） |
| 渲染管线 | Universal RP `com.unity.render-pipelines.universal` 14.0.12 |
| 官方包 | `com.unity.cinemachine` 2.10.7、`com.unity.textmeshpro` 3.0.7、`com.unity.ugui` 1.0.0 |
| 本地插件 | `Assets/Plugins/Demigiant`（**DOTween**）、`Assets/Plugins/UniTask`（**Cysharp UniTask**） |

框架对第三方库的实际依赖点：

- **UniTask**（`Cysharp.Threading.Tasks`）：`ResBase`、`ResLoader`、`ResManager` 的异步加载与 `Resources.UnloadUnusedAssets()`。
- **DOTween**（`DG.Tweening`）：`FadeInAni`、`ScaleScreenAni`、`SlideScreenAni`、Demo 的 `ToastPanelController`。
- **Cinemachine**：`ScreenShake`（`CinemachineImpulseSource`）。
- **TextMeshPro**：Demo 的 `NavigationPanelController`。
- **无 Addressables / Input System 依赖**：资源加载基于 `UnityEngine.Resources`。

---

## 二、目录结构

```
Assets/Scripts/ZGameFramework/
├── README.md                       本文档
├── Editor/                         框架级编辑器扩展（需 UnityEditor）
│   ├── LabelAttributeDrawer.cs     [Label] 特性的 Inspector 绘制器
│   └── ResDebuggerWindow.cs        资源调试窗口（菜单 ZGame/Resource Debugger）
└── Scripts/
    ├── test.cs                     遗留草稿（见「已知问题」）
    ├── Core/                       基础能力，命名空间 ZGameFramework.Core
    │   ├── BaseManager/            Singleton / MonoSingleton / PersistentMonoSingleton
    │   ├── Event/                  GameEvent / EventBus
    │   ├── Mono/                   MonoController / MonoManager（主线程与协程）
    │   ├── Pool/                   ClassPool / ListPool / DictionaryPool / CachePool / PoolManager
    │   └── Resources/              ResManager / ResLoader / ResTable / ResBase
    ├── FrameWork/                  IOC 与架构层，命名空间 ZGameFramework
    │   ├── FrameworkInterface.cs   角色接口 + 能力接口
    │   ├── FrameworkAbstract.cs    AbstractSystem / AbstractModel / AbstractCommand
    │   ├── FrameworkExtensions.cs  能力接口扩展方法（CanExtension）
    │   ├── GameArchitecture.cs     GameArchitecture<T> 架构基类
    │   ├── IOCContainer.cs         IOC 容器（GBK 编码，见「已知问题」）
    │   ├── BindableProperty.cs     可绑定属性（变更通知）
    │   └── UnRegister.cs           IUnregister 与自动反注册触发器
    ├── Modules/GridSystem/         网格系统，命名空间 ZGameFramework.Modules
    ├── UIFramework/                UI 框架，命名空间 ZGameFramework.UIFramework
    │   ├── Core/                   UIFrame / UILayer / UIScreenController / UISetting / 属性接口
    │   ├── Panel/                  APanelController / PanelController / PanelUILayer / PanelPriority
    │   ├── Window/                 WindowController / WindowUILayer / WindowParaLayer / WindowPriority
    │   ├── ViewAnimation/          AniComponent 及实现（Animation / DOTween）
    │   ├── Editor/                 UIFrameworkTools（菜单）、ScreenIdProcessor（Id 生成）
    │   └── UIFrameworkDemoExample/ 完整可运行示例
    └── Utility/                    功能模块，命名空间 ZGameFramework.Utility
        ├── PathFinding/            AStarPathfinder / IPathGraph
        ├── Sound/                  AudioEvent / AudioSourceManager / AudioVolumeManager
        └── Utility/                LabelAttribute / LookAtCamera / ScreenShake
```

---

## 三、命名空间

| 命名空间 | 内容 |
| --- | --- |
| `ZGameFramework` | 架构层：`GameArchitecture<T>`、`IArchitecture`、`AbstractSystem/Model/Command`、`CanExtension`、`IUnregister`、`BindableProperty<T>`、`IOCContainer` |
| `ZGameFramework.Core` | `Singleton<T>`、`MonoSingleton<T>`、`PersistentMonoSingleton<T>`、`GameEvent`、`EventBus`、`MonoManager`、各类对象池、资源模块 |
| `ZGameFramework.UIFramework` | UI 框架全部类型 |
| `ZGameFramework.Modules` | `GridSystem<T>`、`GridModel<T>`、`GridPosition`、`GridPlane` |
| `ZGameFramework.Utility` | 寻路、音频、`LabelAttribute`、`LookAtCamera`、`ScreenShake` |
| `ZGameFramework.EditorTools` | `ResDebuggerWindow`、`LabelAttributeDrawer` |
| `ZGameFramework.Example` | `EventExample.cs`（注意：该文件在 `Core/Event/` 目录下，命名空间却是 `Example`） |
| `UIFramework.Examples` | `ViewAnimation/ScreenTransitions/` 下两个动画脚本 |

---

## 四、架构层

### 4.1 核心类型

| 类型 | 说明 |
| --- | --- |
| `IArchitecture` | 架构对外契约：注册 / 解析 / 发命令 / `Deinit()` |
| `GameArchitecture<T>` | CRTP 泛型单例架构基类，`where T : GameArchitecture<T>, new()` |
| `IOCContainer` | 以 `typeof(T)` 为键的极简服务定位容器 |
| `AbstractSystem` / `AbstractModel` / `AbstractCommand` | 生命周期模板方法基类 |
| `CanExtension` | 为「能力接口」提供 `this.GetModel<T>()`、`this.SendEvent<T>()` 等语法糖 |

### 4.2 角色（你是谁）与能力（你能做什么）

框架把接口拆成两层：**角色接口**决定身份，**能力接口**决定权限。

| 角色接口 | 继承的能力 | 特点 |
| --- | --- | --- |
| `IController` | GetUtility / GetSystem / GetModel / SendCommand / RegisterEvent / SendEvent | 权限最全，UI 控制器使用 |
| `ISystem` | `ICanInit` / GetModel / SendCommand / SendEvent / RegisterEvent / SetArchitecture | **不能**取 System 与 Utility |
| `IModel` | `ICanInit` / GetUtility / SendEvent / SetArchitecture | **不能**取 System、不能取 Model |
| `IUtility` | 无（空标记接口） | 无生命周期、无架构注入，纯工具对象 |
| `ICommand` / `ICommand<TResult>` | GetSystem / GetModel / GetUtility / SendEvent / SendCommand / SetArchitecture | 各自声明 `Execute()` |

能力接口均为空标记接口（除 `ICanInit`、`ICanSetArchitecture`），通过 `CanExtension` 获得默认实现：

```csharp
public static T GetSystem<T>(this ICanGetSystem self)   where T : class, ISystem
public static T GetModel<T>(this ICanGetModel self)     where T : class, IModel
public static T GetUtility<T>(this ICanGetUtility self) where T : class, IUtility

public static void SendCommand<T>(this ICanSendCommand self, T command) where T : class, ICommand
public static void SendCommand<T>(this ICanSendCommand self) where T : class, ICommand, new()
public static TResult SendCommand<TResult>(this ICanSendCommand self, ICommand<TResult> command)

public static void SendEvent<TEvent>(this ICanSendEvent self, TEvent gameEvent) where TEvent : GameEvent
public static void SendEvent<TEvent>(this ICanSendEvent self) where TEvent : GameEvent, new()

public static IUnregister RegisterEvent<T>(this ICanRegisterEvent self, Action action)     where T : GameEvent
public static IUnregister RegisterEvent<T>(this ICanRegisterEvent self, Action<T> action)  where T : GameEvent
public static void UnregisterEvent<T>(this ICanRegisterEvent self, Action action)          where T : GameEvent
public static void UnregisterEvent<T>(this ICanRegisterEvent self, Action<T> action)       where T : GameEvent
```

### 4.3 初始化与销毁顺序

```
首次访问 MyArch.Interface
  └─ InitArchitecture()
       ├─ m_Architecture = new T()
       ├─ m_Architecture.OnInit()      ← 你的注册点
       └─ InitAll()
            ├─ 1) 先初始化所有 IModel   （按注册进容器的顺序，Dictionary 枚举序）
            ├─ 2) 再初始化所有 ISystem
            └─ m_Inited = true
```

`Deinit()` 顺序与初始化相反：

```
Deinit()
  ├─ OnDeinit()                 ← 可 override
  ├─ 先 DeInit 所有 ISystem    （仅 Initialized == true 的）
  ├─ 再 DeInit 所有 IModel
  ├─ m_IOCContainer.Clear()
  ├─ m_Inited = false
  └─ m_Architecture = null      ← 再次访问 Interface 会重新 new T() 并重新注册
```

### 4.4 使用示例

```csharp
using ZGameFramework;

// 1) 定义架构
public class MyArchitecture : GameArchitecture<MyArchitecture>
{
    protected override void OnInit()
    {
        RegisterModel(new BagModel());
        RegisterSystem(new BattleSystem());
        RegisterUtility(new TimeUtility());
    }
}

// 2) Model / System / Utility
public class BagModel : AbstractModel
{
    protected override void OnInit() { /* 初始化数据 */ }
    protected override void OnDeInit() { /* 释放 */ }
}

public class BattleSystem : AbstractSystem
{
    protected override void OnInit()
    {
        // 能力接口扩展方法：取 Model、监听事件
        this.RegisterEvent<PlayerDeadEvent>(OnPlayerDead).UnregisterOnDestroyTrigger(this);
    }
    private void OnPlayerDead(PlayerDeadEvent e) { /* ... */ }
}

public class TimeUtility : IUtility { public float DeltaTime => 0f; }

// 3) 访问
var arch = MyArchitecture.Interface;
var bag  = arch.GetModel<BagModel>();
```

> **关键约束**：`IOCContainer` 的键是**调用点的编译期泛型实参** `typeof(T)`，不是对象的运行时类型。
> 必须「用什么类型注册，就用什么类型获取」：
> `RegisterSystem(new BattleSystem())` → 键为 `BattleSystem`，`GetSystem<BattleSystem>()` 才能命中；
> 若写成 `RegisterSystem<ISystem>(x)`，键变成 `ISystem`，之后 `GetSystem<BattleSystem>()` 会失败并打印
> `"{TypeName}未注册但尝试获取"` 且返回 `null`。

---

## 五、事件系统

命名空间 `ZGameFramework.Core`。事件必须继承 `GameEvent`。

强烈不推荐直接使用eventbus和绕过框架使用事件，请使用this.Register()

this.Register().UnregisterOnDestroyedTrigger(),this.SendEvent()等方式订阅，发布事件


| 类型 | 成员 |
| --- | --- |
| `GameEvent`（abstract，实现 `IPoolable`） | `virtual void OnRecycled()`、`virtual void OnGet()`；编辑器下 `AssertSafe()` |
| `EventBus`（static） | `Register<T>`、`Unregister<T>`、`Publish<T>`、`PublishSignal<T>` |

```csharp
public static IUnregister Register<T>(Action<T> listener) where T : GameEvent   // 带参事件
public static IUnregister Register<T>(Action listener)    where T : GameEvent   // 信号事件
public static void Unregister<T>(Action listener)         where T : GameEvent
public static void Unregister<T>(Action<T> listener)      where T : GameEvent
public static void Publish<T>(T eventData)                where T : GameEvent
public static void PublishSignal<T>()                     where T : GameEvent
```

两种事件形态：

```csharp
// 带参事件：携带数据
public class PlayerDeadEvent : GameEvent { public int PlayerId; public string Cause; }
EventBus.Publish(new PlayerDeadEvent { PlayerId = 1001, Cause = "Lava" });
EventBus.Register<PlayerDeadEvent>(e => Debug.Log($"{e.PlayerId} 死于 {e.Cause}"));

// 信号事件：只表示「发生了」
public class StartUIDemoEvent : GameEvent { }
EventBus.PublishSignal<StartUIDemoEvent>();
EventBus.Register<StartUIDemoEvent>(() => Debug.Log("收到信号"));
```

在 `IController / ISystem / IModel` 内部推荐用 `CanExtension` 语法糖：

```csharp
this.SendEvent(new PlayerDeadEvent { PlayerId = 1, Cause = "Lava" });
this.SendEvent<StartUIDemoEvent>();          // 无参版本 → PublishSignal
this.RegisterEvent<PlayerDeadEvent>(OnDead).UnregisterOnDestroyTrigger(this);
```

### 自动反注册（`UnRegister.cs`）

`Register` / `RegisterEvent` 返回 `IUnregister`，可挂到生命周期触发器上自动注销：

```csharp
IUnregister h = EventBus.Register<MyEvent>(OnMyEvent);

h.UnregisterOnDestroyTrigger(this);        // GameObject 销毁时自动注销
h.UnregisterOnDisableTrigger(this);        // GameObject 失活时自动注销
h.UnregisterWhenCurrentSceneUnloaded();    // 场景卸载时自动注销
```

| 类型 | 说明 |
| --- | --- |
| `IUnregister` | 统一反注册句柄，`void Unregister()` |
| `CustomUnregister`（struct） | 用闭包承载任意反注册动作，保证只执行一次 |
| `UnregisterTrigger`（abstract MonoBehaviour） | 持有 `HashSet<IUnregister>`，子类在 `OnDestroy`/`OnDisable`/场景卸载时批量注销 |
| `UnregisterOnDestroyTrigger` / `UnregisterOnDisableTrigger` / `UnregisterCurrentSceneUnloadedTrigger` | 三种触发器 |
| `IUnregisterList` + `IUnregisterListExtension` | `AddUnregister` / `UnregisterAll`，用于自行管理一批句柄 |

> **注意**：`EventBus.Publish` 会先用 `ListPool` 取监听列表快照再派发，因此**派发过程中注册/注销监听不影响本次派发**（已快照的监听本轮仍会被调用一次）。监听列表本身无锁，非线程安全。

---

## 六、单例与主线程驱动

### 6.1 三种单例基类

| 类型 | 适用于 | 机制与约束 |
| --- | --- | --- |
| `Singleton<T>` | 纯 C# 类 | 反射查找**非 public 的无参构造函数**创建实例，双重检查锁。**T 必须有私有/受保护的无参构造函数**，否则抛 `【单例报错】{T} 缺少私有的无参构造函数！` |
| `MonoSingleton<T>` | 场景中的 MonoBehaviour | `Awake` 中 `Instance = this as T`，重复实例直接 `Destroy(gameObject)`；`OnDestroy` 置空。**不做跨场景保留** |
| `PersistentMonoSingleton<T>` | 需要跨场景的 MonoBehaviour | `Instance` getter 内 `FindObjectOfType`，找不到就自动创建 `{T}AutoCreator` 物体并 `DontDestroyOnLoad` |

```csharp
public class MyManager : Singleton<MyManager>
{
    private MyManager() { }          // 必须：非 public 无参构造
    public void DoSomething() { }
}
MyManager.Instance.DoSomething();
```

### 6.2 主线程驱动：`MonoManager`

让非 MonoBehaviour 的类也能拿到 `Update` 与协程：

```csharp
MonoManager.Instance.AddUpdateListener(OnUpdate);
MonoManager.Instance.RemoveUpdateListener(OnUpdate);

MonoManager.Instance.AddFixedUpdateListener(OnFixedUpdate);
MonoManager.Instance.AddLateUpdateListener(OnLateUpdate);

MonoManager.Instance.StartCoroutine(MyRoutine());
MonoManager.Instance.StopCoroutine(routine);
MonoManager.Instance.StopAllCoroutines();
```

`MonoManager` 是 `Singleton<MonoManager>`，私有构造函数里创建 `[MonoManager]` 物体并挂上 `MonoController`；`MonoController`（MonoBehaviour）在 `Awake` 中 `DontDestroyOnLoad`，把三个事件在对应 Unity 生命周期里派发。**所有接口都转发给 `MonoController`。**

---

## 七、对象池

#### 使用对象池后记得回收！！！

| 类型 | 用途 | 关键 API |
| --- | --- | --- |
| `ClassPool<T>` | 引用类型对象池（`where T : class, new()`） | `Get()` / `Recycle(T)` / `WarmUp(int)` / `Clear()` / `MaxCapacity`（默认 200）/ `Count` / `TotalCreated` / `TotalRecycled` / `TotalRetrieved` / `ResetStats()` |
| `ListPool<T>` | `List<T>` 复用池 | `Get()` / `Recycle(List<T>)` |
| `DictionaryPool<TKey,TValue>` | `Dictionary` 复用池 | `Get()` / `Recycle(Dictionary<,>)` |
| `CachePool<TKey,TValue>` | **LRU 缓存**（非对象池），构造传入 `capacity`（≤0 时按 10） | `TryGetValue(key, out value)` / `Set(key, value)` / `Clear()` |
| `PoolManager` | **GameObject** 池，`PersistentMonoSingleton<PoolManager>` | `GetGameObject(prefab, parent, worldPositionStays)` / `PushGameObject(go)` / `Clear()` |

### 7.1 `IPoolable` 回调

```csharp
public interface IPoolable
{
    void OnRecycled();   // 入池时调用
    void OnGet();        // 出池时调用
}
```

`ClassPool<T>.Get()` / `Recycle()` 内部用 `is IPoolable` 判断，**不强制** `T` 实现该接口。适合在 `OnRecycled` 里清理旧数据（例如清空字段、重置状态）。

```csharp
public class MyData : IPoolable
{
    public int Hp;
    public void OnGet() { }                 // 出池
    public void OnRecycled() { Hp = 0; }    // 入池前重置
}

var d = ClassPool<MyData>.Get();
ClassPool<MyData>.Recycle(d);
```

### 7.2 使用约束

- **`ListPool` / `DictionaryPool` 必须手动归还**，否则泄漏：`Recycle` 会先 `Clear()` 内容（**归还后原数据即丢失**），且**无容量上限**。
- `ClassPool<T>.Recycle` 先调用 `OnRecycled()`，再判断容量；**池满（≥ `MaxCapacity`）时对象被直接丢弃且不抛错**（注意此时不累计 `TotalRecycled`）。
- `ClassPool<T>.pool` 是 `public static readonly Stack<T>`，编辑器调试工具会直接读取它。
- **`PoolManager` 以 `GameObject.name` 作为池键**：不同预制体如果重名会**串池**。请保证预制体命名唯一，取出的实例也会被改名回 `prefab.name`。
- `PoolManager.Clear()` 使用 `Destroy`，实际销毁发生在帧末。

```csharp
var go = ResManager.Instantiate("Prefabs/Enemy", parent);   // 内部走 PoolManager
ResManager.Recycle(go);                                     // 归还到 PoolManager
```

---

## 八、资源模块

基于 `UnityEngine.Resources`，**不是 Addressables**。命名空间 `ZGameFramework.Core`。

| 类型 | 职责 |
| --- | --- |
| `ResManager`（static） | 对外门面：加载、实例化、预热、卸载 |
| `ResLoader` | 一次「加载会话」的引用集合，支持引用计数与整体释放 |
| `ResTable`（static） | 全局资源缓存表，键为 `"{path}_{typeof(T).Name}"` |
| `ResBase` | 单个资源的包装（Asset + 引用计数 + 加载中请求） |
| `ResLoaderExtensions` / `ResLoaderRecycler` | 把 `ResLoader` 绑定到宿主，宿主销毁时自动释放 |

### 8.1 `ResManager` API

```csharp
public static ResLoader GetResLoader();                                    // 从池中取（推荐用于引用计数）
public static int ResCount { get; }

public static T Load<T>(string path) where T : UnityEngine.Object;
public static UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : UnityEngine.Object;
public static async UniTask PreloadAsync(IReadOnlyList<string> paths, Action<float> onProgress = null, CancellationToken token = default);

public static GameObject Instantiate(string path, Transform parent = null);
public static async UniTask<GameObject> InstantiateAsync(string path, Transform parent = null);
public static GameObject Instantiate(GameObject prefab, Transform parent = null);
public static void Recycle(GameObject gameObject);

public static int GetRefCount<T>(string path) where T : UnityEngine.Object;
public static async UniTask UnloadUnused();
```

### 8.2 两种加载路径

**路径 A：直连加载（不计数）** —— 简单，但资源随时可能被 `UnloadUnused` 清掉。

```csharp
var sprite = ResManager.Load<Sprite>("UI/Icons/Coin");
var sprite2 = await ResManager.LoadAsync<Sprite>("UI/Icons/Coin");
```

**路径 B：`ResLoader` 引用计数（推荐）** —— 加载即 `Acquire`，`ReleaseAll` 时统一 `Release`。

```csharp
var loader = ResManager.GetResLoader();
try
{
    var cfg   = loader.LoadSync<TextAsset>("Configs/Level");
    var prefab = await loader.LoadAsync<GameObject>("Prefabs/Enemy", token);
    // ... 使用
}
finally
{
    loader.ReleaseAllAndRecycle();   // 释放全部引用并把 loader 归还对象池
}
```

**自动绑定到宿主（最省心）**：

```csharp
var loader = ResManager.GetResLoader().BindTo(this);
loader.LoadSync<TextAsset>("Configs/Level");
// 宿主 GameObject 销毁时，ResLoaderRecycler 自动 ReleaseAllAndRecycle()
```

`BindTo` 会在宿主上自动 `AddComponent<ResLoaderRecycler>()`；若 loader 已 `IsReleased`/`IsRecycled` 会抛 `ObjectDisposedException`。

### 8.3 预热与卸载

```csharp
await ResManager.PreloadAsync(
    new[] { "Prefabs/Enemy", "Prefabs/Bullet" },
    progress => Debug.Log($"加载中 {progress:P0}"),
    token);

await ResManager.UnloadUnused();   // 清空未用缓存 + Resources.UnloadUnusedAssets()
```

### 8.4 注意事项

- 资源路径必须是 **`Resources` 目录下的相对路径且不带扩展名**。
- 缓存键 = `路径 + "_" + 类型名`，因此**同一路径以不同类型加载会各占一条缓存**；`GetRefCount<T>` 必须用与加载时相同的 `T` 查询。
- `ResManager.Load/LoadAsync` **不做 `Acquire`**，语义与 `ResLoader` 路径不同：这些资源可被 `ClearUnused` 直接移出表。
- `PreloadAsync` 直接使用 `Resources.LoadAsync<UnityEngine.Object>`，**不写入 `ResTable`**，因此预热结果不被资源表跟踪、也不产生引用计数（命中引擎内部缓存，不会重复 IO）。
- `ResLoader` 被释放或回收后再调用 `Load*` 会抛 `InvalidOperationException("ResLoader 已经被释放或回收，不能再使用。")`。
- 释放后必须调用 `ReleaseAll()` 或 `ReleaseAllAndRecycle()`，否则引用计数泄漏。

---

## 九、UI 框架

命名空间 `ZGameFramework.UIFramework`。整体分为 **Panel 层**（可叠加、按优先级分组）与 **Window 层**（互斥、有历史回退与队列）。

### 9.1 分层结构

`UIFrame`（挂在 Canvas 根节点上，实现 `IController`）内部持有两个 UI 层：

```
UIFrame (Canvas + CanvasScaler + GraphicRaycaster)
├── UICamera
├── EventSystem
├── PanelLayer            → PanelUILayer
├── PriorityPanelLayer    → PanelPriority.Priority 的面板
├── TutorialPanelLayer    → PanelPriority.Tutorial 的面板
├── WindowLayer           → WindowUILayer
└── PriorityWindowLayer   → WindowParaLayer（含 DarkenBG 弹窗遮罩）
```

以上层级可由菜单 **`Assets/Create/UI/UI Frame Prefab`** 或 **`Assets/Create/UI/UI Frame in Scene`** 一键生成
（**需要一个名为 `UI` 的 Layer**，代码用 `LayerMask.NameToLayer("UI")` 取层，缺失时层号为 -1）。

一键生成的默认映射是 `None → PanelLayer`、`Priority → PriorityPanelLayer`、`Tutorial → TutorialPanelLayer`；
**`PanelPriority.Blocker` 默认没有映射**，会回退挂到 `PanelLayer` 下。
uGUI 中同一父节点下兄弟顺序越靠后越上层，按生成顺序（自下而上）为：

```
PanelLayer  →  WindowLayer  →  PriorityPanelLayer  →  PriorityWindowLayer(弹窗+遮罩)  →  TutorialPanelLayer
```

### 9.2 优先级

```csharp
public enum PanelPriority { None = 0, Priority = 1, Tutorial = 2, Blocker = 3 }
public enum WindowPriority { ForceForeground = 0, Enqueue = 1 }
```

- **Panel**：优先级在**注册/重挂载时**（`PanelUILayer.ReparentScreen`）决定面板挂到哪个并行层节点下，
  渲染顺序由这些节点的兄弟顺序决定。因此**优先级必须在界面预制体上提前配好，`Show` 时无法更改**；
  同一并行层内部不会再排序（不会 `SetAsLastSibling`），绘制顺序取决于注册/实例化顺序。
  查表失败时会回退挂到 `PanelUILayer` 自身节点下。
- **Window**：
  - `ForceForeground`：立即显示到最前。
  - `Enqueue`：若当前已有窗口，则**排队**，等当前窗口关闭后再显示。

### 9.3 窗口行为（`WindowUILayer`）

| 特性 | 说明 |
| --- | --- |
| 历史回退 | 内部维护 `Stack<WindowHistoryEntry> windowHistory`，关闭当前窗口后自动回到上一个窗口 |
| 队列 | `Queue<WindowHistoryEntry> windowQueue`，`Enqueue` 优先级的窗口排队显示 |
| 前台丢失 | 新窗口打开时，若当前窗口 `HideOnForegroundLost == true` 且新窗口不是弹窗，则隐藏当前窗口 |
| 叠放顺序 | 每次显示都会执行 `HierarchyFixOnShow()` → `transform.SetAsLastSibling()`，因此**最后显示的窗口在最上面** |
| 单前台 | 同一时刻只有一个 `CurrentWindow`；对非当前窗口调用关闭会报错 `正在非当前窗口{0},当前窗口为{1}` |
| 弹窗 | `IsPopup == true` 时挂到 `WindowParaLayer` 并显示 `DarkenBG` 黑底遮罩；关闭时刷新遮罩 |
| 转场遮罩 | 窗口动画播放期间通过 `RequestScreenBlock` / `RequestScreenUnblock` 事件开关 `GraphicRaycaster`，防止连点 |

`HideScreen` 只对**当前窗口**生效，关闭非当前窗口会打印错误：`正在非当前窗口{0},当前窗口为{1}`。

### 9.4 生命周期回调顺序

`UIScreenController<TProps>` 是 Panel/Window 控制器的共同基类，提供以下可重写回调：

```
注册：        Awake() → AddListener()
打开：        Show(props)
                 ├─ 若 props 类型与 TProps 不符 → LogError 并直接 return（不会显示）
                 ├─ SetProperties(props)
                 ├─ HierarchyFixOnShow()
                 ├─ OnPropertiesSet()
                 ├─ GameObject 未激活时播放 animIn 动画；已激活则跳过动画
                 └─ OnTransitionInFinished → IsVisible = true → InTransitionFinished
关闭：        Hide(animate)
                 ├─ 若未配 animOut（或 animate=false）→ 立即 SetActive(false) 并回调
                 │  否则播放 animOut，等动画结束再回调
                 ├─ WhileHiding()          ← 同步调用，不等动画播完
                 └─ OnTransitionOutFinished → IsVisible = false → SetActive(false) → OutTransitionFinished
销毁：        OnDestroy() → ScreenDestroyed（UI 层自动反注册）→ RemoveListener()
```

> `AddListener()` 只在 `Awake` 中执行一次；而 `OnPropertiesSet()` **每次 `Show` 都会调用**（包括不带属性的重复 Show）。
> `WhileHiding()` 是紧跟在「开始播关闭动画」之后同步调用的，**不会等动画播完**。

可用重写点：

| 回调 | 时机 |
| --- | --- |
| `AddListener()` | `Awake` 中，注册按钮点击/事件监听 |
| `RemoveListener()` | `OnDestroy` 中，反注册 |
| `OnPropertiesSet()` | 属性设置完成后，用 `Properties` 刷新界面 |
| `WhileHiding()` | 每次开始隐藏时 |
| `HierarchyFixOnShow()` | 显示前调整层级（`WindowController` 已重写为 `SetAsLastSibling()`） |

### 9.5 属性对象

```csharp
public class PanelProperties : IPanelProperties { public PanelPriority Priority { get; set; } }

public class WindowProperties : IWindowProperties
{
    public WindowPriority WindowQueuePriority { get; set; }
    public bool HideOnForegroundLost { get; set; }
    public bool IsPopup { get; set; }
    public bool SuppressPrefabProperties { get; set; }
}
```

自定义属性请继承它们并在构造函数中传入数据：

```csharp
[Serializable]
public class PlayerWindowProperties : WindowProperties
{
    public readonly List<PlayerDataEntry> PlayerData;
    public PlayerWindowProperties(List<PlayerDataEntry> data) { PlayerData = data; }
}

public class PlayerWindowController : WindowController<PlayerWindowProperties>
{
    protected override void OnPropertiesSet() { Refresh(Properties.PlayerData); }
}
```

> ⚠️ **重要行为**：`WindowController<TProps>.SetProperties` 中，当 `SuppressPrefabProperties == false`（默认）时，
> 会用**预制体上配置的** `HideOnForegroundLost` / `IsPopup` / `WindowQueuePriority` **覆盖**传入的属性对象，
> 即默认「预制体配置优先」。只有把 `SuppressPrefabProperties` 设为 `true`，传入的属性才会生效。
> 另外，`Show(null)` 不会覆盖预制体上已有的 `properties`。

### 9.6 动画

`AniComponent` 是抽象基类：

```csharp
public abstract class AniComponent : MonoBehaviour
{
    public abstract void Animate(Transform target, Action callWhenFinished);
}
```

框架已提供 4 种实现，挂到控制器的 `animIn` / `animOut` 字段即可：

| 实现 | 效果 |
| --- | --- |
| `AnimationView` | 使用 `Animator`/`Animation` 播放 |
| `FadeInAni` | DOTween 淡入淡出 |
| `ScaleScreenAni` | DOTween 缩放弹入（`xYSplit` 控制 X/Y 占比） |
| `SlideScreenAni` | DOTween 位移滑入（`Origin` 指定起始方位） |

也可以继承 `AniComponent` 写自己的动画；**动画结束时必须调用 `callWhenFinished`**，否则框架不会把界面标记为已显示/已隐藏。

### 9.7 `UIFrame` 主要 API

```csharp
// 打开 / 关闭
void OpenWindow(string windowId);
void OpenWindow<TProps>(string windowId, TProps props) where TProps : IWindowProperties;
void CloseWindow(string windowId);
void CloseCurrentWindow();

void ShowPanel(string panelId);
void ShowPanel<TProps>(string panelId, TProps props) where TProps : IPanelProperties;
void HidePanel(string panelId);

void ShowScreen(string id);      // 自动判断是 Panel 还是 Window
void CloseScreen(string id);

// 批量
void HideAll(bool animate = true);
void CloseAllWindow(bool animate = true);
void HideAllPanel(bool animate = true);

// 查询
bool IsPanelOpen(string panelId);
bool IsScreenRegistered(string screenId);

// 注册
void RegisterScreen(string id, IScreenController screenController, Transform screenTransform);
void RegisterPanel<TPanel>(string screenId, TPanel panel) where TPanel : IPanelController;
void RegisterWindow<TWindow>(string screenId, TWindow controller) where TWindow : IWindowController;
void UnregisterPanel<TPanel>(string screenId, TPanel panel) where TPanel : IPanelController;
void UnregisterWindow<TWindow>(string screenId, TWindow controller) where TWindow : IWindowController;

// 其他
Canvas MainCanvas { get; }
Camera UICamera { get; }
protected virtual void Initialize();   // initializeOnAwake = false 时可手动初始化
```

### 9.8 接入一个新界面的完整步骤

1. **确认工程存在 `UI` Layer**（`Edit → Project Settings → Tags and Layers`），否则 `UIFrameworkTools` 生成的层级会出错。
2. **生成 UI 根节点**：菜单 `Assets/Create/UI/UI Frame Prefab`（保存为预制体）或 `UI Frame in Scene`。
3. **编写控制器**：在界面预制体根节点上挂一个继承 `PanelController`（或 `WindowController`）的脚本。

   ```csharp
   public class ToastPanelController : PanelController
   {
       [SerializeField] private Text messageText;

       protected override void OnPropertiesSet()
       {
           messageText.text = ((ToastPanelProperties)Properties).Message;
       }
   }
   ```
4. **命名与挂载规则（代码强制）**：
   - 界面预制体**根节点**必须挂实现 `IScreenController` 的组件，否则 `UISetting.OnValidate` 会报
     `请勿将非IScreenController放入需要注册的面板配置中` 并把它从列表里剔除。
   - **界面 ID 就是预制体名**：`UISetting.CreateUIInstance` 用 `screen.name` 注册，
     而 ID 生成器用 `go.name.Replace(" ", "")`（去空格）生成常量名 —— 所以**预制体名字里不要带空格**，
     并且要与打开时传入的 ID 完全一致。同名预制体会被生成器报错。
5. **创建 `UISetting` 资产**：`Assets/Create/ZGameFramework/UI/UISetting`，把 `UIFrame` 预制体和所有界面预制体拖进 `screenToRegister`。
   （保持「实例化时是否停用」勾选：只有对象是 inactive 时，首次 `Show()` 才会播放入场动画。）
6. **实例化并注册**：

   ```csharp
   public class UIBootstrap : MonoBehaviour
   {
       [SerializeField] private UISetting uiSetting;
       private UIFrame ui;

       private void Start()
       {
           ui = uiSetting.CreateUIInstance(MyArchitecture.Interface);
           ui.OpenWindow("StartGameWindow");
       }
   }
   ```
7. **（可选）生成 Id 常量**：菜单 `Assets/Create/UI/Re-generate UI ScreenIds`。
   该功能由 `ScreenIdProcessor`（`AssetPostprocessor`）实现，会扫描 `Assets/UIFrameworkExamples/Prefabs/Screens`
   下的界面预制体，生成 `public sealed class ScreenIds` 到 `Assets/UIFrameworkExamples/Scripts`（命名空间 `UIFramework.Examples`），
   每个界面生成一个 `public const string <预制体名> = "<预制体名>";`。
   **这四个路径/名称都是硬编码常量，使用前需按你的工程结构调整 `ScreenIdProcessor` 中的字段**；
   且目标 `ScreenIds` 脚本文件必须已存在，否则报 `Could not find ScreenIds script file!`。
   它同时也会在预制体增删/移动时自动重新生成。
   本仓库的 Demo 用的是手写的 `ExampleScreenIds`（两套 Id 约定并存，见「已知问题」）。

### 9.9 示例工程

`UIFrameworkDemoExample/` 是一个完整示例：`UIExampleDemo`（架构入口）、`UIDemoWindowController`（拿到 `UIExampleDemo.Interface` 的控制器基类）、`ExampleScreenIds`（界面 Id 常量）、
`PlayerWindowController`、`ConfirmUIController`、`NavigationPanelController`、`ToastPanelController` 等。
建议先跑通该示例，再照搬其结构。

---

## 十、功能模块  //不重要

### 10.1 网格系统（`ZGameFramework.Modules`）

```csharp
public enum GridPlane { XZ, XY }
public struct GridPosition            // 值类型，支持 ==、!=、+、-、Equals、GetHashCode、ToString
{
    public int x, z, floor;
    public GridPosition(int x, int z, int floor);
}

public class GridModel<TGridObject> : AbstractModel
{
    public GridModel(int width, int height, float cellSize, int floor, float floorHeight,
                     GridPlane gridPlane, Func<GridModel<TGridObject>, GridPosition, TGridObject> createGridObject);
    public Vector3 GetWorldPosition(GridPosition gridPosition);
    public GridPosition GetGridPosition(Vector3 worldPosition);
    public TGridObject GetGridObject(GridPosition gridPosition);
    public bool IsValidPosition(GridPosition gridPosition);
    public int GetWidth(); public int GetHeight(); public int GetFloor();
}

public class GridSystem<TGridObject> : AbstractSystem
{
    public GridSystem(int width, int height, float cellSize, int floorAmount, float floorHeight,
                      GridPlane plane, Func<GridModel<TGridObject>, GridPosition, TGridObject> createGridObject);
    public GridModel<TGridObject> GetGridSystem(int floor);
    public int GetFloor(Vector3 worldPosition);
    public GridPosition GetGridPosition(Vector3 worldPosition);
    public Vector3 GetWorldPosition(GridPosition gridPosition);
    public TGridObject GetGridObject(GridPosition gridPosition);
    public bool IsValidPosition(GridPosition gridPosition);
    public int GetWidth(); public int GetHeight(); public int GetFloorAmount(); public float GetCellSize();
}
```

`GridSystem<TGridObject>` 是 `AbstractSystem`，可作为架构模块注册；`GridModel<TGridObject>` 是 `AbstractModel`，按楼层管理网格数据。
两者的 `OnInit()` 目前都是空实现，网格的尺寸等参数全部经**构造函数**传入（没有 `[SerializeField]` 字段）。

使用注意：

- `GridSystem<TGridObject>.GetGridSystem(int floor)` 返回的是**该楼层的 `GridModel`**，不是系统自身（名字容易误读）。
- `GridModel.GetGridPosition(Vector3)` 返回的 `floor` 恒为构造时传入的楼层，**不会**从世界坐标推断楼层；
  跨楼层换算请用 `GridSystem.GetFloor(Vector3)`。
- `GridModel.GetGridObject()` **不做越界校验**（越界会抛 `IndexOutOfRangeException`），
  `GridSystem.GetWorldPosition` / `GetGridObject` 直接信任 `GridPosition.floor` 且不 Clamp；需要校验时先调 `IsValidPosition`。
- `GridPosition` 没有 `[Serializable]`，若要作为 Inspector 字段序列化需自行加特性。

### 10.2 寻路（`ZGameFramework.Utility`）

```csharp
public interface IPathGraph<T>
{
    List<T> GetNeighbors(T node);       // 返回的列表会被 A* 回收，勿自行持有
    int GetMoveCost(T fromNode, T toNode);
    int GetHeuristicCost(T fromNode, T toNode);
}

public static class AStarPathfinder
{
    public static List<T> FindPath<T>(IPathGraph<T> graph, T start, T end, out int totalCost)
        where T : IEquatable<T>;
}
```

```csharp
var path = AStarPathfinder.FindPath(graph, start, end, out int cost);   // 找不到路时返回 null
```

A* 内部使用 `ListPool` / `DictionaryPool`，**邻居列表由算法负责回收**，实现 `GetNeighbors` 时应返回池化的 `List<T>`（或普通 `List<T>`，算法会 `ListPool.Recycle` 它）。

### 10.3 音频（`ZGameFramework.Utility`）

```csharp
public enum AudioChannel { Master, BGM, SFX, Voice }

public class AudioVolumeManager : Singleton<AudioVolumeManager>
{
    public event Action OnVolumeChanged;
    public bool IsMuted { get; }
    public float GetVolume(AudioChannel channel);
    public void SetVolume(AudioChannel channel, float value);
    public void SetMuted(bool muted);
    public void ToggleMute();
    public float GetFinalVolume(AudioChannel channel);   // = 通道音量 × 主音量（静音时为 0）
}
```

音量通过 `PlayerPrefs` 持久化；`GetFinalVolume` 已包含主音量与静音判断。

```csharp
public class AudioSourceManager : PersistentMonoSingleton<AudioSourceManager>   // 跨场景持久
{
    public AudioSource BGMSource { get; }
    public AudioSource GetAudioSource(AudioChannel channel = AudioChannel.SFX);  // 从池中取
    public void ReturnAudioSource(AudioSource audioSource, float delay);          // 受 timeScale 影响
    public void ReturnAudioSourceRealtime(AudioSource audioSource, float delay);  // 不受 timeScale 影响
    public void SetLocalVolume(AudioSource source, float localVolume);
    public void PlayBGM(AudioClip clip, bool loop = true);
    public void StopBGM();
}
```

`Awake` 中会预热 15 个音源入池，并额外创建一个循环播放的常驻 BGM 音源（不参与池归还）。
音源模板取 Inspector 上的 `audioSourcePrefab`；**若为空则回退到 `ResManager.Load<GameObject>("AudioSourcePrefab")`**，
即依赖 `Resources/AudioSourcePrefab` 这个资源存在（否则会空引用）。
归还使用 Unity 协程；`SetLocalVolume` 会按「通道最终音量」重算，音量变化时订阅 `AudioVolumeManager.OnVolumeChanged` 统一刷新。

音源最终音量 = `局部音量 × 通道最终音量`。游戏暂停时用 `ReturnAudioSourceRealtime`
归还（适用于 `dspTime` / `PlayScheduled` 这类「暂停也照播」的音效）。

音效资源用 `AudioEvent` 描述 —— 它是 `abstract class AudioEvent : ScriptableObject`，
只声明两个抽象方法，**本身没有 `[CreateAssetMenu]`**，需要由子类创建资产：

```csharp
public abstract class AudioEvent : ScriptableObject
{
    public abstract void Play();
    public abstract void Play(Vector3 position);   // 3D 音效
}
```

`SimpleAudioEventExample` 是可直接使用的实现（`[CreateAssetMenu(menuName = "BaseSoundEffExampleSO")]`），
Inspector 上有 `音效数组` / `基础音量` / `基础音调` 三个 `[Label]` 字段；`Play()` 走 2D，`Play(Vector3)` 走 3D 空间化，
音量/音调带 ±0.1 随机抖动，播放后按 `clip.length` 延迟归还音源。

### 10.4 通用组件

| 类型 | 说明 |
| --- | --- |
| `LabelAttribute`（`[Label("中文名")]`） | 配合 `LabelAttributeDrawer`，在 Inspector 上以中文标签显示字段 |
| `LookAtCamera` | 挂在物体上，在 `LateUpdate` 中按 4 种模式处理朝向：`LookAt` / `LookAtReverse` / `ForWard` / `ForWardReverse`（Inspector 选择模式，相机为空时回退 `Camera.main`） |
| `ScreenShake` | `MonoSingleton<ScreenShake>`，挂在带 `CinemachineImpulseSource` 的物体上，`Shake(float intensity)` 触发震屏 |
| `BindableProperty<T>` | 带变更通知的属性容器，`Register` / `RegisterWithInitValue` / `SetValueWithoutEvent` |

> ⚠️ `ScreenShake` **没有加 `[RequireComponent(typeof(CinemachineImpulseSource))]`**，
> 组件缺失时 `impulseSource` 为 null，`Shake()` 会抛 `NullReferenceException`。
> 另外它基于 Cinemachine **2.x**（`using Cinemachine;`）；升级到 Cinemachine 3.x 需改为 `Unity.Cinemachine`。
> `LookAtCamera` 的 `camera` 属性 setter 是空实现（赋值会被丢弃），且 `LookAtReverse` 分支把方向向量当目标点传给 `LookAt`，行为可疑。

```csharp
[Label("移动速度")]
public float moveSpeed = 5f;

var hp = new BindableProperty<int>(100);
hp.RegisterWithInitValue(v => Debug.Log($"HP = {v}"));   // 注册时立即回调一次当前值
hp.Value = 80;                                          // 触发回调（相同值不触发）
hp.SetValueWithoutEvent(60);                            // 赋值但不触发回调
```

---

## 十一、编辑器工具

| 入口 | 类型 | 说明 |
| --- | --- | --- |
| `ZGame/Resource Debugger` | `ResDebuggerWindow` | 资源调试窗口：查看 `ResTable` 全部条目、引用计数、加载中/未使用/缺失状态、池中与使用中的 `ResLoader`，支持搜索与筛选 |
| `Assets/Create/UI/UI Frame Prefab` | `UIFrameworkTools` | 生成完整 UI 根节点并保存为预制体 |
| `Assets/Create/UI/UI Frame in Scene` | `UIFrameworkTools` | 在当前场景生成 UI 根节点 |
| `Assets/Create/UI/Re-generate UI ScreenIds` | `ScreenIdProcessor` | 扫描界面预制体重新生成 Id 常量类 |
| `Assets/Create/ZGameFramework/UI/UISetting` | `UISetting` | 创建 UI 配置资产 |
| `Assets/Create/ZGameFramework/UI/Fake Player Data` | `FakePlayerData` | 示例数据资产 |
| `Assets/Create/BaseSoundEffExampleSO` | `SimpleAudioEventExample` | 创建音效资产 |

`ResDebuggerWindow` 通过反射读取 `ResTable` 的私有字段与 `ClassPool<ResLoader>.pool`，
因此**不要重命名** `ResTable.table`、`ClassPool<T>.pool`、`ResLoader.resList`、`ResLoaderRecycler.resLoaders` 等字段，否则调试窗口会失效。

---

## 十二、已知问题与注意事项

1. **`ParameterizedEvent.cs` 与 `SignalEvent.cs` 整个文件被 `/* */` 注释掉，类并未编译。**
   因此 **不存在** `ParameterizedEvent<T>` / `SignalEvent<T>` 类型，请勿在代码中引用。
   需要「参数化事件 + 对象池」能力时，请直接使用 `EventBus.Publish` / `EventBus.PublishSignal` 配合 `GameEvent`（必要时自行接 `ClassPool`）。

7. **`ResManager.Load/LoadAsync` 不增加引用计数**，其加载的资源可能被 `ClearUnused` 移出缓存表。
   需要稳定持有资源时请使用 `ResManager.GetResLoader()` + `ReleaseAll`，或 `BindTo(宿主)`。

8. **`ListPool` / `DictionaryPool` 必须成对归还**；`Recycle` 会清空内容，归还后原数据失效。

9. **`PoolManager` 以 GameObject 名字为池键**，不同预制体重名会串池。

10. **`WindowController` 的预制体属性默认优先**（`SuppressPrefabProperties == false` 时覆盖传入属性），
    详见 [9.5 属性对象](#95-属性对象)。

11. **`EventBus` 与 `IOCContainer` 均非线程安全**（无锁），请仅在主线程使用。
    `ClassPool` / `ListPool` / `DictionaryPool` / `CachePool` 内部有锁。

12. **`UIScreenController.Show` 的类型校验**：传入的 `props` 必须与控制器声明的 `TProps` 一致，
    否则只打印 `LogError` 并直接返回（界面不会显示，容易误判为「界面打不开」）。

13. **动画必须回调**：自定义 `AniComponent` 时若忘记调用 `callWhenFinished`，
    界面的 `IsVisible` 状态与 `InTransitionFinished` / `OutTransitionFinished` 都不会触发，
    进而导致窗口队列、输入遮罩卡死。

17. **`EventExample.cs` 的 `Demo()` 是先发布后注册**（两次发布不会触发随后注册的回调），
    仅作 API 演示，不要当作正确用法参考。

---

## 附：常用速查

```csharp
// 架构
MyArch.Interface.GetModel<BagModel>();
this.GetSystem<BattleSystem>();                    // 能力接口扩展方法
this.SendEvent(new PlayerDeadEvent { PlayerId = 1 });
this.RegisterEvent<PlayerDeadEvent>(OnDead).UnregisterOnDestroyTrigger(this);

// 单例
MyManager.Instance;                                // Singleton<T>
MyBehaviour.Instance;                              // MonoSingleton<T> / PersistentMonoSingleton<T>

// 主线程
MonoManager.Instance.AddUpdateListener(OnUpdate);
MonoManager.Instance.StartCoroutine(Routine());

// 对象池
var list = ListPool<int>.Get();  ListPool<int>.Recycle(list);
var go   = PoolManager.Instance.GetGameObject(prefab, parent);
PoolManager.Instance.PushGameObject(go);

// 资源
var loader = ResManager.GetResLoader().BindTo(this);
var prefab = loader.LoadSync<GameObject>("Prefabs/Enemy");
var go2    = ResManager.Instantiate(prefab);
await ResManager.UnloadUnused();

// 事件
EventBus.Publish(new PlayerDeadEvent());
EventBus.PublishSignal<StartUIDemoEvent>();

// UI
ui.OpenWindow("PlayerWindow", new PlayerWindowProperties(data));
ui.ShowPanel("ToastPanel");
ui.CloseCurrentWindow();
ui.HideAll();

// 音频
AudioVolumeManager.Instance.SetVolume(AudioChannel.BGM, 0.5f);
var src = AudioSourceManager.Instance.GetAudioSource(AudioChannel.SFX);
AudioSourceManager.Instance.ReturnAudioSource(src, 2f);

// 寻路
var path = AStarPathfinder.FindPath(graph, start, end, out int cost);
```
