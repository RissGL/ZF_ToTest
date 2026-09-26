using System;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;
using ZF.EraGallery;

namespace ZF.Puzzle
{
    /// <summary>
    /// 一条交互规则：**谁 + 什么动作 + 条件全满足 → 干这些事**。
    ///
    /// 匹配顺序很重要：规则表从上往下读，**第一条「目标 + 动作匹配 且 条件全满足」的生效**。
    /// 所以写法是「特殊条件在前，通用兜底在后」。
    /// </summary>
    [Serializable]
    public class InteractionRule
    {
        [Label("备注（只给 Inspector 和日志看）")]
        public string note = "";

        [Label("目标物体 id（留空 = 任何物体）")]
        public string targetId = "";

        [Label("动作")]
        public Verb verb = Verb.Interact;

        [Label("需要用的道具 id（动作 = UseItem 时才看）")]
        public string itemId = "";

        [Label("条件：全部满足才算命中")]
        [SerializeReference]
        public List<PuzzleCondition> conditions = new List<PuzzleCondition>();

        [Label("效果：命中后按顺序执行")]
        [SerializeReference]
        public List<PuzzleEffect> effects = new List<PuzzleEffect>();

        // 多行用 Label 的 lines 参数，不要配 [TextArea]（两个绘制器会打架，字段会叠在一起）
        [Label("条件不满足时，对玩家说（留空 = 不提示）", 3)]
        public string elseFeedback = "";
    }

    /// <summary>
    /// 物体「现在该长什么样」的一条规则：条件全满足就用这个状态。
    /// 这是**跨时代表现联动**的关键 —— 信息时代的壁炉写一条「fire_lit == 1 → lit」，
    /// 石器时代点着火，它自己就烧起来了，两边不需要互相知道对方存在。
    /// </summary>
    [Serializable]
    public class VisualStateRule
    {
        [Label("备注")]
        public string note = "";

        [Label("条件全满足就切到这个状态")]
        [SerializeReference]
        public List<PuzzleCondition> conditions = new List<PuzzleCondition>();

        [Label("状态名（要和物体上的状态组对得上）")]
        public string state = PuzzleStates.Default;
    }

    /// <summary>谜题的两种写法。</summary>
    public enum PuzzleMode
    {
        /// <summary>条件式：写一组完成条件，全满足就解开（简单谜题用这个）。</summary>
        Conditions = 0,

        /// <summary>步骤式：列一串步骤，做一步勾一步（多阶段谜题用这个，能显示进度）。</summary>
        Steps = 1,
    }

    /// <summary>
    /// 谜题的一步（步骤式用）。
    ///
    /// 和条件式最大的区别：**完成过就记死**。
    /// 比如"第一步：拿到燧石"，后来燧石被用掉了，这一步**也不会退回未完成**
    /// —— 不然玩家做过的进度会自己倒退，那种感觉非常糟。
    /// </summary>
    [Serializable]
    public class PuzzleStep
    {
        [Label("步骤 id（存进度用，别乱改）")]
        public string id = "";

        [Label("这一步叫什么（给玩家看的提示）", 2)]
        public string title = "";

        [Label("这一步什么时候算完成（不用条件 = 永远不算完成）")]
        [SerializeReference]
        public List<PuzzleCondition> conditions = new List<PuzzleCondition>();

        [Label("可选步骤：不做也能通关")]
        public bool optional = false;

        [Label("这一步完成时顺手做什么")]
        [SerializeReference]
        public List<PuzzleEffect> onCompleted = new List<PuzzleEffect>();
    }

    /// <summary>
    /// 一个谜题：条件全满足就算解开（不用手动标记），是给玩家看的进度单位。
    /// 标了 isMainPuzzle 的话，解开 = 这个时代通关（会触发 EraGallery 的时代通关）。
    /// </summary>
    [Serializable]
    public class PuzzleDefinition
    {
        [Label("谜题 id")]
        public string id = "";

        [Label("名字（日志 / 以后的 UI 用）")]
        public string title = "";

        [Label("属于哪个时代")]
        public EraId era = EraId.Stone;

        [Label("类型：条件式 / 步骤式")]
        public PuzzleMode mode = PuzzleMode.Conditions;

        [Label("步骤式：必须按顺序做（后面的步骤要等前面的必做步骤做完）")]
        public bool stepsInOrder = true;

        [Label("步骤清单（步骤式用；这时候「完成条件」那块就别填了）")]
        public List<PuzzleStep> steps = new List<PuzzleStep>();

        [Label("完成条件：状态每变一次就重新检查，全部满足就自动完成（条件式用）")]
        [SerializeReference]
        public List<PuzzleCondition> conditions = new List<PuzzleCondition>();

        [Label("完成时执行的效果")]
        [SerializeReference]
        public List<PuzzleEffect> onSolved = new List<PuzzleEffect>();

        [Label("算不算「这个时代通关了」")]
        public bool isMainPuzzle = false;

        /// <summary>步骤式：必做步骤一共几步、做完几步（给 UI 显示进度用）。</summary>
        public void CountSteps(out int requiredTotal, out int requiredDone, IPuzzleModel model)
        {
            requiredTotal = 0;
            requiredDone = 0;

            if (steps == null || model == null)
            {
                return;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                PuzzleStep step = steps[i];
                if (step == null || step.optional || string.IsNullOrEmpty(step.id))
                {
                    continue;
                }

                requiredTotal++;

                if (model.IsStepDone(id, step.id))
                {
                    requiredDone++;
                }
            }
        }
    }

    /// <summary>
    /// 一张谜题规则表：整个游戏的交互规则 + 谜题都写在这儿。
    /// 一张表而不是每个谜题一个资产 —— 跨时代的规则能挨着写，一眼看得出联动关系。
    /// </summary>
    [CreateAssetMenu(menuName = "ZGameFramework/谜题规则表", fileName = "PuzzleTable")]
    public class PuzzleTableSO : ScriptableObject
    {
        [Label("交互规则（从上往下，第一条「目标+动作匹配 且 条件全满足」的生效）")]
        public List<InteractionRule> interactionRules = new List<InteractionRule>();

        [Label("谜题")]
        public List<PuzzleDefinition> puzzles = new List<PuzzleDefinition>();

        public PuzzleDefinition FindPuzzle(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId))
            {
                return null;
            }

            for (int i = 0; i < puzzles.Count; i++)
            {
                if (puzzles[i] != null && puzzles[i].id == puzzleId)
                {
                    return puzzles[i];
                }
            }

            return null;
        }
    }
}
