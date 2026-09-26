using UnityEngine;
using UnityEngine.InputSystem;
using ZGameFramework;
using ZGameFramework.Utility;
using ZF.EraGallery;
using ZF.Game;

namespace ZF.Puzzle
{
    /// <summary>
    /// 谜题那边的鼠标：悬停高亮 + 点击交互 + 切换手里的道具。
    /// 和 EraWorldController 各自扫自己的东西，互不干扰 ——
    /// 因为「已经进到某个时代里」的时候，EraWorldController 那一点会走 TryFocus(同一个窗口) 直接被规则挡掉。
    /// </summary>
    [DisallowMultipleComponent]
    public class PuzzlePointerController : MonoBehaviour, IController
    {
        [Label("主相机（留空用 Camera.main）")]
        [SerializeField] private Camera mainCamera;

        [Label("切换手里道具的按键")]
        [SerializeField] private Key cycleKey = Key.Tab;

        [Label("打印每次交互的命中结果")]
        [SerializeField] private bool logInteractions = true;

        /// <summary>只看 trigger，且不受 Physics2D.queriesHitTriggers 这个全局开关影响。</summary>
        private static readonly ContactFilter2D PickFilter = CreatePickFilter();

        private readonly Collider2D[] m_PickResults = new Collider2D[8];

        private IPuzzleModel m_Model;
        private IEraWindowModel m_EraModel;
        private StateVisualBehaviour m_Hovered;
        public IArchitecture GetArchitecture() => GameApp.Interface;

        private void Awake()
        {
            m_Model = this.GetModel<IPuzzleModel>();
            m_EraModel = this.GetModel<IEraWindowModel>();

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            this.RegisterEvent<PuzzleInteractionEvent>(OnInteraction).UnregisterOnDestroyTrigger(this);
        }

        private void OnDestroy()
        {
            SetHovered(null);
        }

        private void Update()
        {
            if (mainCamera == null || m_Model == null || m_EraModel == null)
            {
                return;
            }

            // 只有「已经进到某个时代窗口里」才响应物体交互。
            // 全景下点物体应该是"进那个时代"，不该顺手把物体也点了。
            if (m_EraModel.FocusState.Value != EraFocusState.Focused)
            {
                SetHovered(null);
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 screenPoint = mouse.position.ReadValue();
            Vector3 worldPoint = mainCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, 0f));

            SetHovered(PickBehaviour(worldPoint));

            if (mouse.leftButton.wasPressedThisFrame && m_Hovered != null)
            {
                this.SendCommand(new InteractCommand(m_Hovered.InteractionId));
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[cycleKey].wasPressedThisFrame)
            {
                this.SendCommand<CycleSelectedItemCommand>();
            }
        }

        private StateVisualBehaviour PickBehaviour(Vector2 worldPoint)
        {
            int count = Physics2D.OverlapPoint(worldPoint, PickFilter, m_PickResults);

            for (int i = 0; i < count; i++)
            {
                Collider2D collider = m_PickResults[i];
                if (collider == null)
                {
                    continue;
                }

                // 物件 / 人物优先于它所在的时代窗口（窗口那个大 trigger 也盖着同一点）
                StateVisualBehaviour behaviour = collider.GetComponentInParent<StateVisualBehaviour>();
                if (behaviour != null)
                {
                    return behaviour;
                }
            }

            return null;
        }

        private void SetHovered(StateVisualBehaviour next)
        {
            if (m_Hovered == next)
            {
                return;
            }

            if (m_Hovered != null)
            {
                m_Hovered.SetHover(false);
            }

            m_Hovered = next;

            if (m_Hovered != null)
            {
                m_Hovered.SetHover(true);
            }
        }

        private void OnInteraction(PuzzleInteractionEvent e)
        {
            if (!logInteractions)
            {
                return;
            }

            string item = string.IsNullOrEmpty(e.UsedItemId) ? "" : $"（用 {e.UsedItemId}）";
            Debug.Log(e.Matched
                ? $"[谜题] 点 {e.TargetId}{item} → 命中规则「{e.RuleNote}」"
                : $"[谜题] 点 {e.TargetId}{item} → 没有规则命中");
        }

        private static ContactFilter2D CreatePickFilter()
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = true;
            return filter;
        }
    }
}
