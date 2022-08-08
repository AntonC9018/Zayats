using System.Collections.Generic;
using Kari.Plugins.AdvancedEnum;
using Kari.Plugins.Flags;
using static Zayats.Core.Assert;

namespace Zayats.Core
{
    [GenerateArrayWrapper("ThingArray")]
    public enum ThingKind
    {
        Player,
        EternalMine,
        RegularMine,
        Coin,
        RespawnPoint,
        Totem,
        Rabbit,
        Tower,
        Horse,
        Snake,
    }

    [NiceFlags]
    public enum ThingFlags
    {
        Solid = 1 << 0,
    }

    [NiceFlags]
    public enum LockFlags
    {
        // JumpedOverSolidThingsThisTurn = 1 << 0,
    }

    public struct ThingCreationProxy
    {
        public GameContext Game;
        public int Id;
    }
    
    public struct PlacementProxy
    {
        private readonly ThingCreationProxy _proxy;

        public PlacementProxy(ThingCreationProxy proxy)
        {
            _proxy = proxy;
        }

        public void At(int position)
        {
            _proxy.Game.State.Cells[position].Add(_proxy.Id);
        }
        public int Randomly(IRandom random)
        {
            int cellIndex = random.GetUnoccupiedCellIndex(_proxy.Game);
            assert(cellIndex != -1);
            _proxy.Game.State.Cells[cellIndex].Add(_proxy.Id);
            return cellIndex;
        }
        public void IntoShop()
        {
            _proxy.Game.State.Shop.Items.Add(_proxy.Id);
        }
        public void IntoInventory(int playerIndex)
        {
            _proxy.Game.State.Players[playerIndex].Items.Add(_proxy.Id);
        }
    }

    public class ThingCreationContext
    {
        private ThingCreationProxy Current;

        public GameContext Game => Current.Game;

        public ThingCreationContext(ThingCreationProxy current)
        {
            Current = current;
        }

        public ThingCreationProxy Create()
        {
            var r = Current;
            Current.Id++;
            return r;
        }
    }

    public static partial class Initialization
    {
        public static ThingCreationContext StartCreating(this GameContext game)
        {
            return new(new() { Game = game, Id = 0 });
        }

        public static ThingCreationProxy CreateWithId(this GameContext game, int id)
        {
            return new() { Game = game, Id = id };
        }
        
        public static PlacementProxy Place(this ThingCreationProxy p)
        {
            return new(p);
        }

        public static PlacementProxy PlaceThing(this GameContext game, int thingId)
        {
            return new(new() { Game = game, Id = thingId });
        }


        public static ref T AddComponent<T>(this ThingCreationProxy p, TypedIdentifier<T> id)
        {
            return ref p.Game.AddComponent(id, p.Id);
        }

        public static ThingCreationProxy Player(this ThingCreationProxy p, int playerIndex)
        {
            p.Game.InitializePlayer(index: playerIndex, p.Id);
            p.AddComponent(Components.RespawnPointIdsId) = new Stack<int>();

            {
                var stats = p.Game.State.Players[playerIndex].Stats; 
                stats.Set(Stats.RollAdditiveBonus, 0);
                stats.Set(Stats.JumpAfterMoveCapacity, 0);
            }
            return p;
        }
        public static ThingCreationProxy EternalMine(this ThingCreationProxy p)
        {
            p.AddComponent(Components.PickupId) = MinePickup.Eternal;
            p.AddComponent(Components.FlagsId) = ThingFlags.Solid;
            return p;
        }
        public static ThingCreationProxy RegularMine(this ThingCreationProxy p)
        {
            p.AddComponent(Components.PickupId) = MinePickup.Regular;
            p.AddComponent(Components.FlagsId) = ThingFlags.Solid;
            return p;
        }
        public static ThingCreationProxy Coin(this ThingCreationProxy p)
        {
            p.AddComponent(Components.CurrencyId) = 1;
            p.AddComponent(Components.PickupId) = PlayerInventoryPickup.Instance;
            return p;
        }
        public static ThingCreationProxy RespawnPoint(this ThingCreationProxy p, int position)
        {
            p.AddComponent(Components.RespawnPositionId) = position;
            return p;
        }
        public static ThingCreationProxy Totem(this ThingCreationProxy p)
        {
            p.AddComponent(Components.PickupId) = TotemPickup.Instance;
            p.AddComponent(Components.AttachedPickupDelegateId);
            return p;
        }

        public static readonly AddStatPickup RabbitPickup = new AddStatPickup(Stats.RollAdditiveBonus, 1);
        public static ThingCreationProxy Rabbit(this ThingCreationProxy p)
        {
            p.AddComponent(Components.PickupId) = RabbitPickup;
            return p;
        }
        public static ThingCreationProxy Tower(this ThingCreationProxy p)
        {
            p.AddComponent(Components.PickupId) = TowerPickup.Instance;
            p.AddComponent(Components.RespawnPointIdId);
            return p;
        }

        public static readonly AddStatPickup HorsePickup = new AddStatPickup(Stats.JumpAfterMoveCapacity, 1);
        public static ThingCreationProxy Horse(this ThingCreationProxy p)
        {
            p.AddComponent(Components.PickupId) = HorsePickup;
            return p;
        }

        public static readonly KillPlayersAction KillPlayersWithPoisonAction = new KillPlayersAction(Reasons.PoisonId);
        public static ThingCreationProxy Snake(this ThingCreationProxy p)
        {
            p.AddComponent(Components.ActivatedItemId) = new()
            {
                Filter = NearbyOtherPlayersFilter.Instance,
                Action = KillPlayersWithPoisonAction,
                Count = 1,
                InitialUses = short.MaxValue,
                UsesLeft = short.MaxValue,
            };
            p.AddComponent(Components.PickupId) = PlayerInventoryPickup.Instance;
            return p;
        }


        public static void InitializeThing(this GameContext game, int thingId, ThingKind thingKind)
        {
            panic("Not implemented");

            switch (thingKind)
            {
                default: panic("Unhandled thing: " + thingKind); break;

                case ThingKind.Player:
                {
                    panic("Players should be handled separately. They require an index to be spawned.");
                    break;
                }
                case ThingKind.EternalMine:
                {
                    break;
                }
                case ThingKind.RegularMine:
                {
                    break;
                }
                case ThingKind.Coin:
                {
                    break;
                }
                case ThingKind.RespawnPoint:
                {
                    break;
                }
                case ThingKind.Totem:
                {
                    break;
                }
                case ThingKind.Rabbit:
                {
                    break;
                }
                case ThingKind.Tower:
                {
                    break;
                }
                case ThingKind.Horse:
                {
                    break;
                }
            }
        }
    }
}