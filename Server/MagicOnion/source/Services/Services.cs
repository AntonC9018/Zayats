using System;
using MagicOnion;
using MagicOnion.Server;
using Zayats.Net.Shared;

namespace Zayats.GameServer;


public class TestService : ServiceBase<ITestService>, ITestService
{
    // `UnaryResult<T>` allows the method to be treated as `async` method.
    public UnaryResult<int> SumAsync(int x, int y)
    {
        Console.WriteLine($"Received:{x}, {y}");
        return new(x + y);
    }
}