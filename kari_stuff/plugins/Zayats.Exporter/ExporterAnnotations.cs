namespace Kari.Zayats.Exporter
{
    using System;
    using System.Diagnostics;

    public enum ExportCategory
    {
        PickupEffect,
        PickupInteraction,
        ActivatedAction,
        TargetFilter,
    }

    public class ExportAttribute : System.Attribute
    {
    }
}
