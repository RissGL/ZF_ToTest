using System;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework;
using ZGameFramework.Utility;
using ZF.EraGallery;
using ZF.Game;

namespace ZF.Puzzle
{
    /// <summary>一个状态对应一组物体：开哪一组，物体就是哪个状态。</summary>
    [Serializable]
    public class StateGroup
    {
        [Label("状态名")]
        public string state = PuzzleStates.Default;

        [Label("这一组物体")]
        public List<GameObject> objects = new List<GameObject>();
    }

    /// <summary>
    /// 场景里一个能点的东西（岩壁、柴堆、壁炉、以后的人…）。
    /// 它只做三件事：报自己的 id、按状态开关自己的状态组、被悬停时亮个框。
    /// 「点了之后发生什么」全在规则表里，这个组件一个字都不写。
    /// </summary>
    [DisallowMultipleComponent]
    public class Interactable : MonoBehaviour, IController
    {
        [Header("身份")]
        [Label("唯一 id（规则表靠它引用这个物体）")]
        [SerializeField] private string id = "";

        [Label("所在时代")]
        [SerializeField] private EraId era = EraId.Stone;

        [Label("显示名（日志 / 提示用）")]
        [SerializeField] private string displayName = "";

        [Header("状态表现")]
        [Label("状态组：同一时刻只开一组")]
        [SerializeField] private List<StateGroup> stateGroups = new List<StateGroup>();

        [Label("状态规则：从上往下，条件全满足就用这个状态")]
        [SerializeField] private List<VisualStateRule> visualRules = new List<VisualStateRule>();

        [Label("没有状态规则命中、也没被 SetObjectState 改过时，用哪个状态")]
        [SerializeField] private string defaultState = PuzzleStates.Default;

        [Header("交互")]
        [Label("点击区域（留空就用自己身上的 BoxCollider2D）")]
        [SerializeField] private BoxCollider2D hitArea;

        [Label("悬停高亮（可为空）")]
        [SerializeField] private SpriteRenderer hoverFrame;

        [Label("运行时按所有状态组自动贴合点击区域")]
        [SerializeField] private bool autoFitHitArea = true;

        [Header("形状层级")]
        [Label("自动排形状层级：按层级顺序依次 +1（打开后 Inspector 里手填的 sortingOrder 会被覆盖，但重叠的形状绝不会画得不确定）")]
        [SerializeField] private bool autoOrderShapes = true;

        [Label("形状层级起点")]
        [SerializeField] private int baseSortingOrder = PuzzleSortingOrder.ObjectBase;

        private IPuzzleModel m_Model;
        private IPuzzleSystem m_System;
        private string m_LastWarnedState;

        public string Id => id;
        public EraId Era => era;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
        public BoxCollider2D HitArea => hitArea;

        public IArchitecture GetArchitecture() => GameApp.Interface;

        private void Awake()
        {
            m_Model = this.GetModel<IPuzzleModel>();
            m_System = this.GetSystem<IPuzzleSystem>();

            if (hitArea == null)
            {
                hitArea = GetComponent<BoxCollider2D>();
            }

            if (hitArea != null)
            {
                hitArea.isTrigger = true;   // 只用来点选，不参与物理
            }

            if (autoOrderShapes)
            {
                AssignShapeOrders();
            }
            else
            {
                WarnOnLayerConflict();
            }

            if (autoFitHitArea)
            {
                // 先把所有状态组都打开再量包围盒 —— 不然量到的只是当前那组，
                // 切到"着火了"那组时火苗就不在可点范围里了。量完关掉，Start 里 Refresh 会把正确的组打开。
                SetAllGroupsActive(true);
                FitHitArea();
                SetAllGroupsActive(false);
            }
        }

        /// <summary>
        /// 按层级顺序（就是搭建时的先后）给形状依次排 sortingOrder。
        /// 打开它就不用自己算层级了；代价是 Inspector 里手填的 sortingOrder 会被它覆盖。
        /// </summary>
        [ContextMenu("自动排形状层级")]
        public void AssignShapeOrders()
        {
            List<SpriteRenderer> shapes = new List<SpriteRenderer>();
            CollectShapes(transform, shapes);

            for (int i = 0; i < shapes.Count; i++)
            {
                shapes[i].sortingOrder = baseSortingOrder + i;
            }
        }

        /// <summary>
        /// 查「层级相同又有重叠」的形状。
        /// 这种画图顺序是不确定的（每次 Play 可能都不一样），表现就是"东西时有时无" —— 属于最难查的一类 bug，
        /// 所以这里直接吼出来。互斥的状态组不会同时出现，不比较。
        /// </summary>
        [ContextMenu("检查形状层级")]
        public void WarnOnLayerConflict()
        {
            List<SpriteRenderer> shapes = new List<SpriteRenderer>();
            CollectShapes(transform, shapes);

            for (int i = 0; i < shapes.Count; i++)
            {
                for (int j = i + 1; j < shapes.Count; j++)
                {
                    if (shapes[i].sortingOrder != shapes[j].sortingOrder)
                    {
                        continue;
                    }

                    int groupA = GroupIndexOf(shapes[i].gameObject);
                    int groupB = GroupIndexOf(shapes[j].gameObject);
                    if (groupA >= 0 && groupB >= 0 && groupA != groupB)
                    {
                        continue;
                    }

                    // 缩小一点点再判相交：Bounds.Intersects 把"边贴着边"也算相交，会误报
                    Bounds a = shapes[i].bounds;
                    a.size -= new Vector3(0.02f, 0.02f, 0f);

                    if (a.size.x <= 0f || a.size.y <= 0f || !a.Intersects(shapes[j].bounds))
                    {
                        continue;
                    }

                    Debug.LogWarning($"[谜题] 「{name}」的「{shapes[i].name}」和「{shapes[j].name}」" +
                                     $"层级都是 {shapes[i].sortingOrder} 又互相重叠，画的顺序不确定。\n" +
                                     "  改法：给它们递增的 sortingOrder，或者把「自动排形状层级」打开。");
                }
            }
        }

        /// <summary>按层级顺序收集形状（深度优先，顺序 = 搭建/Inspector 里的先后）。</summary>
        private static void CollectShapes(Transform parent, List<SpriteRenderer> shapes)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                if (child.TryGetComponent(out SpriteRenderer sprite))
                {
                    shapes.Add(sprite);
                }

                CollectShapes(child, shapes);
            }
        }

        private int GroupIndexOf(GameObject target)
        {
            for (int i = 0; i < stateGroups.Count; i++)
            {
                if (stateGroups[i]?.objects != null && stateGroups[i].objects.Contains(target))
                {
                    return i;
                }
            }

            return -1;
        }

        private void Start()
        {
            m_Model?.Revision.Register(_ => Refresh()).UnregisterOnDestroyTrigger(this);
            Refresh();
        }

        /// <summary>按当前状态重算外观。状态是算出来的，不是存起来的 —— 所以石器时代点个火，这边会自动亮。</summary>
        public void Refresh()
        {
            if (m_System == null)
            {
                return;
            }

            ApplyState(m_System.ResolveVisualState(id, visualRules, defaultState));
        }

        public void SetHover(bool hover)
        {
            if (hoverFrame != null)
            {
                hoverFrame.enabled = hover;
            }
        }

        private void ApplyState(string state)
        {
            bool matched = false;

            for (int i = 0; i < stateGroups.Count; i++)
            {
                StateGroup group = stateGroups[i];
                if (group == null)
                {
                    continue;
                }

                bool on = PuzzleOps.SameState(group.state, state);
                matched |= on;

                if (group.objects != null)
                {
                    for (int j = 0; j < group.objects.Count; j++)
                    {
                        if (group.objects[j] != null && group.objects[j].activeSelf != on)
                        {
                            group.objects[j].SetActive(on);
                        }
                    }
                }
            }

            if (matched || stateGroups.Count == 0)
            {
                return;
            }

            // 状态名没配对（十有八九是打错字），别让物体整个消失 —— 退回默认状态并吼一声
            int fallback = IndexOfState(defaultState);
            if (fallback < 0)
            {
                fallback = 0;
            }

            if (!PuzzleOps.SameState(m_LastWarnedState, state))
            {
                m_LastWarnedState = state;
                Debug.LogWarning($"[谜题] 物体「{name}」没有叫「{state}」的状态组，先退回「{stateGroups[fallback].state}」。");
            }

            ApplyState(stateGroups[fallback].state);
        }

        private void SetAllGroupsActive(bool active)
        {
            for (int i = 0; i < stateGroups.Count; i++)
            {
                StateGroup group = stateGroups[i];
                if (group?.objects == null)
                {
                    continue;
                }

                for (int j = 0; j < group.objects.Count; j++)
                {
                    if (group.objects[j] != null && group.objects[j].activeSelf != active)
                    {
                        group.objects[j].SetActive(active);
                    }
                }
            }
        }

        private int IndexOfState(string state)
        {
            for (int i = 0; i < stateGroups.Count; i++)
            {
                if (stateGroups[i] != null && PuzzleOps.SameState(stateGroups[i].state, state))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>点击区域 = 所有状态组里所有渲染器的并集（包含没开的那几组），这样切状态不会让可点范围跳。</summary>
        [ContextMenu("按视觉重算点击区域")]
        public void FitHitArea()
        {
            if (hitArea == null)
            {
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            bool has = false;
            Bounds bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer == hoverFrame)
                {
                    continue;
                }

                if (has)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    has = true;
                }
            }

            if (!has)
            {
                return;
            }

            hitArea.offset = transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = transform.InverseTransformVector(bounds.size);
            hitArea.size = new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y));
        }
    }
}
