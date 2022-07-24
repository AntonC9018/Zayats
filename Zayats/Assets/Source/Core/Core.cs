using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Zayats.Core
{
    public static class Assert
    {
        public class AssertionException : Exception
        {
            public AssertionException(string message) : base(message)
            {
            }
        }

        [Conditional("DEBUG")]
        public static void Fail(string message = "")
        {
            throw new AssertionException(message);
        }

        [Conditional("DEBUG")]
        public static void assert(bool condition, string message = "")
        {
            if (condition == false)
                Fail(message);
        }

        [Conditional("DEBUG")]
        public static void assertEqual<T>(T a, T b, string message = "")
        {
            if (!a.Equals(b))
                Fail(message);
        }

        [Conditional("DEBUG")]
        public static void assertNoneNull(object[] vals, string message = "")
        {
            if (vals.Any(a => a == null))
                Fail(message + ". Elements were all null.");
        }
    }
}
namespace Zayats.Core
{
    using static Zayats.Core.Assert;

    public static class Initialization
    {
        public static GameContext CreateGameContext(int cellCountNotIncludingFirstAndLast)
        {
            GameContext game = new();
            game.EventHandlers = Events.CreateStorage();

            {
                var components = new object[Components.Count];
                game.State.ComponentsByType = components;
                // assertNoneNull(components);
            }

            {
                var cells = new Data.Cell[cellCountNotIncludingFirstAndLast + 2];
                game.State.Board.Cells = cells;
                foreach (ref var cell in cells.AsSpan())
                    cell.Things = new();
            }

            return game;
        }

        public struct GameInitializationContext
        {
            public GameContext Game;
            public int CurrentId;
        }

        public static void InitializePlayers(this ref GameInitializationContext initContext, int playerCount)
        {
            var players = new Data.Player[playerCount];
            initContext.Game.State.Players = players;

            for (int i = 0; i < playerCount; i++)
            {
                players[i] = new()
                {
                    Bonuses = new(),
                    Items = new(),
                    Position = 0,
                    Stats = Stats.CreateStorage(),
                    Events = Events.CreateStorage(),
                };
            }
        }

        public static void InitializePlayer(this GameContext game, int index, int thingId, ComponentStorage<Components.Player> playerStorage)
        {
            ref var player = ref game.State.Players[index];
            player.ThingId = thingId;
            playerStorage.Add(thingId).Value.PlayerIndex = index;
        }

        public struct MineInitializationContext
        {
            public int EternalMineCount;
            public int NormalMineCount;
            public IRandom Random;
            public Action<int, int> OnMineSpawned;
        }
    }

    public static class Logic
    {
        public enum MovementDetails
        {
            Normal = 0,
            HopOverPlayer = 1,
        }
        public struct MovementContext
        {
            public int PlayerIndex;
            public int Amount;
            public MovementDetails Details;
        }

        public static void MovePlayerInBetweenCells(this GameContext game, ref Data.Player player, int toPosition)
        {
            player.GetCell(game).Things.Remove(player.ThingId);
            player.Position = toPosition;
            game.TriggerSingleThingRemovedFromCellEvent(player.ThingId, player.Position);
            player.GetCell(game).Things.Add(player.ThingId);
            game.TriggerSingleThingAddedToCellEvent(player.ThingId, toPosition);
        }

        public static bool MaybeEndGame(this GameContext game, int playerIndex)
        {
            if (CheckFinish(game.State.Board, game.State.Players[playerIndex].Position))
            {
                game.State.IsOver = true;
                game.HandlePlayerEvent(Events.OnPlayerWon, playerIndex, playerIndex);
                return true;
            }
            return false;
        }

        // Returns true if the game ends as a result of the move.
        public static bool DoPlayerMove(this GameContext game, MovementContext context)
        {
            ref var player = ref game.State.Players[context.PlayerIndex];
            
            int startingPosition = player.Position;
            var toPosition = Math.Min(player.Position + context.Amount, game.State.Board.Cells.Length - 1);
            MovePlayerInBetweenCells(game, ref player, toPosition);

            game.HandlePlayerEvent(Events.OnMoved, context.PlayerIndex, new()
            {
                Movement = context,
                StartingPosition = startingPosition,
            });
            game.HandlePlayerEvent(Events.OnPositionChanged, context.PlayerIndex, new()
            {
                PlayerIndex = context.PlayerIndex,
                StartingPosition = startingPosition,
            });
            return game.MaybeEndGame(context.PlayerIndex);
        }

        public static void MovePlayer(this GameContext game, MovementContext context)
        {
            ref var player = ref game.State.Players[context.PlayerIndex];

            if (DoPlayerMove(game, context))
                return;
            
            int positionAfterMove = player.Position;
            game.CollectPickupsAtCurrentPosition(context.PlayerIndex);
            // Collecting items has triggered a move, so it's been completed already.
            if (player.Position != positionAfterMove)
                return;

            

            // Jumping over other players.
            var players = game.GetDataInCell(Components.PlayerId, player.Position).GetEnumerator();
            while (players.MoveNext())
            {
                var otherPlayerIndex = players.Current.Component.PlayerIndex;
                if (otherPlayerIndex == context.PlayerIndex)
                    continue;
                
                ref var otherPlayer = ref game.State.Players[otherPlayerIndex];

                var contextCopy = new MovementContext
                {
                    PlayerIndex = context.PlayerIndex,
                    Details = MovementDetails.HopOverPlayer,
                    Amount = 1,
                };
                MovePlayer(game, contextCopy);
                return;
            }
        }

        public static void CollectPickupsAtCurrentPosition(this GameContext game, int playerIndex)
        {
            CollectPickupsAtPosition(game, playerIndex, game.State.Players[playerIndex].Position);
        }

        public static void CollectPickupsAtPosition(this GameContext game, int playerIndex, int position)
        {
            var pickupsInCell = game.GetDataInCell(Components.PickupActionId, position);
            var pickupInfos = pickupsInCell.Select(a => (Pickup: a.Component, a.ListIndex, a.ThingId)).Reverse().ToArray();

            ItemInteractionInfo info;
            info.Game = game;
            info.PlayerIndex = playerIndex;
            info.Position = position;
            
            var things = game.State.Board.Cells[position].Things;
            
            // They have to be removed before their effect is executed,
            // because the effect may mess with the cell's content.
            // This is also why we have to enumerate the data in the cell.
            foreach (var pickupInfo in pickupInfos)
            {
                info.ThingId = pickupInfo.ThingId;
                if (pickupInfo.Pickup.ShouldRemoveFromCellOnPickup(info))
                    things.RemoveAt(pickupInfo.ListIndex);
            }

            game.TriggerThingsRemovedFromCellEvent(pickupInfos.Select(i => i.ThingId), pickupInfos.Length, position);

            foreach (var pickupInfo in pickupInfos)
            {
                info.ThingId = pickupInfo.ThingId;
                DoPickupEffectWithEvent(pickupInfo.Pickup, info);
                if (pickupInfo.Pickup.IsInventoryItem(info))
                    AddItemToInventory(info);
            }
        }

        public static void DoPickupEffectWithEvent(this IPickup pickup, ItemInteractionInfo info)
        {
            pickup.DoPickupEffect(info);
            info.Game.HandlePlayerEvent(Events.OnThingPickedUp, info.PlayerIndex, ref info);
        }

        public static void AddThingToShop(this GameContext game, int thingId)
        {
            game.State.Shop.Items.Add(thingId);
        }

        // public static void DoDefaultPickupEffectWithEvent(ItemInteractionInfo info)
        // {
        //     PlayerInventoryPickup.DoDropEffect(info);
        //     info.Game.HandlePlayerEvent(Events.OnThingPickedUp, info.PlayerIndex, new()
        //     {
        //         PlayerIndex = info.PlayerIndex,
        //         ThingId = info.Position,
        //     });
        // }

        public static void AddItemToInventory(ItemInteractionInfo info)
        {
            info.Game.State.Players[info.PlayerIndex].Items.Add(info.ThingId);
            info.Game.HandlePlayerEvent(Events.OnItemAddedToInventory, info.PlayerIndex, ref info);
        }

        public static void RemoveItemFromInventory(ItemInteractionInfo info)
        {
            info.Game.State.Players[info.PlayerIndex].Items.Remove(info.ThingId);
            info.Game.HandlePlayerEvent(Events.OnItemRemovedFromInventory, info.PlayerIndex, ref info);
        }

        public static void DestroyThing(this GameContext game, int thingId)
        {
            // ??
        }

        public struct KillPlayerContext
        {
            public int PlayerIndex;
            public Data.Reason Reason;
        }
        public static bool KillPlayer(this GameContext game, KillPlayerContext context)
        {
            ref var player = ref game.State.Players[context.PlayerIndex];

            {
                var saveContext = new Events.SavePlayerContext
                {
                    Kill = context,
                    WasSaved = false,
                };
                game.HandlePlayerEvent(Events.OnTrySavePlayer, context.PlayerIndex, ref saveContext);

                if (saveContext.WasSaved)
                {
                    game.HandlePlayerEvent(Events.OnPlayerSaved, context.PlayerIndex, new()
                    {
                        Kill = context,
                        SaveReason = saveContext.SaveReason,
                    });
                    return false;
                }
            }

            int startingPosition = player.Position;
            int respawnPosition = GetRespawnPositionByPoppingRespawnPoint(game, context.PlayerIndex);
            
            MovePlayerInBetweenCells(game, ref player, respawnPosition);

            game.HandlePlayerEvent(Events.OnPlayerDied, context.PlayerIndex, context);
            game.HandlePlayerEvent(Events.OnPositionChanged, context.PlayerIndex, new()
            {
                PlayerIndex = context.PlayerIndex,
                StartingPosition = startingPosition,
                Reason = context.Reason,
            });

            return true;
        }

        public static int GetRespawnPositionByPoppingRespawnPoint(this GameContext game, int playerIndex)
        {
            const int defaultPosition = 0;
            int playerId = game.State.Players[playerIndex].ThingId;
            if (game.TryGetComponent(Components.RespawnPointIdsId, playerId, out var respawn))
            {
                var respawnStack = respawn.Value;
                if (respawnStack is null || respawnStack.Count == 0)
                    return defaultPosition;

                int respawnPointId = respawnStack.Pop();
                int respawnPosition = game.GetComponent(Components.RespawnPositionId, respawnPointId).Value;

                game.HandlePlayerEvent(Events.OnRespawnPositionPopped, playerId, new()
                {
                    RespawnPointId = respawnPointId,
                    RespawnPosition = respawnPosition,
                    RespawnPointIds = respawnStack,
                    PlayerIndex = playerIndex,
                });
            }
            return defaultPosition;
        }

        public static void PushRespawnPoint(this GameContext game, int playerIndex, int respawnPointId)
        {
            var respawnPointStorage = game.GetComponentStorage(Components.RespawnPointIdsId);
            int playerId = game.State.Players[playerIndex].ThingId;
            if (!respawnPointStorage.TryGetProxy(playerId, out var respawnPointsStackProxy))
                respawnPointsStackProxy = respawnPointStorage.Add(playerId);
            var stack = respawnPointsStackProxy.Value ??= new();
            stack.Push(respawnPointId);

            game.HandlePlayerEvent(Events.OnRespawnPositionPushed, playerId, new()
            {
                PlayerIndex = playerIndex,
                RespawnPointId = respawnPointId,
                RespawnPointIds = stack,
                RespawnPosition = game.GetComponent(Components.RespawnPositionId, respawnPointId).Value,
            });
        }

        public static int RollAmount(this GameContext game, int playerIndex)
        {
            ref var player = ref game.State.Players[playerIndex];
            int rolledValue = game.Random.GetInt(1, 6);
            int rollBonus = player.Stats.Get(Stats.RollAdditiveBonus);

            // Questionable
            game.HandlePlayerEvent(Events.OnAmountRolled, playerIndex, new()
            {
                PlayerIndex = playerIndex,
                BonusAmount = rollBonus,
                RolledAmount = rolledValue,
            });

            return rolledValue + rollBonus;
        }

        public static bool CheckFinish(in Data.Board board, int position)
        {
            return position >= board.Cells.Length - 1;
        }

        public static void EndCurrentPlayersTurn(this GameContext game)
        {
            ref int a = ref game.State.CurrentPlayerId;
            int previousPlayer = a;
            a = (a + 1) % game.State.Players.Length;
            int currentPlayer = a;

            game.HandleEvent(Events.OnNextTurn, new()
            {
                PreviousPlayerIndex = previousPlayer,
                CurrentPlayerIndex = currentPlayer,
            });
        }

        public static void ExecuteCurrentPlayersTurn(this GameContext game)
        {
            int playerId = game.State.CurrentPlayerId;
            int roll = game.RollAmount(playerId);
            game.MovePlayer(new()
            {
                PlayerIndex = playerId,
                Amount = roll,
                Details = Logic.MovementDetails.Normal,
            });
            if (!game.State.IsOver)
                game.EndCurrentPlayersTurn();
        }
    }

    public class GameContext
    {
        public Data.Game State;
        public Events.Storage EventHandlers;
        public IRandom Random;
        public ILogger Logger;
    }

    public interface ILogger
    {
        void Debug(string message);
        void Debug(string format, object value);
    }

    public struct ComponentProxy
    {
        public object Storage;
        public int Index;
        public readonly ref T Get<T>() => ref ((ComponentStorage<T>) Storage).Data[Index];
    }

    public struct ComponentProxy<T>
    {
        public T[] Storage;
        public int Index;
        public readonly bool Exists => Index != -1;
        public readonly ref T Value => ref Storage[Index];
    }

    public struct ListItemComponentProxy<T>
    {
        public ComponentProxy<T> Proxy;
        public int ListIndex;
        public List<int> List;
        public readonly int ThingId => List[ListIndex];
        public readonly ref T Component => ref Proxy.Value;
    }

    public static class Helper
    {
        public static IEnumerable<ListItemComponentProxy<T>> GetItemInList<T>(List<int> list, ComponentStorage<T> componentStorage)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (componentStorage.TryGetProxy(list[i], out var proxy))
                {
                    yield return new()
                    {
                        Proxy = proxy,
                        ListIndex = i,
                        List = list,
                    };
                }
            }
        }
        public static IEnumerable<ListItemComponentProxy<T>> GetDataInCell<T>(this GameContext game, int componentId, int cellIndex)
        {
            var things = game.State.Board.Cells[cellIndex].Things;
            var componentStorage = game.GetComponentStorage<T>(componentId);
            return GetItemInList(things, componentStorage);
        }

        public static IEnumerable<ListItemComponentProxy<T>> GetDataInItems<T>(this GameContext game, int componentId, int playerIndex)
        {
            var things = game.State.Players[playerIndex].Items;
            var componentStorage = game.GetComponentStorage<T>(componentId);
            return GetItemInList(things, componentStorage);
        }

        public static IEnumerable<ListItemComponentProxy<T>> GetDataInItems<T>(this GameContext game, TypedIdentifier<T> componentId, int playerIndex)
        {
            return GetDataInItems<T>(game, componentId.Id, playerIndex);
        }

        public static IEnumerable<ListItemComponentProxy<T>> GetDataInCell<T>(this GameContext game, TypedIdentifier<T> componentId, int cellId)
        {
            return GetDataInCell<T>(game, componentId.Id, cellId);
        }

        public static ComponentStorage<T> GetComponentStorage<T>(this GameContext game, int componentId)
        {
            return (ComponentStorage<T>) game.State.ComponentsByType[componentId];
        }

        public static ComponentStorage<T> GetComponentStorage<T>(this GameContext game, TypedIdentifier<T> componentId)
        {
            return GetComponentStorage<T>(game, componentId.Id);
        }

        public static bool TryGetComponent<T>(this GameContext game, TypedIdentifier<T> componentId, int thingId, out ComponentProxy<T> proxy)
        {
            return GetComponentStorage(game, componentId).TryGetProxy(thingId, out proxy);
        }

        public static ComponentProxy<T> GetComponent<T>(this GameContext game, TypedIdentifier<T> componentId, int thingId)
        {
            TryGetComponent(game, componentId, thingId, out var result);
            return result;
        }

        public static ref Data.Cell GetCell(this ref Data.Player player, GameContext game)
        {
            return ref game.State.Board.Cells[player.Position];
        }

        public static bool ValidateMine(this in Components.Mine mine)
        {
            if (mine.RemoveOnDetonation && !mine.DestroyOnDetonation)
                return false;
            if (mine.PutInInventoryOnDetonation && !mine.RemoveOnDetonation)
                return false;
            return true;
        }

        public static Events.Proxy<T> GetEventProxy<T>(this GameContext game, int eventId) where T : struct
        {
            return new Events.Proxy<T>
            {
                EventHandlers = game.EventHandlers,
                EventIndex = eventId,
            };
        }

        public static Events.Proxy<T> GetEventProxy<T>(this GameContext game, TypedIdentifier<T> eventId) where T : struct
        {
            return new Events.Proxy<T>
            {
                EventHandlers = game.EventHandlers,
                EventIndex = eventId.Id,
            };
        }

        public static int GetUnoccupiedCellIndex(this IRandom random, GameContext game)
        {
            int lower = 1;
            var cells = game.State.Board.Cells;
            int upperInclusive = cells.Length - 2;
            int attemptCounter = 0;
            const int maxAttempts = 10;
            int t;
            bool maxAttemptsReached = false;
            do
            {
                t = random.GetInt(lower, upperInclusive);
                attemptCounter++;
            }
            while (cells[t].Things.Count != 0
                || (maxAttemptsReached = attemptCounter >= maxAttempts));

            if (maxAttemptsReached)
            {
                for (int i = 0; i < cells.Length; i++)
                {
                    if (cells[i].Things.Count == 0)
                        return i;
                }
                return -1;
            }

            return t;
        }

        public static void HandleEvent<T>(this GameContext game, int eventId, ref T eventData) where T : struct
        {
            game.GetEventProxy<T>(eventId).Handle(game, ref eventData);
        }

        public static void HandleEvent<T>(this GameContext game, TypedIdentifier<T> eventId, ref T eventData) where T : struct
        {
            HandleEvent(game, eventId.Id, ref eventData);
        }

        public static void HandleEvent<T>(this GameContext game, TypedIdentifier<T> eventId, T eventData) where T : struct
        {
            HandleEvent(game, eventId.Id, ref eventData);
        }


        // public static void HandleEvent<T>(this GameContext game, int eventId, ComponentProxy<Events.Storage> perThingEvents, ref T eventData) where T : struct
        // {
        //     game.GetEventProxy<T>(eventId).Handle(game, ref eventData);
        //     if (perThingEvents.HasComponent)
        //         perThingEvents.Component.GetEventProxy<T>(eventId).Handle(game, ref eventData);
        // }

        // public static void HandleEvent<T>(this GameContext game, TypedIdentifier<T> eventId, ComponentProxy<Events.Storage> perThingEvents, ref T eventData) where T : struct
        // {
        //     HandleEvent(game, eventId.Id, perThingEvents, ref eventData);
        // }

        // public static void HandleEvent<T>(this GameContext game, TypedIdentifier<T> eventId, ComponentProxy<Events.Storage> perThingEvents, T eventData) where T : struct
        // {
        //     HandleEvent(game, eventId.Id, perThingEvents, ref eventData);
        // }

        // public static void HandleEventWithContinueCheck<T>(this GameContext game, int eventId, ComponentProxy<Events.Storage> perThingEvents, ref T eventData) where T : struct, Events.IContinue
        // {
        //     game.GetEventProxy<T>(eventId).HandleWithContinueCheck(game, ref eventData);
        //     if (perThingEvents.HasComponent && eventData.Continue)
        //         perThingEvents.Component.GetEventProxy<T>(eventId).HandleWithContinueCheck(game, ref eventData);
        // }

        // public static void HandleEventWithContinueCheck<T>(this GameContext game, TypedIdentifier<T> eventId, ComponentProxy<Events.Storage> perThingEvents, ref T eventData) where T : struct, Events.IContinue
        // {
        //     HandleEventWithContinueCheck(game, eventId.Id, perThingEvents, ref eventData);
        // }
        

        public static void HandlePlayerEvent<T>(this GameContext game, int eventId, int playerIndex, ref T eventData) where T : struct
        {
            game.GetEventProxy<T>(eventId).Handle(game, ref eventData);
            game.State.Players[playerIndex].Events.GetEventProxy<T>(eventId).Handle(game, ref eventData);
        }

        public static void HandlePlayerEvent<T>(this GameContext game, TypedIdentifier<T> eventId, int playerIndex, ref T eventData) where T : struct
        {
            HandlePlayerEvent(game, eventId.Id, playerIndex, ref eventData);
        }

        public static void HandlePlayerEvent<T>(this GameContext game, TypedIdentifier<T> eventId, int playerIndex, T eventData) where T : struct
        {
            HandlePlayerEvent(game, eventId.Id, playerIndex, ref eventData);
        }

        public static void HandlePlayerEventWithContinueCheck<T>(this GameContext game, int eventId, int playerIndex, ref T eventData) where T : struct, Events.IContinue
        {
            game.GetEventProxy<T>(eventId).HandleWithContinueCheck(game, ref eventData);
            if (eventData.Continue)
                game.State.Players[playerIndex].Events.GetEventProxy<T>(eventId).HandleWithContinueCheck(game, ref eventData);
        }

        public static void HandlePlayerEventWithContinueCheck<T>(this GameContext game, TypedIdentifier<T> eventId, int playerIndex, ref T eventData) where T : struct, Events.IContinue
        {
            HandlePlayerEventWithContinueCheck(game, eventId.Id, playerIndex, ref eventData);
        }

        public static Events.Proxy<TEventData> GetPlayerEventProxy<TEventData>(this GameContext game, int playerIndex, int eventId) where TEventData : struct
            => game.State.Players[playerIndex].Events.GetEventProxy<TEventData>(eventId);

        public static ArraySegment<T> AsArraySegment<T>(this T[] from, int offset, int count)
        {
            return new(from, offset, count);
        }

        public static void TriggerSingleThingAddedToCellEvent(this GameContext game, int thingId, int cellPosition)
        {
            var a = ArrayPool<int>.Shared.Rent(1);
            a[0] = thingId;
            game.HandleEvent(Events.OnCellContentChanged, new()
            {
                AddedThings = a.AsArraySegment(0, 1),
                CellPosition = cellPosition,
                RemovedThings = Array.Empty<int>(),
            });
            ArrayPool<int>.Shared.Return(a);
        }

        public static void TriggerSingleThingRemovedFromCellEvent(this GameContext game, int thingId, int cellPosition)
        {
            var a = ArrayPool<int>.Shared.Rent(1);
            a[0] = thingId;
            game.HandleEvent(Events.OnCellContentChanged, new()
            {
                AddedThings = Array.Empty<int>(),
                CellPosition = cellPosition,
                RemovedThings = a.AsArraySegment(0, 1),
            });
            ArrayPool<int>.Shared.Return(a);
        }

        private static T[] Rented<T>(IEnumerable<T> things, int count)
        {
            var a = ArrayPool<T>.Shared.Rent(count);
            int i = 0;
            foreach (T id in things)
                a[i++] = id;
            assert(i == count);
            return a;
        }

        public static void TriggerThingsAddedToCellEvent(this GameContext game, IEnumerable<int> thingIds, int numElementsAdded, int cellPosition)
        {
            var a = Rented(thingIds, numElementsAdded);
            game.HandleEvent(Events.OnCellContentChanged, new()
            {
                AddedThings = a.AsArraySegment(0, numElementsAdded),
                CellPosition = cellPosition,
                RemovedThings = Array.Empty<int>(),
            });
            ArrayPool<int>.Shared.Return(a);
        }

        public static void TriggerThingsRemovedFromCellEvent(this GameContext game, IEnumerable<int> thingIds, int numElementsRemoved, int cellPosition)
        {
            var a = Rented(thingIds, numElementsRemoved);
            game.HandleEvent(Events.OnCellContentChanged, new()
            {
                AddedThings = Array.Empty<int>(),
                CellPosition = cellPosition,
                RemovedThings = a.AsArraySegment(0, numElementsRemoved),
            });
            ArrayPool<int>.Shared.Return(a);
        }
    }

    public interface IRandom
    {
        int GetInt(int lower, int upperInclusive);
    }

    public struct TypedIdentifier<T>
    {
        public int Id;
        public TypedIdentifier(int id) => Id = id;
    }

    public static class Events
    {
        public struct Storage
        {
            public object[] Handlers;
        }
        public static Storage CreateStorage()
        {
            return new Storage
            {
                Handlers = new object[Count],
            };
        }

        public delegate void Handler<T>(GameContext game, ref T eventData) where T : struct;

        public static Proxy<T> GetEventProxy<T>(this Storage eventHandlers, int eventId) where T : struct
        {
            return new Proxy<T>
            {
                EventHandlers = eventHandlers,
                EventIndex = eventId,
            };
        }

        public static Proxy<T> GetEventProxy<T>(this Storage eventHandlers, TypedIdentifier<T> eventId) where T : struct
        {
            return new Proxy<T>
            {
                EventHandlers = eventHandlers,
                EventIndex = eventId.Id,
            };
        }

        public struct Proxy<T> where T : struct
        {
            public Storage EventHandlers;
            public int EventIndex;

            public readonly void Add(Handler<T> handler)
            {
                ref var h = ref EventHandlers.Handlers[EventIndex];
                h = ((Handler<T>) h) + handler;
            }

            public readonly void Remove(Handler<T> handler)
            {
                ref var h = ref EventHandlers.Handlers[EventIndex];
                h = ((Handler<T>) h) - handler;
            }

            public readonly Handler<T> Get() => (Handler<T>) EventHandlers.Handlers[EventIndex];
            public readonly void Set(Handler<T> handler) => EventHandlers.Handlers[EventIndex] = handler;
            public readonly void Handle(GameContext game, ref T eventData)
            {
                Get()?.Invoke(game, ref eventData);
            }
            public readonly void Handle(GameContext game, T eventData) => Handle(game, ref eventData);
        }
        
        public static void HandleWithContinueCheck<T>(this Proxy<T> proxy, GameContext game, ref T eventData)
            where T : struct, IContinue
        {
            assert(eventData.Continue);

            var first = proxy.Get();
            if (first is null)
                return;

            // I guess there's no way to avoid allocating this array...
            var invocationList = first.GetInvocationList();
            assert(invocationList is not null);
            assert(invocationList.Length > 0);

            int index = 0;
            do
            {
                var handler = (Handler<T>) invocationList[index];
                handler(game, ref eventData);
                index++;
            }
            while (index < invocationList.Length && eventData.Continue);
        }

        public interface IContinue
        {
            bool Continue { get; }
        }

        public struct PlayerMovedContext
        {
            public Logic.MovementContext Movement;
            public int StartingPosition;
        }
        public static readonly TypedIdentifier<PlayerMovedContext> OnMoved = new(0);

        public struct PlayerPositionChangedContext
        {
            public int PlayerIndex;
            public int StartingPosition;
            public Data.Reason Reason;
        }
        public static readonly TypedIdentifier<PlayerPositionChangedContext> OnPositionChanged = new(1);
        public static readonly TypedIdentifier<int> OnPlayerWon = new(2);
        public static readonly TypedIdentifier<ItemInteractionInfo> OnThingPickedUp = new(3);
        public struct SavePlayerContext : IContinue
        {
            public bool WasSaved;
            public Data.Reason SaveReason;
            public Logic.KillPlayerContext Kill;

            bool IContinue.Continue => !WasSaved;
        }
        public static readonly TypedIdentifier<SavePlayerContext> OnTrySavePlayer = new(4);

        public struct PlayerSavedContext
        {
            public Data.Reason SaveReason;
            public Logic.KillPlayerContext Kill;
        }
        public static readonly TypedIdentifier<PlayerSavedContext> OnPlayerSaved = new(5);

        public struct RespawnPositionChanged
        {
            public int PlayerIndex;
            public int RespawnPointId;
            public int RespawnPosition;
            public Stack<int> RespawnPointIds;
        }
        public static readonly TypedIdentifier<RespawnPositionChanged> OnRespawnPositionPopped = new(6);
        public static readonly TypedIdentifier<RespawnPositionChanged> OnRespawnPositionPushed = new(7);
        public static readonly TypedIdentifier<Logic.KillPlayerContext> OnPlayerDied = new(8);
        public static readonly TypedIdentifier<ItemInteractionInfo> OnItemAddedToInventory = new(9);
        public static readonly TypedIdentifier<ItemInteractionInfo> OnItemRemovedFromInventory = new(10);
        public struct NextTurnContext
        {
            public int PreviousPlayerIndex;
            public int CurrentPlayerIndex;
        }
        public static readonly TypedIdentifier<NextTurnContext> OnNextTurn = new(11);
        public struct CellContentChangedContext
        {
            // This memory will be invalidated after the event ends.
            // If you need to keep this data, make a copy.
            public ArraySegment<int> AddedThings;
            public ArraySegment<int> RemovedThings;
            public int CellPosition;
        }
        public static readonly TypedIdentifier<CellContentChangedContext> OnCellContentChanged = new(12);
        
        public struct AmountRolledContext
        {
            public int PlayerIndex;
            public int RolledAmount;
            public int BonusAmount;
        }
        public static readonly TypedIdentifier<AmountRolledContext> OnAmountRolled = new(13);

        public const int Count = 14;
    }

    public static class Data
    {
        public struct Player
        {
            public List<int> Items;
            public List<int> Bonuses;
            public int Position;
            public int ThingId;
            public Stats.Storage Stats;
            public Events.Storage Events;
        }

        public struct Cell
        {
            public List<int> Things;
        }

        public struct Board
        {
            public Cell[] Cells;
        }

        public struct Game
        {
            public bool IsOver; 
            public Board Board;
            public Player[] Players;
            public int CurrentPlayerId;
            public object[] ComponentsByType;
            public Shop Shop;
        }

        public struct Shop
        {
            public int[] CellsWhereAccessible;
            public List<int> Items;
        }

        public struct Reason
        {
            public int Id;
            public int Payload;
        }
    }

    public static class Reasons
    {
        public const int UnknownId = 0;
        public const int ExplosionId = 1;
        public const int MagicId = 2;

        public static Data.Reason Unknown => new Data.Reason { Id = UnknownId };
        public static Data.Reason Explosion(int explodedThingId) => new Data.Reason { Id = ExplosionId, Payload = explodedThingId };
        public static Data.Reason Magic(int spellOrItemId) => new Data.Reason { Id = MagicId, Payload = spellOrItemId };
    }

    public class ComponentStorage<T>
    {
        public T[] Data;
        public Dictionary<int, int> MapThingIdToIndex;
        public int Count;

        public bool TryGetProxy(int thingIndex, out ComponentProxy<T> proxy)
        {
            proxy.Storage = Data;
            if (MapThingIdToIndex.TryGetValue(thingIndex, out proxy.Index))
                return true;
            proxy.Index = -1;
            return false;
        }

        public ComponentProxy<T> GetProxy(int thingIndex)
        {
            return new()
            {
                Index = MapThingIdToIndex[thingIndex],
                Storage = Data,
            };
        }

        public ComponentProxy<T> Add(int thingIndex)
        {
            int index = Count;
            Count++;
            if (Count > Data.Length)
                Array.Resize(ref Data, Count * 2);
            MapThingIdToIndex.Add(thingIndex, index);
            return new()
            {
                Index = index,
                Storage = Data,
            };
        }
    }

    public static class Components
    {
        public struct Mine
        {
            public bool RemoveOnDetonation;
            public bool PutInInventoryOnDetonation;
            public bool DestroyOnDetonation;
        }
        public static readonly TypedIdentifier<int> CurrencyCostId = new(0);

        public struct Player
        {
            public int PlayerIndex;
        }
        public static readonly TypedIdentifier<Player> PlayerId = new(1);

        public static readonly TypedIdentifier<int> CurrencyId = new(2);
        public static readonly TypedIdentifier<Events.Storage> ThingSpecificEventsId = new(3);
        public static readonly TypedIdentifier<Stack<int>> RespawnPointIdsId = new(4);
        public static readonly TypedIdentifier<int> RespawnPositionId = new(5);
        public static readonly TypedIdentifier<IPickup> PickupActionId = new(6);
        public static readonly TypedIdentifier<object> AttachedPickupDelegateId = new(7);
        public static readonly TypedIdentifier<int> RespawnPointIdId = new(8);
        public const int Count = 9;

        public static ComponentStorage<T> CreateStorage<T>(TypedIdentifier<T> id, int initialSize = 4)
        {
            return new()
            {
                MapThingIdToIndex = new(initialSize),
                Data = new T[initialSize],
            };
        }

        public static ComponentStorage<T> InitializeStorage<T>(GameContext game, TypedIdentifier<T> componentId, int initialSize = 4)
        {
            var t = CreateStorage<T>(componentId, initialSize);
            game.State.ComponentsByType[componentId.Id] = t;
            return t;
        }

        public struct RollValue
        {
            public int AddedValue;
        }
    }

    public static class Stats
    {
        public struct Storage
        {
            public float[] StatValues;
        }
        public static Stats.Storage CreateStorage()
        {
            return new Storage
            {
                StatValues = new float[Count],
            };
        }

        public struct Proxy
        {
            public Stats.Storage Stats;
            public int Index;
            public ref float Value => ref Stats.StatValues[Index];
        }

        public struct IntProxy
        {
            public Proxy Proxy;
            public readonly int Value
            {
                get => (int) Proxy.Value;
                set => Proxy.Value = (float) value;
            }
        }

        public static Proxy GetProxy(this Stats.Storage storage, int id)
        {
            return new Proxy
            {
                Stats = storage,
                Index = id,
            };
        }
        public static Proxy GetProxy(this Stats.Storage storage, TypedIdentifier<float> id)
        {
            return GetProxy(storage, id.Id);
        }
        public static IntProxy GetProxy(this Stats.Storage storage, TypedIdentifier<int> id)
        {
            return new IntProxy
            {
                Proxy = GetProxy(storage, id.Id),
            };
        }
        public static int Get(this Stats.Storage storage, TypedIdentifier<int> id) => GetProxy(storage, id).Value;
        public static int Set(this Stats.Storage storage, TypedIdentifier<int> id, int value) => GetProxy(storage, id).Value = value;
        public static float Get(this Stats.Storage storage, TypedIdentifier<float> id) => GetProxy(storage, id).Value;
        public static float Set(this Stats.Storage storage, TypedIdentifier<float> id, float value) => GetProxy(storage, id).Value = value;

        public static readonly TypedIdentifier<int> RollAdditiveBonus = new(0);
        public const int Count = 1;
    }
}