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

    public static partial class Initialization
    {
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