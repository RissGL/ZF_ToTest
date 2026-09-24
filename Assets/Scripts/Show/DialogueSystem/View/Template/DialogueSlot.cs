using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 槽 = 模板里的一个「角色位」。
    ///
    /// ★ 为什么要有槽：气泡如果直接按角色绑定，模板就和角色 id 焊死了 ——
    ///   三个角色想换位置组合就得做三份模板。用槽之后，
    ///   模板只描述「这一格有几个位置、各在哪、长什么样」，角色是运行时填进槽里的。
    ///
    /// 槽里的部件都是**互相独立的兄弟节点**（谁缩放/位移都不带动别人）：
    ///   气泡（Bubble）                —— 位置 + 生长方向 + 默认样式
    ///   立绘框（PortraitFrame）       —— 画格：框的视觉 + 自己的进出场动画 + 高亮
    ///   立绘（Portrait）              —— 框里那张画
    ///   背景（PortraitBackground）    —— 这一格的底色/底板
    /// 所以：
    ///   旁白槽   = 只有气泡（没有框、没有立绘、没有背景）
    ///   纯立绘槽 = 只有框 + 立绘（站在那里不说话的角色）
    ///
    /// 层级建议（模板 Prefab 里）：
    ///   Slot_左                    ← 本组件（slotId = "左"），RectTransform 拉满即可
    ///     ├─ Frame_左            ← 立绘框（PortraitFrame）
    ///     ├─ Portrait_左         ← 立绘（Portrait）
    ///     ├─ Background_左       ← 背景（PortraitBackground）
    ///     └─ Bubble_左           ← 气泡（Bubble）
    /// </summary>
    public class DialogueSlot : MonoBehaviour
    {
        [Label("槽 id（数据里的 slotId 填它，就能把这句固定在这个槽说）")]
        public string slotId;

        [Label("气泡（留空 = 自动取子物体里的 Bubble；也没有 = 这个槽不弹气泡）")]
        public Bubble bubble;

        [Label("立绘框（留空 = 自动取子物体里的 PortraitFrame；也没有 = 这个槽不站人，比如旁白）")]
        public PortraitFrame frame;

        [Label("立绘（留空 = 自动取子物体里的 Portrait；和框是兄弟节点，互不影响）")]
        public Portrait portrait;

        [Label("背景（留空 = 自动取子物体里的 PortraitBackground；这一格的底色）")]
        public PortraitBackground background;

        /// <summary>槽 id（没填就用物体名）</summary>
        public string SlotId => string.IsNullOrEmpty(slotId) ? name : slotId;

        public bool HasBubble => bubble != null;
        public bool HasFrame => frame != null;
        public bool HasPortrait => portrait != null;
        public bool HasBackground => background != null;

        private void Awake()
        {
            Collect();
        }

        /// <summary>把子物体里的气泡 / 立绘框 / 立绘 / 背景收上来（模板实例化后也会调一次）</summary>
        public void Collect()
        {
            if (bubble == null)
            {
                bubble = PickOne(GetComponentsInChildren<Bubble>(true), "气泡（Bubble）");
            }
            if (frame == null)
            {
                frame = PickOne(GetComponentsInChildren<PortraitFrame>(true), "立绘框（PortraitFrame）");
            }
            if (portrait == null)
            {
                portrait = PickOne(GetComponentsInChildren<Portrait>(true), "立绘（Portrait）");
            }
            if (background == null)
            {
                background = PickOne(GetComponentsInChildren<PortraitBackground>(true), "背景（PortraitBackground）");
            }
        }

        /// <summary>
        /// ★ 故意不用「GetComponentInChildren 抓第一个」：一个槽里放了两个同类部件时，
        ///   那种写法会**静默**用错一个（美术多加一个框，到底谁显示全看层级顺序，还查不出来）。
        ///   这里收全部，多于一个就报警说清楚哪个生效。
        ///   顺序和 GetComponentInChildren 一致（都是层级顺序），所以只有一个时行为完全不变。
        /// </summary>
        private T PickOne<T>(T[] found, string what) where T : Component
        {
            if (found == null || found.Length == 0)
            {
                return null;
            }

            if (found.Length > 1)
            {
                Debug.LogWarning($"[对话槽] 「{name}」下有 {found.Length} 个{what}，用「{found[0].name}」，其余不会显示" +
                                 "（一个槽只放一个；真要两个就拆成两个槽）", this);
            }

            return found[0];
        }
    }
}
