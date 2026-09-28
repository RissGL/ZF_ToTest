using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZGameFramework;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 对话表现层：把 Model 的四个 BindableProperty 翻译成「换格 + 气泡 + 立绘 + 选项」。
    ///
    /// ★ 这个类只做四件事（别的都给出去了）：
    ///   ① 接线：布局 / 舞台 / 选项面板 / 转场 / 模板来源，串联 Model 的四个事件
    ///   ② 把「换格的时序」交给 <see cref="DialogueGroupSequencer"/>（时间线在那边）
    ///   ③ 把「玩家按一下算什么」交给 <see cref="DialogueInputRouter"/>（节奏在那边）
    ///   ④ 真正"演一句"：领槽 → 弹气泡 → 高亮说话人 → 播台词音效
    ///   角色 / 槽 / 框 / 立绘的语义在 <see cref="SlotDirector"/> 里 —— 表现层不认识角色。
    ///
    /// 换一格的完整编排见 <see cref="DialogueGroupSequencer"/>；
    /// 输入节奏见 <see cref="DialogueInputRouter"/>。
    ///
    /// ★ 它还兼任 <see cref="IDialogueFrameSource"/>：溶解类擦除要"旧画面由哪些部件组成"，
    ///   以及擦完放掉旧画面 —— 这部分知识和布局有关，放在这里最合适。
    /// </summary>
    public class DialogueView : MonoBehaviour, IController, IDialogueFrameSource
    {
        [Header("引用")]
        [Label("布局模板引用（场景里那一份；用 Host 换模板时会被自动替换）")]
        [SerializeField] private DialogueLayoutRefs layout;

        [Label("气泡舞台（没挂 layout 时手填）")]
        [SerializeField] private BubbleStage bubbleStage;

        [Label("选项面板（没挂 layout 时手填）")]
        [SerializeField] private DialogueChoicePanel choicePanel;

        [Label("模板 Host（按 templateId 换模板；空 = 只用场景里那一份布局）")]
        [SerializeField] private DialogueTemplateHost templateHost;

        [Label("默认离场动画（只在组头填的 id 找不到时兜底；组头留空 = 离场时直接切）")]
        [SerializeField] private MonoBehaviour transitionSource;

        [Label("离场动画登记表（组头的「离场动画 id」→ 具体实现）")]
        [SerializeField] private DialogueTransitionLibrary transitionLibrary;

        [Label("继续按钮（可空，比如全屏的点击接收按钮）")]
        [SerializeField] private Button continueButton;

        [Header("行为")]
        [Label("每次换组都播转场（同一个模板也擦）")]
        [SerializeField] private bool transitionOnEveryGroup = true;

        [Label("非说话人的立绘变暗（说话人高亮）")]
        [SerializeField] private bool dimOtherPortraits = true;

        [Header("输入")]
        [Label("继续键")]
        [SerializeField] private KeyCode continueKey = KeyCode.Space;

        [Label("鼠标左键也能继续")]
        [SerializeField] private bool clickToContinue = true;

        [Header("Auto（自动播放，按键 A 可切换）")]
        [Label("开场就打开 Auto")]
        [SerializeField] private bool autoPlay;

        [Label("Auto 停留时长（秒）—— 一句读完后等多久自动推进")]
        [SerializeField] private float autoDelay = 1.6f;

        [Label("切换 Auto 的快捷键（None = 不绑键）")]
        [SerializeField] private KeyCode autoKey = KeyCode.A;

        private IDialogueShowModel m_Model;
        private IDialogueTransition m_DefaultTransition;
        private IDialogueTemplateProvider m_TemplateProvider;
        private DialogueShowTextItem m_LastItem;
        private bool m_Ended;

        private DialogueGroupSequencer m_Sequencer;
        private DialogueInputRouter m_Input;

        /// <summary>溶解类擦除要求"保留旧画面"（换模板时别隐藏旧实例）；见 <see cref="IDialogueTransitionKeepsOldFrame"/></summary>
        private bool m_HoldPreviousFrame;

        /// <summary>现在正在演的是哪一格（换格时用它当"要离场的那一格"）</summary>
        private DialogueShowGroupData m_ShownGroup;

        /// <summary>换格开始时的那个气泡舞台（= 旧画面）。溶解类擦完要单独收它，那时 bubbleStage 已经指向新舞台了</summary>
        private BubbleStage m_PreviousStage;

        /// <summary>换格开始时的那个布局（= 旧画面；溶解类要拿它的部件清单）</summary>
        private DialogueLayoutRefs m_PreviousLayout;

        /// <summary>当前布局里所有可绘制部件的缓存（按布局对象缓存，换布局就失效）</summary>
        private readonly List<Graphic> m_GraphicsCache = new List<Graphic>();
        private readonly List<Graphic> m_EmptyGraphics = new List<Graphic>();
        private DialogueLayoutRefs m_GraphicsCacheSource;

        /// <summary>当前的气泡舞台（换模板时会被替换）</summary>
        public BubbleStage Stage => bubbleStage;

        /// <summary>正在换格编排中</summary>
        public bool IsComposing => m_Sequencer != null && m_Sequencer.IsComposing;

        /// <summary>
        /// 槽导演：**认识 DialogueSpeaker**，负责分槽 / 框 / 立绘 / 背景 / 说话人高亮。
        /// 气泡层自己不认识角色，所以角色相关的调用都走这里。
        /// </summary>
        private SlotDirector Director => bubbleStage != null ? bubbleStage.Director : null;

        private void Awake()
        {
            m_TemplateProvider = templateHost;      // 默认实现；要换成异步 / 对象池用 SetTemplateProvider

            ApplyLayout(layout);
            CacheDefaultTransition();
            BuildSequencer();
            BuildInput();

            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinuePressed);
            }

            SubscribeChoicePanel();
        }

        private void OnDestroy()
        {
            if (choicePanel != null)
            {
                choicePanel.Chosen -= OnChoiceConfirmed;
            }
        }

        private void OnEnable()
        {
            m_Model = this.GetModel<IDialogueShowModel>();
            if (m_Model == null)
            {
                return;
            }

            m_Model.CurrentItem.Register(OnItemChanged).UnregisterOnDestroyTrigger(this);
            m_Model.CurrentGroup.Register(OnGroupChanged).UnregisterOnDestroyTrigger(this);
            m_Model.CurrentChoices.Register(OnChoicesChanged).UnregisterOnDestroyTrigger(this);
            m_Model.IsEnd.Register(OnEndChanged).UnregisterOnDestroyTrigger(this);
        }

        private void Start()
        {
            // 万一章节在本组件启用之前就已经开始了，这里补一次当前这句
            if (m_Model != null && m_Model.CurrentItem.Value != null)
            {
                OnItemChanged(m_Model.CurrentItem.Value);
            }
        }

        private void Update()
        {
            m_Input?.Tick();
        }

        // ===================== 输入 =====================

        /// <summary>推进对话：正在打字 → 先补全；否则走下一句（继续按钮 / 外部脚本都能调）</summary>
        public void Advance()
        {
            m_Input?.Press();
        }

        private void OnContinuePressed()
        {
            m_Input?.Press();
        }

        private void BuildInput()
        {
            m_Input = new DialogueInputRouter
            {
                ContinueKey = continueKey,
                ClickToContinue = clickToContinue,

                // 章末 / 换格中 / 模型还没接上 —— 这一下都不算
                IsBlocked = () => m_Model == null || m_Ended || IsComposing,
                IsTyping = () => bubbleStage != null && bubbleStage.IsTyping,
                IsWaitingChoice = () => m_Model != null && m_Model.CurrentChoices.Value != null,
                CompleteTyping = () => bubbleStage?.CompleteTyping(),
                Advance = () => this.SendCommand<NextDialogueTextCommand>(),

                // Auto：按键 A 切换；停留时长在 Inspector 上（Auto 开着也能随时手动加速）
                Auto = autoPlay,
                AutoDelay = autoDelay,
                AutoKey = autoKey,
            };
        }

        /// <summary>手动开关 Auto（给 UI 按钮用）</summary>
        public void SetAuto(bool auto)
        {
            if (m_Input == null)
            {
                return;
            }

            m_Input.Auto = auto;
            m_Input.NotifyLineStarted();
        }

        /// <summary>Auto 现在开着没（给 UI 高亮用）</summary>
        public bool IsAuto => m_Input != null && m_Input.Auto;

        // ===================== 接线 =====================

        /// <summary>手动指定舞台（老接口，保留）</summary>
        public void SetStage(BubbleStage stage)
        {
            bubbleStage = stage;
        }

        /// <summary>手动指定**默认**擦除（编辑器 / 搭建器用；组头没填擦除 id 的格子用它）</summary>
        public void SetTransition(MonoBehaviour source)
        {
            transitionSource = source;
            CacheDefaultTransition();
        }

        /// <summary>手动指定擦除登记表（编辑器 / 搭建器用）</summary>
        public void SetTransitionLibrary(DialogueTransitionLibrary library)
        {
            transitionLibrary = library;
        }

        /// <summary>换模板来源（默认 = 场景里的 TemplateHost；换成别的实现时编排代码不用动）</summary>
        public void SetTemplateProvider(IDialogueTemplateProvider provider)
        {
            m_TemplateProvider = provider;
        }

        /// <summary>
        /// 把表现层指向某个布局（换模板时调用）：
        /// 舞台、选项面板都换成新的那一份，立绘槽重新收集。
        /// </summary>
        public void ApplyLayout(DialogueLayoutRefs target)
        {
            if (target == null)
            {
                return;
            }

            if (choicePanel != null)
            {
                choicePanel.Chosen -= OnChoiceConfirmed;    // 松掉旧模板的面板
            }

            layout = target;
            target.Init();

            bubbleStage = target.bubbleStage;
            choicePanel = target.choicePanel;

            if (bubbleStage != null)
            {
                bubbleStage.Init();
            }

            SubscribeChoicePanel();

            // 模板自检（同一个模板只会报一次）：槽 id 重复 / 槽没气泡 / 接线缺了
            DialogueTemplateValidator.ValidateAndLog(target.gameObject, target.name);
        }

        private void SubscribeChoicePanel()
        {
            if (choicePanel == null)
            {
                return;
            }

            choicePanel.Chosen -= OnChoiceConfirmed;
            choicePanel.Chosen += OnChoiceConfirmed;
        }

        private void CacheDefaultTransition()
        {
            m_DefaultTransition = transitionSource as IDialogueTransition;

            if (transitionSource != null && m_DefaultTransition == null)
            {
                Debug.LogError($"[对话] 转场组件「{transitionSource.GetType().Name}」没有实现 IDialogueTransition，" +
                               "这次按「直接切」处理", this);
            }
        }

        /// <summary>
        /// 换到下一格时，本格**离场**要播什么动画：
        ///   取的是**上一格（= 正在离开的那一格）**的字段，不是新格的 —— 字段挂在"要走的那一格"上
        ///   上一格没填 id（或这是第一格，还没有"上一格"）→ 返回 null = 这一下不播动画（直接切）
        ///   填了查不到 → 警告 + 回落默认离场动画（不卡流程）
        ///   方向 / 颜色由那一格的数据覆盖：同一套擦除组件就能服务"这格从左往右、下格从右往左"
        /// </summary>
        private IDialogueTransition ResolveTransition(DialogueShowGroupData enteringGroup)
        {
            var leaving = m_ShownGroup;      // 正在离开的那一格：离场动画挂它身上

            if (leaving == null)
            {
                return null;                 // 第一格：没有可离开的东西，不播
            }

            string transitionId = leaving.transitionId;

            if (string.IsNullOrEmpty(transitionId))
            {
                return null;                 // 这一格离场时不播动画
            }

            var transition = transitionLibrary != null ? transitionLibrary.Get(transitionId) : null;

            if (transition == null)
            {
                string known = transitionLibrary != null ? transitionLibrary.IdList() : "（没接登记表）";
                Debug.LogWarning($"[对话] 组「{leaving.groupId}」的离场动画 id「{transitionId}」找不到实现，" +
                                 $"这次用默认离场动画。登记表里的 id：{known}", this);

                transition = m_DefaultTransition;
            }

            // 那一格填了方向 / 颜色就覆盖（没填的项，实现会退回组件上的值）
            if (transition is IDialogueTransitionStyle styled)
            {
                styled.ApplyStyle(DialogueTransitionStyle.From(leaving));
            }

            return transition;
        }

        // ===================== 换格编排（时序在 Sequencer 里，这里只提供"做什么"） =====================

        private void BuildSequencer()
        {
            m_Sequencer = new DialogueGroupSequencer(
                resolveTransition: ResolveTransition,
                hideOld: HideOldGroup,
                holdOld: HoldPreviousFrame,
                releaseOld: ReleasePreviousFrame,
                compose: ComposeGroup,
                reveal: RevealGroup,
                showItem: ShowItem);
        }

        private void OnGroupChanged(DialogueShowGroupData group)
        {
            // 记住旧画面（布局 + 舞台）：溶解类擦完要单独收它 —— 那时 layout / bubbleStage 已经指向新的一份了
            m_PreviousLayout = layout;
            m_PreviousStage = bubbleStage;

            m_Sequencer.TransitionOnEveryGroup = transitionOnEveryGroup;
            m_Sequencer.Begin(group);
        }

        /// <summary>① 收掉旧气泡和选项面板（普通擦除：转场盖住之前就收，反正观众看不见）</summary>
        private void HideOldGroup(DialogueShowGroupData group)
        {
            choicePanel?.Hide();
            bubbleStage?.HideAll();
        }

        /// <summary>② 转场"完全盖住"那一刻：换模板 + 配槽（观众看不见）</summary>
        private void ComposeGroup(DialogueShowGroupData group)
        {
            if (group != null)
            {
                SwitchTemplate(group.templateId);
            }

            // 换格时清空槽占用，并把本组出场角色预先分到槽上、立绘 / 框 / 背景就位
            Director?.ComposeGroup(group);

            // 记下"现在演的是哪一格"：下一格换进来时，离场动画取的就是这一格
            m_ShownGroup = group;
        }

        /// <summary>③ 转场露出之后：组音效 + 立绘 / 框 / 背景入场，播完放行第一句</summary>
        private void RevealGroup(DialogueShowGroupData group, System.Action onDone)
        {
            group?.enterAudio?.Play();

            var director = Director;
            if (director == null)
            {
                onDone?.Invoke();
                return;
            }

            director.EnterAssignedParts(onDone);
        }

        private void SwitchTemplate(int templateId)
        {
            var provider = m_TemplateProvider;

            if (provider == null || provider.CurrentTemplateId == templateId)
            {
                return;
            }

            // 溶解类擦除要旧画面活到擦完 → 让 Host 保留旧实例（压在新的上面），擦完 ReleasePreviousFrame 放掉
            if (provider.Activate(templateId, m_HoldPreviousFrame))
            {
                ApplyLayout(provider.Current);
            }
        }

        // ===================== IDialogueFrameSource（溶解类擦除用） =====================

        /// <summary>现在这一格布局里所有可绘制部件（含未激活的）—— 溶解材质要贴到它们上面</summary>
        public IReadOnlyList<Graphic> CurrentGraphics => CollectGraphics(layout);

        /// <summary>有没有"被保留的旧画面"（只有真换了模板才有；同一个模板换格时没有）</summary>
        public bool HasHeldPreviousFrame => templateHost != null && templateHost.HasHeldPrevious;

        /// <summary>被保留的旧画面里的部件（没有就返回空）</summary>
        public IReadOnlyList<Graphic> HeldPreviousGraphics
        {
            get
            {
                if (!HasHeldPreviousFrame)
                {
                    m_EmptyGraphics.Clear();
                    return m_EmptyGraphics;
                }

                return CollectGraphics(m_PreviousLayout);
            }
        }

        /// <summary>换格开始时告诉后面"这一次要保留旧画面"（由时序器调）</summary>
        private void HoldPreviousFrame()
        {
            m_HoldPreviousFrame = true;
        }

        /// <summary>
        /// 擦完了：放掉旧画面 —— 停用被保留的旧模板实例 + 收掉旧气泡舞台。
        /// ★ 两边都要做：换了模板靠 Host 停用旧实例；同一个模板换格没有"旧实例"可停用，靠收旧气泡。
        /// </summary>
        public void ReleasePreviousFrame()
        {
            m_HoldPreviousFrame = false;

            templateHost?.ReleasePrevious();

            m_PreviousStage?.HideAll();
            m_PreviousStage = null;
            m_PreviousLayout = null;
        }

        /// <summary>按布局缓存一份部件清单（同一个布局只收集一次）</summary>
        private IReadOnlyList<Graphic> CollectGraphics(DialogueLayoutRefs source)
        {
            if (source == null)
            {
                m_EmptyGraphics.Clear();
                return m_EmptyGraphics;
            }

            if (m_GraphicsCacheSource != source)
            {
                m_GraphicsCacheSource = source;
                m_GraphicsCache.Clear();
                m_GraphicsCache.AddRange(source.GetComponentsInChildren<Graphic>(true));
            }

            return m_GraphicsCache;
        }

        // ===================== Model 事件 =====================

        private void OnItemChanged(DialogueShowTextItem item)
        {
            if (item == null)
            {
                return;
            }

            if (ReferenceEquals(item, m_LastItem))
            {
                return;     // 同一句重复通知，忽略
            }

            // 换格还没走完 → 入场门先攒着（放行时才弹）
            if (m_Sequencer != null && m_Sequencer.TryBuffer(item))
            {
                return;
            }

            ShowItem(item);
        }

        /// <summary>④ 真正演这一句：领槽 → 弹气泡 → 高亮说话人 → 台词音效</summary>
        private void ShowItem(DialogueShowTextItem item)
        {
            m_LastItem = item;

            if (bubbleStage == null)
            {
                return;
            }

            var director = Director;
            if (director == null)
            {
                return;
            }

            // 槽：这句填了 slotId 就用它，否则按角色自动领槽（本格内位置稳定）
            // ★ 角色语义在 SlotDirector 里 —— 气泡层不认识角色
            var slot = director.ClaimSlot(item.speaker, item.slotId, item.expression);
            if (slot == null)
            {
                return;     // 领不到槽的原因 ClaimSlot 已经报过
            }

            bubbleStage.ShowInSlot(slot, new BubbleRequest
            {
                key = slot.SlotId,
                text = item.text,
                displayName = item.speaker != null ? item.speaker.name : null,

                // 这一句自己的样式优先；没填就用**本格**的默认样式（组数据上的），再没有才用气泡自带默认
                style = item.bubbleStyle != null
                    ? item.bubbleStyle
                    : (m_ShownGroup != null ? m_ShownGroup.bubbleStyle : null),
            });

            // Auto：新的一句开始演了 → 重新计时
            m_Input?.NotifyLineStarted();

            if (dimOtherPortraits)
            {
                director.SetSpeakerHighlight(item.speaker);
            }

            // 这句台词自己的音效（气泡的入场/退场音效由 BubbleAnimSet 管，这里只管"台词音效"）
            item.audioEff?.Play();
        }

        private void OnChoicesChanged(List<DialogueChoiceData> choices)
        {
            if (choices != null)
            {
                // 该弹选项了：把还在打的那句补全，免得选项盖着没打完的字
                bubbleStage?.CompleteTyping();
                choicePanel?.Show(choices);
            }
            else
            {
                choicePanel?.Hide();
            }
        }

        /// <summary>
        /// 玩家确认了某个选项：发命令跳组。
        /// ★ 确认键（空格/回车）和「继续」是同一个键：选完的那一帧必须挡住 Advance，
        ///   不然同一次按键会顺便把新组第一句的打字机补全掉。
        /// </summary>
        private void OnChoiceConfirmed(int index)
        {
            m_Input?.BlockThisFrame();
            this.SendCommand(new ChooseDialogueCommand(index));
        }

        private void OnEndChanged(bool ended)
        {
            m_Ended = ended;

            if (!ended)
            {
                return;
            }

            // 章末：转场和入场门都作废，画面收干净
            m_Sequencer?.Cancel();
            m_LastItem = null;
            m_ShownGroup = null;        // 下一章的第一格不该继承上一章的"离场"状态

            choicePanel?.Hide();
            bubbleStage?.HideAll();
        }

        public IArchitecture GetArchitecture()
        {
            return GravityAniApp.Interface;
        }
    }
}
