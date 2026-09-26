using System;
using TMPro;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 通用打字机：一个字一个字把正文显示出来。
    ///
    /// ★ 用 TMP 的 maxVisibleCharacters 逐字显示，不是每帧拼新字符串：
    ///   - 不产生垃圾（零 GC）
    ///   - 富文本标签（&lt;color&gt;、&lt;sprite&gt;）不会被打断
    ///   - "点一下直接显示全文"就是把它设成总字数
    ///
    /// 用 Update 驱动 + unscaledDeltaTime：游戏暂停时打字机不该卡住，也不需要协程/async 的取消竞态。
    /// </summary>
    public class TypewriterText : MonoBehaviour
    {
        [Label("正文文本（留空自动取本物体上的 TMP）")]
        [SerializeField] private TMP_Text target;

        [Label("默认速度（秒/字）")]
        [SerializeField] private float defaultSpeed = 0.035f;

        [Label("默认标点停顿（秒）")]
        [SerializeField] private float defaultPunctuationPause = 0.14f;

        private string m_PauseChars = "，。！？…、,.!?;：";
        private float m_Speed = 0.035f;
        private float m_PausePerPunctuation = 0.14f;

        private int m_TotalChars;
        private int m_VisibleChars;
        private float m_Elapsed;
        private float m_PauseUntil;
        private bool m_Playing;
        private Action m_OnComplete;

        /// <summary>正在逐字显示</summary>
        public bool IsPlaying => m_Playing;

        /// <summary>已经显示出来的字数（-1 = 没在播）</summary>
        public int VisibleChars => m_Playing ? m_VisibleChars : -1;

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponent<TMP_Text>();
            }
        }

        /// <summary>开始逐字显示；onComplete 在整句显示完（或调 Complete 之后）回调</summary>
        public void Play(string text, float speed, float punctuationPause, string pauseChars, Action onComplete)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                return;
            }

            m_Speed = speed > 0f ? speed : defaultSpeed;
            m_PausePerPunctuation = punctuationPause > 0f ? punctuationPause : defaultPunctuationPause;
            if (!string.IsNullOrEmpty(pauseChars))
            {
                m_PauseChars = pauseChars;
            }

            m_OnComplete = onComplete;
            m_VisibleChars = 0;
            m_Elapsed = 0f;
            m_PauseUntil = 0f;

            // 一次性赋全文，然后靠 maxVisibleCharacters 控制显示到第几个字
            target.text = text ?? string.Empty;
            target.maxVisibleCharacters = 0;
            target.ForceMeshUpdate();                 // 立刻解析，才能知道总字数
            m_TotalChars = target.textInfo != null ? target.textInfo.characterCount : 0;

            if (m_TotalChars <= 0)
            {
                m_Playing = false;
                InvokeComplete();
                return;
            }

            m_Playing = true;
        }

        /// <summary>立刻显示全文（玩家点「继续」时用）</summary>
        public void Complete()
        {
            if (!m_Playing)
            {
                return;
            }

            m_VisibleChars = m_TotalChars;
            if (target != null)
            {
                target.maxVisibleCharacters = m_TotalChars;
            }

            m_Playing = false;
            InvokeComplete();
        }

        /// <summary>中断（不回调）：换下一句 / 气泡被收掉时用</summary>
        public void Cancel()
        {
            m_Playing = false;
            m_OnComplete = null;
        }

        private void Update()
        {
            if (!m_Playing)
            {
                return;
            }

            if (Time.unscaledTime < m_PauseUntil)
            {
                return;
            }

            m_Elapsed += Time.unscaledDeltaTime;

            int want = m_Speed > 0f ? Mathf.FloorToInt(m_Elapsed / m_Speed) : m_TotalChars;
            if (want <= m_VisibleChars)
            {
                return;
            }

            m_VisibleChars = Mathf.Min(want, m_TotalChars);
            target.maxVisibleCharacters = m_VisibleChars;

            // 刚显示出来的那个字是标点 → 停一下，读起来有节奏
            int index = m_VisibleChars - 1;
            if (index >= 0 && target.textInfo != null && index < target.textInfo.characterCount)
            {
                char c = target.textInfo.characterInfo[index].character;
                if (m_PauseChars.IndexOf(c) >= 0)
                {
                    m_PauseUntil = Time.unscaledTime + m_PausePerPunctuation;
                }
            }

            if (m_VisibleChars >= m_TotalChars)
            {
                m_Playing = false;
                InvokeComplete();
            }
        }

        private void InvokeComplete()
        {
            var callback = m_OnComplete;
            m_OnComplete = null;
            callback?.Invoke();
        }
    }
}
