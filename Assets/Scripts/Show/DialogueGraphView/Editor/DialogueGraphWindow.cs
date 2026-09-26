using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;
// ObjectField 是编辑器控件，用别名明确指向 UnityEditor.UIElements，避免与运行时同名类型歧义
using ObjectField = UnityEditor.UIElements.ObjectField;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 对话节点编辑器（窗口）
    /// 打开：菜单 Tools → 对话节点编辑器；或者在 Project 里双击一个 DialogueGraphData 资产
    ///
    /// 用法：
    ///   1. 顶部「图数据」指定一个 DialogueGraphData 资产（没有就先 右键 Create → DialogueGraph → Graph Data）
    ///   2. 第二行建「分组框 / 组头节点 / 对话 item」，把组头和台词拖进同一个框里 = 归为一组
    ///   3. 连线：组头 out → 组内第一句 → … → 组内最后一句
    ///           组头 nextGroup → 下一组的 in（线性继续）
    ///           组头点「+ 选项」→ 每个选项一个端口 → 目标组的 in（分叉）
    ///   4. 「检查」看校验结果和导出预览；「导出章节」写进 DialogueChapterSO（运行时读的就是它）
    ///
    /// 快捷键：Ctrl+S 保存 / Ctrl+E 检查 / Ctrl+Shift+E 导出 / Ctrl+Z 撤销 / Ctrl+Y 重做
    ///        Ctrl+G 选中节点装进新框 / Ctrl+D 复制选中 / Delete 删除选中
    /// </summary>
    public class DialogueGraphWindow : EditorWindow
    {
        private DialogueGraphView m_View;
        private ObjectField m_DataField;
        private ObjectField m_ChapterField;
        private TextField m_StartGroupField;
        private Button m_SaveButton;
        private Label m_HintLabel;

        private VisualElement m_ReportPanel;
        private ScrollView m_ReportScroll;
        private Label m_ReportTitle;
        private bool m_ReportExpanded = true;

        /// <summary>双击资产打开时 CreateGUI 还没跑，先存这里</summary>
        private DialogueGraphData m_PendingData;

        private bool m_LastDirty;

        [MenuItem("Tools/对话节点编辑器")]
        public static DialogueGraphWindow Open()
        {
            return OpenWith(null);
        }

        /// <summary>打开窗口（可选：直接载入某份图数据）</summary>
        public static DialogueGraphWindow OpenWith(DialogueGraphData data)
        {
            var window = GetWindow<DialogueGraphWindow>();
            window.titleContent = new GUIContent("对话节点编辑器");
            window.minSize = new Vector2(1020f, 640f);
            window.m_PendingData = data;
            window.Show();
            window.Focus();
            return window;
        }

        // Project 里双击 DialogueGraphData 资产 → 直接开编辑器
        [OnOpenAsset(0)]
        private static bool OnOpenGraphDataAsset(int instanceId, int line)
        {
            var data = EditorUtility.InstanceIDToObject(instanceId) as DialogueGraphData;
            if (data == null)
            {
                return false;
            }

            OpenWith(data);
            return true;
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            m_View?.DisposeSnapshotBuffer();
        }

        // UI Toolkit 窗口的官方入口（比 OnEnable 更可靠）
        private void CreateGUI()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            // ===================== 第一行：文件 / 校验 / 导出 =====================
            var barTop = MakeBar();

            m_DataField = new ObjectField("图数据")
            {
                objectType = typeof(DialogueGraphData),
            };
            m_DataField.style.width = 300f;
            m_DataField.RegisterValueChangedCallback(evt =>
            {
                var data = evt.newValue as DialogueGraphData;
                m_View?.LoadFromData(data);

                if (data != null)
                {
                    FrameAllAfterLoad();      // 图坐标可能离原点很远，不框住会以为"某个组没读进来"
                }

                if (m_StartGroupField != null)
                {
                    m_StartGroupField.SetValueWithoutNotify(
                        data != null && data.startGroupId != null ? data.startGroupId : string.Empty);
                }

                RefreshReportPanel();
            });
            barTop.Add(m_DataField);

            m_SaveButton = MakeButton("保存", Save);
            barTop.Add(m_SaveButton);
            barTop.Add(MakeButton("重新加载", Reload));
            barTop.Add(MakeButton("撤销", () => { m_View.Undo(); RefreshReportPanel(); }));
            barTop.Add(MakeButton("重做", () => { m_View.Redo(); RefreshReportPanel(); }));

            m_StartGroupField = new TextField("起始组");
            m_StartGroupField.style.width = 180f;
            m_StartGroupField.tooltip = "留空 = 自动推断（没有任何外来线连进来的那个组）。填了就按填的来，会写进图数据";
            m_StartGroupField.RegisterValueChangedCallback(evt =>
            {
                if (m_View != null && m_View.Data != null)
                {
                    m_View.Data.startGroupId = evt.newValue;
                    EditorUtility.SetDirty(m_View.Data);
                }
            });
            barTop.Add(m_StartGroupField);

            m_ChapterField = new ObjectField("输出章节")
            {
                objectType = typeof(DialogueChapterSO),
            };
            m_ChapterField.style.width = 250f;
            barTop.Add(m_ChapterField);

            barTop.Add(MakeButton("检查", Check));
            barTop.Add(MakeButton("导出章节", ExportChapter));

            rootVisualElement.Add(barTop);

            // ===================== 第二行：编辑 =====================
            var barEdit = MakeBar();
            barEdit.Add(MakeButton("新建 分组框", () => m_View.CreateGroupBox(m_View.GetSpawnPosition())));
            barEdit.Add(MakeButton("新建 组头节点", () => m_View.CreateGroupNode(m_View.GetSpawnPosition())));
            barEdit.Add(MakeButton("新建 对话 item", () => m_View.CreateDialogueNode(m_View.GetSpawnPosition())));
            barEdit.Add(MakeButton("选中装新框 Ctrl+G", GroupSelection));
            barEdit.Add(MakeButton("自动串联这一组", AutoChain));

            m_HintLabel = new Label();
            m_HintLabel.style.marginLeft = 12f;
            m_HintLabel.style.color = new Color(0.72f, 0.72f, 0.72f);
            barEdit.Add(m_HintLabel);
            rootVisualElement.Add(barEdit);

            // ===================== 图 =====================
            // 注意：用 flexGrow 占满剩余空间，不要用 StretchToParentSize()
            // （后者是绝对定位，会盖住工具栏和下面的面板）
            m_View = new DialogueGraphView();
            m_View.style.flexGrow = 1f;
            rootVisualElement.Add(m_View);

            // ===================== 底部：检查结果 / 导出预览 =====================
            rootVisualElement.Add(BuildReportPanel());

            // 快捷键
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            // 让图拿到焦点，不然刚打开窗口时 Ctrl+S / Ctrl+E 不生效
            rootVisualElement.schedule.Execute(() =>
            {
                if (m_View != null)
                {
                    m_View.Focus();
                }
            }).ExecuteLater(60);

            // 双击资产进来的：这时候才轮到把数据塞进去（赋值会触发上面的回调去加载）
            if (m_PendingData != null)
            {
                m_DataField.value = m_PendingData;
                m_PendingData = null;
            }

            RefreshReportPanel();
        }

        private static VisualElement MakeBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.height = 26f;
            bar.style.flexShrink = 0f;
            bar.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            bar.style.paddingLeft = 6f;
            bar.style.paddingRight = 6f;
            return bar;
        }

        private static Button MakeButton(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.marginLeft = 6f;
            button.style.height = 20f;
            return button;
        }

        // ===================== 底部面板 =====================

        private VisualElement BuildReportPanel()
        {
            m_ReportPanel = new VisualElement();
            m_ReportPanel.style.flexShrink = 0f;
            m_ReportPanel.style.borderTopWidth = 1f;
            m_ReportPanel.style.borderTopColor = new Color(0.13f, 0.13f, 0.13f);
            m_ReportPanel.style.backgroundColor = new Color(0.19f, 0.19f, 0.19f);

            m_ReportTitle = new Label();
            m_ReportTitle.style.paddingLeft = 8f;
            m_ReportTitle.style.paddingTop = 4f;
            m_ReportTitle.style.paddingBottom = 4f;
            m_ReportTitle.tooltip = "点这行展开 / 收起";
            m_ReportTitle.RegisterCallback<MouseDownEvent>(_ => ToggleReport());
            m_ReportPanel.Add(m_ReportTitle);

            m_ReportScroll = new ScrollView();
            m_ReportScroll.style.height = 132f;
            m_ReportPanel.Add(m_ReportScroll);

            return m_ReportPanel;
        }

        private void ToggleReport()
        {
            m_ReportExpanded = !m_ReportExpanded;

            if (m_ReportScroll != null)
            {
                m_ReportScroll.style.display = m_ReportExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void RefreshReportPanel()
        {
            if (m_ReportScroll == null || m_ReportTitle == null)
            {
                return;
            }

            m_ReportScroll.Clear();

            var report = m_View != null ? m_View.LastReport : null;

            if (report == null)
            {
                m_ReportTitle.text = "检查结果：（还没检查）点「检查」或者按 Ctrl+E";
                return;
            }

            m_ReportTitle.text = report.HasErrors
                ? $"检查结果：✗ {report.ErrorCount} 个错误，{report.WarningCount} 个警告"
                : $"检查结果：✓ 通过｜{report.Groups.Count} 个组，{report.TotalChoiceCount} 个选项，" +
                  $"起始组「{report.StartGroupId}」" +
                  (report.WarningCount > 0 ? $"｜{report.WarningCount} 个警告" : string.Empty);

            foreach (var issue in report.Issues)
            {
                var label = MakeReportRow(issue.ToString(),
                    issue.IsError ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 0.85f, 0.4f));

                if (!string.IsNullOrEmpty(issue.NodeGuid))
                {
                    string guid = issue.NodeGuid;
                    label.tooltip = "点一下跳到出问题的地方";
                    label.RegisterCallback<MouseDownEvent>(_ => m_View.FocusNode(guid));
                }

                m_ReportScroll.Add(label);
            }

            if (report.HasErrors || report.Groups.Count == 0)
            {
                return;
            }

            m_ReportScroll.Add(MakeReportRow("—— 导出预览（按执行顺序）——", new Color(0.62f, 0.62f, 0.62f)));

            foreach (var group in report.Groups)
            {
                string line = (group.IsStart ? "★ " : "     ") +
                              $"{group.GroupId}｜{group.TextCount} 句｜「{group.FirstLine}」";

                if (group.ChoiceLines.Count > 0)
                {
                    line += "｜选项：" + string.Join(" / ", group.ChoiceLines);
                }
                else if (!string.IsNullOrEmpty(group.NextGroupId))
                {
                    line += $"｜下一组：{group.NextGroupId}";
                }
                else
                {
                    line += "｜本章结束";
                }

                var row = MakeReportRow(line,
                    group.IsStart ? new Color(0.55f, 1f, 0.55f) : new Color(0.85f, 0.85f, 0.85f));

                if (!string.IsNullOrEmpty(group.HeaderGuid))
                {
                    string guid = group.HeaderGuid;
                    row.tooltip = "点一下跳到这个组";
                    row.RegisterCallback<MouseDownEvent>(_ => m_View.FocusNode(guid));
                }

                m_ReportScroll.Add(row);
            }
        }

        private static Label MakeReportRow(string text, Color color)
        {
            var label = new Label(text);
            label.style.paddingLeft = 10f;
            label.style.color = color;
            return label;
        }

        // ===================== 操作 =====================

        private void Save()
        {
            var data = m_DataField.value as DialogueGraphData;
            if (data == null)
            {
                Warn("请先在顶部指定一个 DialogueGraphData 资产。");
                return;
            }

            m_View.SaveToData(data);
            Hint($"已保存：{data.name}");
        }

        private void Reload()
        {
            var data = m_DataField.value as DialogueGraphData;
            if (data == null)
            {
                Warn("请先指定图数据资产。");
                return;
            }

            if (m_View.IsDirty &&
                !EditorUtility.DisplayDialog("重新加载", "当前有未保存的改动，重新加载会丢掉。继续？", "重载", "取消"))
            {
                return;
            }

            m_View.LoadFromData(data);
            m_StartGroupField.SetValueWithoutNotify(data.startGroupId != null ? data.startGroupId : string.Empty);
            RefreshReportPanel();
            FrameAllAfterLoad();
            Hint($"已重新加载：{data.name}");
        }

        /// <summary>
        /// 载入后把视图框到全部内容上。
        /// ★ 图里的坐标可能是很大的负数（这张图在 -3700 一带），视口默认停在原点附近，
        ///   打开就只看得见原点旁边的那一组，很容易误以为"某个组根本没读进来"。
        ///   要等布局算完才框得准，所以延迟一小会儿再调。
        /// </summary>
        private void FrameAllAfterLoad()
        {
            if (m_View == null)
            {
                return;
            }

            m_View.schedule.Execute(() =>
            {
                if (m_View != null)
                {
                    m_View.FrameAll();
                }
            }).ExecuteLater(80);
        }

        /// <summary>只校验 + 出预览，绝不写资产</summary>
        private void Check()
        {
            if (m_DataField.value as DialogueGraphData == null)
            {
                Warn("请先指定图数据资产。");
                return;
            }

            m_View.ExportToChapter(null, true);

            var report = m_View.LastReport;
            m_View.MarkStartGroup(report != null ? report.StartGroupId : null);
            RefreshReportPanel();
        }

        private void ExportChapter()
        {
            var data = m_DataField.value as DialogueGraphData;
            var chapter = m_ChapterField.value as DialogueChapterSO;

            if (data == null)
            {
                Warn("请先在顶部指定一个 DialogueGraphData 资产。");
                return;
            }

            if (chapter == null)
            {
                Warn("请指定要写入的 DialogueChapterSO（右键 Create → Dialogue → DialogueChapter）。");
                return;
            }

            if (!EditorUtility.DisplayDialog("导出确认",
                    $"会把图里的组覆盖写入：{chapter.name}\n\n（chapterId 保留；startGroupId 和 groups 全部重建）",
                    "导出", "取消"))
            {
                return;
            }

            m_View.SaveToData(data);       // 先把图存了，免得图和导出结果不一致
            var report = m_View.ExportToChapter(chapter);

            if (report != null && report.IsSuccess)
            {
                m_View.MarkStartGroup(report.StartGroupId);
                Hint($"已写入 {chapter.name}");
            }

            RefreshReportPanel();
        }

        private void GroupSelection()
        {
            if (m_View.GroupSelectionIntoNewBox(out string message))
            {
                RefreshReportPanel();
                Hint(message);
            }
            else
            {
                Warn(message);
            }
        }

        private void AutoChain()
        {
            if (m_View.AutoChainGroup(m_View.GetSelectedGroupHeader(), out string message))
            {
                RefreshReportPanel();
                Hint(message);
            }
            else
            {
                Warn(message);
            }
        }

        private void Hint(string message)
        {
            if (m_HintLabel != null)
            {
                m_HintLabel.text = message;
            }
        }

        private void Warn(string message)
        {
            EditorUtility.DisplayDialog("提示", message, "好");
        }

        // ===================== 快捷键 =====================

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!evt.ctrlKey && !evt.commandKey)
            {
                return;
            }

            // 正在输入框里打字（比如写台词）：不抢快捷键，Ctrl+Z 交给输入框自己
            if (IsTextFieldFocused(evt.target as VisualElement))
            {
                return;
            }

            switch (evt.keyCode)
            {
                case KeyCode.S:
                    Save();
                    break;
                case KeyCode.E:
                    if (evt.shiftKey)
                    {
                        ExportChapter();
                    }
                    else
                    {
                        Check();
                    }
                    break;
                case KeyCode.Z:
                    if (evt.shiftKey)
                    {
                        m_View.Redo();
                    }
                    else
                    {
                        m_View.Undo();
                    }
                    RefreshReportPanel();
                    break;
                case KeyCode.Y:
                    m_View.Redo();
                    RefreshReportPanel();
                    break;
                case KeyCode.G:
                    GroupSelection();
                    break;
                case KeyCode.D:
                    m_View.DuplicateSelection();
                    RefreshReportPanel();
                    break;
                default:
                    return;
            }

            evt.StopPropagation();
            evt.PreventDefault();
        }

        private static bool IsTextFieldFocused(VisualElement target)
        {
            if (target == null)
            {
                return false;
            }

            if (target is TextField)
            {
                return true;
            }

            return target.GetFirstAncestorOfType<TextField>() != null;
        }

        // ===================== 脏标记 =====================

        private void OnEditorUpdate()
        {
            if (m_View == null)
            {
                return;
            }

            bool dirty = m_View.IsDirty;
            if (dirty == m_LastDirty)
            {
                return;
            }

            m_LastDirty = dirty;

            if (m_SaveButton != null)
            {
                m_SaveButton.text = dirty ? "保存 *" : "保存";
            }

            // 关窗口时 Unity 会问"要不要保存"（走 SaveChanges / DiscardChanges）
            hasUnsavedChanges = dirty;
            saveChangesMessage = "对话图有未保存的改动，要先保存吗？";
        }

        public override void SaveChanges()
        {
            Save();
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            base.DiscardChanges();
        }
    }
}
