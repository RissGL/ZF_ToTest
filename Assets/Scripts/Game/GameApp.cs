using ZGameFramework;
using ZF.EraGallery;
using ZF.Puzzle;

namespace ZF.Game
{
    /// <summary>
    /// 整个游戏唯一的架构入口（Model / System 都注册在这儿）。
    ///
    /// 为什么是一个而不是每个功能一个：时代窗口和谜题是互相咬着的 ——
    /// 谜题条件要读「玩家现在进到哪个时代里了」，谜题完成又要触发「这个时代通关了」。
    /// 分成两个架构的话，这些调用只能绕 EraGalleryApp.Interface 硬跳，事件总线（全局）能用但 Model/System 就割裂了。
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
        }
    }
}
