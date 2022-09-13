using System.Threading.Tasks;
using MagicOnion;
using MessagePack;
using Kari.Plugins.Flags;

namespace Zayats.Net.Shared
{
    public interface ITestService : IService<ITestService>
    {
        UnaryResult<int> SumAsync(int x, int y);
    }

    // Server -> Client
    public interface IGamingHubReceiver
    {
        void OnJoin(Player player);
        void OnLeave(Player player);
    }
    
    // Client -> Server
    public interface IGamingHub : IStreamingHub<IGamingHub, IGamingHubReceiver>
    {
        // Task<Player[]> GetPlayers(string room);
        Task<Player[]> JoinAsync(string roomName, string userName);
        Task LeaveAsync();
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
    public class Player
    {
        [Key(0)] public string Name { get; set; }
    }
}