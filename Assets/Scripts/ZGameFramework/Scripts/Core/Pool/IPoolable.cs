namespace ZGameFramework.Core
{
    public interface IPoolable
    {
        void OnRecycled();
        void OnGet();
    }
}