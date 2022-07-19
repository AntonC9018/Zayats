using System;
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
            game.EventHandlers = new object[Events.Count];

            {
                var components = new object[Components.Count];
                game.State.ComponentsByType = components;
                // assertNoneNull(components);
            }

            {
                var cells = new Data.Cell[cellCountNotIncludingFirstAndLast];
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
                    Currency = new(),
                    Position = 0,
                };
            }
        }

        public static void InitializePlayer(int id, int thingId, Data.Player[] players, ComponentStorage<Components.Player> playerStorage)
        {
            players[id].ThingId = thingId;
            playerStorage.Add(thingId).Component.PlayerId = id;
        }

        public struct MineInitializationContext
        {
            public int EternalMineCount;
            public int NormalMineCount;
            public IRandom Random;
            public Action<int, int> OnMineSpawned;
        }

        public static void InitializeMines(this ref GameInitializationContext initContext, MineInitializationContext mineContext)
        {
            int totalMineCount = mineContext.EternalMineCount + mineContext.NormalMineCount;
            var componentStorage = Components.InitializeStorage(initContext.Game, Components.MineId, totalMineCount);

            for (int i = 0; i < totalMineCount; i++)
            {
                int randomCellIndex = mineContext.Random.GetUnoccupiedCellIndex(initContext.Game);
                if (randomCellIndex == -1)
                    return;

                ref var t = ref componentStorage.Add(initContext.CurrentId).Component;
                int mineTypeIndex;
                if (i < mineContext.EternalMineCount)
                {
                    t.DestroyOnDetonation = false;
                    t.PutInInventoryOnDetonation = false;
                    t.RemoveOnDetonation = false;
                    mineTypeIndex = 0;
                }
                else
                {
                    t.DestroyOnDetonation = false;
                    t.RemoveOnDetonation = true;
                    t.PutInInventoryOnDetonation = true;
                    mineTypeIndex = 1;
                }
                assert(t.ValidateMine(), "Mine flags were invalid.");
                
                initContext.Game.State.Board.Cells[randomCellIndex].Things.Add(initContext.CurrentId);
                mineContext.OnMineSpawned?.Invoke(mineTypeIndex, initContext.CurrentId);

                initContext.CurrentId++;
            }
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
            public int PlayerId;
            public int Amount;
            public MovementDetails Details;
        }

        public static void TranslatePlayerToPosition_NoEvents(this GameContext game, ref Data.Player player, int toPosition)
        {
            player.GetCell(game).Things.Remove(player.ThingId);
            player.Position = toPosition;
            player.GetCell(game).Things.Add(player.ThingId);
        }

        // Returns true if the game ends as a result of the move.
        public static bool DoPlayerMove(this GameContext game, MovementContext context)
        {
            ref var player = ref game.State.Players[context.PlayerId];
            
            int startingPosition = player.Position;

            var toPosition = Math.Min(player.Position + context.Amount, game.State.Board.Cells.Length - 1);
            TranslatePlayerToPosition_NoEvents(game, ref player, toPosition);
            
            HandleEvent(game, Events.OnMoved, new()
            {
                Movement = context,
                StartingPosition = startingPosition,
            });
            HandleEvent(game, Events.OnPositionChanged, new()
            {
                PlayerId = context.PlayerId,
                StartingPosition = startingPosition,
            });

            if (CheckFinish(game.State.Board, player.Position))
            {
                HandleEvent(game, Events.OnPlayerWon, context.PlayerId);
                return true;
            }

            return false;
        }

        public static void MovePlayer(this GameContext game, MovementContext context)
        {
            ref var player = ref game.State.Players[context.PlayerId];

            if (DoPlayerMove(game, context))
                return;

            // Mines.
            var mines = game.GetDataInCell(Components.MineId, player.Position).GetEnumerator();
            if (mines.MoveNext())
            {
                var mineIt = mines.Current;
                int thingId = mineIt.ThingId;
                
                ref var mine = ref mineIt.Component;
                if (mine.RemoveOnDetonation)
                    mineIt.List.RemoveAt(mineIt.ListIndex);
                if (mine.PutInInventoryOnDetonation)
                    AddInventoryItem(game, context.PlayerId, thingId); 
                if (mine.DestroyOnDetonation)
                    DestroyThing(game, thingId);
                
                KillPlayer(game, new()
                {
                    Reason = Reasons.Explosion(thingId),
                    PlayerId = context.PlayerId,
                });
                return;
            }

            // Jumping over other players.
            var players = game.GetDataInCell(Components.PlayerId, player.Position).GetEnumerator();
            while (players.MoveNext())
            {
                var otherPlayerId = players.Current.Component.PlayerId;
                if (otherPlayerId == context.PlayerId)
                    continue;
                
                ref var otherPlayer = ref game.State.Players[otherPlayerId];

                var contextCopy = new MovementContext
                {
                    PlayerId = context.PlayerId,
                    Details = MovementDetails.HopOverPlayer,
                    Amount = 1,
                };
                MovePlayer(game, contextCopy);
                return;
            }
        }

        public static void CollectCoinsAtCurrentPosition(this GameContext game, int playerId)
        {
            CollectCoinsAtPosition(game, playerId, game.State.Players[playerId].Position);
        }

        public static void CollectCoinsAtPosition(this GameContext game, int playerId, int position)
        {
            ref var player = ref game.State.Players[playerId];
            var currenciesInCell = game.GetDataInCell(Components.CurrencyId, position);
            var currencies = currenciesInCell.Select(a => (a.ListIndex, a.ThingId)).Reverse().ToArray();

            var things = game.State.Board.Cells[position].Things;
            foreach (var index in currencies.Select(a => a.ListIndex))
                things.RemoveAt(index);

            foreach (var thingId in currencies.Select(a => a.ThingId))
                AddInventoryItem(game, playerId, thingId);
        }

        public static void AddInventoryItem(this GameContext game, int playerId, int thingId)
        {
            game.State.Players[playerId].Items.Add(thingId);
            game.HandleEvent(Events.OnAddedInventoryItem, new()
            {
                PlayerId = playerId,
                ThingId = thingId,
            });
        }

        public static void DestroyThing(this GameContext game, int thingId)
        {
            // ??
        }

        public struct KillPlayerContext
        {
            public int PlayerId;
            public Data.Reason Reason;
        }
        public static bool KillPlayer(this GameContext game, KillPlayerContext context)
        {
            ref var player = ref game.State.Players[context.PlayerId];

            // TODO: handle items
            bool wasSaved = false;
            if (wasSaved)
                return false;

            int startingPosition = player.Position;
            TranslatePlayerToPosition_NoEvents(game, ref player, 0);

            HandleEvent(game, Events.OnPositionChanged, new()
            {
                PlayerId = context.PlayerId,
                StartingPosition = startingPosition,
            });

            return true;
        }

        public static int RollAmount(this GameContext game, int playerId)
        {
            ref var player = ref game.State.Players[playerId];
            int rolledValue = game.Random.GetInt(1, 6);
            return rolledValue;
        }

        public static bool CheckFinish(in Data.Board board, int position)
        {
            return position >= board.Cells.Length - 1;
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
    }

    public class GameContext
    {
        public Data.Game State;
        public object[] EventHandlers;
        public IRandom Random;
    }

    public struct ComponentProxy
    {
        public object Storage;
        public int Index;
        public readonly ref T Get<T>() where T : struct => ref ((ComponentStorage<T>) Storage).Data[Index];
    }

    public struct ComponentProxy<T> where T : struct
    {
        public T[] Storage;
        public int Index;
        public readonly ref T Component => ref Storage[Index];
    }

    public struct ListItemComponentProxy<T> where T : struct
    {
        public ComponentProxy<T> Proxy;
        public int ListIndex;
        public List<int> List;
        public readonly int ThingId => List[ListIndex];
        public readonly ref T Component => ref Proxy.Component;
    }

    public static class Helper
    {
        public static IEnumerable<ListItemComponentProxy<T>> GetItemInList<T>(List<int> list, ComponentStorage<T> componentStorage) where T : struct
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (componentStorage.TryGetSingle(list[i], out var proxy))
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
        public static IEnumerable<ListItemComponentProxy<T>> GetDataInCell<T>(this GameContext game, int componentId, int cellId) where T : struct
        {
            var things = game.State.Board.Cells[cellId].Things;
            var componentStorage = game.GetComponentStorage<T>(componentId);
            return GetItemInList(things, componentStorage);
        }

        public static IEnumerable<ListItemComponentProxy<T>> GetDataInItems<T>(this GameContext game, int componentId, int playerId) where T : struct
        {
            var things = game.State.Players[playerId].Items;
            var componentStorage = game.GetComponentStorage<T>(componentId);
            return GetItemInList(things, componentStorage);
        }

        public static IEnumerable<ListItemComponentProxy<T>> GetDataInItems<T>(this GameContext game, TypedIdentifier<T> componentId, int playerId) where T : struct
        {
            return GetDataInItems<T>(game, componentId.Id, playerId);
        }

        public static IEnumerable<ListItemComponentProxy<T>> GetDataInCell<T>(this GameContext game, TypedIdentifier<T> componentId, int cellId) where T : struct
        {
            return GetDataInCell<T>(game, componentId.Id, cellId);
        }

        public static ComponentStorage<T> GetComponentStorage<T>(this GameContext game, int componentId) where T : struct
        {
            return (ComponentStorage<T>) game.State.ComponentsByType[componentId];
        }

        public static ComponentStorage<T> GetComponentStorage<T>(this GameContext game, TypedIdentifier<T> componentId) where T : struct
        {
            return GetComponentStorage<T>(game, componentId.Id);
        }

        public static bool TryGetComponent<T>(this GameContext game, TypedIdentifier<T> componentId, int thingId, out ComponentProxy<T> proxy) where T : struct
        {
            return GetComponentStorage(game, componentId).TryGetSingle(thingId, out proxy);
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
    }

    public interface IRandom
    {
        int GetInt(int lower, int upperInclusive);
        int GetUnoccupiedCellIndex(GameContext game);
    }

    public struct TypedIdentifier<T>
    {
        public int Id;
        public TypedIdentifier(int id) => Id = id;
    }

    public static class Events
    {
        public delegate void Handler<T>(GameContext game, ref T eventData) where T : struct;

        public static Proxy<T> GetEventProxy<T>(this object[] eventHandlers, int eventId) where T : struct
        {
            return new Proxy<T>
            {
                EventHandlers = eventHandlers,
                EventIndex = eventId,
            };
        }

        public static Proxy<T> GetEventProxy<T>(this object[] eventHandlers, TypedIdentifier<T> eventId) where T : struct
        {
            return new Proxy<T>
            {
                EventHandlers = eventHandlers,
                EventIndex = eventId.Id,
            };
        }

        public struct Proxy<T> where T : struct
        {
            public object[] EventHandlers;
            public int EventIndex;

            public readonly void Add(Handler<T> handler)
            {
                ref var h = ref EventHandlers[EventIndex];
                h = ((Handler<T>) h) + handler;
            }

            public readonly void Remove(Handler<T> handler)
            {
                ref var h = ref EventHandlers[EventIndex];
                h = ((Handler<T>) h) - handler;
            }

            public readonly Handler<T> Get() => (Handler<T>) EventHandlers[EventIndex];
            public readonly void Set(Handler<T> handler) => EventHandlers[EventIndex] = handler;
            public readonly void Handle(GameContext game, ref T eventData) => Get()?.Invoke(game, ref eventData);
            public readonly void Handle(GameContext game, T eventData) => Handle(game, ref eventData);
        }

        public struct PlayerMovedContext
        {
            public Logic.MovementContext Movement;
            public int StartingPosition;
        }
        public static TypedIdentifier<PlayerMovedContext> OnMoved = new(0);

        public struct PlayerPositionChangedContext
        {
            public int PlayerId;
            public int StartingPosition;
        }
        public static TypedIdentifier<PlayerPositionChangedContext> OnPositionChanged = new(1);
        public static TypedIdentifier<int> OnPlayerWon = new(2);

        public struct AddedInventoryItemContext
        {
            public int PlayerId;
            public int ThingId;
        }
        public static TypedIdentifier<AddedInventoryItemContext> OnAddedInventoryItem = new(3);

        public const int Count = 4;
    }

    public static class Data
    {
        public struct Player
        {
            public List<int> Items;
            public List<int> Bonuses;
            public List<int> Currency;
            public int Position;
            public int ThingId;
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
            public int Data;
        }
    }

    public static class Reasons
    {
        public const int UnknownId = 0;
        public const int ExplosionId = 1;

        public static Data.Reason Unknown => new Data.Reason { Id = UnknownId };
        public static Data.Reason Explosion(int explodedThingId) => new Data.Reason { Id = ExplosionId, Data = explodedThingId };
    }

    public class ComponentStorage<T> where T : struct
    {
        public T[] Data;
        public Dictionary<int, int> MapThingIdToIndex;
        public int Count;

        public bool TryGetSingle(int thingIndex, out ComponentProxy<T> proxy)
        {
            proxy.Storage = Data;
            return (MapThingIdToIndex.TryGetValue(thingIndex, out proxy.Index));
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

    public class Components
    {
        public static TypedIdentifier<Mine> MineId = new(0);
        public struct Mine
        {
            public bool RemoveOnDetonation;
            public bool PutInInventoryOnDetonation;
            public bool DestroyOnDetonation;
        }

        public static TypedIdentifier<Player> PlayerId = new(1);
        public struct Player
        {
            public int PlayerId;
        }

        public static TypedIdentifier<int> CurrencyId = new(2);
        public const int Count = 3;

        public static ComponentStorage<T> CreateStorage<T>(TypedIdentifier<T> id, int initialSize = 4) where T : struct
        {
            return new()
            {
                MapThingIdToIndex = new(initialSize),
                Data = new T[initialSize],
            };
        }

        public static ComponentStorage<T> InitializeStorage<T>(GameContext game, TypedIdentifier<T> componentId, int initialSize = 4) where T : struct
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
}