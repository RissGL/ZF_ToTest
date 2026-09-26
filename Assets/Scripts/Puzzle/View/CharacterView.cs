using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework;
using ZGameFramework.Utility;
using ZF.EraGallery;

namespace ZF.Puzzle
{
    /// <summary>
    /// 场景里的一个人物。
    ///
    /// 关键点：**他在哪个时代由 CharacterModel 说了算**，这个组件只是照着模型的记录，
    /// 把自己挂到那个时代窗口下面。于是：
    ///   · 「进哪个时代才看得见他」是白送的 —— 窗口不活跃，他自然不显示，一行代码不用写；
    ///   · 跨窗口搬家 = 改一条 Model 记录 + 换个父节点，不用管场景怎么摆；
    ///   · 存档只要存「谁在哪个时代」。
    ///
    /// 他同时也是一个能点的东西（继承 StateVisualBehaviour），所以点人物说话、给东西都直接能用。
    /// 「能不能操控人」以后再说：操控层只需要往 CharacterModel 里写，这个类不用改。
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterView : StateVisualBehaviour
    {
        [Header("人物身份")]
        [Label("人物 id（规则表靠它引用这个人）")]
        [SerializeField] private string characterId = "";

        [Label("显示名（日志 / 提示用）")]
        [SerializeField] private string displayName = "";

        [Label("开局所在时代")]
        [SerializeField] private EraId homeEra = EraId.Stone;

        [Header("站位（在时代窗口里的位置）")]
        [Label("站位高度（脚底贴着地景上沿）")]
        [SerializeField] private float seatY = -0.70f;

        [Label("同代多人时的间距")]
        [SerializeField] private float seatSpacing = 0.70f;

        private ICharacterModel m_Characters;
        private ICharacterSystem m_CharacterSystem;
        private EraId m_ShownEra;
        private bool m_Placed;
        private bool m_WarnedMissingWindow;

        public override string InteractionId => characterId;
        public string CharacterId => characterId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? characterId : displayName;
        public EraId HomeEra => homeEra;

        /// <summary>他现在在哪个时代（问模型）。</summary>
        public EraId CurrentEra => m_Characters != null ? m_Characters.GetEra(characterId) : homeEra;

        /// <summary>人物默认压在物件上面：人应该站在炉子/箱子前面，而不是被盖住。</summary>
        protected override int DefaultSortingOrder => PuzzleSortingOrder.CharacterBase;

        protected override void Awake()
        {
            // 先把自己的 Model 拿好、登记进去，再走基类的 Awake ——
            // 基类 Awake 结尾会 Refresh 一次，那时这些已经就位，人物一上来就摆在对的时代里
            m_Characters = this.GetModel<ICharacterModel>();
            m_CharacterSystem = this.GetSystem<ICharacterSystem>();

            // 向模型登记：他存在，开局在 homeEra
            m_Characters?.Register(characterId, homeEra);

            base.Awake();
        }

        protected override void Start()
        {
            // 基类负责订阅「谜题状态」（状态组/外观），这里再补一个「人物位置」和「动画请求」
            base.Start();

            m_Characters?.Revision.Register(_ => Refresh()).UnregisterOnDestroyTrigger(this);
            this.RegisterEvent<CharacterAnimationEvent>(OnAnimationRequested).UnregisterOnDestroyTrigger(this);
            Refresh();
        }

        /// <summary>换个时代。搬动了返回 true；已经在那个时代 / 不认识这个人返回 false。</summary>
        public bool TryMoveTo(EraId era) =>
            m_CharacterSystem != null && m_CharacterSystem.TryMove(characterId, era);

        // ===================== 动画接口 =====================
        //
        // 规则表里这样编排（不用写代码）：
        //   PlayCharacterAnimation("leave")  →  MoveSelectedCharacter(delayBefore = 0.6)  →  PlayCharacterAnimation("arrive")
        // 「等动画播完」由效果基类的 delayBefore 负责。

        private void OnAnimationRequested(CharacterAnimationEvent e)
        {
            if (!PuzzleOps.SameState(e.CharacterId, characterId))
            {
                return;
            }

            OnPlayAnimation(e.Clip);
        }

        /// <summary>
        /// 【换真动画就 override 这里】接 Animator.SetTrigger / DOTween 序列 / 帧动画都行。
        /// 默认实现只是几个占位缩放，保证"看得出他走了"。
        /// </summary>
        protected virtual void OnPlayAnimation(string clip)
        {
            switch (clip)
            {
                case "leave":
                    PlayLeavePlaceholder();
                    return;

                case "arrive":
                    PlayArriveFeedback();
                    return;

                default:
                    PlayArriveFeedback();
                    return;
            }
        }

        /// <summary>离场占位：缩小 + 朝裂隙那边（右侧）挪一点，像被吸进去。</summary>
        private void PlayLeavePlaceholder()
        {
            transform.DOKill();
            transform.localScale = Vector3.one;

            Sequence sequence = DOTween.Sequence();
            sequence.Append(transform.DOScale(new Vector3(0.25f, 0.25f, 1f), 0.45f).SetEase(Ease.InBack));
            sequence.Join(transform.DOLocalMoveX(transform.localPosition.x + 1.2f, 0.45f).SetEase(Ease.InQuad));
            sequence.SetUpdate(true);
        }

        public override void Refresh()
        {
            base.Refresh();     // 状态组照常按状态规则刷
            PlaceInEra();       // 位置跟着模型里的时代走

            // 被点名了就把他那个高亮框钉亮（复用悬停框，省一套"选中"形状）
            SetOutlineLocked(m_Characters != null &&
                             PuzzleOps.SameState(m_Characters.SelectedCharacter.Value, characterId));
        }

        private void PlaceInEra()
        {
            if (m_Characters == null)
            {
                return;
            }

            EraId era = m_Characters.GetEra(characterId);
            EraWindow window = EraWindowRegistry.Get(era);

            if (window == null)
            {
                if (!m_WarnedMissingWindow)
                {
                    m_WarnedMissingWindow = true;
                    Debug.LogWarning($"[人物] 「{DisplayName}」现在应该在 {era}，但场景里没有这个时代的窗口，" +
                                     "他只能先留在原地。多半是那个时代的窗口被删了。");
                }

                return;
            }

            m_WarnedMissingWindow = false;

            if (transform.parent != window.transform)
            {
                transform.SetParent(window.transform, false);
            }

            // 站位按「这个时代现在有几个人」自动排：一个人就站正中，两个人左右分开，三个人再往外。
            // 位置是算出来的，不是存起来的 —— 所以谁搬走、谁搬来，剩下的人会自动重新站好，永远不会叠在一起。
            List<string> peers = m_Characters.InEra(era);
            int slot = peers.IndexOf(characterId);
            if (slot < 0)
            {
                slot = 0;
            }

            float offset = (slot - (peers.Count - 1) * 0.5f) * seatSpacing;
            transform.localPosition = new Vector3(offset, seatY, 0f);

            // 第一次摆位不抖；之后每次换时代抖一下，至少让人看得出"他过去了"
            if (m_Placed && m_ShownEra != era)
            {
                PlayArriveFeedback();
            }

            m_ShownEra = era;
            m_Placed = true;
        }

        /// <summary>换时代的反馈。以后要换成"走一段路 / 穿门"的演出，改这里就行。</summary>
        private void PlayArriveFeedback()
        {
            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * 0.22f, 0.4f, 6, 0.7f).SetUpdate(true);
        }

        private void OnDestroy() => transform.DOKill();
    }
}
