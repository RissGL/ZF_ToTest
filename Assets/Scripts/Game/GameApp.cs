using ZGameFramework;
using ZF.EraGallery;
using ZF.Puzzle;

namespace ZF.Game
{
    /// <summary>
    /// 整个游戏唯一的架构入口（Model / System 都注册在这儿）。
    ///
    /// 为什么是一个而不是每个功能一个：时代窗口、谜题、人物三块是互相咬着的 ——
    /// 谜题条件要读「玩家现在进到哪个时代里了」「谁现在在哪个时代」，
    /// 谜题完成要触发「这个时代通关了」，人物搬家又要让谜题条件重算。
    /// 分成多个架构的话这些调用只能互相硬跳，Model/System 就割裂了。
    /// 所以一个游戏一个架构，功能靠命名空间和文件夹分。
    /// </summary>
    public class GameApp : GameArchitecture<GameApp>
    {
        protected override void OnInit()
        {
            // ---- 四个时代窗口 ----
            this.RegisterModel<IEraWindowModel>(new EraWindowModel());
            this.RegisterSystem<IEraWindowSystem>(new EraWindowSystem());

            // ---- 场景解密 ----
            this.RegisterModel<IPuzzleModel>(new PuzzleModel());
            this.RegisterSystem<IPuzzleSystem>(new PuzzleSystem());

            // ---- 人物 ----
            this.RegisterModel<ICharacterModel>(new CharacterModel());
            this.RegisterSystem<ICharacterSystem>(new CharacterSystem());
        }
    }
}
