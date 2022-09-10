using System;
using System.Linq;
using System.Threading.Tasks;
using MagicOnion;
using MagicOnion.Server;
using MagicOnion.Server.Hubs;
using MessagePack;
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

// Server implementation
// implements : StreamingHubBase<THub, TReceiver>, THub
public class GamingHub : StreamingHubBase<IGamingHub, IGamingHubReceiver>, IGamingHub
{
    // this class is instantiated per connected so fields are cache area of connection.
    private IGroup _room;
    private Player _self;
    private IInMemoryStorage<Player> _storage;

    public async Task<Player[]> JoinAsync(string roomName, string userName)
    {
        Console.WriteLine(userName + " joined.");
        _self = new Player() { Name = userName };

        // Group can bundle many connections and it has inmemory-storage so add any type per group. 
        (_room, _storage) = await Group.AddAsync(roomName, _self);

        // Typed Server->Client broadcast.
        Broadcast(_room).OnJoin(_self);

        return _storage.AllValues.ToArray();
    }

    public async Task LeaveAsync()
    {
        await _room.RemoveAsync(this.Context);
        Broadcast(_room).OnLeave(_self);
    }

    // You can hook OnConnecting/OnDisconnected by override.
    protected override ValueTask OnDisconnected()
    {
        // on disconnecting, if automatically removed this connection from group.
        return CompletedTask;
    }
}