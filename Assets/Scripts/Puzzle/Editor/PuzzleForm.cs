using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZF.EraGallery;

namespace ZF.Puzzle.EditorTools
{
    /// <summary>
    /// 给策划用的**一行一句话**规则表单。
    ///
    /// 刻意不画"字段名: 值"那种竖着堆的字段墙 —— 每条条件 / 效果都压成**一行**，
    /// 行内用「类型下拉 + 参数控件」横向拼成一句人话：
    ///
    ///     当玩家   [用道具点 ▾] [柴堆 ▾]
    ///     如果     [持有道具 ▾] [燧石 ▾]                                   [✕]
    ///     那么     [消耗道具 ▾] [燧石 ▾]                                   [✕]
    ///              [设置flag ▾] [fire_lit ▾] [= ▾] [1]                     [✕]
    ///              [说一句话 ▾] ［火星落进干柴，火窜起来了。           ］  [✕]
    ///     否则说   ［                                            ］
    ///
    /// 类型也能直接在行里换（把"持有道具"改成"选中道具"不用删了重加）；
    /// id / flag / 道具 / 人物 / 谜题 / 状态名 一律带 ▾ 从真实存在的候选里挑，不用手打。
    /// </summary>
    public class PuzzleForm
    {
        private class Entry
        {
            public string Group;
            public string Name;
            public Type Type;
            public Func<object> Create;
        }

        private static readonly Entry[] ConditionEntries =
        {
            E<FlagCondition>("世界状态", "flag 满足某个值", () => new FlagCondition { flag = "新flag", op = FlagOp.Equals, value = 1f }),
            E<ObjectStateCondition>("世界状态", "物体是某个状态", () => new ObjectStateCondition { state = "lit" }),
            E<HasItemCondition>("背包", "持有道具", () => new HasItemCondition()),
            E<SelectedItemCondition>("背包", "手里选中道具", () => new SelectedItemCondition()),
            E<PuzzleSolvedCondition>("谜题", "谜题已完成", () => new PuzzleSolvedCondition()),
            E<CharacterInEraCondition>("人物", "某个人在某个时代", () => new CharacterInEraCondition()),
            E<CharacterCountInEraCondition>("人物", "某个时代里有几个人", () => new CharacterCountInEraCondition { op = FlagOp.GreaterOrEqual, count = 2f }),
            E<SelectedCharacterCondition>("人物", "点名的是某个人", () => new SelectedCharacterCondition()),
            E<SelectedCharacterInEraCondition>("人物", "点名的人在某个时代", () => new SelectedCharacterInEraCondition()),
            E<EraFocusedCondition>("时代窗口", "玩家正进在某个时代里", () => new EraFocusedCondition()),
            E<AndCondition>("组合", "「全部」满足", () => new AndCondition()),
            E<OrCondition>("组合", "「任意一个」满足", () => new OrCondition()),
            E<NotCondition>("组合", "「取反」", () => new NotCondition()),
        };

        private static readonly Entry[] EffectEntries =
        {
            E<SetFlagEffect>("世界状态", "设置 flag", () => new SetFlagEffect { flag = "新flag", value = 1f }),
            E<AddFlagEffect>("世界状态", "flag 加一点", () => new AddFlagEffect { flag = "新flag", delta = 1f }),
            E<SetObjectStateEffect>("世界状态", "换物体状态", () => new SetObjectStateEffect { state = "lit" }),
            E<GiveItemEffect>("背包", "获得道具", () => new GiveItemEffect()),
            E<ConsumeItemEffect>("背包", "消耗道具", () => new ConsumeItemEffect()),
            E<SelectItemEffect>("背包", "拿在手里", () => new SelectItemEffect()),
            E<SolvePuzzleEffect>("谜题", "完成谜题", () => new SolvePuzzleEffect()),
            E<MoveCharacterEffect>("人物", "把人搬到某个时代", () => new MoveCharacterEffect()),
            E<MoveCharacterStepEffect>("人物", "把这个人送到下一个时代", () => new MoveCharacterStepEffect()),
            E<MoveSelectedCharacterEffect>("人物", "把点名的人搬过去", () => new MoveSelectedCharacterEffect()),
            E<MoveEraCharactersEffect>("人物", "一个时代的人整体搬走", () => new MoveEraCharactersEffect()),
            E<SelectCharacterEffect>("人物", "点名 / 取消点名", () => new SelectCharacterEffect()),
            E<PlayCharacterAnimationEffect>("人物", "让他播一段动画（动画接口）", () => new PlayCharacterAnimationEffect()),
            E<FeedbackEffect>("给玩家", "说一句话", () => new FeedbackEffect()),
            E<DebugLogEffect>("给玩家", "只写 Console", () => new DebugLogEffect()),
        };

        private static Entry E<T>(string group, string name, Func<T> create) where T : class
        {
            return new Entry { Group = group, Name = name, Type = typeof(T), Create = () => create() };
        }

        private const float TypeWidth = 132f;
        private const float TinyWidth = 22f;
        private const float IdWidth = 140f;
        private const float EnumWidth = 84f;
        private const float NumberWidth = 56f;

        private readonly PuzzleScanResult m_Scan;
        private readonly List<string> m_Flags = new List<string>();
        private readonly List<string> m_Items = new List<string>();
        private readonly List<string> m_Objects = new List<string>();
        private readonly List<string> m_Characters = new List<string>();
        private readonly List<string> m_Puzzles = new List<string>();
        private readonly List<string> m_States = new List<string>();

        /// <summary>目标候选 = `@类别`（一次管一批）+ 具体 id。</summary>
        private readonly List<string> m_Targets = new List<string>();

        /// <summary>人物 id 候选 + `@self`（"被点的那个"）。</summary>
        private readonly List<string> mCharacterRefs = new List<string>();

        /// <summary>需要"是哪个目标自己"的字段用它。</summary>
        private readonly List<string> mObjectRefs = new List<string>();

        public PuzzleForm(PuzzleScanResult scan)
        {
            m_Scan = scan;
            RefreshCandidates();
        }

        /// <summary>候选变了（重新扫描过）就调一次。</summary>
        public void RefreshCandidates()
        {
            m_Flags.Clear();
            m_Items.Clear();
            m_Objects.Clear();
            m_Characters.Clear();
            m_Puzzles.Clear();
            m_States.Clear();
            m_Targets.Clear();
            mCharacterRefs.Clear();
            mObjectRefs.Clear();

            if (m_Scan == null)
            {
                return;
            }

            m_Flags.AddRange(m_Scan.WrittenFlags);
            m_Flags.AddRange(m_Scan.ReadFlags);
            m_Items.AddRange(m_Scan.GivenItems);
            m_Items.AddRange(m_Scan.UsedItems);
            m_Objects.AddRange(m_Scan.InteractionIds);
            m_Characters.AddRange(m_Scan.CharacterIds);
            m_States.AddRange(m_Scan.ObjectStates);

            m_Flags.Sort();
            m_Items.Sort();
            m_Objects.Sort();
            m_Characters.Sort();
            m_States.Sort();

            // 类别在前（`@rift` 这种是"一次管一批"，比具体 id 更常用），再列具体 id
            foreach (string category in m_Scan.Categories)
            {
                if (!string.IsNullOrEmpty(category))
                {
                    m_Targets.Add(PuzzleRef.CategoryRef(category));
                }
            }

            m_Targets.Sort();
            m_Targets.AddRange(m_Objects);

            mCharacterRefs.AddRange(m_Characters);
            mCharacterRefs.Add(PuzzleRef.Self);

            mObjectRefs.AddRange(m_Objects);
            mObjectRefs.Add(PuzzleRef.Self);
        }

        public void SetPuzzleIds(List<string> ids)
        {
            m_Puzzles.Clear();

            if (ids != null)
            {
                m_Puzzles.AddRange(ids);
                m_Puzzles.Sort();
            }
        }

        // ===================== 画一条规则 =====================

        /// <summary>
        /// 画一条规则。onMoveRule 传 ±1 = 想改优先级（调用方**推迟到下一帧**再动数组）。
        /// 返回 true = 这次有改动。
        /// </summary>
        public bool Draw(SerializedProperty rule, int ruleIndex, Action<int> onMoveRule)
        {
            EditorGUI.BeginChangeCheck();

            DrawHeader(rule, ruleIndex, onMoveRule);
            DrawTargetRow(rule);
            DrawConditionBlock(rule.FindPropertyRelative("conditions"), 0);
            DrawEffectBlock(rule.FindPropertyRelative("effects"), 0);
            DrawTextRow("否则说", rule.FindPropertyRelative("elseFeedback"));
            DrawTextRow("备注", rule.FindPropertyRelative("note"));

            return EditorGUI.EndChangeCheck();
        }

        // ===================== 画一个谜题 =====================

        /// <summary>画一个谜题。完成条件和完成效果用的是同一套「一行一句话」的控件。</summary>
        public bool DrawPuzzle(SerializedProperty puzzle, int index)
        {
            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"谜题 #{index + 1}", EditorStyles.boldLabel, GUILayout.Width(64f));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("条件全满足就自动完成", EditorStyles.miniLabel, GUILayout.Width(160f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("名字", GUILayout.Width(48f));
                Text(puzzle.FindPropertyRelative("title"), 150f);
                EditorGUILayout.LabelField("id", GUILayout.Width(18f));
                Text(puzzle.FindPropertyRelative("id"), 130f);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("属于", GUILayout.Width(48f));
                EraPopup(puzzle.FindPropertyRelative("era"));

                EditorGUILayout.LabelField("算作「这个时代通关」", GUILayout.Width(140f));
                SerializedProperty isMain = puzzle.FindPropertyRelative("isMainPuzzle");
                isMain.boolValue = EditorGUILayout.Toggle(isMain.boolValue, GUILayout.Width(20f));
            }

            SerializedProperty mode = puzzle.FindPropertyRelative("mode");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("写法", GUILayout.Width(48f));
                EnumPopup(mode, new[] { "条件式（写一组条件，全满足就完成）", "步骤式（列一串步骤，做一步勾一步）" }, 300f);
            }

            EditorGUILayout.Space(2f);

            if (mode.intValue == (int)PuzzleMode.Steps)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("必须按顺序做", GUILayout.Width(90f));
                    SerializedProperty inOrder = puzzle.FindPropertyRelative("stepsInOrder");
                    inOrder.boolValue = EditorGUILayout.Toggle(inOrder.boolValue, GUILayout.Width(20f));
                    EditorGUILayout.LabelField("（勾上：后面的步骤要等前面的必做步骤做完）", EditorStyles.miniLabel);
                }

                DrawStepBlock(puzzle.FindPropertyRelative("steps"));
            }
            else
            {
                DrawConditionBlock(puzzle.FindPropertyRelative("conditions"), 0, "完成条件（全满足就完成）");
            }

            DrawEffectBlock(puzzle.FindPropertyRelative("onSolved"), 0, "整个谜题完成时执行");

            EditorGUILayout.Space(2f);

            return EditorGUI.EndChangeCheck();
        }

        // ===================== 步骤清单 =====================

        private void DrawStepBlock(SerializedProperty list)
        {
            if (list == null)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("步骤清单", EditorStyles.boldLabel, GUILayout.Width(90f));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("＋ 加一步", GUILayout.Width(80f)))
                {
                    list.arraySize++;
                    SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
                    added.FindPropertyRelative("id").stringValue = "s" + list.arraySize;
                    added.FindPropertyRelative("title").stringValue = "第 " + list.arraySize + " 步";
                    added.FindPropertyRelative("optional").boolValue = false;
                    added.FindPropertyRelative("conditions").arraySize = 0;
                    added.FindPropertyRelative("onCompleted").arraySize = 0;
                }
            }

            if (list.arraySize == 0)
            {
                EditorGUILayout.LabelField("（一步都没写 —— 这个谜题不会自己完成）", EditorStyles.miniLabel);
                return;
            }

            int deleteAt = -1;
            int duplicateAt = -1;
            int moveFrom = -1;
            int moveTo = -1;

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty step = list.GetArrayElementAtIndex(i);
                SerializedProperty optional = step.FindPropertyRelative("optional");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"{i + 1}.", EditorStyles.boldLabel, GUILayout.Width(22f));

                        SerializedProperty title = step.FindPropertyRelative("title");
                        title.stringValue = EditorGUILayout.TextField(title.stringValue, GUILayout.Width(150f));

                        EditorGUILayout.LabelField("id", GUILayout.Width(16f));
                        SerializedProperty id = step.FindPropertyRelative("id");
                        id.stringValue = EditorGUILayout.TextField(id.stringValue, GUILayout.Width(70f));

                        optional.boolValue = EditorGUILayout.ToggleLeft("可选", optional.boolValue, GUILayout.Width(48f));

                        GUILayout.FlexibleSpace();

                        GUI.enabled = i > 0;
                        if (GUILayout.Button("↑", EditorStyles.miniButton, GUILayout.Width(TinyWidth)))
                        {
                            moveFrom = i;
                            moveTo = i - 1;
                        }

                        GUI.enabled = i < list.arraySize - 1;
                        if (GUILayout.Button("↓", EditorStyles.miniButton, GUILayout.Width(TinyWidth)))
                        {
                            moveFrom = i;
                            moveTo = i + 1;
                        }

                        GUI.enabled = true;

                        if (GUILayout.Button("⧉", EditorStyles.miniButton, GUILayout.Width(TinyWidth)))
                        {
                            duplicateAt = i;
                        }

                        if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(TinyWidth)))
                        {
                            deleteAt = i;
                        }
                    }

                    DrawConditionBlock(step.FindPropertyRelative("conditions"), 1,
                        optional.boolValue ? "这一步算完成（可选的）" : "这一步算完成");
                    DrawEffectBlock(step.FindPropertyRelative("onCompleted"), 1, "这步做完顺手");
                }
            }

            if (deleteAt >= 0)
            {
                Delete(list, deleteAt);
            }
            else if (duplicateAt >= 0)
            {
                Duplicate(list, duplicateAt);
            }
            else if (moveFrom >= 0)
            {
                list.MoveArrayElement(moveFrom, moveTo);
            }
        }

        private void DrawHeader(SerializedProperty rule, int ruleIndex, Action<int> onMoveRule)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"规则 #{ruleIndex + 1}", EditorStyles.boldLabel, GUILayout.Width(64f));

                if (onMoveRule != null)
                {
                    if (GUILayout.Button("▲ 上移", GUILayout.Width(56f)))
                    {
                        onMoveRule(-1);
                    }

                    if (GUILayout.Button("▼ 下移", GUILayout.Width(56f)))
                    {
                        onMoveRule(1);
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("从上往下试，第一条条件全满足的生效", EditorStyles.miniLabel, GUILayout.Width(220f));
            }
        }

        private void DrawTargetRow(SerializedProperty rule)
        {
            SerializedProperty target = rule.FindPropertyRelative("targetId");
            SerializedProperty verb = rule.FindPropertyRelative("verb");
            SerializedProperty item = rule.FindPropertyRelative("itemId");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("当玩家", GUILayout.Width(48f));

                int mode = Mathf.Clamp(verb.intValue, 0, 2);
                int picked = EditorGUILayout.Popup(mode, new[] { "任意动作", "空手点", "用道具点" }, GUILayout.Width(EnumWidth + 20f));

                if (picked != mode)
                {
                    verb.intValue = picked;
                    mode = picked;
                }

                if (mode == 2)
                {
                    Picker(item, m_Items, "道具", IdWidth, "(不要求)");
                }

                Picker(target, m_Targets, null, IdWidth + 60f, "(任何目标)");
            }
        }

        // ===================== 条件 =====================

        private void DrawConditionBlock(SerializedProperty list, int indent, string title = null)
        {
            if (list == null)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(indent * 14f);
                EditorGUILayout.LabelField(title ?? (indent == 0 ? "并且满足" : "其中全部满足"),
                    GUILayout.Width(title != null ? 180f : 72f));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("＋ 加条件", GUILayout.Width(80f)))
                {
                    AddMenu(list, ConditionEntries);
                }
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                DrawEntryRow(list, i, ConditionEntries, indent, false);
            }

            if (list.arraySize == 0 && indent == 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(48f);
                    EditorGUILayout.LabelField(
                        title != null
                            ? "（没写完成条件 —— 它不会自己完成，只能被「完成谜题」效果显式完成）"
                            : "（无条件 —— 它会挡住下面同目标的规则！）",
                        EditorStyles.miniLabel);
                }
            }
        }

        private void DrawEffectBlock(SerializedProperty list, int indent, string title = null)
        {
            if (list == null)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(indent * 14f);
                EditorGUILayout.LabelField(title ?? (indent == 0 ? "那么就" : "其中全部"),
                    GUILayout.Width(title != null ? 180f : 72f));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("＋ 加效果", GUILayout.Width(80f)))
                {
                    AddMenu(list, EffectEntries);
                }
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                DrawEntryRow(list, i, EffectEntries, indent, true);
            }
        }

        /// <summary>一行 = 一条条件 / 效果。</summary>
        private void DrawEntryRow(SerializedProperty list, int index, Entry[] entries, int indent, bool isEffect)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(index);
            object value = element.managedReferenceValue;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(indent * 14f + 16f);

                // 类型（可以直接换，不用删了重加）
                int typeIndex = IndexOfType(entries, value);
                int picked = EditorGUILayout.Popup(typeIndex, Names(entries, isEffect), GUILayout.Width(TypeWidth));

                if (picked != typeIndex && picked >= 0)
                {
                    element.managedReferenceValue = entries[picked].Create();
                    value = element.managedReferenceValue;
                }

                if (value == null)
                {
                    EditorGUILayout.LabelField("（空的，删掉重加）", EditorStyles.miniLabel);
                }
                else if (isEffect)
                {
                    DrawEffectParams(element, value);
                }
                else
                {
                    DrawConditionParams(element, value, indent);
                }

                GUILayout.FlexibleSpace();

                if (isEffect)
                {
                    GUI.enabled = index > 0;
                    if (GUILayout.Button("↑", EditorStyles.miniButton, GUILayout.Width(TinyWidth)))
                    {
                        list.MoveArrayElement(index, index - 1);
                    }

                    GUI.enabled = index < list.arraySize - 1;
                    if (GUILayout.Button("↓", EditorStyles.miniButton, GUILayout.Width(TinyWidth)))
                    {
                        list.MoveArrayElement(index, index + 1);
                    }

                    GUI.enabled = true;
                }

                if (GUILayout.Button("⧉", EditorStyles.miniButton, GUILayout.Width(TinyWidth)))
                {
                    Duplicate(list, index);
                }

                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(TinyWidth)))
                {
                    Delete(list, index);
                }
            }

            // 组合条件往下一层缩进
            if (!isEffect && value is AndCondition and)
            {
                DrawConditionBlock(element.FindPropertyRelative("items"), indent + 1);
            }
            else if (!isEffect && value is OrCondition or)
            {
                DrawConditionBlock(element.FindPropertyRelative("items"), indent + 1);
            }
            else if (!isEffect && value is NotCondition)
            {
                // 取反很少用（flag 直接选「不等于」更简单），里面那条就用普通字段画
                SerializedProperty inner = element.FindPropertyRelative("item");
                if (inner != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space((indent + 1) * 14f + 16f);
                        EditorGUILayout.PropertyField(inner, new GUIContent("  ↳ 要取反的那条"), true);
                    }
                }
            }
        }

        // ===================== 行内参数 =====================

        private void DrawConditionParams(SerializedProperty element, object value, int indent)
        {
            switch (value)
            {
                case FlagCondition _:
                    Picker(element.FindPropertyRelative("flag"), m_Flags, "flag", IdWidth, "(不检查)");
                    EnumPopup(element.FindPropertyRelative("op"), new[] { "等于", "不等于", "大于", "大于等于", "小于", "小于等于" });
                    Number(element.FindPropertyRelative("value"));
                    return;

                case ObjectStateCondition _:
                    Picker(element.FindPropertyRelative("interactableId"), mObjectRefs, "物体", IdWidth, "(不检查)");
                    Picker(element.FindPropertyRelative("state"), m_States, "状态名", IdWidth, "(留空)");
                    return;

                case HasItemCondition _:
                case SelectedItemCondition _:
                    Picker(element.FindPropertyRelative("itemId"), m_Items, "道具", IdWidth, "(不要求)");
                    return;

                case PuzzleSolvedCondition _:
                    Picker(element.FindPropertyRelative("puzzleId"), m_Puzzles, "谜题", IdWidth, "(不检查)");
                    return;

                case CharacterInEraCondition _:
                    Picker(element.FindPropertyRelative("characterId"), mCharacterRefs, "人物", IdWidth, "(不限)");
                    EditorGUILayout.LabelField("在", GUILayout.Width(18f));
                    EraRefRow(element, "eraRef", "era", false);
                    return;

                case CharacterCountInEraCondition _:
                    EraRefRow(element, "eraRef", "era", false);
                    EditorGUILayout.LabelField("里的人数", GUILayout.Width(56f));
                    EnumPopup(element.FindPropertyRelative("op"), new[] { "等于", "不等于", "大于", "大于等于", "小于", "小于等于" });
                    Number(element.FindPropertyRelative("count"), 44f);
                    return;

                case SelectedCharacterCondition _:
                    Picker(element.FindPropertyRelative("characterId"), mCharacterRefs, "点名的是", IdWidth, "(任意人)");
                    return;

                case SelectedCharacterInEraCondition _:
                    EditorGUILayout.LabelField("点名的人在", GUILayout.Width(66f));
                    EraRefRow(element, "eraRef", "era", false);
                    return;

                case EraFocusedCondition _:
                    EraPopup(element.FindPropertyRelative("era"));
                    return;

                case AndCondition _:
                case OrCondition _:
                    EditorGUILayout.LabelField("（里面每一条都要满足 —— 见下面缩进的那些）", EditorStyles.miniLabel);
                    return;

                case NotCondition _:
                    EditorGUILayout.LabelField("（把里面那条的结果反过来）", EditorStyles.miniLabel);
                    return;
            }
        }

        private void DrawEffectParams(SerializedProperty element, object value)
        {
            switch (value)
            {
                case SetFlagEffect _:
                    Picker(element.FindPropertyRelative("flag"), m_Flags, "flag", IdWidth, "(新建)");
                    EditorGUILayout.LabelField("=", GUILayout.Width(12f));
                    Number(element.FindPropertyRelative("value"));
                    return;

                case AddFlagEffect _:
                    Picker(element.FindPropertyRelative("flag"), m_Flags, "flag", IdWidth, "(新建)");
                    EditorGUILayout.LabelField("+=", GUILayout.Width(20f));
                    Number(element.FindPropertyRelative("delta"));
                    return;

                case SetObjectStateEffect _:
                    Picker(element.FindPropertyRelative("interactableId"), mObjectRefs, "物体", IdWidth, "(自己)");
                    EditorGUILayout.LabelField("→", GUILayout.Width(14f));
                    Picker(element.FindPropertyRelative("state"), m_States, "状态名", IdWidth, "(lit / cold / open…)");
                    return;

                case GiveItemEffect _:
                    EditorGUILayout.LabelField("给", GUILayout.Width(16f));
                    Picker(element.FindPropertyRelative("itemId"), m_Items, null, IdWidth, "(新建道具)");
                    return;

                case ConsumeItemEffect _:
                    EditorGUILayout.LabelField("拿走", GUILayout.Width(32f));
                    Picker(element.FindPropertyRelative("itemId"), m_Items, null, IdWidth, "(留空?)");
                    return;

                case SelectItemEffect _:
                    EditorGUILayout.LabelField("拿在手里", GUILayout.Width(56f));
                    Picker(element.FindPropertyRelative("itemId"), m_Items, null, IdWidth, "(松手)");
                    return;

                case SolvePuzzleEffect _:
                    Picker(element.FindPropertyRelative("puzzleId"), m_Puzzles, "谜题", IdWidth, "(留空?)");
                    return;

                case MoveCharacterEffect _:
                    Picker(element.FindPropertyRelative("characterId"), mCharacterRefs, "人物", IdWidth, "(留空?)");
                    EditorGUILayout.LabelField("→", GUILayout.Width(14f));
                    EraRefRow(element, "eraRef", "targetEra", true);
                    return;

                case MoveCharacterStepEffect _:
                    Picker(element.FindPropertyRelative("characterId"), mCharacterRefs, "人物", IdWidth, "(留空?)");
                    EnumPopup(element.FindPropertyRelative("step"), new[] { "往前一个时代", "往回一个时代" }, EnumWidth + 40f);
                    return;

                case MoveSelectedCharacterEffect _:
                    EditorGUILayout.LabelField("点名的那个人 →", GUILayout.Width(84f));
                    EraRefRow(element, "eraRef", "targetEra", true);
                    return;

                case MoveEraCharactersEffect _:
                    EraRefRow(element, "fromEraRef", "fromEra", false);
                    EditorGUILayout.LabelField("里的人 →", GUILayout.Width(56f));
                    EraRefRow(element, "eraRef", "targetEra", true);
                    return;

                case SelectCharacterEffect _:
                    Picker(element.FindPropertyRelative("characterId"), mCharacterRefs, "点名", IdWidth, "(取消点名)");
                    return;

                case PlayCharacterAnimationEffect _:
                    EditorGUILayout.LabelField("让", GUILayout.Width(18f));
                    Picker(element.FindPropertyRelative("characterId"), mCharacterRefs, null, IdWidth, "(点名的那个人)");
                    EditorGUILayout.LabelField("播", GUILayout.Width(18f));
                    Picker(element.FindPropertyRelative("clip"), null, null, 90f, "(动画名)");
                    return;

                case FeedbackEffect _:
                    EditorGUILayout.LabelField("说：", GUILayout.Width(28f));
                    Text(element.FindPropertyRelative("message"), 0f);
                    return;

                case DebugLogEffect _:
                    EditorGUILayout.LabelField("日志：", GUILayout.Width(34f));
                    Text(element.FindPropertyRelative("message"), 0f);
                    return;
            }
        }

        // ===================== 控件 =====================

        /// <summary>字符串 + ▾ 从真实存在的候选里挑（策划最容易在这里打错字）。</summary>
        private static void Picker(SerializedProperty property, List<string> candidates, string label, float width, string emptyHint)
        {
            if (property == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(label))
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(label.Length * 13f + 4f));
            }

            property.stringValue = EditorGUILayout.TextField(property.stringValue, GUILayout.Width(width));

            if (!GUILayout.Button("▾", EditorStyles.miniButton, GUILayout.Width(TinyWidth - 4f)))
            {
                return;
            }

            GenericMenu menu = new GenericMenu();

            if (!string.IsNullOrEmpty(emptyHint))
            {
                menu.AddItem(new GUIContent(emptyHint), string.IsNullOrEmpty(property.stringValue), () =>
                {
                    property.stringValue = "";
                    property.serializedObject.ApplyModifiedProperties();
                });
                menu.AddSeparator("");
            }

            if (candidates == null || candidates.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("（还没有候选 —— 先在别处用到它）"));
            }
            else
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    string candidate = candidates[i];
                    if (string.IsNullOrEmpty(candidate))
                    {
                        continue;
                    }

                    menu.AddItem(new GUIContent(RefLabel(candidate)), candidate == property.stringValue, () =>
                    {
                        property.stringValue = candidate;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                }
            }

            menu.ShowAsContext();
        }

        /// <summary>候选菜单里的说法：`@rift` / `@self` 得让人一眼看懂是什么。</summary>
        public static string RefLabel(string value)
        {
            if (PuzzleRef.IsSelf(value))
            {
                return "（被点的那个目标自己）";
            }

            return PuzzleRef.IsCategory(value)
                ? value + "（这一类，一次管一批）"
                : value;
        }

        private static void EraPopup(SerializedProperty property)
        {
            EnumPopup(property, new[] { "石器时代", "蒸汽时代", "电气时代", "信息时代" }, EnumWidth + 24f);
        }

        /// <summary>
        /// 「算哪个时代」：默认是写死的某个时代；选「被点目标所在的时代」之后，
        /// 一条规则就能管所有裂隙（点哪个裂隙，就是哪个时代的事）。效果里还能选"它的下一个"。
        /// </summary>
        private static void EraRefRow(SerializedProperty element, string refField, string eraField, bool allowNext)
        {
            SerializedProperty eraRef = element.FindPropertyRelative(refField);
            if (eraRef == null)
            {
                return;
            }

            string[] names = allowNext
                ? new[] { "指定时代", "被点目标所在时代", "它的下一个时代", "它的上一个时代" }
                : new[] { "指定时代", "被点目标所在时代" };

            int index = Mathf.Clamp(eraRef.intValue, 0, names.Length - 1);
            int picked = EditorGUILayout.Popup(index, names, GUILayout.Width(EnumWidth + 60f));

            if (picked != index)
            {
                eraRef.intValue = picked;
            }

            // 只有"指定时代"才需要那个具体的时代下拉，其它选项都是算出来的
            if (eraRef.intValue == 0)
            {
                EraPopup(element.FindPropertyRelative(eraField));
            }
        }

        private static void EnumPopup(SerializedProperty property, string[] names, float width = 0f)
        {
            if (property == null)
            {
                return;
            }

            int index = Mathf.Clamp(property.intValue, 0, names.Length - 1);
            int picked = width > 0f
                ? EditorGUILayout.Popup(index, names, GUILayout.Width(width))
                : EditorGUILayout.Popup(index, names, GUILayout.Width(EnumWidth));

            if (picked != index)
            {
                property.intValue = picked;
            }
        }

        private static void Number(SerializedProperty property, float width = NumberWidth)
        {
            if (property == null)
            {
                return;
            }

            property.floatValue = EditorGUILayout.FloatField(property.floatValue, GUILayout.Width(width));
        }

        private static void Text(SerializedProperty property, float width)
        {
            if (property == null)
            {
                return;
            }

            if (width > 0f)
            {
                property.stringValue = EditorGUILayout.TextField(property.stringValue, GUILayout.Width(width));
            }
            else
            {
                property.stringValue = EditorGUILayout.TextField(property.stringValue);
            }
        }

        private static void DrawTextRow(string label, SerializedProperty property)
        {
            if (property == null)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(48f));
                property.stringValue = EditorGUILayout.TextField(property.stringValue);
            }
        }

        // ===================== 增删复制 =====================

        // 这里也不用写 Undo.RecordObject：下面每个回调最后都走
        // serializedObject.ApplyModifiedProperties()，而它本身就会登记 Undo。

        private static void AddMenu(SerializedProperty list, Entry[] entries)
        {
            GenericMenu menu = new GenericMenu();

            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                menu.AddItem(new GUIContent(entry.Group + "/" + entry.Name), false, () =>
                {
                    SerializedObject so = list.serializedObject;

                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue = entry.Create();
                    so.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }

        private static void Delete(SerializedProperty list, int index)
        {
            int size = list.arraySize;
            list.DeleteArrayElementAtIndex(index);

            // managed reference 第一次有时只是置空，还得再删一次
            if (list.arraySize == size)
            {
                list.DeleteArrayElementAtIndex(index);
            }
        }

        private static void Duplicate(SerializedProperty list, int index)
        {
            object source = list.GetArrayElementAtIndex(index).managedReferenceValue;
            if (source == null)
            {
                return;
            }

            object copy;

            try
            {
                copy = JsonUtility.FromJson(JsonUtility.ToJson(source), source.GetType());
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[谜题] 复制失败：{exception.Message}");
                return;
            }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue = copy;
        }

        // ===================== 杂项 =====================

        private static string[] Names(Entry[] entries, bool isEffect)
        {
            string[] names = new string[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                names[i] = isEffect ? entries[i].Name : entries[i].Name;
            }

            return names;
        }

        private static int IndexOfType(Entry[] entries, object value)
        {
            if (value == null)
            {
                return 0;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Type == null)
                {
                    continue;
                }

                if (entries[i].Type == value.GetType())
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
