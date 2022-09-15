using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MagicOnion;
using MagicOnion.Server;
using MagicOnion.Server.Hubs;
using MessagePack;
using Zayats.Core;
using Zayats.Net.Shared;

namespace Zayats.GameServer;


public class TestService : ServiceBase<ITestService>, ITestService
{
    public UnaryResult<int> SumAsync(int x, int y)
    {
        Console.WriteLine($"Received:{x}, {y}");
        return new(x + y);
    }
}

public class RoomState
{
    public RoomFlags Flags;
    public List<PlayerId> Players;
    public int OwnerIndex;
    public GameContext OnGoingGame;
}

public class PlayerState
{
    public RoomId Room;
}

public class SharedState
{
    public ConcurrentDictionary<RoomId, Room> _rooms;
    public ConcurrentDictionary<PlayerId, PlayerState> _players;
}

public class GamingHub : StreamingHubBase<IGamingHub, IGameReceiver>, IGamingHub
{
    private SharedState _sharedState;
    private IGroup _room;

    public GamingHub(SharedState sharedState)
    {
        _sharedState = sharedState;
        Group.RawGroupRepository
    }

    public Task UseItem(UseItemRequest request)
    {
        throw new NotImplementedException();
    }

    public Task ExecuteTurn(bool pass = false)
    {
        throw new NotImplementedException();
    }

    public Task LeaveRoom()
    {
        throw new NotImplementedException();
    }

    public Task KickPlayer()
    {
        throw new NotImplementedException();
    }

    public Task StartGame()
    {
        throw new NotImplementedException();
    }

    public Task<GeneralPlayerInfo> Login(string login, string password)
    {
        throw new NotImplementedException();
    }

    public Task<GeneralPlayerInfo[]> GetPlayers(int maxPlayers)
    {
        throw new NotImplementedException();
    }

    public Task<Room> GetRoom(RoomId room)
    {
        throw new NotImplementedException();
    }

    public Task<Room[]> FindRooms(SearchQuery query)
    {
        throw new NotImplementedException();
    }

    public async Task<Room> JoinRoom(RoomId room)
    {
        string roomName = room.Name;
        var group = await Group.AddAsync(roomName);
        group.AddAsync()
    }

    public Task<Room> GetRoomImIn()
    {
        throw new NotImplementedException();
    }

    public Task<GameSynchronization> SyncGame(RoomId room)
    {
        throw new NotImplementedException();
    }

    public Task<RoomId?> CreateRoom()
    {
        throw new NotImplementedException();
    }
}