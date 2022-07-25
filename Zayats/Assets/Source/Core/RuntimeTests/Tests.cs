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
            game.State.Board.Cells[initialPosition].Things.Add(playerId);

            return ref player;
        }

        public static (GameContext, PredictableRandom) BasicSinglePlayer(int cellCount)
        {
            var (game, rand) = Basic(cellCount, playerCount: 1);
            ref var player = ref InitializePlayer(game, 0);
            return (game, rand);
        }

        public static void AddItem(GameContext game, int itemId, IPickup pickup, int position = 1)
        {
            game.AddComponent(Components.PickupActionId, itemId) = pickup;
            game.State.Board.Cells[position].Things.Add(itemId);
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
            AddItem(game, itemId, PlayerInventoryPickup.Instance);

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
            AddItem(game, itemId, pickup);

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();
            ref var player = ref game.State.Players[0];

            Assert.AreEqual(itemId, player.Items.Single(), "The item that was picked up was the item added above.");
            Assert.AreEqual(addedStatValue, player.Stats.Get(statId), "The stats got added");
        }

        [Test]
        public void ToppleOverPlayers()
        {
            var (game, rand) = Basic(cellCount: 3, playerCount: 2);
            ref var player0 = ref InitializePlayer(game, playerIndex: 0);
            ref var player1 = ref InitializePlayer(game, playerIndex: 1, initialPosition: 1);

            game.GetEventProxy(OnMoved).Add(
                (GameContext game, ref PlayerMovedContext context) => 
                {
                    Assert.AreEqual(0, context.Movement.PlayerIndex);

                    ref var player = ref game.State.Players[0];
                    // Thing is triggered after the move is done.
                    if (player.MoveCount == 1)
                    {
                        Assert.AreEqual(MovementDetails.Normal, context.Movement.Details);
                        Assert.AreEqual(0, context.StartingPosition);
                    }
                    else
                    {
                        Assert.AreEqual(2, player.MoveCount, "Too many moves triggered??");
                        Assert.AreEqual(MovementDetails.ToppleOverPlayer, context.Movement.Details);
                        Assert.AreEqual(1, context.StartingPosition);
                    }
                });

            rand.NextAmount = 1;
            game.ExecuteCurrentPlayersTurn();
            Assert.AreEqual(2, player0.MoveCount);
            Assert.AreEqual(2, player0.Position);
        }
    }
}
