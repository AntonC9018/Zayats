using System;
using System.Collections.Generic;
using System.Linq;
using Kari.Plugins.AdvancedEnum;
using Kari.Plugins.Forward;
using Kari.Zayats.Exporter;

namespace Zayats.Core
{
    using static GameEvents;
    using static ForwardOptions;
    using static Assert;

    public class AttachEventOnPickupEffect<TEventData> : IPickupEffect where TEventData : struct
    {
        private readonly int _eventId;
        private readonly Events.Handler<GameContext, TEventData> _handler;

        public AttachEventOnPickupEffect(int eventId, Events.Handler<GameContext, TEventData> handler)
        {
            _eventId = eventId;
            _handler = handler;
        }

        public Events.Proxy<GameContext, TEventData> GetEventProxy(GameContext game, int playerIndex)
            => game.GetPlayerEventProxy<TEventData>(playerIndex, _eventId);

        public void DoDropEffect(GameContext game, ItemInterationContext info)
        {
            GetEventProxy(game, info.PlayerIndex).Remove(_handler);
        }

        public virtual void DoPickupEffect(GameContext game, ItemInterationContext info)
        {
            GetEventProxy(game, info.PlayerIndex).Add(_handler);
        }

        public virtual bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInterationContext info)
        {
            return true;
        }

        public virtual bool IsInventoryItem(GameContext game, ItemInterationContext info)
        {
            return true;
        }
    }

    // public partial struct AddItemContext
    // {
    //     [Forward] public ItemInterationContext PlayerInteration;
    //     public static implicit operator ItemInterationContext(AddItemContext context) => context.PlayerInteration;
    // }

    public partial struct ItemRemovedContext
    {
        [Forward] public ItemInterationContext PlayerInteration;
        public int ItemIndex;
    }

    public struct ItemInterationContext
    {
        public int ItemId
        {
            readonly get => ThingId;
            set => ThingId = value;
        }
        public int ThingId;
        public int PlayerIndex;
        public int Position;
    }

    public interface IPickupEffect
    {
        void DoPickupEffect(GameContext game, ItemInterationContext info);
        void DoDropEffect(GameContext game, ItemInterationContext info);
    }

    public interface IPickupInteraction
    {
        bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInterationContext info);
        bool IsInventoryItem(GameContext game, ItemInterationContext info);
    }

    public class ConstantPickupInteration : IPickupInteraction
    {
        private bool _shouldRemove;
        private bool _isInventory;

        public ConstantPickupInteration(bool shouldRemove, bool isInventory)
        {
            _shouldRemove = shouldRemove;
            _isInventory = isInventory;
        }

        public bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInterationContext info) => _shouldRemove;
        public bool IsInventoryItem(GameContext game, ItemInterationContext info) => _isInventory;
    }

    public abstract class AttachEffectHandlerPickupEffectBase<TEventData> : IPickupEffect where TEventData : struct
    {
        protected abstract int EventId { get; }
        protected abstract void DoEffect(GameContext game, ItemInterationContext info, ref TEventData eventData);
        protected abstract void DoDrop(GameContext game, ItemInterationContext info);
        
        protected void DoAttach(GameContext game, ItemInterationContext info)
        {
            Events.Handler<GameContext, TEventData> wrappedDelegate = (GameContext game, ref TEventData eventData) => DoEffect(game, info, ref eventData);
            game.GetPlayerEventProxy<TEventData>(info.PlayerIndex, EventId).Add(wrappedDelegate);
            game.GetComponent(Components.AttachedPickupDelegateId, info.ThingId) = wrappedDelegate;
        }

        protected void DoDetach(GameContext game, ItemInterationContext info)
        {
            ref object obj = ref game.GetComponent(Components.AttachedPickupDelegateId, info.ThingId);
            var wrappedDelegate = (Events.Handler<GameContext, TEventData>) obj;
            obj = null;
            game.GetPlayerEventProxy<TEventData>(info.PlayerIndex, EventId).Remove(wrappedDelegate);
        }

        public void DoPickupEffect(GameContext game, ItemInterationContext info)
        {
            DoAttach(game, info);
        }

        public void DoDropEffect(GameContext game, ItemInterationContext info)
        {
            DoDetach(game, info);
            DoDrop(game, info);
        }
    }

    public static class PickupHelper
    {
        public static AttachEventOnPickupEffect<TEventData> GetAttachEventComponent<TEventData>(
            TypedIdentifier<TEventData> eventId, Events.Handler<GameContext, TEventData> handler) where TEventData : struct
        {
            return new(eventId.Id, handler);
        }

        public static Components.Pickup AsPickup(this IPickupEffect effect)
        {
            return new()
            {
                Interaction = Pickups.DefaultInventoryInteraction,
                Effect = effect,
            };
        }
    }

    public sealed class TotemPickupEffect : AttachEffectHandlerPickupEffectBase<SavePlayerContext>
    {
        [Export]
        public static readonly TotemPickupEffect Instance = new(Reasons.ExplosionId);

        private TotemPickupEffect(int reasonFromWhichToProtect)
        {
            _reasonFromWhichToProtect = reasonFromWhichToProtect;
        }
        protected override int EventId => GameEvents.OnTrySavePlayer.Id;
        private int _reasonFromWhichToProtect;
        
        protected override void DoDrop(GameContext game, ItemInterationContext info)
        {
            game.AddThingToShop(new()
            {
                ThingId = info.ThingId,
                Reason = Reasons.ItemUsedUp(info.PlayerIndex),
            });
        }
        protected override void DoEffect(GameContext game, ItemInterationContext info, ref SavePlayerContext eventData)
        {
            int reason = eventData.Kill.Reason.Id;

            // foreach (int reasonId in _reasonsFromWhichToProtect)
            if (reason == _reasonFromWhichToProtect)
            {
                eventData.SaveReason = Reasons.Magic(info.ThingId);
                eventData.WasSaved = true;

                DoDropEffect(game, info);
                game.RemoveItemFromInventory(info);
            }
        }
    }

    public sealed class DoNothingPickupEffect : IPickupEffect
    {
        [Export]
        public static readonly DoNothingPickupEffect Instance = new();
        private DoNothingPickupEffect(){}

        void IPickupEffect.DoDropEffect(GameContext game, ItemInterationContext info)
        {
        }

        void IPickupEffect.DoPickupEffect(GameContext game, ItemInterationContext info)
        {
        }
    }

    public class MinePickupEffect : IPickupEffect
    {
        public readonly bool DestroyOnDetonation;
        private MinePickupEffect(bool destroyOnDetonation)
        {
            DestroyOnDetonation = destroyOnDetonation;
        }

        [Export]
        public static readonly MinePickupEffect DestroysOnDetonation = new(true);
        [Export]
        public static readonly MinePickupEffect StaysOnDetonation = new(false);

        public void DoDropEffect(GameContext game, ItemInterationContext info)
        {
        }

        public void DoPickupEffect(GameContext game, ItemInterationContext info)
        {
            game.KillPlayer(new()
            {
                PlayerIndex = info.PlayerIndex,
                Reason = Reasons.Explosion(info.ThingId),
            });
            if (DestroyOnDetonation)
                game.DestroyThing(info.ThingId);
        }
    }

    [Serializable]
    public struct StatBoost
    {
        public float Value;
        public int Index;

        public StatBoost(TypedIdentifier<float> id, float value) : this(id.Id, value)
        {
        }
        
        public StatBoost(TypedIdentifier<int> id, int value) : this(id.Id, (float) value)
        {
        }
        
        public StatBoost(int id, float value)
        {
            Value = value;
            Index = id;
        }

        public readonly Stats.Proxy GetProxy(GameContext game, int playerIndex)
        {
            return game.State.Players[playerIndex].Stats.GetProxy(Index);
        }

        public readonly void AddValue(GameContext game, int playerIndex)
        {
            GetProxy(game, playerIndex).Value += Value;
        }

        public readonly void SubtractValue(GameContext game, int playerIndex)
        {
            GetProxy(game, playerIndex).Value -= Value;
        }
    }

    public sealed class AddStatPickupEffect : IPickupEffect
    {
        public readonly StatBoost _boost;
        
        public AddStatPickupEffect(StatBoost boost)
        {
            _boost = boost;
        }


        public void DoDropEffect(GameContext game, ItemInterationContext info) => _boost.AddValue(game, info.PlayerIndex);
        public void DoPickupEffect(GameContext game, ItemInterationContext info) => _boost.SubtractValue(game, info.PlayerIndex);
    }

    public sealed class TowerPickupEffect : IPickupEffect
    {
        [Export]
        public static readonly TowerPickupEffect Instance = new();
        private TowerPickupEffect(){}

        public void DoDropEffect(GameContext game, ItemInterationContext info)
        {
            int respawnPointId = game.GetComponent(Components.RespawnPointIdId, info.ThingId);
            int respawnPosition = game.GetComponent(Components.RespawnPositionId, respawnPointId);
            // How do we handle dropping back on cell?
            game.State.Cells[respawnPosition].Add(info.ThingId);
        }

        public void DoPickupEffect(GameContext game, ItemInterationContext info)
        {
            int respawnPointId = game.GetComponent(Components.RespawnPointIdId, info.ThingId);
            game.PushRespawnPoint(info.PlayerIndex, respawnPointId);
        }
    }

    public static class Pickups
    {
        [Export]
        public static readonly ConstantPickupInteration DefaultInventoryInteraction = new(shouldRemove: true, isInventory: true);
        public static readonly Components.Pickup Tower = new()
        {
            Effect = TowerPickupEffect.Instance,
            Interaction = DefaultInventoryInteraction,
        };

        [Export]
        public static readonly ConstantPickupInteration _EternalMineInteraction = new(shouldRemove: false, isInventory: false);
        public static readonly Components.Pickup EternalMine = new()
        {
            Effect = MinePickupEffect.StaysOnDetonation,
            Interaction = _EternalMineInteraction,
        };

        public static readonly ConstantPickupInteration _RegularMineInteraction = DefaultInventoryInteraction;
        public static readonly Components.Pickup RegularMine = new()
        {
            Effect = MinePickupEffect.StaysOnDetonation,
            Interaction = _RegularMineInteraction,
        };

        public static readonly Components.Pickup Totem = new()
        {
            Effect = TotemPickupEffect.Instance,
            Interaction = DefaultInventoryInteraction,
        };
    }

    public enum TargetKind
    {
        None,
        Cell,
        Player,
        Thing,
    }

    public interface ITargetFilter
    {
        TargetKind Kind { get; }

        // Returns either cells indices, player indices or thing id's
        IEnumerable<int> GetValid(GameContext game, ItemInterationContext context);
    }

    public sealed class NearbyOtherPlayersFilter : ITargetFilter
    {
        [Export]
        public static readonly NearbyOtherPlayersFilter Instance = new();
        public TargetKind Kind => TargetKind.Player;
        public IEnumerable<int> GetValid(GameContext game, ItemInterationContext context)
        {
            if (context.Position == 0)
                yield break;

            for (int i = 0; i < game.State.Players.Length; i++)
            {
                if (i == context.PlayerIndex)
                    continue;
                var p = game.State.Players[i].Position;
                if (p == 0 || p == game.State.Cells.Length - 1)
                    continue;
                if (Math.Abs(p - context.Position) <= 1)
                    yield return p; 
            }
            
            #if false
            {
                var pos = context.Position - 1;
                if (pos > 0)
                {
                    foreach (var p in game.GetDataInCell(Components.PlayerId, cellIndex: pos))
                        yield return p.Value.PlayerIndex;
                }
            }
            // Even though the player topples over other players,
            // that mechanic might be disabled or different.
            {
                var pos = context.Position;
                if (pos < game.State.Cells.Length - 1)
                {
                    foreach (var p in game.GetDataInCell(Components.PlayerId, cellIndex: pos))
                    {
                        int i = p.Value.PlayerIndex;
                        if (i != context.PlayerIndex)
                            yield return i;
                    }
                }
            }
            {
                var pos = context.Position + 1;
                if (pos < game.State.Cells.Length - 1)
                {
                    foreach (var p in game.GetDataInCell(Components.PlayerId, cellIndex: pos))
                        yield return p.Value.PlayerIndex;
                }
            }
            #endif
        }
    }

    public sealed class UnoccupiedCellFilter : ITargetFilter
    {
        [Export]
        public static readonly UnoccupiedCellFilter Instance = new();
        public TargetKind Kind => TargetKind.Cell;
        public IEnumerable<int> GetValid(GameContext game, ItemInterationContext context)
        {
            return game.GetUnoccupiedCellIndices();
        }
    }

    public sealed class NoTargetFilter : ITargetFilter
    {
        [Export]
        public static readonly NoTargetFilter Instance = new();
        public TargetKind Kind => TargetKind.None;
        public IEnumerable<int> GetValid(GameContext game, ItemInterationContext context)
        {
            return Enumerable.Empty<int>();
        }
    }

    public interface ITargetedActivatedAction
    {
        void DoAction(GameContext game, ItemInterationContext context, IEnumerable<int> targets);
    }

    public sealed class KillPlayersAction : ITargetedActivatedAction
    {
        public int ReasonId { get; }
        public KillPlayersAction(int reasonId)
        {
            ReasonId = reasonId;
        }

        public void DoAction(GameContext game, ItemInterationContext context, IEnumerable<int> targets)
        {
            foreach (var t in targets)
            {
                game.KillPlayer(new()
                {
                    PlayerIndex = t,
                    Reason = new()
                    {
                        Id = ReasonId,
                        Payload = context.ThingId,
                    }
                });
            }
        }
    }

    public sealed class AddStatBonusToUser_Action : ITargetedActivatedAction
    {
        private readonly Data.StatBonus _bonus;
        public AddStatBonusToUser_Action(StatBoost boost, int lastsForTurns = 1)
        {
            _bonus.Boost = boost;
            _bonus.LastsForTurns = lastsForTurns;
        }

        public void DoAction(GameContext game, ItemInterationContext context, IEnumerable<int> targets)
        {
            ref var player = ref game.State.Players[context.PlayerIndex];
            player.StatBonuses.Add(_bonus);
        }
    }


    [GenerateArrayWrapper]
    public enum ItemUsability
    {
        None,
        NotEnoughSpots,
        Usable,
    }

    public static class ItemHelper
    {
        public static ItemUsability GetItemUsability(this GameContext game, int playerIndex, int itemId)
        {
            if (!game.TryGetComponent(Components.ActivatedItemId, itemId, out var proxy)
                || proxy.Value.Action is null)
            {
                return ItemUsability.None;
            }

            var valid = proxy.Value.Filter.GetValid(game, new()
            {
                PlayerIndex = playerIndex,
                Position = game.State.Players[playerIndex].Position,
                ThingId = itemId,
            });
            int requiredCount = proxy.Value.RequiredTargetCount;
            if (valid.Take(requiredCount).Count() < requiredCount)
                return ItemUsability.NotEnoughSpots;

            return ItemUsability.Usable;
        }
    }

    public class PlaceItemFromInventoryAction : ITargetedActivatedAction
    {
        [Export]
        public static readonly PlaceItemFromInventoryAction Instance = new();
        private PlaceItemFromInventoryAction(){}
        public void DoAction(GameContext game, ItemInterationContext context, IEnumerable<int> targets)
        {
            assert(targets.Count() == 1);
            var t = targets.First();

            var itemId = context.ItemId;
            var playerIndex = context.PlayerIndex;

            game.RemoveItemFromInventory(context);
            game.PlaceThing(itemId).At(t);

            // TODO: integrate this into the placement logic.
            game.TriggerSingleThingAddedToCellEvent(itemId, t, Reasons.Placement);
        }
    }

    // public class ActivatedItemConfig
    // {
    //     public IActivatedAction Action { get; }
    //     public ITargetFilter Filter { get; }
    //     public int InitialUses { get; }

    //     public ActivatedItemConfig(IActivatedAction action, ITargetFilter filter, int initialUses)
    //     {
    //         Action = action;
    //         Filter = filter;
    //         InitialUses = initialUses;
    //     }
    // }
}