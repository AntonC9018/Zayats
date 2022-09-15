using System.Threading.Tasks;
using MagicOnion;
using MessagePack;
using Kari.Plugins.Flags;
using System.Collections.Generic;

namespace Zayats.Net.Shared
{
    using static Require;

    public interface ITestService : IService<ITestService>
    {
        UnaryResult<int> SumAsync(int x, int y);
    }

    public interface IRoomReceiver
    {
        void OnJoin(RoomPlayer player);
        void OnLeave(int playerIndex, LeaveRoomReason reason);
        void OnGameStart();
    }
    
    // Server -> Client
    public interface IGameReceiver
    {
        void OnUseItem(UseItemRequest useItem);
        void OnExecuteTurn(bool pass);
    }

    public interface IGamingHubReceiver : IGameReceiver, IRoomReceiver
    {
        void OnTest();
    }

    public enum LeaveRoomReason
    {
        Kicked,
        OwnDecision,
    }

    [NiceFlags]
    public enum Require
    {
        Authorization = 1 << 0,
        RoomMember = 1 << 1,
        _RoomOwner = 1 << 2,
        RoomOwner = RoomMember | _RoomOwner,
        InGame = 1 << 3,
        NotInGame = 1 << 4,
        OwnTurn = 1 << 5,
        NotInRoom = 1 << 6,
    }

    public class RequireAttribute : System.Attribute
    {
        public Require Flags { get; set; }
        public RequireAttribute(Require flags)
        {
            Flags = flags;
        }
    }

    public interface IGameOperations
    {
        // Game logic
        [Require(Authorization | InGame | OwnTurn)]
        Task UseItem(UseItemRequest request);

        [Require(Authorization | InGame | OwnTurn)]
        Task ExecuteTurn(bool pass = false);
    }

    public interface IRoomOperations
    {
        // Can be used while in game to leave both the game and the room.
        [Require(Authorization | RoomMember)]
        Task LeaveRoom();

        [Require(Authorization | RoomOwner | NotInGame)]
        Task KickPlayer();

        [Require(Authorization | RoomOwner | NotInGame)]
        Task StartGame();
    }

    public interface IGlobalOperations
    {
        Task<GeneralPlayerInfo> Login(string login, string password);
        Task<GeneralPlayerInfo[]> GetPlayers(int maxPlayers);
        Task<Room> GetRoom(RoomId room);
        Task<Room[]> FindRooms(SearchQuery query);
        
        [Require(Authorization | NotInRoom)]
        Task<Room> JoinRoom(RoomId room);

        // Needed for synchronization.
        // May return null.
        [Require(Authorization)]
        Task<Room> GetRoomImIn();

        // Needed for synchronization or loading a watched game.
        [Require(Authorization)]
        Task<GameSynchronization> SyncGame(RoomId room);

        [Require(Authorization)]
        Task<RoomId?> CreateRoom();
    }

    // Client -> Server
    public interface IGamingHub : IGameOperations, IRoomOperations, IGlobalOperations,
        IStreamingHub<IGamingHub, IGamingHubReceiver>
    {
    }

    public interface IGamingHubService : IGameOperations, IRoomOperations, IGlobalOperations,
        IService<IGamingHubService>
    {
        Task Test2();
    }

    [MessagePackObject]
    public struct UseItemRequest
    {
        [Key(0)] public int ItemIndex;
        [Key(1)] public int[] Targets;
    }

    [MessagePackObject]
    public struct GameSynchronization
    {
        [Key(0)] public byte[] SerializedGameState { get; set; }
        [Key(1)] public int[] PlayerIndexMap { get; set; }
    }

    [MessagePackObject]
    public struct LoginResult
    {
        [Key(0)] public PlayerId? Id { get; set; }
        [Key(1)] public LoginStatus Status { get; set; }
    }

    [NiceFlags]
    public enum LoginStatus
    {
        Success = 0,
        InvalidName = 1 << 0,
        InvalidPassword = 1 << 1,
        IncorrectNameOrPassword = 1 << 2,
    }

    [MessagePackObject]
    public struct SearchQuery
    {
        [Key(0)] public string RawText { get; set; }
    }

    [MessagePackObject]
    public struct RoomId
    {
        [Key(0)] public string Name { get; set; }
        public readonly override string ToString() => Name;
    }

    [MessagePackObject]
    public class Room
    {
        [Key(0)] public RoomId Id { get; set; }
        [Key(1)] public RoomFlags Flags { get; set; }
        [Key(2)] public List<RoomPlayer> Players { get; set; }
        [Key(3)] public GeneralGameInfo Game { get; set; }
        [Key(4)] public int OwnerIndex { get; set; }
        
        // For now not under a key
        public string Name => Id.Name;
        public RoomPlayer Owner => Players[OwnerIndex]; 
    }

    [MessagePackObject]
    public class GeneralGameInfo
    {
        [Key(0)] public string GameModeName { get; set; }
    }

    [NiceFlags]
    public enum RoomFlags
    {
        RequiresPassword,
        RequiresName,
    }

    public enum StreamingHubState
    {
        NotInRoom,
        InRoom,
        InGame,
    }
    
    [MessagePackObject]
    public struct PlayerId
    {
        [Key(0)] public string Name { get; set; }
    }

    [MessagePackObject]
    public struct Avatar
    {
        [Key(0)] public string Url { get; set; }
    }

    [MessagePackObject]
    public class RoomPlayer
    {
        [Key(0)] public PlayerId Id { get; set; }
        [Key(1)] public Avatar Avatar { get; set; }
    }

    [MessagePackObject]
    public class GeneralPlayerInfo
    {
        [Key(0)] public PlayerId Id { get; set; }
        [Key(1)] public Avatar Avatar { get; set; }
        [Key(2)] public string Name { get; set; }
    }
}