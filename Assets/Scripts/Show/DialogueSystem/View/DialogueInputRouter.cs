using System;
using UnityEngine;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 输入路由：键盘 / 鼠标 / 继续按钮 → 对话意图（继续 / 补全打字）。
    ///
    /// ★ 节奏是 AVG 铁律，所以判定顺序固定在这里，别在别处再判一遍：
    ///   同一帧只处理一次（否则一次点击跳两句）
    ///   → 正在换格 / 章末 → 这一下不算
    ///   → 正在打字 → 这一下 = 补全，**不推进**
    ///   → 正在等选项 → 这一下不算（选项面板自己收键）
    ///   → 否则推进一句
    ///
    /// ★ 扩展位（Auto 已经在这里了；Skip / Backlog 也加这一层，不碰编排和表现）：
    ///   - Auto：打开 <see cref="Auto"/> 后，<see cref="TickAuto"/> 到点自己推进（玩家一按键就重新计时）
    ///   - Skip：在 <see cref="Press"/> 的分发里跳过"补全打字"这一步，或把打字速度调满
    ///   - Backlog：在 <see cref="Advance"/> 触发前反查历史即可，不需要新的输入源
    /// </summary>
    public class DialogueInputRouter
    {
        /// <summary>继续键</summary>
        public KeyCode ContinueKey = KeyCode.Space;

        /// <summary>回车 / 小键盘回车也算继续</summary>
        public bool EnterKeyToo = true;

        /// <summary>鼠标左键也算继续</summary>
        public bool ClickToContinue = true;

        /// <summary>被挡住（正在换格 / 章末）：这一下不算</summary>
        public Func<bool> IsBlocked;

        /// <summary>正在打字：这一下 = 补全</summary>
        public Func<bool> IsTyping;

        /// <summary>正在等玩家选选项：这一下不算</summary>
        public Func<bool> IsWaitingChoice;

        /// <summary>补全当前这句的打字机</summary>
        public Action CompleteTyping;

        /// <summary>推进到下一句</summary>
        public Action Advance;

        /// <summary>Auto（自动播放）开关；开着的时候不用玩家按键，到点自己推进</summary>
        public bool Auto;

        /// <summary>Auto 模式下"这一句读完"等多久再推进（秒）</summary>
        public float AutoDelay = 1.6f;

        /// <summary>切换 Auto 的快捷键（KeyCode.None = 不绑键）</summary>
        public KeyCode AutoKey = KeyCode.A;

        private int m_LastPressFrame = -1;

        /// <summary>上一句是什么时候开始等的（打字打完/新句弹出的时刻）</summary>
        private float m_AutoSince;

        /// <summary>每帧调一次（DialogueView.Update 里）</summary>
        public void Tick()
        {
            if (AutoKey != KeyCode.None && Input.GetKeyDown(AutoKey))
            {
                Auto = !Auto;
                m_AutoSince = Time.time;        // 刚打开时给玩家一点反应时间
            }

            if (Pressed())
            {
                // 玩家自己按了 → Auto 重新计时（别刚按完又被自动推一句）
                m_AutoSince = Time.time;
                Press();
                return;
            }

            TickAuto();
        }

        /// <summary>
        /// Auto：等到"没在打字、没在等选项、没在换格"且停留够 AutoDelay，就自己推进。
        /// ★ 玩家任何时候按一下都会重新计时（见 Tick），所以 Auto 开着也能随时手动加速。
        /// </summary>
        private void TickAuto()
        {
            if (!Auto)
            {
                return;
            }

            if (IsBlocked != null && IsBlocked())
            {
                m_AutoSince = Time.time;    // 换格/章末期间不算"在等"
                return;
            }

            if (IsWaitingChoice != null && IsWaitingChoice())
            {
                return;                     // 等玩家选选项：Auto 不代劳
            }

            if (IsTyping != null && IsTyping())
            {
                m_AutoSince = Time.time;    // 还在打字：读完再开始等
                return;
            }

            if (Time.time - m_AutoSince < AutoDelay)
            {
                return;
            }

            m_AutoSince = Time.time;
            Advance?.Invoke();
        }

        /// <summary>新的一句开始演了（打字开始时/换格放行时调）→ Auto 重新计时</summary>
        public void NotifyLineStarted()
        {
            m_AutoSince = Time.time;
        }

        /// <summary>「继续」：键盘 / 鼠标 / 继续按钮三条路都汇到这里</summary>
        public void Press()
        {
            // 同一帧只处理一次：键盘和按钮同帧都触发时，别一次点击跳两句
            if (Time.frameCount == m_LastPressFrame)
            {
                return;
            }
            m_LastPressFrame = Time.frameCount;

            if (IsBlocked != null && IsBlocked())
            {
                return;
            }

            if (IsTyping != null && IsTyping())
            {
                CompleteTyping?.Invoke();
                return;                 // 补全不算推进
            }

            if (IsWaitingChoice != null && IsWaitingChoice())
            {
                return;
            }

            Advance?.Invoke();
        }

        /// <summary>
        /// 把"这一帧的继续"作废。
        /// ★ 选项确认键和继续键是同一个（空格）：选完那一帧必须挡一下，
        ///   不然同一次按键会顺手把新组第一句的打字机补全掉。
        /// </summary>
        public void BlockThisFrame()
        {
            m_LastPressFrame = Time.frameCount;
        }

        private bool Pressed()
        {
            if (Input.GetKeyDown(ContinueKey))
            {
                return true;
            }

            if (EnterKeyToo && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                return true;
            }

            return ClickToContinue && Input.GetMouseButtonDown(0);
        }
    }
}
