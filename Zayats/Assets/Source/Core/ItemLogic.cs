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

    }

    public struct ItemInteractionInfo
    {
        public GameContext Game;
        public int ThingId;
        public int PlayerIndex;
        public int Position;
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
            info.Game.GetComponent(Components.AttachedPickupDelegateId, info.ThingId).Component = wrappedDelegate;
        }

        protected void DoDetach(ItemInteractionInfo info)
        {
            ref object obj = ref info.Game.GetComponent(Components.AttachedPickupDelegateId, info.ThingId).Component;
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

        protected void ReturnToShop(ItemInteractionInfo info)
        {
            info.Game.State.Shop.Items.Add(info.ThingId);
        }
    }

    public static class PickupHelper
    {
        public static AttachEventOnPickup<TEventData> GetAttachEventComponent<TEventData>(
            TypedIdentifier<TEventData> eventId, Events.Handler<TEventData> handler) where TEventData : struct
        {
            return new(eventId.Id, handler);
        }

        public static void DropAddingToCell(this IPickup pickup, ItemInteractionInfo info)
        {
            info.Game.State.Board.Cells[info.Position].Things.Add(info.ThingId);
            pickup.DoDropEffect(info);
        }

        public static void PickupRemovingFromCell(this IPickup pickup, ItemInteractionInfo info)
        {
            info.Game.State.Board.Cells[info.Position].Things.Remove(info.ThingId);
            pickup.DoPickupEffect(info);
        }
    }

    public class TotemPickup : AttachEffectHandlerPickupBase<Events.SavePlayerContext>
    {
        public static readonly TotemPickup Instance = new();
        private TotemPickup(){}
        protected override int EventId => Events.OnTrySavePlayer.Id;
        protected override void DoDrop(ItemInteractionInfo info)
        {
            ReturnToShop(info);
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

        void IPickup.DoDropEffect(ItemInteractionInfo info)
        {
            info.Game.State.Players[info.PlayerIndex].Items.Remove(info.ThingId);
        }

        void IPickup.DoPickupEffect(ItemInteractionInfo info)
        {
            info.Game.State.Players[info.PlayerIndex].Items.Add(info.ThingId);
        }

        public static void DoDropEffect(ItemInteractionInfo info)
        {
            info.Game.State.Players[info.PlayerIndex].Items.Remove(info.ThingId);
        }

        public static void DoPickupEffect(ItemInteractionInfo info)
        {
            info.Game.State.Players[info.PlayerIndex].Items.Add(info.ThingId);
        }
    }

    public sealed class MinePickup : IPickup
    {
        private static Components.Mine GetMineComponent(ItemInteractionInfo info)
        {
            var flags = info.Game.GetComponent(Components.MineId, info.ThingId).Component;
            return flags;
        }

        void IPickup.DoDropEffect(ItemInteractionInfo info)
        {
            var flags = GetMineComponent(info);
            
        }

        void IPickup.DoPickupEffect(ItemInteractionInfo info)
        {
            throw new System.NotImplementedException();
        }
    }
}