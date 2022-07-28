using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zayats.Unity.View;

namespace Zayats.Core.Tests
{
    using static Zayats.Core.Events;
    using Assert = NUnit.Framework.Assert;
    
    public class PredictableRandom : IRandom
    {
        public int NextAmount { get; set; }
        public int GetInt(int lower, int upperInclusive)
        {
            return Math.Clamp(NextAmount, lower, upperInclusive);
        }
    }

    public class Tests
    {
        public static (GameContext, PredictableRandom) Basic(int cellCount, int playerCount)
        {
            var game = Initialization.CreateGame(cellCountNotIncludingStartAndFinish: cellCount - 2, playerCount);
            
            var rand = new PredictableRandom();
            game.Random = rand;
            
            game.Logger = new View.UnityLogger();

            Components.InitializeStorage(game, Components.PlayerId, playerCount);
            Components.InitializeStorage(game, Components.CurrencyCostId);
            Components.InitializeStorage(game, Components.CurrencyId);
            Components.InitializeStorage(game, Components.ThingSpecificEventsId);
            Components.InitializeStorage(game, Components.RespawnPointIdsId);
            Components.InitializeStorage(game, Components.RespawnPositionId);
            Components.InitializeStorage(game, Components.PickupActionId);
            Components.InitializeStorage(game, Components.AttachedPickupDelegateId);
            Components.InitializeStorage(game, Components.RespawnPointIdId);
            Components.InitializeStorage(game, Components.FlagsId);
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
            game.AddComponent(Components.PlayerId, playerId).PlayerIndex = playerId;
            game.State.Cells[initialPosition].Add(playerId);

            return ref player;
        }

        public static (GameContext, PredictableRandom) BasicSinglePlayer(int cellCount)
        {
            var (game, rand) = Basic(cellCount, playerCount: 1);
            ref var player = ref InitializePlayer(game, 0);
            return (game, rand);
        }

        public static void AddPickup(GameContext game, int itemId, IPickup pickup, int position = 1)
        {
            game.AddComponent(Components.PickupActionId, itemId) = pickup;
            game.State.Cells[position].Add(itemId);
        }

        [Test]
        public void RollTheIndicatedAmount()
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 6);
            int playerIndex = 0;

            rand.NextAmount = 1;
            Assert.AreEqual(1, game.RollAmount(playerIndex));

            rand.NextAmount = 5;
            Assert.AreEqual(5, game.RollAmount(playerIndex));
        }
        
        [Test]
        public void PlayerCanMove()
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 6);
            ref var player = ref game.State.Players[0];

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();
            Assert.AreEqual(1, player.Position, "Player moved by the amount indicated.");

            rand.NextAmount = 5;
            game.ExecuteCurrentPlayersTurn();
            Assert.AreEqual(5, player.Position, "Player is on last position.");
            Assert.True(game.State.IsOver, "The game ended as a result.");
        }
        
        [Test]
        public void EquipSimple()
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 3);

            int itemId = 1;
            AddPickup(game, itemId, PlayerInventoryPickup.Instance);

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();
            ref var player = ref game.State.Players[0];
            Assert.AreEqual(1, player.Position, "Player moved by the amount indicated.");
            Assert.AreEqual(itemId, player.Items.Single(), "The item that was picked up was the item added above.");
        }

        [Test]
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

            Assert.AreEqual(itemId, player.Items.Single(), "The item that was picked up was the item added above.");
            Assert.AreEqual(addedStatValue, player.Stats.Get(statId), "The stats got added");
        }

        [Test]
        public void MovementCounter()
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 3);
            
            game.GetEventProxy(OnPositionChanged).Add(
                (GameContext game, ref PlayerPositionChangedContext context) => 
                {
                    Assert.AreEqual(0, context.InitialMoveCount);
                    Assert.AreEqual(1, game.State.Players[0].Counters.Get(Counters.Move));
                });
            
            ref int moveCount = ref game.State.Players[0].Counters.Get(Counters.Move);
            Assert.AreEqual(0, moveCount);
            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();
            Assert.AreEqual(1, moveCount);
        }

        [Test]
        public void ToppleOverPlayers()
        {
            var (game, rand) = Basic(cellCount: 3, playerCount: 2);
            ref var player0 = ref InitializePlayer(game, playerIndex: 0);
            ref var player1 = ref InitializePlayer(game, playerIndex: 1, initialPosition: 1);

            game.GetEventProxy(OnPositionChanged).Add(
                (GameContext game, ref PlayerPositionChangedContext context) => 
                {
                    Assert.AreEqual(0, context.Movement.PlayerIndex);

                    // Thing is triggered after the move is done.
                    if (context.InitialMoveCount == 0)
                    {
                        Assert.AreEqual(MovementKind.Normal, context.Movement.Kind);
                        Assert.AreEqual(0, context.InitialPosition);
                    }
                    else
                    {
                        Assert.AreEqual(1, context.InitialMoveCount, "Too many moves triggered??");
                        Assert.AreEqual(MovementKind.ToppleOverPlayer, context.Movement.Kind);
                        Assert.AreEqual(1, context.InitialPosition);
                    }
                });

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();
            Assert.AreEqual(2, player0.Counters.Get(Counters.Move));
            Assert.AreEqual(2, player0.Position);
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

        [Test]
        public void RegularMine()
        {
            int mineId = 1;
            var game = MineSetup(mineId, MinePickup.Regular);

            game.ExecuteCurrentPlayersTurn();
            
            ref var player = ref game.State.Players[0];
            Assert.AreEqual(1, player.Counters.Get(Counters.Death));
            Assert.AreEqual(0, player.Position, "The player has been respawned at the initial position.");
            Assert.AreEqual(2, player.Counters.Get(Counters.Move), "Both the moves counted.");
            Assert.AreEqual(mineId, player.Items.Single(), "The mine has been picked up.");
            Assert.AreEqual(0, game.State.Cells[1].Count, "No things on the second position.");
        }

        [Test]
        public void EternalMine()
        {
            int mineId = 1;
            var game = MineSetup(mineId, MinePickup.Eternal);

            game.ExecuteCurrentPlayersTurn();

            ref var player = ref game.State.Players[0];
            Assert.AreEqual(1, player.Counters.Get(Counters.Death));
            Assert.AreEqual(0, player.Position, "The player has been respawned at the initial position.");
            Assert.AreEqual(2, player.Counters.Get(Counters.Move), "Both the moves counted.");
            Assert.AreEqual(0, player.Items.Count, "Nothing picked up.");
            Assert.AreEqual(mineId, game.State.Cells[1].Single(), "The mine stayed at its initial position.");
        }

        private static void AddSolidThing(GameContext game, ref int id, int position)
        {
            game.AddComponent(Components.FlagsId, id) = ThingFlags.Solid;
            game.State.Cells[position].Add(id);
            id++;
        }

        #pragma warning disable CS8509 // Non-exhaustive switch

        [Test]
        public void HoppingOverSolidThings()
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 9);
            ref var player = ref game.State.Players[0];

            player.Stats.Set(Stats.JumpAfterMoveCapacity, 1);

            var id = 1;
            AddSolidThing(game, ref id, 2);
            AddSolidThing(game, ref id, 4);
            AddSolidThing(game, ref id, 7);

            game.GetEventProxy(Events.OnPositionChanged).Add(
                (GameContext game, ref PlayerPositionChangedContext context) =>
                {
                    var kind = context.InitialMoveCount switch
                    {
                        0 => MovementKind.Normal,
                        1 => MovementKind.HopOverThing,
                        2 => MovementKind.Normal,
                    };
                    Assert.AreEqual(kind, context.Movement.Kind);
                });

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();
            Assert.AreEqual(2, player.Counters.Get(Counters.Move), "A normal move, and 1 hop.");
            Assert.AreEqual(3, player.Position);

            rand.NextAmount = 2;
            game.ExecuteCurrentPlayersTurn();
            Assert.AreEqual(3, player.Counters.Get(Counters.Move));
            Assert.AreEqual(5, player.Position);
        }

        [Test]
        public void HoppingOverSolidThings_MultiStat()
        {
            var (game, rand) = BasicSinglePlayer(cellCount: 9);
            ref var player = ref game.State.Players[0];

            player.Stats.Set(Stats.JumpAfterMoveCapacity, 2);

            var id = 1;
            AddSolidThing(game, ref id, 2);
            AddSolidThing(game, ref id, 3);
            AddSolidThing(game, ref id, 5);

            game.GetEventProxy(Events.OnPositionChanged).Add(
                (GameContext game, ref PlayerPositionChangedContext context) =>
                {
                    var expectedKind = context.InitialMoveCount switch
                    {
                        0 => MovementKind.Normal,
                        _ => MovementKind.HopOverThing,
                    };
                    Assert.AreEqual(expectedKind, context.Movement.Kind);

                    var expectedTargetPosition = context.InitialMoveCount switch
                    {
                        0 => 1,
                        1 => 4,
                    };
                    Assert.AreEqual(expectedTargetPosition, context.TargetPosition);
                });

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();

            Assert.AreEqual(2, player.Counters.Get(Counters.Move), "A normal move, and 1 hop.");
            Assert.AreEqual(4, player.Position);
        }
    }
}
