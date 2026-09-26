using System.Collections.Generic;
using UnityEngine;
using ZGameFramework;
using ZF.EraGallery;

namespace ZF.Puzzle
{
    /// <summary>
    /// 人物迁移的规则层：谁能搬到哪、一次搬几个。
    ///
    /// 「通过一定方式移动」里的**方式**不在这里 —— 方式就是规则表里某条规则（点时间裂隙、解开谜题、
    /// 用某个道具…），那条规则的效果里放一个 MoveCharacterEffect / MoveEraCharactersEffect 就行。
    /// 这里只负责把状态改掉、发事件、让视图跟着走。
    /// </summary>
    public interface ICharacterSystem : ISystem
    {
        /// <summary>把一个人搬到某个时代。真的搬动了才返回 true（已经在那个时代 / 不认识这个人则 false）。</summary>
        bool TryMove(string characterId, EraId targetEra);

        /// <summary>把一个时代里的人整体搬到另一个时代（「这个时代通关了，人一起走」）。返回实际搬了几个。</summary>
        int TryMoveEra(EraId fromEra, EraId targetEra);

        /// <summary>
        /// 点名某个人（传 "" 取消）。点名的那个就是「单独送走」的对象 ——
        /// 规则里的 MoveSelectedCharacterEffect 送的就是他。
        /// </summary>
        bool Select(string characterId);

        /// <summary>
        /// 【动画接口】要求某个人播一段动画。characterId 留空 = 当前点名的那个。
        /// 这只是**发事件**（`CharacterAnimationEvent`），状态一个都不改 ——
        /// 所以规则表里可以把它和 Move* 效果用 `delayBefore` 排成
        /// 「播离场动画 → 等一会儿 → 真搬 → 播到场动画」这样一条链。
        /// </summary>
        bool PlayAnimation(string characterId, string clip);

        /// <summary>所有人回到自己开局的年代。</summary>
        void ResetAll();
    }

    public class CharacterSystem : AbstractSystem, ICharacterSystem
    {
        protected override void OnInit()
        {
        }

        public bool TryMove(string characterId, EraId targetEra)
        {
            ICharacterModel model = this.GetModel<ICharacterModel>();
            if (model == null)
            {
                return false;
            }

            if (!model.IsKnown(characterId))
            {
                Debug.LogWarning($"[人物] 场上没有 id 为「{characterId}」的人物（CharacterView 没挂、或者 id 打错了），搬不动。");
                return false;
            }

            EraId from = model.GetEra(characterId);
            if (from == targetEra)
            {
                return false;
            }

            model.SetEra(characterId, targetEra);

            this.SendEvent(new CharacterMovedEvent
            {
                CharacterId = characterId,
                FromEra = from,
                ToEra = targetEra,
            });

            // 人的位置变了 → 「某个时代里有几个人」这类谜题条件可能跟着变。
            // 注意：ISystem 上没有 GetSystem（只有 GetModel），所以要绕架构拿一次。
            this.GetArchitecture().GetSystem<IPuzzleSystem>()?.RecheckPuzzles();
            return true;
        }

        public int TryMoveEra(EraId fromEra, EraId targetEra)
        {
            if (fromEra == targetEra)
            {
                return 0;
            }

            ICharacterModel model = this.GetModel<ICharacterModel>();
            if (model == null)
            {
                return 0;
            }

            List<string> moving = model.InEra(fromEra);

            int moved = 0;
            for (int i = 0; i < moving.Count; i++)
            {
                if (TryMove(moving[i], targetEra))
                {
                    moved++;
                }
            }

            return moved;
        }

        public bool Select(string characterId)
        {
            ICharacterModel model = this.GetModel<ICharacterModel>();
            if (model == null)
            {
                return false;
            }

            string value = characterId ?? "";

            if (!string.IsNullOrEmpty(value) && !model.IsKnown(value))
            {
                Debug.LogWarning($"[人物] 场上没有 id 为「{value}」的人物，点不了名。");
                return false;
            }

            if (PuzzleOps.SameState(model.SelectedCharacter.Value, value))
            {
                return true;   // 已经是他了（或者本来就是空手），不算失败
            }

            model.SetSelectedCharacter(value);

            this.SendEvent(new CharacterSelectedEvent { CharacterId = value });
            return true;
        }

        public bool PlayAnimation(string characterId, string clip)
        {
            ICharacterModel model = this.GetModel<ICharacterModel>();

            // 留空 = 点名的那个人
            string target = !string.IsNullOrEmpty(characterId)
                ? characterId
                : model != null ? model.SelectedCharacter.Value : "";

            if (string.IsNullOrEmpty(target) || model == null || !model.IsKnown(target))
            {
                return false;
            }

            this.SendEvent(new CharacterAnimationEvent { CharacterId = target, Clip = clip ?? "" });
            return true;
        }

        public void ResetAll()
        {
            this.GetModel<ICharacterModel>()?.ResetAll();
            this.GetArchitecture().GetSystem<IPuzzleSystem>()?.RecheckPuzzles();
        }
    }
}
