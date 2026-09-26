using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
// 编辑器控件用别名明确指向 UnityEditor.UIElements，避免与运行时同名类型歧义
using ObjectField = UnityEditor.UIElements.ObjectField;
using ColorField = UnityEditor.UIElements.ColorField;

namespace ZF.DialoguePresentation
{
    /// <summary>节点上一个字段用哪种控件</summary>
    public enum NodeFieldKind
    {
        Text,
        Object,
        Enum,

        /// <summary>颜色（ColorField）</summary>
        Color,

        /// <summary>小数（FloatField）</summary>
        Float,

        /// <summary>资产列表（多行 ObjectField + 增删按钮）</summary>
        ObjectList,
    }

    /// <summary>
    /// 「资产列表」控件：一行一个 ObjectField，右边一个 × 删，底部一个 + 添加。
    /// 用来编 `List<DialogueSpeaker>` 这种"一格里有好几个人"的字段。
    /// </summary>
    public sealed class NodeObjectListControl : VisualElement
    {
        private readonly System.Type m_ElementType;
        private readonly List<ObjectField> m_Rows = new List<ObjectField>();
        private readonly VisualElement m_RowContainer;

        public NodeObjectListControl(string label, System.Type elementType)
        {
            m_ElementType = elementType;

            var header = new Label(label);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(header);

            m_RowContainer = new VisualElement();
            Add(m_RowContainer);

            var addButton = new Button(() =>
            {
                AddRow(null);
                NotifyChanged();
            }) { text = "+ 添加" };
            addButton.style.height = 18f;
            addButton.style.marginTop = 2f;
            Add(addButton);
        }

        /// <summary>
        /// 通知编辑器"这个字段变了"（记脏 / 压撤销栈）。
        /// ★ 行里 ObjectField 的变更**不会自动冒到外层控件**，所以这里手动发一个 ChangeEvent；
        ///   节点视图那边是按 `ChangeEvent&lt;Object&gt;` 统一挂回调的，收得到。
        /// </summary>
        private void NotifyChanged()
        {
            using (var evt = ChangeEvent<UnityEngine.Object>.GetPooled(null, null))
            {
                SendEvent(evt);
            }
        }

        private void AddRow(UnityEngine.Object value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var field = new ObjectField { objectType = m_ElementType };
            field.value = value;
            field.style.flexGrow = 1f;
            field.style.marginLeft = 0f;
            field.RegisterValueChangedCallback(_ => NotifyChanged());

            var removeButton = new Button(() =>
            {
                m_Rows.Remove(field);
                row.RemoveFromHierarchy();
                NotifyChanged();
            }) { text = "×" };
            removeButton.style.width = 18f;
            removeButton.style.height = 16f;

            row.Add(field);
            row.Add(removeButton);
            m_RowContainer.Add(row);
            m_Rows.Add(field);
        }

        /// <summary>读成一坨 object（调用方按目标字段类型塞进去）</summary>
        public System.Collections.IList ReadValues()
        {
            var list = (System.Collections.IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(m_ElementType));

            foreach (var field in m_Rows)
            {
                if (field.value != null && m_ElementType.IsInstanceOfType(field.value))
                {
                    list.Add(field.value);
                }
            }

            return list;
        }

        /// <summary>按数据重建行（先清空）</summary>
        public void WriteValues(System.Collections.IList values)
        {
            m_Rows.Clear();
            m_RowContainer.Clear();

            if (values == null)
            {
                return;
            }

            foreach (var value in values)
            {
                AddRow(value as UnityEngine.Object);
            }
        }
    }

    /// <summary>
    /// 台词节点上一个「数据字段」的完整定义：控件怎么建、数据怎么读怎么写、导出对齐到哪个字段名。
    /// </summary>
    public sealed class NodeFieldDef
    {
        /// <summary>节点侧字段名（= DialogueGraphData.NodeData 里的字段名）</summary>
        public string Name;

        /// <summary>运行时侧字段名（空 = 和 Name 同名）；对不上时用这个改名</summary>
        public string RuntimeName;

        public string Label;
        public NodeFieldKind Kind;

        /// <summary>Kind = Object 时的资产类型（拖错类型会被 ObjectField 挡住）</summary>
        public Type ObjectType;

        /// <summary>Kind = Enum 时的默认值</summary>
        public object EnumDefault;

        public bool Multiline;
        public string Tooltip;

        /// <summary>
        /// 节点值 → 运行时值 的转换（类型不一样时用，比如组头的模板 id 节点侧是文本、运行时是 int）。
        /// 参数是导出时的告警收集表（可以往里加"这个值不合法"之类的提示，会显示在导出报告里）。
        /// </summary>
        public Func<object, IList<string>, object> ToTarget;

        /// <summary>导出到运行时数据时用的字段名</summary>
        public string TargetName => string.IsNullOrEmpty(RuntimeName) ? Name : RuntimeName;

        /// <summary>按定义建控件（节点视图和别的地方都用它，保证表里加一条就多一个控件）</summary>
        public VisualElement CreateControl()
        {
            VisualElement control;

            switch (Kind)
            {
                case NodeFieldKind.Object:
                    control = new ObjectField(Label) { objectType = ObjectType };
                    break;

                case NodeFieldKind.Enum:
                    control = new EnumField(Label, (Enum)EnumDefault);
                    break;

                case NodeFieldKind.Color:
                    control = new ColorField(Label);
                    break;

                case NodeFieldKind.Float:
                    control = new FloatField(Label);
                    break;

                case NodeFieldKind.ObjectList:
                    control = new NodeObjectListControl(Label, ObjectType ?? typeof(UnityEngine.Object));
                    break;

                default:
                    var text = new TextField(Label);
                    if (Multiline)
                    {
                        text.multiline = true;
                    }
                    control = text;
                    break;
            }

            if (!string.IsNullOrEmpty(Tooltip))
            {
                control.tooltip = Tooltip;
            }

            return control;
        }

        /// <summary>从控件读出值（string / UnityEngine.Object / Enum）</summary>
        public object Read(VisualElement control)
        {
            if (control == null)
            {
                return null;
            }

            switch (Kind)
            {
                case NodeFieldKind.Object:
                    return ((ObjectField)control).value;

                case NodeFieldKind.Enum:
                    return ((EnumField)control).value;

                case NodeFieldKind.Color:
                    return ((ColorField)control).value;

                case NodeFieldKind.Float:
                    return ((FloatField)control).value;

                case NodeFieldKind.ObjectList:
                    return ((NodeObjectListControl)control).ReadValues();

                default:
                    return ((TextField)control).value;
            }
        }

        /// <summary>把值写进控件（值类型不匹配时忽略并报错，别静默写坏数据）</summary>
        public void Write(VisualElement control, object value)
        {
            if (control == null)
            {
                return;
            }

            switch (Kind)
            {
                case NodeFieldKind.Object:
                    ((ObjectField)control).value = value as UnityEngine.Object;
                    break;

                case NodeFieldKind.Enum:
                    ((EnumField)control).value = value as Enum ?? (Enum)EnumDefault;
                    break;

                case NodeFieldKind.Color:
                    ((ColorField)control).value = value is Color color ? color : Color.clear;
                    break;

                case NodeFieldKind.Float:
                    ((FloatField)control).value = value is float number ? number : 0f;
                    break;

                case NodeFieldKind.ObjectList:
                    ((NodeObjectListControl)control).WriteValues(value as System.Collections.IList);
                    break;

                default:
                    ((TextField)control).value = value as string ?? string.Empty;
                    break;
            }
        }
    }

    /// <summary>
    /// 台词节点的字段表 —— **加一个台词字段只改这里**（再改一下运行时数据类 DialogueShowTextItem）。
    ///
    /// 为什么要有这张表：以前加一个字段（比如"这句要不要摇一摇"）要手改 4 个地方 ——
    /// 节点视图的控件声明 / 控件创建 / 事件注册 / 节点存数据 / 节点读数据 / 导出到运行时数据，
    /// 漏一处就是"编辑器里填了、运行时没有"这种最难查的 bug。
    /// 现在：控件建出来、节点 ⇄ SO 数据、导出 ⇄ 运行时数据，全部按这张表的 Name 自动对齐，
    /// 对不上会在导出时直接报错（而不是悄悄丢掉）。
    /// </summary>
    public static class DialogueNodeFields
    {
        /// <summary>台词节点上的数据字段（顺序 = 界面上从上到下的顺序）</summary>
        public static readonly NodeFieldDef[] Line =
        {
            new NodeFieldDef
            {
                Name = "speaker",
                Label = "角色",
                Kind = NodeFieldKind.Object,
                ObjectType = typeof(DialogueSpeaker),
                Tooltip = "空 = 旁白（旁白会优先用没有立绘框的槽）",
            },

            new NodeFieldDef
            {
                Name = "text",
                Label = "台词",
                Kind = NodeFieldKind.Text,
                Multiline = true,
            },

            new NodeFieldDef
            {
                Name = "expression",
                Label = "表情",
                Kind = NodeFieldKind.Enum,
                EnumDefault = DialogueCharExpressionEnum.defaultFace,
                Tooltip = "没配的表情会自动回退到「平静」",
            },

            new NodeFieldDef
            {
                Name = "audio",
                RuntimeName = "audioEff",     // 运行时那边叫 audioEff
                Label = "音效",
                Kind = NodeFieldKind.Object,
                ObjectType = typeof(ShowAudioEventSO),
                Tooltip = "这句台词播的音效（拖 ShowAudioEventSO 资产）",
            },

            new NodeFieldDef
            {
                Name = "bubbleStyle",
                Label = "气泡样式",
                Kind = NodeFieldKind.Object,
                ObjectType = typeof(BubbleStyleSO),
                Tooltip = "这一句用哪套气泡外观/动画。空 = 用气泡实例上的默认样式",
            },

            new NodeFieldDef
            {
                Name = "slotId",
                Label = "槽 id",
                Kind = NodeFieldKind.Text,
                Tooltip = "空 = 自动分配（本格内这个角色第一次说话时领一个空槽，之后固定用它）。\n" +
                          "填了就固定用这个槽（模板里槽的 slotId 要同名），" +
                          "比如想让这句一定在右边说，填「右」；想让他冒出第二个位置也用这个字段",
            },
        };

        private static readonly Dictionary<string, FieldInfo> s_NodeFields = new Dictionary<string, FieldInfo>();
        private static readonly Dictionary<string, FieldInfo> s_ItemFields = new Dictionary<string, FieldInfo>();
        private static readonly Dictionary<string, FieldInfo> s_GroupFields = new Dictionary<string, FieldInfo>();
        private static bool s_Validated;

        /// <summary>组头节点上的固定字段（选项行是动态的，不走表）</summary>
        public static readonly NodeFieldDef[] Group =
        {
            new NodeFieldDef
            {
                Name = "templateId",
                Label = "分镜模板",
                Kind = NodeFieldKind.Text,
                Tooltip = "分镜模板 id（整数），对应表现层的构图模板；空 = 0",
                ToTarget = (value, warnings) =>
                {
                    string raw = value as string;
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        return 0;
                    }

                    if (int.TryParse(raw.Trim(), out int id))
                    {
                        return id;
                    }

                    warnings?.Add($"分镜模板「{raw}」不是整数，按 0 处理");
                    return 0;
                },
            },

            new NodeFieldDef
            {
                Name = "audio",
                RuntimeName = "enterAudio",     // 运行时那边叫 enterAudio（进这一格的音效）
                Label = "组音效",
                Kind = NodeFieldKind.Object,
                ObjectType = typeof(ShowAudioEventSO),
                Tooltip = "进这一格时播的音效",
            },

            new NodeFieldDef
            {
                Name = "transitionId",
                Label = "离场动画 id",
                Kind = NodeFieldKind.Text,
                Tooltip = "这一格**离场**（演完换到下一格）时播什么：填场景里 DialogueTransitionLibrary 登记表里的 id，\n" +
                          "比如 wipe（推过去擦）/ bars_in（条纹刷入）/ manga_dissolve（漫画网点擦除）/ instant（直接切）。\n" +
                          "★ 字段挂在**本格**上、**离开本格那一刻**播 —— 所以「从 A 擦到 B」用的是 A（上一格）这个值。\n" +
                          "★ 空 = 本格离场时直接切，不做动画；第一格开场不播动画",
            },

            new NodeFieldDef
            {
                Name = "transitionDirection",
                Label = "离场方向",
                Kind = NodeFieldKind.Enum,
                EnumDefault = WipeDirection.ComponentDefault,
                Tooltip = "同一套擦除组件可以服务所有方向：这里选方向就不用在场景里配第二套组件了。\n" +
                          "（用组件上的方向）= 不覆盖，用擦除组件自己配的",
            },

            new NodeFieldDef
            {
                Name = "transitionColor",
                Label = "离场颜色",
                Kind = NodeFieldKind.Color,
                Tooltip = "覆盖擦除组件的纸色 / 墨边色（不同格子不同颜色，也不用配第二套组件）。\n" +
                          "★ Alpha = 0 视为没填 = 用组件上的颜色",
            },

            new NodeFieldDef
            {
                Name = "transitionDuration",
                Label = "离场动画时长",
                Kind = NodeFieldKind.Float,
                Tooltip = "覆盖擦除组件上配的时长（秒）。\n" +
                          "★ 0 = 没填 = 用组件上的时长。\n" +
                          "条纹那种擦除，这里指**每一条刷进/刷出去的时长**",
            },

            new NodeFieldDef
            {
                Name = "groupBubbleStyle",
                RuntimeName = "bubbleStyle",        // 运行时那边叫 bubbleStyle（台词节点上的同名那个是"这一句"的）
                Label = "本格默认气泡样式",
                Kind = NodeFieldKind.Object,
                ObjectType = typeof(BubbleStyleSO),
                Tooltip = "整格换画风用：这格里没单独填气泡样式的台词都用它。\n" +
                          "空 = 用气泡实例上的默认样式",
            },

            new NodeFieldDef
            {
                Name = "cast",
                Label = "本格出场角色",
                Kind = NodeFieldKind.ObjectList,
                ObjectType = typeof(DialogueSpeaker),
                Tooltip = "**不在这格说话的也算**：换格时他们就会先站到场上（不用等开口才出现）。\n" +
                          "顺序靠前的先拿槽位（想固定谁站哪边，去填槽的 slotId 或台词上的槽 id）",
            },
        };

        private static NodeFieldDef FindIn(NodeFieldDef[] table, string name)
        {
            foreach (var def in table)
            {
                if (def.Name == name)
                {
                    return def;
                }
            }

            return null;
        }

        /// <summary>按字段名找台词节点字段的定义</summary>
        public static NodeFieldDef Find(string name)
        {
            return FindIn(Line, name);
        }

        /// <summary>按字段名找组头节点字段的定义</summary>
        public static NodeFieldDef FindGroup(string name)
        {
            return FindIn(Group, name);
        }

        /// <summary>把节点上的值写进 SO 节点数据（只碰表里的字段，guid / 位置之类不动）</summary>
        public static void ApplyToNodeData(DialogueNodeView node, DialogueGraphData.NodeData data)
        {
            EnsureValidated();

            foreach (var def in Line)
            {
                var field = GetNodeField(def);
                if (field != null)
                {
                    field.SetValue(data, node.ReadField(def.Name));
                }
            }
        }

        /// <summary>把 SO 节点数据读回节点控件</summary>
        public static void ApplyFromNodeData(DialogueNodeView node, DialogueGraphData.NodeData data)
        {
            EnsureValidated();

            foreach (var def in Line)
            {
                var field = GetNodeField(def);
                node.WriteField(def.Name, field != null ? field.GetValue(data) : null);
            }
        }

        /// <summary>把节点上的值写进运行时数据（导出用）</summary>
        public static void ApplyToShowItem(DialogueNodeView node, DialogueShowTextItem item, List<string> warnings = null)
        {
            EnsureValidated();

            foreach (var def in Line)
            {
                var field = GetItemField(def);
                if (field != null)
                {
                    field.SetValue(item, ConvertToTarget(def, node.ReadField(def.Name), warnings));
                }
            }
        }

        // ===================== 组头节点 =====================

        /// <summary>把组头上的值写进 SO 节点数据（选项文本另行处理）</summary>
        public static void ApplyToNodeData(GroupNodeView node, DialogueGraphData.NodeData data)
        {
            EnsureValidated();

            foreach (var def in Group)
            {
                var field = GetNodeField(def);
                if (field != null)
                {
                    field.SetValue(data, node.ReadField(def.Name));
                }
            }
        }

        /// <summary>把 SO 节点数据读回组头控件</summary>
        public static void ApplyFromNodeData(GroupNodeView node, DialogueGraphData.NodeData data)
        {
            EnsureValidated();

            foreach (var def in Group)
            {
                var field = GetNodeField(def);
                node.WriteField(def.Name, field != null ? field.GetValue(data) : null);
            }
        }

        /// <summary>把组头上的值写进运行时组数据（导出用；groupId / 出口不在这里）</summary>
        public static void ApplyToGroupData(GroupNodeView node, DialogueShowGroupData group, List<string> warnings = null)
        {
            EnsureValidated();

            foreach (var def in Group)
            {
                var field = GetGroupField(def);
                if (field != null)
                {
                    field.SetValue(group, ConvertToTarget(def, node.ReadField(def.Name), warnings));
                }
            }
        }

        private static object ConvertToTarget(NodeFieldDef def, object value, List<string> warnings)
        {
            return def.ToTarget != null ? def.ToTarget(value, warnings) : value;
        }

        /// <summary>
        /// ★ 加字段时最有用的一步：表里的字段必须**两边都存在**。
        /// 少一边就报错说清楚少在哪边，而不是导出后运行时莫名少一份数据。
        /// </summary>
        public static void EnsureValidated()
        {
            if (s_Validated)
            {
                return;
            }

            s_Validated = true;
            var nodeType = typeof(DialogueGraphData.NodeData);
            var itemType = typeof(DialogueShowTextItem);
            var groupType = typeof(DialogueShowGroupData);

            foreach (var def in Line)
            {
                ValidateNodeSide(nodeType, def, "DialogueNodeFields.Line 和 DialogueShowTextItem");
            }

            foreach (var def in Group)
            {
                ValidateNodeSide(nodeType, def, "DialogueNodeFields.Group 和 DialogueShowGroupData");
            }

            foreach (var def in Line)
            {
                var target = itemType.GetField(def.TargetName, BindingFlags.Public | BindingFlags.Instance);
                if (target == null)
                {
                    Debug.LogError($"[对话图] 字段表里的「{def.Name}」在运行时数据 DialogueShowTextItem 里找不到" +
                                   $"（期望叫「{def.TargetName}」，可用 RuntimeName 改名）");
                }
            }

            foreach (var def in Group)
            {
                var target = groupType.GetField(def.TargetName, BindingFlags.Public | BindingFlags.Instance);
                if (target == null)
                {
                    Debug.LogError($"[对话图] 组头字段「{def.Name}」在运行时数据 DialogueShowGroupData 里找不到" +
                                   $"（期望叫「{def.TargetName}」，可用 RuntimeName 改名）");
                }
            }
        }

        private static void ValidateNodeSide(Type nodeType, NodeFieldDef def, string hint)
        {
            var nodeField = nodeType.GetField(def.Name, BindingFlags.Public | BindingFlags.Instance);
            if (nodeField == null)
            {
                Debug.LogError($"[对话图] 字段表里的「{def.Name}」在 DialogueGraphData.NodeData 里不存在，" +
                               $"加字段请同时改这两处：{hint}");
                return;
            }

            var controlType = ControlValueType(def);
            if (!nodeField.FieldType.IsAssignableFrom(controlType) &&
                !controlType.IsAssignableFrom(nodeField.FieldType))
            {
                Debug.LogError($"[对话图] 字段「{def.Name}」类型不匹配：" +
                               $"节点数据是 {nodeField.FieldType.Name}，控件给的是 {controlType.Name}");
            }
        }

        private static Type ControlValueType(NodeFieldDef def)
        {
            switch (def.Kind)
            {
                case NodeFieldKind.Object:
                    return def.ObjectType ?? typeof(UnityEngine.Object);

                case NodeFieldKind.Enum:
                    return def.EnumDefault != null ? def.EnumDefault.GetType() : typeof(Enum);

                case NodeFieldKind.Color:
                    return typeof(Color);

                case NodeFieldKind.Float:
                    return typeof(float);

                case NodeFieldKind.ObjectList:
                    return typeof(List<>).MakeGenericType(def.ObjectType ?? typeof(UnityEngine.Object));

                default:
                    return typeof(string);
            }
        }

        private static FieldInfo GetNodeField(NodeFieldDef def)
        {
            if (s_NodeFields.TryGetValue(def.Name, out var cached))
            {
                return cached;
            }

            var field = typeof(DialogueGraphData.NodeData)
                .GetField(def.Name, BindingFlags.Public | BindingFlags.Instance);
            s_NodeFields[def.Name] = field;
            return field;
        }

        private static FieldInfo GetItemField(NodeFieldDef def)
        {
            if (s_ItemFields.TryGetValue(def.TargetName, out var cached))
            {
                return cached;
            }

            var field = typeof(DialogueShowTextItem)
                .GetField(def.TargetName, BindingFlags.Public | BindingFlags.Instance);
            s_ItemFields[def.TargetName] = field;
            return field;
        }

        private static FieldInfo GetGroupField(NodeFieldDef def)
        {
            if (s_GroupFields.TryGetValue(def.TargetName, out var cached))
            {
                return cached;
            }

            var field = typeof(DialogueShowGroupData)
                .GetField(def.TargetName, BindingFlags.Public | BindingFlags.Instance);
            s_GroupFields[def.TargetName] = field;
            return field;
        }
    }
}
