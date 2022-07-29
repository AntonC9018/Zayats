namespace Zayats.Core
{
    public class AttachEventOnPickup<TEventData> : IPickup where TEventData : struct
    {
        private readonly int _eventId;
        private readonly Events.Handler<TEventData> _handler;

        public AttachEventOnPickup(int eventId, Events.Handler<TEventData> handler)
        {
            _eventId = eventId;
            _handler = handler;
        }

        public Events.Proxy<TEventData> GetEventProxy(GameContext game, int playerIndex)
            => game.GetPlayerEventProxy<TEventData>(playerIndex, _eventId);

        public void DoDropEffect(GameContext game, ItemInteractionInfo info)
        {
            GetEventProxy(game, info.PlayerIndex).Remove(_handler);
        }

        public virtual void DoPickupEffect(GameContext game, ItemInteractionInfo info)
        {
            GetEventProxy(game, info.PlayerIndex).Add(_handler);
        }

        public virtual bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInteractionInfo info)
        {
            return true;
        }

        public virtual bool IsInventoryItem(GameContext game, ItemInteractionInfo info)
        {
            return true;
        }
    }

    public struct ItemInteractionInfo
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
        void DoPickupEffect(GameContext game, ItemInteractionInfo info);
        void DoDropEffect(GameContext game, ItemInteractionInfo info);
    }

    public interface IPickup : IPickupEffect
    {
        bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInteractionInfo info);
        bool IsInventoryItem(GameContext game, ItemInteractionInfo info);
    }
    
    public abstract class AttachEffectHandlerPickupBase<TEventData> : IPickup where TEventData : struct
    {
        protected abstract int EventId { get; }
        protected abstract void DoEffect(GameContext game, ItemInteractionInfo info, ref TEventData eventData);
        protected abstract void DoDrop(GameContext game, ItemInteractionInfo info);
        
        protected void DoAttach(GameContext game, ItemInteractionInfo info)
        {
            Events.Handler<TEventData> wrappedDelegate = (GameContext game, ref TEventData eventData) => DoEffect(game, info, ref eventData);
            game.GetPlayerEventProxy<TEventData>(info.PlayerIndex, EventId).Add(wrappedDelegate);
            game.GetComponent(Components.AttachedPickupDelegateId, info.ThingId) = wrappedDelegate;
        }

        protected void DoDetach(GameContext game, ItemInteractionInfo info)
        {
            ref object obj = ref game.GetComponent(Components.AttachedPickupDelegateId, info.ThingId);
            var wrappedDelegate = (Events.Handler<TEventData>) obj;
            obj = null;
            game.GetPlayerEventProxy<TEventData>(info.PlayerIndex, EventId).Remove(wrappedDelegate);
        }

        public void DoPickupEffect(GameContext game, ItemInteractionInfo info)
        {
            DoAttach(game, info);
        }

        public void DoDropEffect(GameContext game, ItemInteractionInfo info)
        {
            DoDetach(game, info);
            DoDrop(game, info);
        }

        public virtual bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInteractionInfo info) => true;
        public virtual bool IsInventoryItem(GameContext game, ItemInteractionInfo info) => true;
    }

    public static class PickupHelper
    {
        public static AttachEventOnPickup<TEventData> GetAttachEventComponent<TEventData>(
            TypedIdentifier<TEventData> eventId, Events.Handler<TEventData> handler) where TEventData : struct
        {
            return new(eventId.Id, handler);
        }
    }

    public sealed class TotemPickup : AttachEffectHandlerPickupBase<Events.SavePlayerContext>
    {
        public static readonly TotemPickup Instance = new(Reasons.ExplosionId);
        private TotemPickup(int reasonFromWhichToProtect)
        {
            _reasonFromWhichToProtect = reasonFromWhichToProtect;
        }
        protected override int EventId => Events.OnTrySavePlayer.Id;
        private int _reasonFromWhichToProtect;
        
        protected override void DoDrop(GameContext game, ItemInteractionInfo info)
        {
            game.AddThingToShop(info.ThingId);
        }
        protected override void DoEffect(GameContext game, ItemInteractionInfo info, ref Events.SavePlayerContext eventData)
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

        public bool IsInventoryItem(GameContext game, ItemInteractionInfo info) => true;
        public bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInteractionInfo info) => true;

        void IPickupEffect.DoDropEffect(GameContext game, ItemInteractionInfo info)
        {
        }

        void IPickupEffect.DoPickupEffect(GameContext game, ItemInteractionInfo info)
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

        public void DoDropEffect(GameContext game, ItemInteractionInfo info)
        {
            game.AddThingToShop(info.ThingId);
        }

        public void DoPickupEffect(GameContext game, ItemInteractionInfo info)
        {
            game.KillPlayer(new()
            {
                PlayerIndex = info.PlayerIndex,
                Reason = Reasons.Explosion(info.ThingId),
            });
            if (_mine.DestroyOnDetonation)
                game.DestroyThing(info.ThingId);
        }

        public bool IsInventoryItem(GameContext game, ItemInteractionInfo info) => _mine.PutInInventoryOnDetonation;
        public bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInteractionInfo info) => _mine.RemoveOnDetonation;
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

        private Stats.Proxy GetProxy(GameContext game, in ItemInteractionInfo info)
        {
            return game.State.Players[info.PlayerIndex].Stats.GetProxy(StatIndex);
        }

        public void DoDropEffect(GameContext game, ItemInteractionInfo info) => GetProxy(game, info).Value -= StatValue;
        public void DoPickupEffect(GameContext game, ItemInteractionInfo info) => GetProxy(game, info).Value += StatValue;
        public bool IsInventoryItem(GameContext game, ItemInteractionInfo info) => true;
        public bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInteractionInfo info) => true;
    }

    public sealed class TowerPickup : IPickup
    {
        public static readonly TowerPickup Instance = new();
        private TowerPickup(){}

        public void DoDropEffect(GameContext game, ItemInteractionInfo info)
        {
            int respawnPointId = game.GetComponent(Components.RespawnPointIdId, info.ThingId);
            int respawnPosition = game.GetComponent(Components.RespawnPositionId, respawnPointId);
            // How do we handle dropping back on cell?
            game.State.Cells[respawnPosition].Add(info.ThingId);
        }

        public void DoPickupEffect(GameContext game, ItemInteractionInfo info)
        {
            int respawnPointId = game.GetComponent(Components.RespawnPointIdId, info.ThingId);
            game.PushRespawnPoint(info.PlayerIndex, respawnPointId);
        }

        public bool IsInventoryItem(GameContext game, ItemInteractionInfo info) => true;
        public bool ShouldRemoveFromCellOnPickup(GameContext game, ItemInteractionInfo info) => true;
    }
}