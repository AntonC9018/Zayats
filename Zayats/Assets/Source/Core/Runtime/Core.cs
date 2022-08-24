using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Common;
using Kari.Plugins.AdvancedEnum;
using Kari.Plugins.Forward;
using Zayats.Core.Generated;
using Kari.Zayats.Exporter;

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
            game.Events = GameEvents.CreateStorage();
            game.PostMovementMechanics = new()
            {
                (g, c) => Logic.CollectPickupsAtCurrentPosition(g, c.PlayerIndex),
                (g, c) => Logic.ToppleOverPlayers(g, c.PlayerIndex),
                Logic.JumpOverSolidThings,
            };
            game.State.Shop.Items = new();

            {
                // var components = new object[Components.Count];
                // game.State.ComponentsByType = components;
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
                    StatBonuses = new(),
                    Items = new(),
                    Position = 0,
                    Stats = Stats.CreateStorage(),
                    Events = GameEvents.CreateStorage(),
                    Counters = Counters.CreateStorage(),

                    // Set later on.
                    ThingId = -1,
                };
            }

            return game;
        }


        public static void InitializePlayer(this GameContext game, int index, int thingId)
        {
            ref var player = ref game.State.Players[index];
            player.ThingId = thingId;
            game.AddComponent(Components.PlayerId, thingId).PlayerIndex = index;
        }

        public static void AssociateRespawnPointIdsOneToOne(this GameContext game)
        {
            var respawnPositionStorage = game.GetComponentStorage(Components.RespawnPositionId);
            var respawnPointIdStorage = game.GetComponentStorage(Components.RespawnPointIdId);
            
            int[] idsOfPositions = respawnPositionStorage.MapThingIdToIndex.Keys.ToArray();
            int[] idsOfPointStorage = respawnPointIdStorage.MapThingIdToIndex.Keys.ToArray();
            assert(idsOfPositions.Length >= idsOfPointStorage.Length, "why and also how?");

            for (int i = 0; i < idsOfPointStorage.Length; i++)
            {
                int refereeId = idsOfPointStorage[i];
                respawnPointIdStorage.GetProxy(refereeId).Value = idsOfPositions[i];
                int position = respawnPositionStorage.Data[idsOfPositions[i]];
                game.PlaceThing(refereeId).At(position);
            }
        }
    }
    public enum MovementKind
    {
        Normal = 0,
        HopOverThing = 1,
        ToppleOverPlayer = 2,
        Death = 3,
        Count,
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

        public static void MovePlayerInBetweenCells(this GameContext game, ref Data.Player player, int toPosition, Data.Reason reason)
        {
            panic("Not implemented correctly");
        }

        public static void AddThingToCell_WithoutPlayerEvent(this GameContext game, int thingId, int toPosition, Data.Reason reason)
        {
            game.State.Cells[toPosition].Add(thingId);
            game.TriggerSingleThingAddedToCellEvent(thingId, toPosition, reason);
        }

        public static bool MaybeEndGame(this GameContext game, int playerIndex)
        {
            if (CheckFinish(game.State.Board, game.State.Players[playerIndex].Position))
            {
                game.State.IsOver = true;
                game.HandlePlayerEvent(GameEvents.OnPlayerWon, playerIndex, playerIndex);
                return true;
            }
            return false;
        }

        public static void MovePlayerToPosition(this GameContext game, GameEvents.PlayerPositionChangedContext context)
        {
            ref var player = ref game.State.Players[context.PlayerIndex];

            game.HandlePlayerEvent(GameEvents.OnPositionAboutToChange, context.PlayerIndex, ref context);
            
            // Remove from cell
            player.GetCell(game).Remove(player.ThingId);
            int prevPosition = player.Position;
            game.TriggerSingleThingRemovedFromCellEvent(player.ThingId, prevPosition, context.Reason);

            game.HandlePlayerEvent(GameEvents.OnPositionChanging, context.PlayerIndex, ref context);
            
            // Add to cell
            player.Position = context.TargetPosition;
            player.GetCell(game).Add(player.ThingId);
            game.TriggerSingleThingAddedToCellEvent(player.ThingId, context.TargetPosition, context.Reason);

            game.HandlePlayerEvent(GameEvents.OnPositionChanged, context.PlayerIndex, ref context);

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
            var moveStart = game.StartMove(context.PlayerIndex);
            var toPosition = Math.Min(player.Position + context.Amount, game.State.Cells.Length - 1);
            outContext = new()
            {
                Kind = context.Details,
                PlayerIndex = context.PlayerIndex,
                TargetPosition = toPosition,
            };
            game.MovePlayerToPosition(new()
            {
                MoveStart = moveStart,
                Movement = outContext,
            });
        }

        public static void MovePlayer_DoPostMovementMechanics(this GameContext game, MovementStartContext startContext)
        {
            assert(startContext.Amount > 0);
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

            if (pickupInfos.Length == 0)
                return;

            ItemInterationContext info;
            info.PlayerIndex = playerIndex;
            info.Position = position;
            
            var things = game.State.Cells[position];
            var removed = new List<int>();
            
            // They have to be removed before their effect is executed,
            // because the effect may mess with the cell's content.
            // This is also why we have to enumerate the data in the cell.
            foreach (var pickupInfo in pickupInfos)
            {
                info.ThingId = pickupInfo.ThingId;
                if (pickupInfo.Pickup.Interaction.ShouldRemoveFromCellOnPickup(game, info))
                {
                    things.RemoveAt(pickupInfo.ListIndex);
                    removed.Add(pickupInfo.ThingId);
                }
            }
            
            // TODO: don't allocate and copy here.
            if (removed.Count > 0)
                game.TriggerThingsRemovedFromCellEvent(removed, removed.Count, position);

            foreach (var pickupInfo in pickupInfos)
            {
                info.ThingId = pickupInfo.ThingId;
                DoPickupEffectWithEvent(game, pickupInfo.Pickup.Effect, info);
                if (pickupInfo.Pickup.Interaction.IsInventoryItem(game, info))
                    AddItemToInventory_WithoutPickupEffect(game, info);
            }
        }

        public static void DoPickupEffectWithEvent(this GameContext game, IPickupEffect pickup, ItemInterationContext info)
        {
            pickup.DoPickupEffect(game, info);
            game.HandlePlayerEvent(GameEvents.OnThingPickedUp, info.PlayerIndex, ref info);
        }

        public static void AddThingToShop(this GameContext game, GameEvents.ThingAddedToShopContext context)
        {
            var shopItems = game.State.Shop.Items;
            if (shopItems.Contains(context.ThingId))
                panic($"Adding an item to shop even though it's already there. ID: {context.ThingId}");
            shopItems.Add(context.ThingId);
            game.HandleEvent(GameEvents.OnThingAddedToShop, ref context);
        }

        // public static void DoDefaultPickupEffectWithEvent(ItemInteractionInfo info)
        // {
        //     PlayerInventoryPickup.DoDropEffect(info);
        //     game.HandlePlayerEvent(GameEvents.OnThingPickedUp, info.PlayerIndex, new()
        //     {
        //         PlayerIndex = info.PlayerIndex,
        //         ThingId = info.Position,
        //     });
        // }

        public static void AddItemToInventory_MaybeDoPickupEffect(this GameContext game, ItemInterationContext context)
        {
            if (game.TryGetComponentValue(Components.PickupId, context.ThingId, out var pickup))
            {
                assert(pickup.Interaction.IsInventoryItem(game, context));
                pickup.Effect.DoPickupEffect(game, context);
            }
            AddItemToInventory_WithoutPickupEffect(game, context);
        }

        public static void AddItemToInventory_DoPickupEffect(this GameContext game, Components.Pickup pickup, ItemInterationContext context)
        {
            assert(pickup.Interaction.IsInventoryItem(game, context));
            pickup.Effect.DoPickupEffect(game, context);
            AddItemToInventory_WithoutPickupEffect(game, context);
        }

        public static void AddItemToInventory_WithoutPickupEffect(this GameContext game, ItemInterationContext context)
        {
            var items = game.State.Players[context.PlayerIndex].Items;
            items.Add(context.ThingId);

            game.HandlePlayerEvent(GameEvents.OnItemAddedToInventory, context.PlayerIndex, ref context);
        }

        public static void RemoveItemFromInventory(this GameContext game, ItemInterationContext context)
        {
            var items = game.State.Players[context.PlayerIndex].Items;
            var index = items.IndexOf(context.ThingId);
            items.RemoveAt(index);

            game.HandlePlayerEvent(GameEvents.OnItemRemovedFromInventory, context.PlayerIndex, new()
            {
                ItemIndex = index,
                PlayerInteration = context,
            });
        }

        public static void RemoveItemFromInventory_AtIndex(this GameContext game, int playerIndex, int itemIndex)
        {
            ref var player = ref game.State.Players[playerIndex];
            int itemId = player.Items[itemIndex];
            player.Items.RemoveAt(itemIndex);
            game.HandlePlayerEvent(GameEvents.OnItemRemovedFromInventory, playerIndex, new()
            {
                ItemIndex = itemIndex,
                PlayerIndex = playerIndex,
                Position = player.Position,
                ThingId = itemId,
            });
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
                var saveContext = new GameEvents.SavePlayerContext
                {
                    Kill = context,
                    WasSaved = false,
                };
                game.HandlePlayerEvent(GameEvents.OnTrySavePlayer, context.PlayerIndex, ref saveContext);

                if (saveContext.WasSaved)
                {
                    game.HandlePlayerEvent(GameEvents.OnPlayerSaved, context.PlayerIndex, new()
                    {
                        Kill = context,
                        SaveReason = saveContext.SaveReason,
                    });
                    return false;
                }
            }

            player.Counters.Get(Counters.Death)++;

            game.HandlePlayerEvent(GameEvents.OnPlayerDied, context.PlayerIndex, context);

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
            if (!game.TryGetComponent(Components.RespawnPointIdsId, playerId, out var respawn))
                return defaultPosition;

            var respawnStack = respawn.Value;
            if (respawnStack is null || respawnStack.Count == 0)
                return defaultPosition;

            int respawnPointId = respawnStack.Pop();
            int respawnPosition = game.GetComponent(Components.RespawnPositionId, respawnPointId);

            game.HandlePlayerEvent(GameEvents.OnRespawnPositionPopped, playerId, new()
            {
                RespawnPointId = respawnPointId,
                RespawnPosition = respawnPosition,
                RespawnPointIds = respawnStack,
                PlayerIndex = playerIndex,
            });
            return respawnPosition;
        }

        public static void PushRespawnPoint(this GameContext game, int playerIndex, int respawnPointId)
        {
            var respawnPointStorage = game.GetComponentStorage(Components.RespawnPointIdsId);
            int playerId = game.State.Players[playerIndex].ThingId;
            if (!respawnPointStorage.TryGetProxy(playerId, out var respawnPointsStackProxy))
                respawnPointsStackProxy = respawnPointStorage.Add(playerId);
            var stack = respawnPointsStackProxy.Value ??= new();
            stack.Push(respawnPointId);

            game.HandlePlayerEvent(GameEvents.OnRespawnPositionPushed, playerIndex, new()
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
            game.HandlePlayerEvent(GameEvents.OnAmountRolled, playerIndex, new()
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
            // Tick the bonuses.
            var bonuses = game.State.CurrentPlayer.StatBonuses;
            for (int i = bonuses.Count - 1; i >= 0; i--)
            {
                ref var bonus = ref bonuses.GetRef(i);
                bonus.LastsForTurns--;
                if (bonus.LastsForTurns != 0)
                    continue;

                var boost = bonus.Boost;
                game.State.CurrentPlayer.Stats.GetProxy(boost.Index).Value -= boost.Value;
                bonuses.RemoveAt(i);
            }

            // Next player
            ref int a = ref game.State.CurrentPlayerIndex;
            int previousPlayer = a;
            a = (a + 1) % game.State.Players.Length;
            int currentPlayer = a;

            game.HandleEvent(GameEvents.OnNextTurn, new()
            {
                PreviousPlayerIndex = previousPlayer,
                CurrentPlayerIndex = currentPlayer,
            });
            
            // Clear all turn locks
            game.State.TurnLock = 0;
        }

        public static void ExecuteCurrentPlayersTurn(this GameContext game)
        {
            int playerId = game.State.CurrentPlayerIndex;
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

        public static bool IsShoppingAvailable(this GameContext game, int playerIndex)
        {
            int position = game.State.Players[playerIndex].Position;
            return game.State.Shop.CellsWhereAccessible.Any(p => p == position);
        }

        public static void AddBonusToPlayer(this GameContext game, int playerIndex, Data.StatBonus bonus)
        {
            ref var player = ref game.State.Players[playerIndex];
            player.StatBonuses.Add(bonus);

            var boost = bonus.Boost;
            player.Stats.GetProxy(boost.Index).Value += boost.Value;
        }

        public struct StartPurchaseContext
        {
            public int PlayerIndex;
            public int ThingShopIndex;
        }

        public struct PurchaseContext
        {
            public List<(int Index, int ThingId)> CoinsToPayWith;
            public StartPurchaseContext Start;
            public IList<int> SelectedCoinPlacementPositions;

            public readonly int TotalCost => CoinsToPayWith.Count;
            public readonly bool Empty => CoinsToPayWith.Count == 0;
            public readonly bool NotEnoughCoins => CoinsToPayWith == null;
            public readonly int PlayerIndex => Start.PlayerIndex;
            public readonly int ThingShopIndex => Start.ThingShopIndex;
        }

        public enum StartPurchaseResult
        {
            NotEnoughCoins,
            ItemIsFree,
            RequiresToSpendCoins,
        }

        public static IEnumerable<int> GetUnoccupiedCellIndices(this GameContext game)
        {
            var cells = game.State.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Count == 0)
                    yield return i;
            }
        }

        public static StartPurchaseResult StartBuyingThingFromShop(
            this GameContext game,
            StartPurchaseContext context,
            List<(int Index, int ThingId)> outSpentCoins)
        {
            assert(outSpentCoins is not null,
                "The list will be used to output the coin id's that need to be removed, and, as such," +
                "needs to be initialized before calling this function.");
            ref var player = ref game.State.Players[context.PlayerIndex];

            var shopItems = game.State.Shop.Items;
            assert(context.ThingShopIndex < shopItems.Count);
            
            int itemId = shopItems[context.ThingShopIndex];
            if (!game.TryGetComponentValue(Components.CurrencyCostId, itemId, out int cost))
                cost = 0;

            outSpentCoins.Clear();

            if (cost == 0)
                return StartPurchaseResult.ItemIsFree;

            var coins = game.GetDataInItems(Components.CurrencyId, context.PlayerIndex);

            foreach (var coin in coins)
            {
                assert(coin.Value == 1, "We don't allow other values (at least for now)");
                cost--;
                outSpentCoins.Add((coin.ListIndex, coin.ThingId));
                if (cost == 0)
                    break;
            }

            if (cost != 0)
                return StartPurchaseResult.NotEnoughCoins;

            return StartPurchaseResult.RequiresToSpendCoins;
        }

        public static void EndBuyingThingFromShop(this GameContext game, PurchaseContext context)
        {
            assert(context.CoinsToPayWith.Count == context.SelectedCoinPlacementPositions.Count);

            int coinCount = context.CoinsToPayWith.Count;
            for (int i = coinCount - 1; i >= 0; i--)
            {
                game.RemoveItemFromInventory_AtIndex(context.PlayerIndex, context.CoinsToPayWith[i].Index);
            }
            for (int i = 0; i < coinCount; i++)
            {
                game.AddThingToCell_WithoutPlayerEvent(
                    context.CoinsToPayWith[i].ThingId,
                    context.SelectedCoinPlacementPositions[i],
                    Reasons.Buying(context.PlayerIndex));
            }

            game.AddThingFromShopToInventory(context.ThingShopIndex, context.PlayerIndex);
        }

        public static void AddThingFromShopToInventory(this GameContext game, int thingShopIndex, int playerIndex)
        {
            ref var player = ref game.State.Players[playerIndex];
            var shopItems = game.State.Shop.Items;
            int boughtIndex = thingShopIndex;
            int boughtId = shopItems[boughtIndex];
            shopItems.RemoveAt(boughtIndex);

            AddItemToInventory_MaybeDoPickupEffect(game, new()
            {
                PlayerIndex = playerIndex,
                ItemId = boughtId,
                Position = player.Position,
            });
        }

        public struct UseItemContext
        {
            public ItemInterationContext Interaction;
            public ComponentProxy<Components.ActivatedItem> Item;
            public int[] SelectedTargets;
        }

        public static bool ValidateItemUse(this GameContext game, UseItemContext context)
        {
            ref var item = ref context.Item.Value;
            var distinct = context.SelectedTargets.Distinct().ToArray();
            if (distinct.Length != context.SelectedTargets.Length)
                return false;

            var validTargets = item.Filter.GetValid(game, context.Interaction).ToHashSet();
            foreach (var t in context.SelectedTargets)
            {
                if (!validTargets.Contains(t))
                    return false;
            }
            return true;
        }

        public static void UseItem(this GameContext game, UseItemContext context)
        {
            assert(ValidateItemUse(game, context));
            
            ref var item = ref context.Item.Value;

            // Update the referenced values first, because the proxy gets invalidated after any logic happens.
            bool shouldRemove = false;
            if (item.UsesLeft != short.MaxValue)
            {
                item.UsesLeft--;
                if (item.UsesLeft == 0)
                {
                    shouldRemove = true;
                    item.UsesLeft = item.InitialUses;
                }
            }

            assert(context.SelectedTargets.Length == context.Item.Value.RequiredTargetCount);
            item.Action.DoAction(game, context.Interaction, context.SelectedTargets);

            if (shouldRemove)
            {
                int itemId = context.Interaction.ThingId;
                ref var player = ref game.State.Players[context.Interaction.PlayerIndex];
                int newItemIndex = player.Items.IndexOf(itemId);
                if (newItemIndex != -1)
                {
                    player.Items.RemoveAt(itemId);
                    game.AddThingToShop(new()
                    {
                        ThingId = itemId,
                        Reason = Reasons.ItemUsedUp(context.Interaction.PlayerIndex),
                    });
                }
            }
        }

        public static ItemInterationContext GetItemInteractionContextForCurrentPlayer(this GameContext game, int itemIndex)
        {
            int pindex = game.State.CurrentPlayerIndex;
            ref var player = ref game.State.Players[pindex];
            return new()
            {
                PlayerIndex = pindex,
                ThingId = player.Items[itemIndex],
                Position = player.Position,
            };
        }
    }

    [Serializable]
    public class GameContext : IGetEvents
    {
        public Data.Game State;
        public Events.Storage Events { get; set; }
        public IRandom Random;
        public ILogger Logger;

        public delegate void PostMovementMechanic(GameContext game, Logic.MovementContext context);
        public List<PostMovementMechanic> PostMovementMechanics;
    }

    public interface ILogger
    {
        void Debug(string message);
        void Dump(object obj);
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

    public interface IGetEvents
    {
        Events.Storage Events { get; }
    }

    public static class GameHelper
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

        public static IEnumerable<ListItemComponentProxy<T>> GetDataInCell<T>(this GameContext game, TypedIdentifier<T> componentId, int cellIndex)
        {
            return GetDataInCell<T>(game, componentId.Id, cellIndex);
        }

        public static ComponentStorage<T> GetComponentStorage<T>(this GameContext game, int componentId)
        {
            return (ComponentStorage<T>) game.State.Components.Storages[componentId];
        }

        public static ComponentStorage<T> GetComponentStorage<T>(this GameContext game, TypedIdentifier<T> componentId)
        {
            return GetComponentStorage<T>(game.State, componentId.Id);
        }

        public static ComponentStorage<T> GetComponentStorage<T>(this in Data.Game game, int componentId)
        {
            return (ComponentStorage<T>) game.Components.Storages[componentId];
        }

        public static ComponentStorage<T> GetComponentStorage<T>(this in Data.Game game, TypedIdentifier<T> componentId)
        {
            return GetComponentStorage<T>(game, componentId.Id);
        }

        public static bool TryGetComponent<T>(this GameContext game, TypedIdentifier<T> componentId, int thingId, out ComponentProxy<T> proxy)
        {
            return GetComponentStorage(game.State, componentId).TryGetProxy(thingId, out proxy);
        }

        public static bool TryGetComponentValue<T>(this GameContext game, TypedIdentifier<T> componentId, int thingId, out T value)
        {
            if (TryGetComponent<T>(game, componentId, thingId, out var proxy))
            {
                value = proxy.Value;
                return true;
            }
            value = default;
            return false;
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

        public static Events.Proxy<G, T> GetEventProxy<G, T>(this G game, int eventId)
            where G : IGetEvents
            where T : struct
        {
            return new Events.Proxy<G, T>
            {
                EventHandlers = game.Events,
                EventIndex = eventId,
            };
        }

        public static Events.Proxy<G, T> GetEventProxy<G, T>(this G game, TypedIdentifier<T> eventId)
            where G : IGetEvents
            where T : struct
        {
            return new Events.Proxy<G, T>
            {
                EventHandlers = game.Events,
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

        public static void HandleEvent<G, T>(this G game, int eventId, ref T eventData)
            where G : IGetEvents
            where T : struct
        {
            game.GetEventProxy<G, T>(eventId).Handle(game, ref eventData);
        }

        public static void HandleEvent<G, T>(this G game, TypedIdentifier<T> eventId, ref T eventData)
            where G : IGetEvents
            where T : struct
        {
            HandleEvent(game, eventId.Id, ref eventData);
        }

        public static void HandleEvent<G, T>(this G game, TypedIdentifier<T> eventId, T eventData)
            where G : IGetEvents
            where T : struct
        {
            HandleEvent(game, eventId.Id, ref eventData);
        }

        public static void HandlePlayerEvent<T>(this GameContext game, int eventId, int playerIndex, ref T eventData) where T : struct
        {
            game.GetEventProxy<GameContext, T>(eventId).Handle(game, ref eventData);
            game.State.Players[playerIndex].Events.GetEventProxy<GameContext, T>(eventId).Handle(game, ref eventData);
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
            game.GetEventProxy<GameContext, T>(eventId).HandleWithContinueCheck(game, ref eventData);
            if (eventData.Continue)
                game.State.Players[playerIndex].Events.GetEventProxy<GameContext, T>(eventId).HandleWithContinueCheck(game, ref eventData);
        }

        public static void HandlePlayerEventWithContinueCheck<T>(this GameContext game, TypedIdentifier<T> eventId, int playerIndex, ref T eventData) where T : struct, Events.IContinue
        {
            HandlePlayerEventWithContinueCheck(game, eventId.Id, playerIndex, ref eventData);
        }

        public static Events.Proxy<GameContext, TEventData> GetPlayerEventProxy<TEventData>(this GameContext game, int playerIndex, int eventId) where TEventData : struct
            => game.State.Players[playerIndex].Events.GetEventProxy<GameContext, TEventData>(eventId);

        public static ArraySegment<T> AsArraySegment<T>(this T[] from, int offset, int count)
        {
            return new(from, offset, count);
        }

        public static void TriggerSingleThingAddedToCellEvent(this GameContext game, int thingId, int cellPosition, Data.Reason reason)
        {
            var a = ArrayPool<int>.Shared.Rent(1);
            a[0] = thingId;
            game.HandleEvent(GameEvents.OnCellContentChanged, new()
            {
                AddedThings = a.AsArraySegment(0, 1),
                CellPosition = cellPosition,
                RemovedThings = Array.Empty<int>(),
                Reason = reason,
            });
            ArrayPool<int>.Shared.Return(a);
        }

        public static void TriggerSingleThingRemovedFromCellEvent(this GameContext game, int thingId, int cellPosition, Data.Reason reason)
        {
            var a = ArrayPool<int>.Shared.Rent(1);
            a[0] = thingId;
            game.HandleEvent(GameEvents.OnCellContentChanged, new()
            {
                AddedThings = Array.Empty<int>(),
                CellPosition = cellPosition,
                RemovedThings = a.AsArraySegment(0, 1),
                Reason = reason,
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
            game.HandleEvent(GameEvents.OnCellContentChanged, new()
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
            game.HandleEvent(GameEvents.OnCellContentChanged, new()
            {
                AddedThings = Array.Empty<int>(),
                CellPosition = cellPosition,
                RemovedThings = a.AsArraySegment(0, numElementsRemoved),
            });
            ArrayPool<int>.Shared.Return(a);
        }

        public static void InitializeComponentStorages(this GameContext game, int defaultSize = 4)
        {
            game.State.Components = Components.CreateComponentStorages(defaultSize);
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

    public static partial class Events
    {
        public struct Storage
        {
            public object[] Handlers;

            public Storage(int count)
            {
                Handlers = new object[count];
            }
        }

        public delegate void Handler<G, T>(G game, ref T eventData) where T : struct;

        public static Proxy<G, T> GetEventProxy<G, T>(this Storage eventHandlers, int eventId) where T : struct
        {
            return new Proxy<G, T>
            {
                EventHandlers = eventHandlers,
                EventIndex = eventId,
            };
        }

        public static Proxy<G, T> GetEventProxy<G, T>(this Storage eventHandlers, TypedIdentifier<T> eventId) where T : struct
        {
            return new Proxy<G, T>
            {
                EventHandlers = eventHandlers,
                EventIndex = eventId.Id,
            };
        }

        public struct Proxy<G, T> where T : struct
        {
            public Storage EventHandlers;
            public int EventIndex;

            public readonly void Add(Handler<G, T> handler)
            {
                ref var h = ref EventHandlers.Handlers[EventIndex];
                h = ((Handler<G, T>) h) + handler;
            }

            public readonly void Add(Action<G, T> handler)
            {
                Add((G g, ref T t) => handler(g, t));
            } 

            public readonly void Add(Action<G> handler)
            {
                Add((G g, ref T t) => handler(g));
            }

            public readonly void Add(Action handler)
            {
                Add((G g, ref T t) => handler());
            }

            public readonly void Remove(Handler<G, T> handler)
            {
                ref var h = ref EventHandlers.Handlers[EventIndex];
                h = ((Handler<G, T>) h) - handler;
            }

            public readonly Handler<G, T> Get() => (Handler<G, T>) EventHandlers.Handlers[EventIndex];
            public readonly void Set(Handler<G, T> handler) => EventHandlers.Handlers[EventIndex] = handler;
            public readonly void Handle(G game, ref T eventData)
            {
                Get()?.Invoke(game, ref eventData);
            }
            public readonly void Handle(G game, T eventData) => Handle(game, ref eventData);
        }
        
        public static void HandleWithContinueCheck<G, T>(this Proxy<G, T> proxy, G game, ref T eventData)
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
                var handler = (Handler<G, T>) invocationList[index];
                handler(game, ref eventData);
                index++;
            }
            while (index < invocationList.Length && eventData.Continue);
        }

        public interface IContinue
        {
            bool Continue { get; }
        }
    }


    public static partial class GameEvents
    {
        public static Events.Storage CreateStorage() => new(Count);

        public struct PlayerMovedContext
        {
            public Logic.MovementContext Movement;
            public MoveStartInfo MoveStart;
        }
        public static readonly TypedIdentifier<PlayerMovedContext> OnMoved = new(0);
        
        [Forward]
        public partial struct PlayerPositionChangedContext
        {
            [Forward(RejectPattern = "Kind")]
            public Logic.MovementContext Movement;
            [Forward]
            public MoveStartInfo MoveStart;

            public readonly MovementKind MovementKind => Movement.Kind;
            public readonly Data.Reason Reason => Reasons.PlayerMovement(MovementKind, PlayerIndex);
        }
        public static readonly TypedIdentifier<PlayerPositionChangedContext> OnPositionChanged = new(1);
        public static readonly TypedIdentifier<int> OnPlayerWon = new(2);
        public static readonly TypedIdentifier<ItemInterationContext> OnThingPickedUp = new(3);
        public struct SavePlayerContext : Events.IContinue
        {
            public bool WasSaved;
            public Data.Reason SaveReason;
            public Logic.KillPlayerContext Kill;

            readonly bool Events.IContinue.Continue => !WasSaved;
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
        public static readonly TypedIdentifier<ItemInterationContext> OnItemAddedToInventory = new(9);
        public static readonly TypedIdentifier<ItemRemovedContext> OnItemRemovedFromInventory = new(10);
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
            public Data.Reason Reason;
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
        public static readonly TypedIdentifier<PlayerPositionChangedContext> OnPositionChanging = new(14);
        public static readonly TypedIdentifier<PlayerPositionChangedContext> OnPositionAboutToChange = new(15);
        public struct ThingAddedToShopContext
        {
            public int ThingId;
            public Data.Reason Reason;
        }
        public static readonly TypedIdentifier<ThingAddedToShopContext> OnThingAddedToShop = new(16);

        public const int Count = 17;
    }

    public static class Data
    {
        [Serializable]
        public struct StatBonus
        {
            public StatBoost Boost;
            public int LastsForTurns;
        }

        [Serializable]
        public struct Player
        {
            public List<int> Items;
            public List<StatBonus> StatBonuses;
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
            public int CurrentPlayerIndex;
            public Components.Storage Components;
            public Shop Shop;
            public int LastThingId;

            public readonly List<int>[] Cells => Board.Cells;
            public readonly ArraySegment<List<int>> IntermediateCells => Cells.AsArraySegment(1, Cells.Length - 2);
            public readonly ref Player CurrentPlayer => ref Players[CurrentPlayerIndex];
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
        public const int PoisonId = 3;
        public const int BuyingId = 4;
        public const int PlayerMovementOffset = 5;
        private const int Waypoint0 = PlayerMovementOffset + (int) MovementKind.Count;
        public const int DebugId = Waypoint0 + 0;
        public const int PlacementId = Waypoint0 + 1;
        public const int ItemUsedUpId = Waypoint0 + 2;

        public static Data.Reason Unknown => new Data.Reason { Id = UnknownId };
        public static Data.Reason Explosion(int explodedThingId) => new Data.Reason { Id = ExplosionId, Payload = explodedThingId };
        public static Data.Reason Magic(int spellOrItemId) => new Data.Reason { Id = MagicId, Payload = spellOrItemId };
        public static Data.Reason Buying(int playerIndex) => new Data.Reason { Id = BuyingId, Payload = playerIndex, };
        public static Data.Reason PlayerMovement(MovementKind movementKind, int playerId)
            => new Data.Reason { Id = PlayerMovementOffset + (int) movementKind, Payload = playerId, };
        
        public static (MovementKind Kind, int PlayerId)? MatchPlayerMovement(this Data.Reason reason)
        {
            if (reason.Id < PlayerMovementOffset || reason.Id >= PlayerMovementOffset + (int) MovementKind.Count)
                return null;
            return ((MovementKind)(reason.Id - PlayerMovementOffset), reason.Payload);
        }

        public static Data.Reason Debug => new Data.Reason { Id = DebugId };
        public static Data.Reason Placement => new Data.Reason { Id = PlacementId };
        public static Data.Reason ItemUsedUp(int playerIndex) => new Data.Reason { Id = ItemUsedUpId, Payload = playerIndex };
    }

    public interface IComponentStorage
    {
        void SetCountToMapSize();
    }

    [Serializable]
    public class ComponentStorage<T> : IComponentStorage
    {
        public T[] Data;
        public Dictionary<int, int> MapThingIdToIndex;
        public int Count;

        // Used for serialization.
        void IComponentStorage.SetCountToMapSize()
        {
            Count = MapThingIdToIndex.Count;
        }

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

        public IEnumerable<(int Id, ComponentProxy<T> Proxy)> Enumerate()
        {
            // a terrible implementation for now
            foreach (var kvp in MapThingIdToIndex.OrderBy(kvp => kvp.Value))
            {
                yield return (kvp.Key, new()
                {
                    Index = kvp.Value,
                    Storage = Data,
                });
            }
        }
    }

    public static class Components
    {
        public struct Storage
        {
            public IComponentStorage[] Storages;
        }

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
        public struct Pickup
        {
            public IPickupEffect Effect;
            public IPickupInteraction Interaction;
        }
        public static readonly TypedIdentifier<Pickup> PickupId = new(6);
        public static readonly TypedIdentifier<object> AttachedPickupDelegateId = new(7);
        public static readonly TypedIdentifier<int> RespawnPointIdId = new(8);
        public static readonly TypedIdentifier<ThingFlags> FlagsId = new(9);

        public struct ActivatedItem
        {
            public ITargetFilter Filter;
            public ITargetedActivatedAction Action;
            public short RequiredTargetCount;
            public short UsesLeft;
            public short InitialUses;
        }
        public static readonly TypedIdentifier<ActivatedItem> ActivatedItemId = new(10);
        public const int Count = 11;

        public static ComponentStorage<T> CreateStorage<T>(TypedIdentifier<T> id, int initialSize = 4)
        {
            return new()
            {
                MapThingIdToIndex = new(initialSize),
                Data = new T[initialSize],
            };
        }

        public static ComponentStorage<T> CreateEmptyStorage<T>()
        {
            return new()
            {
            };
        }

        public static ComponentStorage<T> InitializeStorage<T>(ref Storage storage, TypedIdentifier<T> componentId, int initialSize = 4)
        {
            var t = CreateStorage<T>(componentId, initialSize);
            storage.Storages[componentId.Id] = t;
            return t;
        }

        public static ComponentStorage<T> InitializeEmptyStorage<T>(ref Storage storage, TypedIdentifier<T> componentId, int initialSize = 4)
        {
            var t = CreateEmptyStorage<T>();
            storage.Storages[componentId.Id] = t;
            return t;
        }

        public static Storage CreateComponentStorages(int defaultSize = 4)
        {
            Storage storage;
            storage.Storages = new IComponentStorage[Count];
            Components.InitializeStorage(ref storage, Components.CurrencyCostId, defaultSize);
            Components.InitializeStorage(ref storage, Components.PlayerId, defaultSize);
            Components.InitializeStorage(ref storage, Components.CurrencyId, defaultSize);
            Components.InitializeStorage(ref storage, Components.ThingSpecificEventsId, defaultSize);
            Components.InitializeStorage(ref storage, Components.RespawnPointIdsId, defaultSize);
            Components.InitializeStorage(ref storage, Components.RespawnPositionId, defaultSize);
            Components.InitializeStorage(ref storage, Components.PickupId, defaultSize);
            Components.InitializeStorage(ref storage, Components.AttachedPickupDelegateId, defaultSize);
            Components.InitializeStorage(ref storage, Components.RespawnPointIdId, defaultSize);
            Components.InitializeStorage(ref storage, Components.FlagsId, defaultSize);
            Components.InitializeStorage(ref storage, Components.ActivatedItemId, defaultSize);
            return storage;
        }

        public static Storage CreateEmptyStorages()
        {
            Storage storage;
            storage.Storages = new IComponentStorage[Count];
            Components.InitializeEmptyStorage(ref storage, Components.CurrencyCostId);
            Components.InitializeEmptyStorage(ref storage, Components.PlayerId);
            Components.InitializeEmptyStorage(ref storage, Components.CurrencyId);
            Components.InitializeEmptyStorage(ref storage, Components.ThingSpecificEventsId);
            Components.InitializeEmptyStorage(ref storage, Components.RespawnPointIdsId);
            Components.InitializeEmptyStorage(ref storage, Components.RespawnPositionId);
            Components.InitializeEmptyStorage(ref storage, Components.PickupId);
            Components.InitializeEmptyStorage(ref storage, Components.AttachedPickupDelegateId);
            Components.InitializeEmptyStorage(ref storage, Components.RespawnPointIdId);
            Components.InitializeEmptyStorage(ref storage, Components.FlagsId);
            Components.InitializeEmptyStorage(ref storage, Components.ActivatedItemId);
            return storage;
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