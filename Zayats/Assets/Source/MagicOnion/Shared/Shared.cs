using System.Threading.Tasks;
using MagicOnion;
using MessagePack;

namespace Zayats.Net.Shared
{
    public interface ITestService : IService<ITestService>
    {
        UnaryResult<int> SumAsync(int x, int y);
    }

    // Server -> Client definition
    public interface IGamingHubReceiver
    {
        // The method must have a return type of `void` and can have up to 15 parameters of any type.
        void OnJoin(Player player);
        void OnLeave(Player player);

    }
    
    // Client -> Server definition
    // implements `IStreamingHub<TSelf, TReceiver>`  and share this type between server and client.
    public interface IGamingHub : IStreamingHub<IGamingHub, IGamingHubReceiver>
    {
        // The method must return `Task` or `Task<T>` and can have up to 15 parameters of any type.
        Task<Player[]> JoinAsync(string roomName, string userName);
        Task LeaveAsync();
    }
    
    // for example, request object by MessagePack.
    [MessagePackObject]
    public class Player
    {
        [Key(0)]
        public string Name { get; set; }
    }
}