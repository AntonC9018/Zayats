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
        Dictionary<string, GameObject> _players;
        IGamingHub _client;
    
        public async Task<GameObject> ConnectAsync(ChannelBase grpcChannel, string roomName, string playerName)
        {
            this._client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(grpcChannel, this);

            var roomPlayers = await _client.JoinAsync(roomName, playerName);

            _players = new();
            foreach (var player in roomPlayers)
                (this as IGamingHubReceiver).OnJoin(player);
    
            return _players[playerName];
        }
    
        // methods send to server.
    
        public async Task LeaveAsync()
        {
            await _client.LeaveAsync();
        }
    
        // dispose client-connection before channel.ShutDownAsync is important!
        public Task DisposeAsync()
        {
            return _client.DisposeAsync();
        }
    
        // You can watch connection state, use this for retry etc.
        public Task WaitForDisconnect()
        {
            return _client.WaitForDisconnect();
        }
    
        // Receivers of message from server.
    
        void IGamingHubReceiver.OnJoin(Player player)
        {
            if (_players is null)
                return;

            Debug.Log("Join Player:" + player.Name);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = player.Name;
            _players[player.Name] = cube;
        }
    
        void IGamingHubReceiver.OnLeave(Player player)
        {
            if (_players is null)
                return;

            Debug.Log("Leave Player:" + player.Name);
            if (_players.TryGetValue(player.Name, out var cube))
                GameObject.Destroy(cube);
        }
    }
}