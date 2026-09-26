using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 模板 Host：按 templateId 实例化 / 切换模板，并**隐藏保留**已实例化的模板
    /// （切回来的那一份直接激活，立绘和气泡不用重建）。
    ///
    /// 用法（挂在 Canvas 下，和模板实例同一层）：
    ///   Canvas
    ///     ├─ DialogueTemplateHost     ← 本组件（实例都挂在它下面）
    ///     │    ├─ Template_0 (运行时实例)
    ///     │    └─ Template_1 (运行时实例，隐藏保留)
    ///     └─ DialogueFlow
    ///          └─ DialogueView        ← templateHost 引用它，换组时按 templateId 调 Activate
    ///
    /// 它只负责「给我一个 templateId，还我一个激活好的布局」，不认识台词和流程。
    /// 它是 <see cref="IDialogueTemplateProvider"/> 的默认实现：编排层只认那个接口，
    /// 以后要换成异步加载 / 对象池实现，编排代码不用改。
    /// </summary>
    public class DialogueTemplateHost : MonoBehaviour, IDialogueTemplateProvider
    {
        [Header("引用")]
        [Label("模板表（templateId → Prefab）")]
        [SerializeField] private DialogueTemplateTable table;

        [Label("实例容器（空 = 挂在本物体下）")]
        [SerializeField] private RectTransform container;

        [Header("行为")]
        [Label("表里找不到 templateId 时退回的模板（一般 0 = 默认模板）")]
        [SerializeField] private int fallbackTemplateId = 0;

        [Label("切换时打日志")]
        [SerializeField] private bool logSwitches = true;

        private readonly Dictionary<int, DialogueLayoutRefs> m_Instances = new Dictionary<int, DialogueLayoutRefs>();
        private bool m_Validated;

        /// <summary>被"保留"的旧模板实例（溶解擦除期间压在新实例上面，擦完由 ReleasePrevious 放掉）</summary>
        private DialogueLayoutRefs m_Held;

        /// <summary>现在有没有保留着旧画面</summary>
        public bool HasHeldPrevious => m_Held != null;

        /// <summary>
        /// 放掉被保留的旧画面（隐藏保留，不销毁 —— 下次切回这个模板直接复用）。
        /// 没在保留状态时是空操作。
        /// </summary>
        public void ReleasePrevious()
        {
            if (m_Held == null)
            {
                return;
            }

            if (m_Held != Current)
            {
                m_Held.gameObject.SetActive(false);
            }

            m_Held = null;
        }

        /// <summary>现在激活的布局（null = 一个都没激活）</summary>
        public DialogueLayoutRefs Current { get; private set; }

        /// <summary>当前模板 id（-1 = 还没有）</summary>
        public int CurrentTemplateId { get; private set; } = -1;

        /// <summary>模板表（可空 = 没接模板系统，只用场景里那一份）</summary>
        public DialogueTemplateTable Table => table;

        private RectTransform Container
        {
            get
            {
                if (container == null)
                {
                    container = transform as RectTransform;
                }
                return container;
            }
        }

        private void Awake()
        {
            ValidateTable();
        }

        // ===================== 对外 =====================

        /// <summary>
        /// 切到某个 templateId：实例过就复用、没实例过就创建；旧的隐藏保留。
        /// 返回 false = 表里没有这个 id（也没配兜底模板），调用方保持原样即可。
        ///
        /// ★ <see cref="holdPrevious"/> = 溶解/交叉类擦除要"旧画面活到擦完"：
        ///   这时旧实例**不隐藏**，还会被提到新实例**上面**（观众看到的是旧画面被侵蚀掉、露出新的）。
        ///   擦完由调用方调 <see cref="ReleasePrevious"/> 放掉它。
        /// </summary>
        public bool Activate(int templateId, bool holdPrevious = false)
        {
            ValidateTable();

            var layout = GetOrCreate(templateId);

            if (layout == null && fallbackTemplateId != templateId)
            {
                Debug.LogWarning($"[对话模板] 表里没有 templateId={templateId}（表里的 id：{Table.IdList()}），" +
                                 $"退回 templateId={fallbackTemplateId}");
                layout = GetOrCreate(fallbackTemplateId);
                templateId = fallbackTemplateId;
            }

            if (layout == null)
            {
                Debug.LogError($"[对话模板] 表里没有 templateId={templateId}，也没有兜底模板，这次不换模板");
                return false;
            }

            if (Current == layout)
            {
                layout.gameObject.SetActive(true);
                return true;
            }

            ReleasePrevious();

            var previous = Current;

            layout.gameObject.SetActive(true);
            Current = layout;
            CurrentTemplateId = templateId;

            if (previous != null)
            {
                if (holdPrevious)
                {
                    // 保留旧画面：压在新实例上面，等擦完再放掉
                    m_Held = previous;
                    previous.transform.SetAsLastSibling();
                }
                else
                {
                    previous.gameObject.SetActive(false);
                }
            }

            if (logSwitches)
            {
                Debug.Log($"[对话模板] 切到 templateId={templateId}（{layout.name}）" + (holdPrevious ? "（保留旧画面）" : ""));
            }

            return true;
        }

        /// <summary>全部隐藏（退出对话 / 清场用）</summary>
        public void DeactivateAll()
        {
            m_Held = null;

            foreach (var pair in m_Instances)
            {
                if (pair.Value != null)
                {
                    pair.Value.gameObject.SetActive(false);
                }
            }

            Current = null;
            CurrentTemplateId = -1;
        }

        /// <summary>取某个 id 的实例（没有就创建，创建出来是隐藏的）</summary>
        public DialogueLayoutRefs GetOrCreate(int templateId)
        {
            if (m_Instances.TryGetValue(templateId, out var cached) && cached != null)
            {
                return cached;
            }

            var prefab = table != null ? table.Get(templateId) : null;
            if (prefab == null)
            {
                return null;
            }

            // instantiateInWorldSpace 必须 false：UI 要的是"原样的 local 布局"（锚点/偏移/pivot/缩放），
            // 用 true 会被父节点的缩放和世界坐标搅乱，模板实例化出来就变形了
            var instance = Instantiate(prefab, Container, false);
            instance.name = $"{prefab.name}_{templateId}";
            instance.gameObject.SetActive(false);

            // 实例化出来的模板要把自己的部件收一遍（立绘槽、气泡舞台）
            instance.Init();
            if (instance.bubbleStage != null)
            {
                instance.bubbleStage.Init();
            }

            // 自查一次：槽 id 重复 / 槽没气泡 / 接线缺了，都在这里报出来（同一个模板只报一次）
            DialogueTemplateValidator.ValidateAndLog(instance.gameObject, $"{prefab.name}（templateId={templateId}）");

            m_Instances[templateId] = instance;
            return instance;
        }

        // ===================== 内部 =====================

        private void ValidateTable()
        {
            if (m_Validated || table == null)
            {
                return;
            }

            m_Validated = true;

            var errors = new List<string>();
            var warnings = new List<string>();
            table.Validate(errors, warnings);

            foreach (var error in errors)
            {
                Debug.LogError($"[对话模板] 模板表有问题：{error}", table);
            }
            foreach (var warning in warnings)
            {
                Debug.LogWarning($"[对话模板] {warning}", table);
            }
        }
    }
}
