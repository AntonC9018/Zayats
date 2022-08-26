using System.Collections.Generic;
using Kari.Plugins.AdvancedEnum;
using Kari.Plugins.Flags;
using static Zayats.Core.Assert;
using Kari.Zayats.Exporter;

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
        Booze,
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

    public struct DynamicThingCreationProxy
    {
        private ThingCreationProxy Proxy;
        public readonly GameContext Game => Proxy.Game;
        public readonly int Id => Proxy.Id;
    }

    #if false
    public enum PlacementKind
    {
        At,
        Random,
        Shop,
        Inventory,
    }

    public struct PlacementConfiguration
    {
        public PlacementKind Kind;
        public int Payload;
    }
    #endif
    
    public struct PlacementProxy
    {
        private readonly ThingCreationProxy _proxy;

        public PlacementProxy(ThingCreationProxy proxy)
        {
            _proxy = proxy;
        }

        public void At(int position)
        {
            _proxy.Game.AddThingToCell_WithoutPlayerEvent(_proxy.Id, position, Reasons.Placement);
        }
        public int Randomly(IRandom random)
        {
            int cellIndex = random.GetUnoccupiedCellIndex(_proxy.Game);
            assert(cellIndex != -1);
            At(cellIndex);
            return cellIndex;
        }
        public void IntoShop()
        {
            _proxy.Game.AddThingToShop(new()
            {
                ThingId = _proxy.Id,
                Reason = Reasons.Placement,
            });
        }
        public void IntoInventory(int playerIndex)
        {
            _proxy.Game.AddItemToInventory_MaybeDoPickupEffect(new()
            {
                PlayerIndex = playerIndex,
                Position = 0,
                ThingId = _proxy.Id,
            });
        }
    }

    public struct ThingCreationContext
    {
        public readonly GameContext Game;

        public ThingCreationContext(GameContext game)
        {
            Game = game;
        }

        public ThingCreationProxy Create()
        {
            var r = Game.State.LastThingId;
            Game.State.LastThingId++;
            return new ThingCreationProxy { Game = Game, Id = r };
        }
    }

    public static partial class Initialization
    {
        public static ThingCreationContext StartCreating(this GameContext game)
        {
            return new(game);
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
            p.AddComponent(Components.PickupId) = Pickups.EternalMine;
            p.AddComponent(Components.FlagsId) = ThingFlags.Solid;
            return p;
        }
        public static ThingCreationProxy RegularMine(this ThingCreationProxy p)
        {
            p.AddComponent(Components.PickupId) = Pickups.RegularMine;
            p.AddComponent(Components.FlagsId) = ThingFlags.Solid;
            p.AddComponent(Components.ActivatedItemId) = new()
            {
                Action = PlaceItemFromInventoryAction.Instance,
                Filter = UnoccupiedCellFilter.Instance,
                InitialUses = 1,
                RequiredTargetCount = 1,
            };
            return p;
        }
        public static ThingCreationProxy Coin(this ThingCreationProxy p)
        {
            p.AddComponent(Components.CurrencyId) = 1;
            p.AddComponent(Components.PickupId) = DoNothingPickupEffect.Instance.AsPickup();
            return p;
        }
        public static ThingCreationProxy RespawnPoint(this ThingCreationProxy p, int position)
        {
            p.AddComponent(Components.RespawnPositionId) = position;
            return p;
        }
        public static ThingCreationProxy Totem(this ThingCreationProxy p)
        {
            p.AddComponent(Components.PickupId) = Pickups.Totem;
            p.AddComponent(Components.AttachedPickupDelegateId);
            return p;
        }

        [Export]
        public static readonly AddStatPickupEffect RabbitPickupEffect = new(boost: new(Stats.RollAdditiveBonus, 1));
        public static ThingCreationProxy Rabbit(this ThingCreationProxy p)
        {
            p.AddComponent(Components.PickupId) = RabbitPickupEffect.AsPickup();
            return p;
        }
        
        public static ThingCreationProxy Tower(this ThingCreationProxy p)
        {
            p.AddComponent(Components.PickupId) = Pickups.Tower;
            p.AddComponent(Components.RespawnPointIdId);
            return p;
        }

        [Export]
        public static readonly AddStatPickupEffect HorsePickup = new(boost: new(Stats.JumpAfterMoveCapacity, 1));
        public static ThingCreationProxy Horse(this ThingCreationProxy p)
        {
            p.AddComponent(Components.PickupId) = HorsePickup.AsPickup();
            return p;
        }

        [Export]
        public static readonly KillPlayersAction KillPlayersWithPoisonAction = new KillPlayersAction(Reasons.PoisonId);
        public static ThingCreationProxy Snake(this ThingCreationProxy p)
        {
            p.AddComponent(Components.ActivatedItemId) = new()
            {
                Filter = NearbyOtherPlayersFilter.Instance,
                Action = KillPlayersWithPoisonAction,
                RequiredTargetCount = 1,
                InitialUses = short.MaxValue,
                UsesLeft = short.MaxValue,
            };
            p.AddComponent(Components.PickupId) = DoNothingPickupEffect.Instance.AsPickup();
            return p;
        }

        [Export]
        public static readonly AddStatBonusToUser_Action BoozeAction = new AddStatBonusToUser_Action(
            boost: new(Stats.RollAdditiveBonus, value: 1),
            lastsForTurns: 1); 
        public static ThingCreationProxy Booze(this ThingCreationProxy p)
        {
            p.AddComponent(Components.ActivatedItemId) = new()
            {
                Filter = NoTargetFilter.Instance,
                Action = BoozeAction,
                InitialUses = 1,
                UsesLeft = 1,
                RequiredTargetCount = 0,
            };
            p.AddComponent(Components.PickupId) = DoNothingPickupEffect.Instance.AsPickup();
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