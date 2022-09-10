using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using MagicOnion.Client;
using UnityEngine;
using Zayats.Net.Shared;

namespace Zayats.Unity.Net
{
    public class GamingHubClient : IGamingHubReceiver
    {
        Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();
    
        IGamingHub client;
    
        public async Task<GameObject> ConnectAsync(ChannelBase grpcChannel, string roomName, string playerName)
        {
            this.client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(grpcChannel, this);
    
            var roomPlayers = await client.JoinAsync(roomName, playerName);
            foreach (var player in roomPlayers)
            {
                (this as IGamingHubReceiver).OnJoin(player);
            }
    
            return players[playerName];
        }
    
        // methods send to server.
    
        public async Task LeaveAsync()
        {
            await client.LeaveAsync();
        }
    
        // dispose client-connection before channel.ShutDownAsync is important!
        public Task DisposeAsync()
        {
            return client.DisposeAsync();
        }
    
        // You can watch connection state, use this for retry etc.
        public Task WaitForDisconnect()
        {
            return client.WaitForDisconnect();
        }
    
        // Receivers of message from server.
    
        void IGamingHubReceiver.OnJoin(Player player)
        {
            Debug.Log("Join Player:" + player.Name);
    
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = player.Name;
            players[player.Name] = cube;
        }
    
        void IGamingHubReceiver.OnLeave(Player player)
        {
            Debug.Log("Leave Player:" + player.Name);
    
            if (players.TryGetValue(player.Name, out var cube))
            {
                GameObject.Destroy(cube);
            }
        }
    }
}