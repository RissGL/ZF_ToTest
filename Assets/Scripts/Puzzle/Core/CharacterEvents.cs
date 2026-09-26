using ZGameFramework.Core;
using ZF.EraGallery;

namespace ZF.Puzzle
{
    /// <summary>有人换时代了。演出（走一段路 / 穿门 / 淡出淡入）、音效、成就都挂这个。</summary>
    public class CharacterMovedEvent : GameEvent
    {
        public string CharacterId;
        public EraId FromEra;
        public EraId ToEra;
    }

    /// <summary>
    /// 点名 / 取消点名了某个人（CharacterId 为空 = 取消）。
    /// 想给"选中"加光圈、加音效、显示名字，都挂这个。
    /// </summary>
    public class CharacterSelectedEvent : GameEvent
    {
        public string CharacterId;
    }
}
