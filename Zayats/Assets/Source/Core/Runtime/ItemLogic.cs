using Kari.Plugins.Forward;

namespace Zayats.Core
{
    using static GameEvents;
    using static ForwardOptions;

    public class AttachEventOnPickup<TEventData> : IPickup where TEventData : struct
    {
        private readonly int _eventId;
        private readonly Events.Handler<GameContext, TEventData> _handler;

        public AttachEventOnPickup(int eventId, Events.Handler<GameContext, TEventData> handler)
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

    public interface IPickup : IPickupEffect
    {
        bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInterationContext info);
        bool IsInventoryItem(GameContext game, ItemInterationContext info);
    }
    
    public abstract class AttachEffectHandlerPickupBase<TEventData> : IPickup where TEventData : struct
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

        public virtual bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInterationContext info) => true;
        public virtual bool IsInventoryItem(GameContext game, ItemInterationContext info) => true;
    }

    public static class PickupHelper
    {
        public static AttachEventOnPickup<TEventData> GetAttachEventComponent<TEventData>(
            TypedIdentifier<TEventData> eventId, Events.Handler<GameContext, TEventData> handler) where TEventData : struct
        {
            return new(eventId.Id, handler);
        }
    }

    public sealed class TotemPickup : AttachEffectHandlerPickupBase<SavePlayerContext>
    {
        public static readonly TotemPickup Instance = new(Reasons.ExplosionId);
        private TotemPickup(int reasonFromWhichToProtect)
        {
            _reasonFromWhichToProtect = reasonFromWhichToProtect;
        }
        protected override int EventId => GameEvents.OnTrySavePlayer.Id;
        private int _reasonFromWhichToProtect;
        
        protected override void DoDrop(GameContext game, ItemInterationContext info)
        {
            game.AddThingToShop(info.ThingId);
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

    public sealed class PlayerInventoryPickup : IPickup
    {
        public static readonly PlayerInventoryPickup Instance = new();
        private PlayerInventoryPickup(){}

        public bool IsInventoryItem(GameContext game, ItemInterationContext info) => true;
        public bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInterationContext info) => true;

        void IPickupEffect.DoDropEffect(GameContext game, ItemInterationContext info)
        {
        }

        void IPickupEffect.DoPickupEffect(GameContext game, ItemInterationContext info)
        {
        }
    }

    public class MinePickup : IPickup
    {
        public Components.Mine _mine;
        
        public static readonly MinePickup Regular = new(new()
        {
            DestroyOnDetonation = false,
            PutInInventoryOnDetonation = true,
            RemoveOnDetonation = true,
        });
        public static readonly MinePickup Eternal = new(new()
        {
            DestroyOnDetonation = false,
            PutInInventoryOnDetonation = false,
            RemoveOnDetonation = false,
        });

        public MinePickup(Components.Mine mine)
        {
            _mine = mine;
        }

        public void DoDropEffect(GameContext game, ItemInterationContext info)
        {
            game.AddThingToShop(info.ThingId);
        }

        public void DoPickupEffect(GameContext game, ItemInterationContext info)
        {
            game.KillPlayer(new()
            {
                PlayerIndex = info.PlayerIndex,
                Reason = Reasons.Explosion(info.ThingId),
            });
            if (_mine.DestroyOnDetonation)
                game.DestroyThing(info.ThingId);
        }

        public bool IsInventoryItem(GameContext game, ItemInterationContext info) => _mine.PutInInventoryOnDetonation;
        public bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInterationContext info) => _mine.RemoveOnDetonation;
    }

    public sealed class AddStatPickup : IPickup
    {
        public AddStatPickup(TypedIdentifier<float> id, float value) : this(id.Id, value)
        {
        }
        
        public AddStatPickup(TypedIdentifier<int> id, int value) : this(id.Id, (float) value)
        {
        }
        
        public AddStatPickup(int id, float value)
        {
            StatValue = value;
            StatIndex = id;
        }

        public float StatValue { get; }
        public int StatIndex { get; }

        private Stats.Proxy GetProxy(GameContext game, in ItemInterationContext info)
        {
            return game.State.Players[info.PlayerIndex].Stats.GetProxy(StatIndex);
        }

        public void DoDropEffect(GameContext game, ItemInterationContext info) => GetProxy(game, info).Value -= StatValue;
        public void DoPickupEffect(GameContext game, ItemInterationContext info) => GetProxy(game, info).Value += StatValue;
        public bool IsInventoryItem(GameContext game, ItemInterationContext info) => true;
        public bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInterationContext info) => true;
    }

    public sealed class TowerPickup : IPickup
    {
        public static readonly TowerPickup Instance = new();
        private TowerPickup(){}

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

        public bool IsInventoryItem(GameContext game, ItemInterationContext info) => true;
        public bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInterationContext info) => true;
    }

    public enum ActivatedItemKind
    {
        None,
        SelectCell,
        SelectEmptyCell,
        SelectPlayer,
        SelectPlayerOtherThanSelf,
    }
}