using MagicOnion;

namespace Zayats.GameServer.Shared
{
    public interface ITestService : IService<ITestService>
    {
        UnaryResult<int> SumAsync(int x, int y);
    }
}