using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ZGameFramework;
using ZGameFramework.Utility;
using ZF.DialoguePresentation;
using ZF.EraGallery;
using ZF.Puzzle;
using Object = UnityEngine.Object;

namespace ZF.Game
{
    /// <summary>
    /// **串起谜题和对话系统的那一根线**：谜题发出的每一句反馈，都由**那个时代的那个人物**用对话系统的模板说出来；
    /// 玩家点一下，气泡退场，然后才轮到下一句。
    ///
    /// 数据流：
    ///     PuzzleSystem 发 PuzzleFeedbackEvent（"得先有块能打火的石头。"）
    ///          ↓ 这里接住
    ///     谁来说：被点的那个人物；点的不是人 → **它所在时代里的那个人**（对应时代对应角色）
    ///          ↓ SlotDirector.ClaimSlot(speaker) —— 走对话系统那套「谁说话谁上槽」（含立绘 / 高亮）
    ///     BubbleStage.ShowInSlot(...)   ← 对话模板 Template_Tip
    ///          ↓ 玩家点左键 / 空格
    ///     打字中 → 补全这一句（不推进，和对话系统一个规矩）
    ///     打完   → 气泡退场 → 下一句
    ///
    /// 用的是**对话系统那一套模板**，不是代码糊的 UI：
    ///   气泡的位置 / 纸色 / 字号 / 打字机 / 进出场动画，全都在
    ///   `Assets/Prefab/Templates/Template_Tip.prefab` 里配着 —— 想改就去改那个 Prefab，这里一行都不用动。
    ///
    /// 舞台从哪来（按顺序找，找到就用）：
    ///   ① 场景里已经有 `DialogueTemplateHost` 而且已经激活过一份布局 → 用它的舞台（你搭的对话场景）
    ///   ② 场景里已经有任意 `BubbleStage` → 用它
    ///   ③ 都没有 → 把 `template` 这个模板 Prefab 实例化到一个新建的 Canvas 下（`Init()` 走模板自己的初始化）
    ///   所以"先用 Template_Tip 顶着、以后换成真对话场景"不用改代码。
    ///
    /// 三个"必须这样"的细节：
    ///   ① **触发反馈的那一帧不算"点掉"**：触发交互的点击和气泡同一帧，不挡掉气泡会立刻自己关掉。
    ///   ② **气泡不吞"点东西"的那一下**（场景点击不归它管）：点到东西就照点，气泡在同一帧退场 ——
    ///      否则每交互一次都要先点一下把气泡关掉，"点石头拿燧石 → 点钻木取火"会莫名多出一下。
    ///      点空白处才是"我就关掉它"。
    ///   ③ **排队**：连着点两下不丢消息，一句一句过。
    /// </summary>
    [DisallowMultipleComponent]
    public class PuzzleFeedbackBubble : MonoBehaviour, IController
    {
        [Serializable]
        private class SpeakerBinding
        {
            [Label("谜题里的人物 id（CharacterView 上那个 id）")]
            public string characterId;

            [Label("对话系统里的说话人")]
            public DialogueSpeaker speaker;
        }

        [Label("对话模板 Prefab（默认 Template_Tip：一个旁白气泡，够「冒一句」用）")]
        [SerializeField] private DialogueLayoutRefs template;

        [Label("人物 id → 对话说话人（提示由「这个时代的那个人」说出来）")]
        [SerializeField] private List<SpeakerBinding> speakers = new List<SpeakerBinding>();

        [Label("那个时代一个人都没有时，退回哪个槽（模板里的旁白槽）")]
        [SerializeField] private string fallbackSlotId = "旁白";

        [Label("说话人名字取不到时显示什么")]
        [SerializeField] private string anonymousSpeaker = "旁白";

        [Label("最多排队几句（超了丢最老的，免得连点攒一堆）")]
        [SerializeField] private int maxPending = 4;

        [Label("一句话打完之后，过几秒自己收掉（0 = 一直留着，等玩家点掉）")]
        [SerializeField] private float autoHideDelay = 2.5f;

        [Label("没有对话场景时，代码建的 Canvas 用多大基准（和模板 Prefab 的参考分辨率对齐）")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

        private struct Line
        {
            /// <summary>谁说（模板里走「谁说话谁上槽」，没人的时代是 null）</summary>
            public DialogueSpeaker Speaker;

            /// <summary>没有说话人时，气泡上显示谁的名字</summary>
            public string FallbackName;

            public string Text;
        }

        private readonly Queue<Line> m_Pending = new Queue<Line>();

        private BubbleStage m_Stage;
        private IPuzzleModel m_Model;
        private ICharacterModel m_Characters;
        private DialogueSpeaker m_ActiveSpeaker;
        private DialogueSlot m_SpeakingSlot;
        private string m_ActiveSlotId;
        private bool m_NeedsAssignmentReset;
        private bool m_Showing;
        private int m_ShownFrame = -1;
        private float m_AutoHideTimer;

        public IArchitecture GetArchitecture() => GameApp.Interface;

        /// <summary>气泡正显示着。</summary>
        public bool IsShowing => m_Showing;

        private void Awake()
        {
            m_Stage = ResolveStage();
            m_Model = this.GetModel<IPuzzleModel>();
            m_Characters = this.GetModel<ICharacterModel>();

            this.RegisterEvent<PuzzleFeedbackEvent>(OnFeedback).UnregisterOnDestroyTrigger(this);
        }

        private void OnDestroy()
        {
            m_Pending.Clear();
        }

        // ===================== 舞台从哪来 =====================

        private BubbleStage ResolveStage()
        {
            // ① 场景里已经有对话场景：模板 Host 激活过布局 → 直接借它的舞台
            DialogueTemplateHost host = Object.FindObjectOfType<DialogueTemplateHost>();
            if (host != null && host.Current != null && host.Current.bubbleStage != null)
            {
                return host.Current.bubbleStage;
            }

            // ② 场景里已经有裸的气泡舞台
            BubbleStage existing = Object.FindObjectOfType<BubbleStage>();
            if (existing != null)
            {
                return existing;
            }

            // ③ 都没有 → 实例化我们指定的那份对话模板
            if (template == null)
            {
                Debug.LogWarning("[谜题气泡] 没配对话模板 Prefab，反馈就只能进 Console。\n" +
                                 "  修法：跑一下 Tools/谜题/搭建解密演示（会自动填上 Template_Tip），" +
                                 "或者手动把 Assets/Prefab/Templates/Template_Tip.prefab 拖到 PuzzleRoot 上" +
                                 "这个组件的「对话模板 Prefab」字段。");
                return null;
            }

            GameObject canvasGo = new GameObject("对话（谜题反馈）", typeof(Canvas), typeof(CanvasScaler));

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;      // 压在世界和别的 UI 上面

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // worldPositionStays = false：UI 要的是"原样的 local 布局"（锚点 / 偏移 / pivot / 缩放），
            // 用 true 会被父节点的缩放和世界坐标搅乱 —— 和 DialogueTemplateHost 里的注释同一个道理
            DialogueLayoutRefs instance = Object.Instantiate(template, canvasGo.transform, false);
            instance.name = template.name + "（谜题反馈）";
            instance.gameObject.SetActive(true);

            // 模板自己的初始化：收槽 + 把选项面板提到最上层
            instance.Init();

            // 正常情况引用是连好的；万一没连（模板被改过），自己往下找一个，别让反馈静默消失
            BubbleStage stage = instance.bubbleStage != null
                ? instance.bubbleStage
                : instance.GetComponentInChildren<BubbleStage>(true);

            if (stage == null)
            {
                Debug.LogWarning($"[谜题气泡] 模板「{template.name}」里没有气泡舞台（BubbleStage），反馈弹不出来。");
            }

            return stage;
        }

        // ===================== 接反馈 =====================

        private void OnFeedback(PuzzleFeedbackEvent e)
        {
            if (e == null || string.IsNullOrEmpty(e.Message))
            {
                return;
            }

            DialogueSpeaker speaker = FindSpeaker(SpeakingCharacter(e.TargetId));

            string fallbackName = m_Model != null ? m_Model.GetTargetDisplayName(e.TargetId) : "";
            if (string.IsNullOrEmpty(fallbackName))
            {
                fallbackName = anonymousSpeaker;
            }

            m_Pending.Enqueue(new Line
            {
                Speaker = speaker,
                FallbackName = speaker != null && !string.IsNullOrEmpty(speaker.name) ? speaker.name : fallbackName,
                Text = e.Message,
            });

            while (maxPending > 0 && m_Pending.Count > maxPending)
            {
                m_Pending.Dequeue();
            }

            Pump();
        }

        /// <summary>
        /// 这句话该由谁来说：
        ///   ① 点的就是人物本人 → 他自己说
        ///   ② 点的不是人 → **它所在时代里的那个人**（"对应时代对应角色"）
        ///   ③ 那个时代一个人都没有（都搬走了）→ 没有说话人，退回旁白槽
        /// </summary>
        private string SpeakingCharacter(string targetId)
        {
            if (m_Characters == null)
            {
                return null;
            }

            // 没有目标（谜题完成这种整句通报）→ 不硬塞一个角色，走旁白
            if (string.IsNullOrEmpty(targetId))
            {
                return null;
            }

            if (m_Characters.IsKnown(targetId))
            {
                return FindSpeaker(targetId) != null ? targetId : null;
            }

            // 人物的时代问人物模型（人是会走的），物体的时代问登记表 —— 和 PuzzleSystem 里同一个口径
            EraId era = m_Model != null ? m_Model.GetTargetEra(targetId) : EraId.Stone;
            List<string> inEra = m_Characters.InEra(era);

            for (int i = 0; i < inEra.Count; i++)
            {
                if (FindSpeaker(inEra[i]) != null)
                {
                    return inEra[i];
                }
            }

            return null;
        }

        private DialogueSpeaker FindSpeaker(string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || speakers == null)
            {
                return null;
            }

            for (int i = 0; i < speakers.Count; i++)
            {
                SpeakerBinding binding = speakers[i];
                if (binding != null && binding.speaker != null && binding.characterId == characterId)
                {
                    return binding.speaker;
                }
            }

            return null;
        }

        // ===================== 点一下退场 =====================

        private void Update()
        {
            if (!m_Showing)
            {
                return;
            }

            bool typing = m_Stage != null && m_Stage.IsTyping;

            // 打完字之后没人管它，自己收掉：一句话是"冒个泡"，不该一直杵在画面上。
            // 玩家想早点收，点一下就行（下面那段）。0 = 关掉这个自动收。
            if (autoHideDelay > 0.0001f && !typing)
            {
                m_AutoHideTimer += Time.unscaledDeltaTime;

                if (m_AutoHideTimer >= autoHideDelay)
                {
                    Hide();
                    return;
                }
            }
            else
            {
                m_AutoHideTimer = 0f;
            }

            // ① 触发反馈的那次点击和气泡同一帧：那一帧不算"点掉"
            if (Time.frameCount == m_ShownFrame)
            {
                return;
            }

            if (!Pressed())
            {
                return;
            }

            // ② 打字中：这一下是"补全这一句"，**不推进** —— 和对话系统 DialogueInputRouter 同一个规矩，
            //    节奏这种东西只能有一处说了算
            if (typing)
            {
                m_Stage.CompleteTyping();
                return;
            }

            Hide();
        }

        private static bool Pressed()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            Keyboard keyboard = Keyboard.current;
            return keyboard != null &&
                   (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
        }

        private void Hide()
        {
            m_Showing = false;
            m_AutoHideTimer = 0f;

            if (m_Stage != null)
            {
                SlotDirector director = m_Stage.Director;

                if (director != null)
                {
                    director.SetSpeakerHighlight(null);     // 收工：高亮还回去
                }

                // 气泡退场（Bubble 自己的退场动画）
                m_Stage.Hide(m_ActiveSlotId);

                // ★ 立绘 / 框 / 背景也要退场 —— 一句话说完，"这一幕"就结束了。
                //   光关气泡会留一个空框立在那儿（就是"框框还在"那个现象）。
                //   这里走的是对话系统 SlotVisual 自己的退场动画（模板里配的那套）。
                RetreatVisuals(m_SpeakingSlot);
            }

            // 下一句要重新领槽：不清的话上一句占着槽，这一句就只能去抢旁白位（多来几句就没位置了）
            m_NeedsAssignmentReset = true;

            m_ActiveSpeaker = null;
            m_ActiveSlotId = null;
            m_SpeakingSlot = null;
            Pump();
        }

        /// <summary>让这个槽的框 / 立绘 / 背景播各自的退场动画。</summary>
        private static void RetreatVisuals(DialogueSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            slot.frame?.Hide();
            slot.portrait?.Hide();
            slot.background?.Hide();
        }

        private void Pump()
        {
            if (m_Showing || m_Stage == null || m_Pending.Count == 0)
            {
                return;
            }

            Line line = m_Pending.Dequeue();

            Bubble bubble = ShowLine(line);

            // 借来的舞台上没有槽（或者那个槽上没挂气泡）时 Show 会返回 null。
            // 这时候**千万不能**把自己标成"正在显示"：那样输入闸门就永远关着，游戏点不动了。
            if (bubble == null)
            {
                return;
            }

            m_Showing = true;
            m_ShownFrame = Time.frameCount;
            m_AutoHideTimer = 0f;
        }

        /// <summary>
        /// 说这一句 —— 走的是对话系统同一条路：
        /// `SlotDirector.ClaimSlot(speaker)`（谁说话谁上槽，含立绘 / 高亮）→ `ShowInSlot`。
        /// 没有说话人（那个时代没人了）时才退回旁白槽。
        /// </summary>
        private Bubble ShowLine(Line line)
        {
            SlotDirector director = m_Stage.Director;

            // 上一句是"演完退场"走的 → 这里把占用表和部件一起归位，这一句重新领槽。
            // 顺序很重要：**先归位再领槽**，不然上一句占着那个槽，这一句只能去抢旁白位。
            if (director != null && m_NeedsAssignmentReset)
            {
                director.ClearAssignments();
                m_NeedsAssignmentReset = false;
            }

            if (line.Speaker != null && director != null)
            {
                DialogueSlot slot = director.ClaimSlot(line.Speaker, null);

                if (slot != null)
                {
                    // 框 / 立绘 / 背景入场（和对话系统一样）。
                    // ★ 不等它演完就弹气泡：某个动画万一没回调，这一句就被卡住了；
                    //   而且对"冒一句"这种短反馈来说，边入场边出字反而更利索。
                    director.EnterAssignedParts(null);

                    Bubble bubble = m_Stage.ShowInSlot(slot, new BubbleRequest
                    {
                        key = slot.SlotId,
                        text = line.Text,
                        displayName = line.Speaker.name,
                    });

                    if (bubble != null)
                    {
                        m_ActiveSlotId = slot.SlotId;
                        m_SpeakingSlot = slot;
                        m_ActiveSpeaker = line.Speaker;

                        // 和 DialogueView 一样：说话的那个亮着，别人压暗
                        director.SetSpeakerHighlight(line.Speaker);
                        return bubble;
                    }
                }
            }

            m_ActiveSlotId = fallbackSlotId;
            m_SpeakingSlot = null;
            return m_Stage.Show(new BubbleRequest
            {
                key = fallbackSlotId,
                text = line.Text,
                displayName = line.FallbackName,
            });
        }
    }
}
