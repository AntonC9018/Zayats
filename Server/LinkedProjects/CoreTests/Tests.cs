using System;
using System.Linq;
using Xunit;

namespace Zayats.Core.Facts
{
    using static Zayats.Core.Components;
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

        public void Dump(object obj)
        {
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

        public static (GameContext, PredictableRandom, ThingCreationContext) Basic(int cellCount, int playerCount)
        {
            var game = Initialization.CreateGame(cellCountNotIncludingStartAndFinish: cellCount - 2, playerCount);
            var creating = game.StartCreating();
            
            var rand = new PredictableRandom();
            game.Random = rand;
            
            game.Logger = new Logger();

            game.InitializeComponentStorages(defaultSize: 0);
            
            Assert.True(game.State.Components.Storages.All(f => f is not null), "Not all component storage units have been initialized.");

            return (game, rand, creating);
        }

        public static ref Data.Player InitializePlayer(ThingCreationContext creating, int playerIndex, int initialPosition = 0)
        {
            creating.Create().Player(playerIndex).Place().At(initialPosition);
            return ref creating.Game.State.Players[playerIndex];
        }

        public static (GameContext, PredictableRandom, ThingCreationContext) BasicSinglePlayer(int cellCount)
        {
            var (game, rand, creating) = Basic(cellCount, playerCount: 1);
            creating.Create().Player(playerIndex: 0).Place().At(0);
            return (game, rand, creating);
        }

        public static int AddPickup(ThingCreationContext creating, Pickup pickup, int position = 1)
        {
            var p = creating.Create();
            p.AddComponent(Components.PickupId) = pickup;
            p.Place().At(position);
            return p.Id;
        }

        [Fact]
        public void RollTheIndicatedAmount()
        {
            var (game, rand, creating) = BasicSinglePlayer(cellCount: 6);
            int playerIndex = 0;

            rand.NextAmount = 1;
            AssertEqual(1, game.RollAmount(playerIndex));

            rand.NextAmount = 5;
            AssertEqual(5, game.RollAmount(playerIndex));
        }
        
        [Fact]
        public void PlayerCanMove()
        {
            var (game, rand, creating) = BasicSinglePlayer(cellCount: 6);
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
            var (game, rand, creating) = BasicSinglePlayer(cellCount: 3);

            int itemId = AddPickup(creating, DoNothingPickupEffect.Instance.AsPickup());

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();
            ref var player = ref game.State.Players[0];
            AssertEqual(1, player.Position, "Player moved by the amount indicated.");
            AssertEqual(itemId, player.Items.Single(), "The item that was picked up was the item added above.");
        }

        [Fact]
        public void EquipStatIncrease()
        {
            var (game, rand, creating) = BasicSinglePlayer(cellCount: 3);

            int addedStatValue = 3;
            // Choose a stat that doesn't affect movement here.
            var statId = Stats.JumpAfterMoveCapacity;
            var pickup = new AddStatPickupEffect(new StatBoost(statId, addedStatValue)).AsPickup();

            int itemId = AddPickup(creating, pickup);

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();
            ref var player = ref game.State.Players[0];

            AssertEqual(itemId, player.Items.Single(), "The item that was picked up was the item added above.");
            AssertEqual(addedStatValue, player.Stats.Get(statId), "The stats got added");
        }

        [Fact]
        public void MovementCounter()
        {
            var (game, rand, creating) = BasicSinglePlayer(cellCount: 3);
            
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
            var (game, rand, creating) = Basic(cellCount: 3, playerCount: 2);
            ref var player0 = ref InitializePlayer(creating, playerIndex: 0);
            ref var player1 = ref InitializePlayer(creating, playerIndex: 1, initialPosition: 1);

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

        private (GameContext, int mineId) MineSetup(Pickup pickup)
        {
            var (game, rand, creating) = BasicSinglePlayer(cellCount: 3);
            rand.NextAmount = 1;
            int mineId = AddPickup(creating, pickup, position: 1);

            return (game, mineId);
        }

        [Fact]
        public void RegularMine()
        {
            var (game, mineId) = MineSetup(Pickups.RegularMine);

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
            var (game, mineId) = MineSetup(Pickups.EternalMine);

            game.ExecuteCurrentPlayersTurn();

            ref var player = ref game.State.Players[0];
            AssertEqual(1, player.Counters.Get(Counters.Death));
            AssertEqual(0, player.Position, "The player has been respawned at the initial position.");
            AssertEqual(2, player.Counters.Get(Counters.Move), "Both the moves counted.");
            AssertEqual(0, player.Items.Count, "Nothing picked up.");
            AssertEqual(mineId, game.State.Cells[1].Single(), "The mine stayed at its initial position.");
        }

        private static int AddSolidThing(ThingCreationContext creating, int position)
        {
            var p = creating.Create();
            p.AddComponent(Components.FlagsId) = ThingFlags.Solid;
            p.Place().At(position);
            return p.Id;
        }

        #pragma warning disable CS8509 // Non-exhaustive switch

        [Fact]
        public void HoppingOverSolidThings()
        {
            var (game, rand, creating) = BasicSinglePlayer(cellCount: 9);
            ref var player = ref game.State.Players[0];

            player.Stats.Set(Stats.JumpAfterMoveCapacity, 1);

            AddSolidThing(creating, 2);
            AddSolidThing(creating, 4);
            AddSolidThing(creating, 7);

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
            var (game, rand, creating) = BasicSinglePlayer(cellCount: 9);
            ref var player = ref game.State.Players[0];

            player.Stats.Set(Stats.JumpAfterMoveCapacity, 2);

            AddSolidThing(creating, 2);
            AddSolidThing(creating, 3);
            AddSolidThing(creating, 5);

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
            var (game, rand, creating) = BasicSinglePlayer(cellCount: 3);
            ref var player = ref game.State.Players[0];


            // int itemId = 1;
            // game.AddComponent(Components.PickupActionId, itemId) = TowerPickup.Instance;

            // int respawnPointId = 2;
            // int respawnPointPosition = 1;
            // game.AddComponent(Components.RespawnPositionId, respawnPointId) = respawnPointPosition;

            // game.AddComponent(Components.RespawnPointIdId, itemId) = respawnPointId;

            int totemId = 1;
            
            game.AddComponent(Components.PickupId, totemId) = Pickups.Totem;
            game.AddComponent(Components.AttachedPickupDelegateId, totemId);

            game.AddItemToInventory_DoPickupEffect(Pickups.Totem, new()
            {
                PlayerIndex = 0,
                Position = 0,
                ThingId = totemId,
            });

            AssertEqual(totemId, player.Items.Single());

            int minePosition = 1;
            int mineId = AddPickup(creating, Pickups.EternalMine, minePosition);

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
            var (game, rand, creating) = BasicSinglePlayer(cellCount: 4);
            ref var player = ref game.State.Players[0];

            int minePosition = 2;
            int mineId = AddPickup(creating, Pickups.EternalMine, minePosition);

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
