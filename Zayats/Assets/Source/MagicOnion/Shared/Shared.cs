using MagicOnion;

namespace Zayats.Net.Shared
{
    public interface ITestService : IService<ITestService>
    {
        UnaryResult<int> SumAsync(int x, int y);
    }
}