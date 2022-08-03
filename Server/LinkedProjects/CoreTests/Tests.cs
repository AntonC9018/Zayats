using System;
using System.Linq;
using Xunit;

namespace Zayats.Core.Facts
{
    using static Zayats.Core.GameEvents;
    using Assert = Xunit.Assert;
    
    public class PredictableRandom : IRandom
    {
        public int NextAmount { get; set; }
        public int GetInt(int lower, int upperInclusive)
        {
            return Math.Clamp(NextAmount, lower, upperInclusive);
        }
    }

    public class Logger : ILogger
    {
        public void Debug(string message)
        {
            System.Console.WriteLine(message);
        }

        public void Debug(string format, object value)
        {
            System.Console.WriteLine(String.Format(format, value));
        }
    }

    public class Tests
    {
        private static void AssertEqual<T>(T a, T b, string message = null)
        {
            try
            {
                Assert.Equal(a, b);
            }
            catch (Xunit.Sdk.EqualException)
            {
                Console.WriteLine(message);
                throw;
            }
        }

        public static (GameContext, PredictableRandom) Basic(int cellCount, int playerCount)
        {
            var game = Initialization.CreateGame(cellCountNotIncludingStartAndFinish: cellCount - 2, playerCount);
            
            var rand = new PredictableRandom();
            game.Random = rand;
            
            game.Logger = new Logger();

            Components.InitializeStorage(game, Components.PlayerId, playerCount);
            Components.InitializeStorage(game, Components.CurrencyCostId);
            Components.InitializeStorage(game, Components.CurrencyId);
            Components.InitializeStorage(game, Components.ThingSpecificEventsId);
            Components.InitializeStorage(game, Components.RespawnPointIdsId);
            Components.InitializeStorage(game, Components.RespawnPositionId);
            Components.InitializeStorage(game, Components.PickupId);
            Components.InitializeStorage(game, Components.AttachedPickupDelegateId);
            Components.InitializeStorage(game, Components.RespawnPointIdId);
            Components.InitializeStorage(game, Components.FlagsId);
            Components.InitializeStorage(game, Components.ActivatedItemId);
            
            Assert.True(game.State.ComponentsByType.All(f => f is not null), "Not all component storage units have been initialized.");

            return (game, rand);
        }

        public static ref Data.Player InitializePlayer(GameContext game, int playerIndex, int initialPosition = 0)
        {
            return ref InitializePlayer(game, playerIndex, playerIndex, initialPosition);
        }

        public static ref Data.Player InitializePlayer(GameContext game, int playerIndex, int playerId, int initialPosition = 0)
        {
            ref var player = ref game.State.Players[playerIndex];
            player.ThingId = playerId;
            game.AddComponent(Components.PlayerId, playerId).PlayerIndex = playerIndex;
            game.State.Cells[initialPosition].Add(playerId);

            return ref player;
        }

        public static (GameContext, PredictableRandom) BasicSinglePlayer(int cellCount)
        {
            var (game, rand) = Basic(cellCount, playerCount: 1);
            ref var player = ref InitializePlayer(game, playerIndex: 0);
            return (game, rand);
        }

        public static void AddPickup(GameContext game, int itemId, IPickup pickup, int position = 1)
        {
            game.AddComponent(Components.PickupId, itemId) = pickup;
            game.State.Cells[position].Add(itemId);
        }

        [Fact]
        public void RollTheIndicatedAmount()
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 6);
            int playerIndex = 0;

            rand.NextAmount = 1;
            AssertEqual(1, game.RollAmount(playerIndex));

            rand.NextAmount = 5;
            AssertEqual(5, game.RollAmount(playerIndex));
        }
        
        [Fact]
        public void PlayerCanMove()
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 6);
            ref var player = ref game.State.Players[0];

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();
            AssertEqual(1, player.Position, "Player moved by the amount indicated.");

            rand.NextAmount = 5;
            game.ExecuteCurrentPlayersTurn();
            AssertEqual(5, player.Position, "Player is on last position.");
            Assert.True(game.State.IsOver, "The game ended as a result.");
        }
        
        [Fact]
        public void EquipSimple()
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 3);

            int itemId = 1;
            AddPickup(game, itemId, PlayerInventoryPickup.Instance);

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();
            ref var player = ref game.State.Players[0];
            AssertEqual(1, player.Position, "Player moved by the amount indicated.");
            AssertEqual(itemId, player.Items.Single(), "The item that was picked up was the item added above.");
        }

        [Fact]
        public void EquipStatIncrease()
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 3);

            int itemId = 1;
            int addedStatValue = 3;
            // Choose a stat that doesn't affect movement here.
            var statId = Stats.JumpAfterMoveCapacity;
            var pickup = new AddStatPickup(statId, addedStatValue);
            AddPickup(game, itemId, pickup);

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();
            ref var player = ref game.State.Players[0];

            AssertEqual(itemId, player.Items.Single(), "The item that was picked up was the item added above.");
            AssertEqual(addedStatValue, player.Stats.Get(statId), "The stats got added");
        }

        [Fact]
        public void MovementCounter()
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 3);
            
            game.GetEventProxy(OnPositionChanged).Add(
                (GameContext game, ref PlayerPositionChangedContext context) => 
                {
                    AssertEqual(0, context.InitialMoveCount);
                    AssertEqual(1, game.State.Players[0].Counters.Get(Counters.Move));
                });
            
            ref int moveCount = ref game.State.Players[0].Counters.Get(Counters.Move);
            AssertEqual(0, moveCount);
            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();
            AssertEqual(1, moveCount);
        }

        [Fact]
        public void ToppleOverPlayers()
        {
            var (game, rand) = Basic(cellCount: 3, playerCount: 2);
            ref var player0 = ref InitializePlayer(game, playerIndex: 0);
            ref var player1 = ref InitializePlayer(game, playerIndex: 1, initialPosition: 1);

            game.GetEventProxy(OnPositionChanged).Add(
                (GameContext game, ref PlayerPositionChangedContext context) => 
                {
                    AssertEqual(0, context.Movement.PlayerIndex);

                    // Thing is triggered after the move is done.
                    if (context.InitialMoveCount == 0)
                    {
                        AssertEqual(MovementKind.Normal, context.Movement.Kind);
                        AssertEqual(0, context.InitialPosition);
                    }
                    else
                    {
                        AssertEqual(1, context.InitialMoveCount, "Too many moves triggered??");
                        AssertEqual(MovementKind.ToppleOverPlayer, context.Movement.Kind);
                        AssertEqual(1, context.InitialPosition);
                    }
                });

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();
            AssertEqual(2, player0.Counters.Get(Counters.Move));
            AssertEqual(2, player0.Position);
        }

        class FlagReference
        {
            public bool Value = false;
        }

        private GameContext MineSetup(int mineId, MinePickup pickup)
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 3);
            rand.NextAmount = 1;
            AddPickup(game, mineId, pickup, position: 1);

            return game;
        }

        [Fact]
        public void RegularMine()
        {
            int mineId = 1;
            var game = MineSetup(mineId, MinePickup.Regular);

            game.ExecuteCurrentPlayersTurn();
            
            ref var player = ref game.State.Players[0];
            AssertEqual(1, player.Counters.Get(Counters.Death));
            AssertEqual(0, player.Position, "The player has been respawned at the initial position.");
            AssertEqual(2, player.Counters.Get(Counters.Move), "Both the moves counted.");
            AssertEqual(mineId, player.Items.Single(), "The mine has been picked up.");
            AssertEqual(0, game.State.Cells[1].Count, "No things on the second position.");
        }

        [Fact]
        public void EternalMine()
        {
            int mineId = 1;
            var game = MineSetup(mineId, MinePickup.Eternal);

            game.ExecuteCurrentPlayersTurn();

            ref var player = ref game.State.Players[0];
            AssertEqual(1, player.Counters.Get(Counters.Death));
            AssertEqual(0, player.Position, "The player has been respawned at the initial position.");
            AssertEqual(2, player.Counters.Get(Counters.Move), "Both the moves counted.");
            AssertEqual(0, player.Items.Count, "Nothing picked up.");
            AssertEqual(mineId, game.State.Cells[1].Single(), "The mine stayed at its initial position.");
        }

        private static void AddSolidThing(GameContext game, ref int id, int position)
        {
            game.AddComponent(Components.FlagsId, id) = ThingFlags.Solid;
            game.State.Cells[position].Add(id);
            id++;
        }

        #pragma warning disable CS8509 // Non-exhaustive switch

        [Fact]
        public void HoppingOverSolidThings()
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 9);
            ref var player = ref game.State.Players[0];

            player.Stats.Set(Stats.JumpAfterMoveCapacity, 1);

            var id = 1;
            AddSolidThing(game, ref id, 2);
            AddSolidThing(game, ref id, 4);
            AddSolidThing(game, ref id, 7);

            game.GetEventProxy(GameEvents.OnPositionChanged).Add(
                (GameContext game, ref PlayerPositionChangedContext context) =>
                {
                    var kind = context.InitialMoveCount switch
                    {
                        0 => MovementKind.Normal,
                        1 => MovementKind.HopOverThing,
                        2 => MovementKind.Normal,
                    };
                    AssertEqual(kind, context.Movement.Kind);
                });

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();
            AssertEqual(2, player.Counters.Get(Counters.Move), "A normal move, and 1 hop.");
            AssertEqual(3, player.Position);

            rand.NextAmount = 2;
            game.ExecuteCurrentPlayersTurn();
            AssertEqual(3, player.Counters.Get(Counters.Move));
            AssertEqual(5, player.Position);
        }

        [Fact]
        public void HoppingOverSolidThings_MultiStat()
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 9);
            ref var player = ref game.State.Players[0];

            player.Stats.Set(Stats.JumpAfterMoveCapacity, 2);

            var id = 1;
            AddSolidThing(game, ref id, 2);
            AddSolidThing(game, ref id, 3);
            AddSolidThing(game, ref id, 5);

            game.GetEventProxy(GameEvents.OnPositionChanged).Add(
                (GameContext game, ref PlayerPositionChangedContext context) =>
                {
                    var expectedKind = context.InitialMoveCount switch
                    {
                        0 => MovementKind.Normal,
                        _ => MovementKind.HopOverThing,
                    };
                    AssertEqual(expectedKind, context.Movement.Kind);

                    var expectedTargetPosition = context.InitialMoveCount switch
                    {
                        0 => 1,
                        1 => 4,
                    };
                    AssertEqual(expectedTargetPosition, context.TargetPosition);
                });

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();

            AssertEqual(2, player.Counters.Get(Counters.Move), "A normal move, and 1 hop.");
            AssertEqual(4, player.Position);
        }

        [Fact]
        public void TotemSaves()
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 3);
            ref var player = ref game.State.Players[0];


            // int itemId = 1;
            // game.AddComponent(Components.PickupActionId, itemId) = TowerPickup.Instance;

            // int respawnPointId = 2;
            // int respawnPointPosition = 1;
            // game.AddComponent(Components.RespawnPositionId, respawnPointId) = respawnPointPosition;

            // game.AddComponent(Components.RespawnPointIdId, itemId) = respawnPointId;

            int totemId = 1;
            
            game.AddComponent(Components.PickupId, totemId) = TotemPickup.Instance;
            game.AddComponent(Components.AttachedPickupDelegateId, totemId);

            game.AddItemToInventory_DoPickupEffect(TotemPickup.Instance, new()
            {
                PlayerIndex = 0,
                Position = 0,
                ThingId = totemId,
            });

            AssertEqual(totemId, player.Items.Single());

            int mineId = 2;
            int minePosition = 1;
            AddPickup(game, mineId, MinePickup.Eternal, minePosition);

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();

            AssertEqual(0, player.Items.Count);
            AssertEqual(totemId, game.State.Shop.Items.Single(), "Item returned to shop.");
            AssertEqual(minePosition, player.Position, "Player landed on top of the mine, but wasn't brought back.");
            AssertEqual(0, player.Counters.Get(Counters.Death));
            
            game.ExecuteCurrentPlayersTurn();

            AssertEqual(minePosition + 1, player.Position);
        }

        [Fact]
        public void RespawnPoints()
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 4);
            ref var player = ref game.State.Players[0];

            int mineId = 1;
            int minePosition = 2;
            AddPickup(game, mineId, MinePickup.Eternal, minePosition);

            int respawnPointId = 2;
            int respawnPointPosition = 1;
            game.AddComponent(Components.RespawnPositionId, respawnPointId) = respawnPointPosition;

            game.PushRespawnPoint(playerIndex: 0, respawnPointId);
            
            rand.NextAmount = 2;
            game.ExecuteCurrentPlayersTurn();

            AssertEqual(respawnPointPosition, player.Position);
            AssertEqual(0, game.GetComponent(Components.RespawnPointIdsId, player.ThingId).Count);
            AssertEqual(1, player.Counters.Get(Counters.Death));
        }
    }
}
