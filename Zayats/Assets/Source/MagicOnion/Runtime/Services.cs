using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using MagicOnion.Client;
using UnityEngine;
using Zayats.Net.Shared;

namespace Zayats.Unity.Net
{
    public class GameClient : IGamingHubReceiver
    {
        private IGamingHub _client;

        private PlayerId? _playerIdentity;
        private StreamingHubState _roomState;

        private bool IsLoggedIn => _playerIdentity.HasValue;
    
        public async Task ConnectAsync(ChannelBase grpcChannel, string roomName, string playerName)
        {
            _client = await StreamingHubClient.ConnectAsync<IGamingHub, IGameReceiver>(grpcChannel, this);
        }

        public async Task LeaveAsync()
        {
            await _client.LeaveRoom();
        }
    
        public Task DisposeAsync()
        {
            return _client.DisposeAsync();
        }
    
        public Task WaitForDisconnect()
        {
            return _client.WaitForDisconnect();
        }

        void IGameReceiver.OnUseItem(UseItemRequest useItem)
        {
            throw new System.NotImplementedException();
        }

        void IGameReceiver.OnExecuteTurn(bool pass)
        {
            throw new System.NotImplementedException();
        }

        void IRoomReceiver.OnJoin(RoomPlayer player)
        {
            throw new System.NotImplementedException();
        }

        void IRoomReceiver.OnLeave(int playerIndex, LeaveRoomReason reason)
        {
            throw new System.NotImplementedException();
        }

        void IRoomReceiver.OnGameStart()
        {
            throw new System.NotImplementedException();
        }
    }
}