namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 模板来源：「给我一个 templateId，还我一个能用的布局」。
    ///
    /// 为什么要有这个口：换格编排只关心"这一刻该显示哪一份布局"，
    /// 不该关心模板是**从 prefab 现场实例化**、还是对象池里捞的、还是异步加载进来的。
    /// 默认实现是 <see cref="DialogueTemplateHost"/>（表 → Prefab → 隐藏保留复用），
    /// 以后要换异步加载 / 资源引用方案，只需要再写一个实现，编排代码一行都不用动。
    ///
    /// 约定：
    ///   - `Activate` 返回 true 之后，`Current` 必须是可用的布局（已经初始化过、已经激活）
    ///   - 返回 false = 换不了（表里没有、也配了兜底还是找不到），调用方保持原来那份布局不动
    ///   - `CurrentTemplateId` 用来判断"是不是已经在用这一份了"（同 id 就不用换）
    /// </summary>
    public interface IDialogueTemplateProvider
    {
        /// <summary>现在激活的布局（null = 还没有）</summary>
        DialogueLayoutRefs Current { get; }

        /// <summary>当前模板 id（-1 = 还没有）</summary>
        int CurrentTemplateId { get; }

        /// <summary>
        /// 切到某个 templateId；false = 换不了。
        ///
        /// <paramref name="holdPrevious"/> = 溶解/交叉类擦除要"**旧画面活到擦完**"：
        /// 实现应当把旧的那一份保留着（压在新的上面），擦完由调用方调 ReleasePrevious 放掉。
        /// 不支持保留的实现可以忽略这个参数（退化成普通切换，溶解会缺旧画面）。
        /// </summary>
        bool Activate(int templateId, bool holdPrevious = false);
    }
}
