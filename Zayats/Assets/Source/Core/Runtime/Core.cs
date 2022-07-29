using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Kari.Plugins.AdvancedEnum;
using Zayats.Core.Generated;

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

        [Conditional("DEBUG")]
        public static void panic(string message = "")
        {
            Fail(message);
        }
    }
}
namespace Zayats.Core
{
    using static Zayats.Core.Assert;

    public static partial class Initialization
    {
        public static GameContext CreateGame(int cellCountNotIncludingStartAndFinish, int playerCount)
        {
            GameContext game = new();
            game.EventHandlers = Events.CreateStorage();
            game.PostMovementMechanics = new()
            {
                (g, c) => Logic.CollectPickupsAtCurrentPosition(g, c.PlayerIndex),
                (g, c) => Logic.ToppleOverPlayers(g, c.PlayerIndex),
                Logic.JumpOverSolidThings,
            };
            game.State.Shop.Items = new();

            {
                var components = new object[Components.Count];
                game.State.ComponentsByType = components;
                // assertNoneNull(components);
            }

            {
                var cells = new List<int>[cellCountNotIncludingStartAndFinish + 2];
                game.State.Board.Cells = cells;
                foreach (ref var cell in cells.AsSpan())
                    cell = new();
            }
            
            var players = new Data.Player[playerCount];
            game.State.Players = players;

            for (int i = 0; i < playerCount; i++)
            {
                players[i] = new()
                {
                    Bonuses = new(),
                    Items = new(),
                    Position = 0,
                    Stats = Stats.CreateStorage(),
                    Events = Events.CreateStorage(),
                    Counters = Counters.CreateStorage(),

                    // Set later on.
                    ThingId = -1,
                };
            }

            return game;
        }


        public static void InitializePlayer(this GameContext game, int index, int thingId, ComponentStorage<Components.Player> playerStorage)
        {
            ref var player = ref game.State.Players[index];
            player.ThingId = thingId;
            playerStorage.Add(thingId).Value.PlayerIndex = index;
        }
    }
    public enum MovementKind
    {
        Normal = 0,
        HopOverThing = 1,
        ToppleOverPlayer = 2,
        Death = 3,
    }

    public static class Logic
    {
        public struct MovementContext
        {
            public int PlayerIndex;
            public MovementKind Kind;
            public int TargetPosition;
        }

        public struct MovementStartContext
        {
            public int PlayerIndex;
            public MovementKind Details;
            public int Amount;
        }

        public static void MovePlayerInBetweenCells(this GameContext game, ref Data.Player player, int toPosition)
        {
            player.GetCell(game).Remove(player.ThingId);
            int prevPosition = player.Position;
            player.Position = toPosition;
            game.TriggerSingleThingRemovedFromCellEvent(player.ThingId, prevPosition);
            player.GetCell(game).Add(player.ThingId);
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

        public static void MovePlayerToPosition(this GameContext game, Events.PlayerPositionChangedContext context)
        {
            ref var player = ref game.State.Players[context.PlayerIndex];
            MovePlayerInBetweenCells(game, ref player, context.TargetPosition);
            game.HandlePlayerEvent(Events.OnPositionChanged, context.PlayerIndex, ref context);
            game.MaybeEndGame(context.PlayerIndex);
        }

        public static MoveStartInfo StartMove(this GameContext game, int playerIndex)
        {
            ref var player = ref game.State.Players[playerIndex];
            ref int moveCount = ref player.Counters.Get(Counters.Move);
            MoveStartInfo moveStart;
            moveStart.InitialMoveCount = moveCount;
            moveCount++;
            moveStart.InitialPosition = player.Position;
            return moveStart;
        }

        public static void MovePlayer(this GameContext game, MovementStartContext context, out MovementContext outContext)
        {
            ref var player = ref game.State.Players[context.PlayerIndex];
            var moveStart = StartMove(game, context.PlayerIndex);
            var toPosition = Math.Min(player.Position + context.Amount, game.State.Cells.Length - 1);
            outContext = new()
            {
                Kind = context.Details,
                PlayerIndex = context.PlayerIndex,
                TargetPosition = toPosition,
            };
            MovePlayerToPosition(game, new()
            {
                MoveStart = moveStart,
                Movement = outContext,
            });
        }

        public static void MovePlayer_DoPostMovementMechanics(this GameContext game, MovementStartContext startContext)
        {
            ref var player = ref game.State.Players[startContext.PlayerIndex];

            MovePlayer(game, startContext, out var context);
            if (game.State.IsOver)
                return;

            ref int moveCount = ref player.Counters.Get(Counters.Move);
            int moveCountAfterMove = moveCount;

            foreach (var mechanic in game.PostMovementMechanics)
            {
                mechanic(game, context);
                if (moveCount != moveCountAfterMove)
                    return;
            }
        }

        public static void ToppleOverPlayers(this GameContext game, int playerIndex)
        {
            ref var player = ref game.State.Players[playerIndex];
            
            // Jumping over other players.
            var players = game.GetDataInCell(Components.PlayerId, player.Position);
            foreach (var other in players)
            {
                var otherPlayerIndex = other.Value.PlayerIndex;
                if (otherPlayerIndex == playerIndex)
                    continue;
                
                ref var otherPlayer = ref game.State.Players[otherPlayerIndex];

                var contextCopy = new MovementStartContext
                {
                    PlayerIndex = playerIndex,
                    Details = MovementKind.ToppleOverPlayer,
                    Amount = 1,
                };
                MovePlayer_DoPostMovementMechanics(game, contextCopy);
                break;
            }
        }

        public static bool AcquireTurnLock(this GameContext game, LockFlags lockFlags)
        {
            ref var turnLock = ref game.State.TurnLock;
            if (turnLock.Has(lockFlags))
                return false;
            turnLock.Set(lockFlags);
            return true;
        }

        public static void ReturnTurnLock(this GameContext game, LockFlags lockFlags)
        {
            ref var turnLock = ref game.State.TurnLock;
            assert(turnLock.Has(lockFlags));
            turnLock.Unset(lockFlags);
        }

        public static void JumpOverSolidThings(this GameContext game, MovementContext context)
        {
            if (context.Kind == MovementKind.HopOverThing)
                return;

            int playerIndex = context.PlayerIndex;
            ref var player = ref game.State.Players[playerIndex];
            var stats = player.Stats;
            int jump = stats.Get(Stats.JumpAfterMoveCapacity);
            int playerId = player.ThingId;
            int distance = 0;

            if (jump == 0)
                return;

            for (int i = 1; i <= jump + 1; i++)
            {
                int position = player.Position + i;

                assert(position < game.State.Cells.Length, "Should not need to check this.");

                bool hasSolidThings = game.GetDataInCell(Components.FlagsId, position)
                    .Any(f => f.Value.HasEither(ThingFlags.Solid));

                if (hasSolidThings)
                    distance = i;
                else
                    break;
            }

            // The thing right after is not solid.
            if (distance == 0)
                return;

            // There are more than `jump` solid thing in front.
            if (distance == jump + 1)
                return;

            game.MovePlayer_DoPostMovementMechanics(new()
            {
                Amount = distance + 1,
                Details = MovementKind.HopOverThing,
                PlayerIndex = playerIndex,
            });
        }

        public static void CollectPickupsAtCurrentPosition(this GameContext game, int playerIndex)
        {
            CollectPickupsAtPosition(game, playerIndex, game.State.Players[playerIndex].Position);
        }

        public static void CollectPickupsAtPosition(this GameContext game, int playerIndex, int position)
        {
            var pickupsInCell = game.GetDataInCell(Components.PickupId, position);
            var pickupInfos = pickupsInCell.Select(a => (Pickup: a.Value, a.ListIndex, a.ThingId)).Reverse().ToArray();

            ItemInteractionInfo info;
            info.PlayerIndex = playerIndex;
            info.Position = position;
            
            var things = game.State.Cells[position];
            
            // They have to be removed before their effect is executed,
            // because the effect may mess with the cell's content.
            // This is also why we have to enumerate the data in the cell.
            foreach (var pickupInfo in pickupInfos)
            {
                info.ThingId = pickupInfo.ThingId;
                if (pickupInfo.Pickup.ShouldRemoveFromCellOnPickup(game, info))
                    things.RemoveAt(pickupInfo.ListIndex);
            }

            game.TriggerThingsRemovedFromCellEvent(pickupInfos.Select(i => i.ThingId), pickupInfos.Length, position);

            foreach (var pickupInfo in pickupInfos)
            {
                info.ThingId = pickupInfo.ThingId;
                DoPickupEffectWithEvent(game, pickupInfo.Pickup, info);
                if (pickupInfo.Pickup.IsInventoryItem(game, info))
                    AddItemToInventory_WithoutPickupEffect(game, info);
            }
        }

        public static void DoPickupEffectWithEvent(this GameContext game, IPickup pickup, ItemInteractionInfo info)
        {
            pickup.DoPickupEffect(game, info);
            game.HandlePlayerEvent(Events.OnThingPickedUp, info.PlayerIndex, ref info);
        }

        public static void AddThingToShop(this GameContext game, int thingId)
        {
            game.State.Shop.Items.Add(thingId);
        }

        // public static void DoDefaultPickupEffectWithEvent(ItemInteractionInfo info)
        // {
        //     PlayerInventoryPickup.DoDropEffect(info);
        //     game.HandlePlayerEvent(Events.OnThingPickedUp, info.PlayerIndex, new()
        //     {
        //         PlayerIndex = info.PlayerIndex,
        //         ThingId = info.Position,
        //     });
        // }

        public static void AddItemToInventory_DoPickupEffect(this GameContext game, IPickup pickup, ItemInteractionInfo info)
        {
            assert(pickup.IsInventoryItem(game, info));
            pickup.DoPickupEffect(game, info);
            AddItemToInventory_WithoutPickupEffect(game, info);
        }

        public static void AddItemToInventory_WithoutPickupEffect(this GameContext game, ItemInteractionInfo info)
        {
            game.State.Players[info.PlayerIndex].Items.Add(info.ThingId);
            game.HandlePlayerEvent(Events.OnItemAddedToInventory, info.PlayerIndex, ref info);
        }

        public static void RemoveItemFromInventory(this GameContext game, ItemInteractionInfo info)
        {
            game.State.Players[info.PlayerIndex].Items.Remove(info.ThingId);
            game.HandlePlayerEvent(Events.OnItemRemovedFromInventory, info.PlayerIndex, ref info);
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

            player.Counters.Get(Counters.Death)++;

            game.HandlePlayerEvent(Events.OnPlayerDied, context.PlayerIndex, context);

            {
                var moveStart = StartMove(game, context.PlayerIndex);
                int respawnPosition = GetRespawnPositionByPoppingRespawnPoint(game, context.PlayerIndex);
                MovePlayerToPosition(game, new()
                {
                    MoveStart = moveStart,
                    Movement = new()
                    {
                        Kind = MovementKind.Death,
                        PlayerIndex = context.PlayerIndex,
                        TargetPosition = respawnPosition,
                    }
                });
            }

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
                int respawnPosition = game.GetComponent(Components.RespawnPositionId, respawnPointId);

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

            game.HandlePlayerEvent(Events.OnRespawnPositionPushed, playerIndex, new()
            {
                PlayerIndex = playerIndex,
                RespawnPointId = respawnPointId,
                RespawnPointIds = stack,
                RespawnPosition = game.GetComponent(Components.RespawnPositionId, respawnPointId),
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

            // Clear all turn locks
            game.State.TurnLock = 0;
        }

        public static void ExecuteCurrentPlayersTurn(this GameContext game)
        {
            int playerId = game.State.CurrentPlayerId;
            int roll = game.RollAmount(playerId);
            game.MovePlayer_DoPostMovementMechanics(new()
            {
                PlayerIndex = playerId,
                Amount = roll,
                Details = MovementKind.Normal,
            });
            if (!game.State.IsOver)
                game.EndCurrentPlayersTurn();
        }
    }

    [Serializable]
    public class GameContext
    {
        public Data.Game State;
        public Events.Storage EventHandlers;
        public IRandom Random;
        public ILogger Logger;

        public delegate void PostMovementMechanic(GameContext game, Logic.MovementContext context);
        public List<PostMovementMechanic> PostMovementMechanics;
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
        public readonly ref T Value => ref Proxy.Value;
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
            var things = game.State.Cells[cellIndex];
            var componentStorage = game.GetComponentStorage<T>(componentId);
            assert(componentStorage is not null, $"Component storage {componentId} has not been found.");
            return GetItemInList(things, componentStorage);
        }

        public static IEnumerable<ListItemComponentProxy<T>> GetDataInItems<T>(this GameContext game, int componentId, int playerIndex)
        {
            var things = game.State.Players[playerIndex].Items;
            var componentStorage = game.GetComponentStorage<T>(componentId);
            assert(componentStorage is not null, $"Component storage {componentId} has not been found.");
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

        public static ComponentProxy<T> GetComponentProxy<T>(this GameContext game, TypedIdentifier<T> componentId, int thingId)
        {
            TryGetComponent(game, componentId, thingId, out var result);
            return result;
        }

        public static ref T GetComponent<T>(this GameContext game, TypedIdentifier<T> componentId, int thingId)
        {
            TryGetComponent(game, componentId, thingId, out var result);
            return ref result.Value;
        }

        public static ref T AddComponent<T>(this GameContext game, TypedIdentifier<T> componentId, int thingId)
        {
            return ref GetComponentStorage(game, componentId).Add(thingId).Value;
        }

        public static ComponentProxy<T> AddComponent_Proxy<T>(this GameContext game, TypedIdentifier<T> componentId, int thingId)
        {
            return GetComponentStorage(game, componentId).Add(thingId);
        }

        public static List<int> GetCell(this ref Data.Player player, GameContext game)
        {
            return game.State.Cells[player.Position];
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
            var cells = game.State.Cells;
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
            while (cells[t].Count != 0
                || (maxAttemptsReached = attemptCounter >= maxAttempts));

            if (maxAttemptsReached)
            {
                for (int i = 0; i < cells.Length; i++)
                {
                    if (cells[i].Count == 0)
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

    public struct MoveStartInfo
    {
        public int InitialMoveCount;
        public int InitialPosition;
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
            public MoveStartInfo MoveStart;
        }
        public static readonly TypedIdentifier<PlayerMovedContext> OnMoved = new(0);

        public struct PlayerPositionChangedContext
        {
            public Logic.MovementContext Movement;
            public MoveStartInfo MoveStart;

            public readonly int TargetPosition => Movement.TargetPosition;
            public readonly MovementKind Reason => Movement.Kind;
            public readonly int PlayerIndex => Movement.PlayerIndex;
            public readonly int InitialPosition => MoveStart.InitialPosition;
            public readonly int InitialMoveCount => MoveStart.InitialMoveCount;
        }
        public static readonly TypedIdentifier<PlayerPositionChangedContext> OnPositionChanged = new(1);
        public static readonly TypedIdentifier<int> OnPlayerWon = new(2);
        public static readonly TypedIdentifier<ItemInteractionInfo> OnThingPickedUp = new(3);
        public struct SavePlayerContext : IContinue
        {
            public bool WasSaved;
            public Data.Reason SaveReason;
            public Logic.KillPlayerContext Kill;

            readonly bool IContinue.Continue => !WasSaved;
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
            public readonly int Amount => RolledAmount + BonusAmount;
        }
        public static readonly TypedIdentifier<AmountRolledContext> OnAmountRolled = new(13);

        public const int Count = 14;
    }

    public static class Data
    {
        [Serializable]
        public struct Player
        {
            public List<int> Items;
            public List<int> Bonuses;
            public int Position;
            public int ThingId;
            public Stats.Storage Stats;
            public Events.Storage Events;
            public Counters.Storage Counters;
        }

        [Serializable]
        public struct Board
        {
            public List<int>[] Cells;
        }

        [Serializable]
        public struct Game
        {
            public LockFlags TurnLock;
            public bool IsOver; 
            public Board Board;
            public Player[] Players;
            public int CurrentPlayerId;
            public object[] ComponentsByType;
            public Shop Shop;

            public readonly List<int>[] Cells => Board.Cells;
        }

        [Serializable]
        public struct Shop
        {
            public int[] CellsWhereAccessible;
            public List<int> Items;
        }

        [Serializable]
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

    [Serializable]
    public class ComponentStorage<T>
    {
        public T[] Data;
        public Dictionary<int, int> MapThingIdToIndex;
        public int Count;

        public bool TryGetProxy(int thingId, out ComponentProxy<T> proxy)
        {
            proxy.Storage = Data;
            if (MapThingIdToIndex.TryGetValue(thingId, out proxy.Index))
                return true;
            proxy.Index = -1;
            return false;
        }

        public ComponentProxy<T> GetProxy(int thingId)
        {
            return new()
            {
                Index = MapThingIdToIndex[thingId],
                Storage = Data,
            };
        }

        public ComponentProxy<T> Add(int thingId)
        {
            if (MapThingIdToIndex.ContainsKey(thingId))
                panic($"Component of type {typeof(T).Name} already exists for thing {thingId}.");

            int index = Count;
            Count++;
            if (Count > Data.Length)
                Array.Resize(ref Data, Count * 2);
            MapThingIdToIndex.Add(thingId, index);
            return new()
            {
                Index = index,
                Storage = Data,
            };
        }
    }

    public static class Components
    {
        [Serializable]
        public struct Mine
        {
            public bool RemoveOnDetonation;
            public bool PutInInventoryOnDetonation;
            public bool DestroyOnDetonation;
        }
        public static readonly TypedIdentifier<int> CurrencyCostId = new(0);

        [Serializable]
        public struct Player
        {
            public int PlayerIndex;
        }
        public static readonly TypedIdentifier<Player> PlayerId = new(1);

        public static readonly TypedIdentifier<int> CurrencyId = new(2);
        public static readonly TypedIdentifier<Events.Storage> ThingSpecificEventsId = new(3);
        public static readonly TypedIdentifier<Stack<int>> RespawnPointIdsId = new(4);
        public static readonly TypedIdentifier<int> RespawnPositionId = new(5);
        // public struct Pickup
        // {
        //     public IPickupEffect Effect;
        //     public bool IsInventoryItem;
        //     public bool ShouldRemoveFromCellOnPickup;
        // }
        public static readonly TypedIdentifier<IPickup> PickupId = new(6);
        public static readonly TypedIdentifier<object> AttachedPickupDelegateId = new(7);
        public static readonly TypedIdentifier<int> RespawnPointIdId = new(8);
        public static readonly TypedIdentifier<ThingFlags> FlagsId = new(9);
        public const int Count = 10;

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

        [Serializable]
        public struct RollValue
        {
            public int AddedValue;
        }
    }

    public static class Stats
    {
        [Serializable]
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
        public static readonly TypedIdentifier<int> JumpAfterMoveCapacity = new(1);
        public const int Count = 2;
    }

    public static class Counters
    {
        [Serializable]
        public struct Storage
        {
            public int[] CounterValues;
        }
        public static Storage CreateStorage()
        {
            return new Storage
            {
                CounterValues = new int[Count],
            };
        }

        public struct Proxy
        {
            public Storage Counters;
            public int Index;
            public ref int Value => ref Counters.CounterValues[Index];
        }

        public static Proxy GetProxy(this Storage storage, int id)
        {
            return new Proxy
            {
                Counters = storage,
                Index = id,
            };
        }

        public static ref int Get(this Storage storage, int id) => ref GetProxy(storage, id).Value;
        public static int Set(this Storage storage, int id, int value) => GetProxy(storage, id).Value = value;

        public const int Move = 0;
        public const int Death = 1;
        public const int Count = 2;
    }
}