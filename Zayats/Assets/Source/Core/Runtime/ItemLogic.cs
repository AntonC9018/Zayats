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

        public void DoDropEffect(ItemInteractionInfo info)
        {
            GetEventProxy(info.Game, info.PlayerIndex).Remove(_handler);
        }

        public virtual void DoPickupEffect(ItemInteractionInfo info)
        {
            GetEventProxy(info.Game, info.PlayerIndex).Add(_handler);
        }

        public virtual bool ShouldRemoveFromCellOnPickup(ItemInteractionInfo info)
        {
            return true;
        }

        public virtual bool IsInventoryItem(ItemInteractionInfo info)
        {
            return true;
        }
    }

    public struct ItemInteractionInfo
    {
        public GameContext Game;
        public int ThingId;
        public int PlayerIndex;
        public int Position;
    }

    public interface IPickupEffect
    {
        void DoPickupEffect(ItemInteractionInfo info);
        void DoDropEffect(ItemInteractionInfo info);
    }

    public interface IPickup : IPickupEffect
    {
        bool ShouldRemoveFromCellOnPickup(ItemInteractionInfo info);
        bool IsInventoryItem(ItemInteractionInfo info);
    }
    
    public abstract class AttachEffectHandlerPickupBase<TEventData> : IPickup where TEventData : struct
    {
        protected abstract int EventId { get; }
        protected abstract void DoEffect(ItemInteractionInfo info, ref TEventData eventData);
        protected abstract void DoDrop(ItemInteractionInfo info);
        
        protected void DoAttach(ItemInteractionInfo info)
        {
            Events.Handler<TEventData> wrappedDelegate = (GameContext game, ref TEventData eventData) => DoEffect(info, ref eventData);
            info.Game.GetPlayerEventProxy<TEventData>(info.PlayerIndex, EventId).Add(wrappedDelegate);
            info.Game.GetComponent(Components.AttachedPickupDelegateId, info.ThingId) = wrappedDelegate;
        }

        protected void DoDetach(ItemInteractionInfo info)
        {
            ref object obj = ref info.Game.GetComponent(Components.AttachedPickupDelegateId, info.ThingId);
            var wrappedDelegate = (Events.Handler<TEventData>) obj;
            obj = null;
            info.Game.GetPlayerEventProxy<TEventData>(info.PlayerIndex, EventId).Remove(wrappedDelegate);
        }

        public void DoPickupEffect(ItemInteractionInfo info)
        {
            DoAttach(info);
        }

        public void DoDropEffect(ItemInteractionInfo info)
        {
            DoDetach(info);
            DoDrop(info);
        }

        public virtual bool ShouldRemoveFromCellOnPickup(ItemInteractionInfo info) => true;
        public virtual bool IsInventoryItem(ItemInteractionInfo info) => true;
    }

    public static class PickupHelper
    {
        public static AttachEventOnPickup<TEventData> GetAttachEventComponent<TEventData>(
            TypedIdentifier<TEventData> eventId, Events.Handler<TEventData> handler) where TEventData : struct
        {
            return new(eventId.Id, handler);
        }
    }

    public class TotemPickup : AttachEffectHandlerPickupBase<Events.SavePlayerContext>
    {
        public static readonly TotemPickup Instance = new();
        private TotemPickup(){}
        protected override int EventId => Events.OnTrySavePlayer.Id;
        protected override void DoDrop(ItemInteractionInfo info)
        {
            info.Game.AddThingToShop(info.ThingId);
        }
        protected override void DoEffect(ItemInteractionInfo info, ref Events.SavePlayerContext eventData)
        {
            eventData.SaveReason = Reasons.Magic(info.ThingId);
            eventData.WasSaved = true;
            DoDropEffect(info);
        }
    }

    public sealed class PlayerInventoryPickup : IPickup
    {
        public static readonly PlayerInventoryPickup Instance = new();
        private PlayerInventoryPickup(){}

        public bool IsInventoryItem(ItemInteractionInfo info) => true;
        public bool ShouldRemoveFromCellOnPickup(ItemInteractionInfo info) => true;

        void IPickupEffect.DoDropEffect(ItemInteractionInfo info)
        {
        }

        void IPickupEffect.DoPickupEffect(ItemInteractionInfo info)
        {
        }
    }

    public class MinePickup : IPickup
    {
        public Components.Mine _mine;

        public MinePickup(Components.Mine mine)
        {
            _mine = mine;
        }

        public void DoDropEffect(ItemInteractionInfo info)
        {
            info.Game.AddThingToShop(info.ThingId);
        }

        public void DoPickupEffect(ItemInteractionInfo info)
        {
            info.Game.KillPlayer(new()
            {
                PlayerIndex = info.PlayerIndex,
                Reason = Reasons.Explosion(info.ThingId),
            });
            if (_mine.DestroyOnDetonation)
                info.Game.DestroyThing(info.ThingId);
        }

        public bool IsInventoryItem(ItemInteractionInfo info) => _mine.PutInInventoryOnDetonation;
        public bool ShouldRemoveFromCellOnPickup(ItemInteractionInfo info) => _mine.RemoveOnDetonation;
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

        private Stats.Proxy GetProxy(in ItemInteractionInfo info)
        {
            return info.Game.State.Players[info.PlayerIndex].Stats.GetProxy(StatIndex);
        }

        public void DoDropEffect(ItemInteractionInfo info) => GetProxy(info).Value -= StatValue;
        public void DoPickupEffect(ItemInteractionInfo info) => GetProxy(info).Value += StatValue;
        public bool IsInventoryItem(ItemInteractionInfo info) => true;
        public bool ShouldRemoveFromCellOnPickup(ItemInteractionInfo info) => true;
    }

    public sealed class TowerPickup : IPickup
    {
        public static readonly TowerPickup Instance = new();
        private TowerPickup(){}

        public void DoDropEffect(ItemInteractionInfo info)
        {
            int respawnPointId = info.Game.GetComponent(Components.RespawnPointIdId, info.ThingId);
            int respawnPosition = info.Game.GetComponent(Components.RespawnPositionId, respawnPointId);
            // How do we handle dropping back on cell?
            info.Game.State.Board.Cells[respawnPosition].Things.Add(info.ThingId);
        }

        public void DoPickupEffect(ItemInteractionInfo info)
        {
            int respawnPointId = info.Game.GetComponent(Components.RespawnPointIdId, info.ThingId);
            info.Game.PushRespawnPoint(info.PlayerIndex, respawnPointId);
        }

        public bool IsInventoryItem(ItemInteractionInfo info) => true;
        public bool ShouldRemoveFromCellOnPickup(ItemInteractionInfo info) => true;
    }
}