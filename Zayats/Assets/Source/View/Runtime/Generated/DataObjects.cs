// <auto-generated>
// This file has been autogenerated by Kari.
// </auto-generated>

#pragma warning disable

namespace Zayats.Unity.View
{
    public partial struct SpawnMapStuff
    {
        public static bool operator==(SpawnMapStuff a, SpawnMapStuff b)
        {
            return a.Corners == b.Corners
                && a.MapParent == b.MapParent
                && a.CellPrefab == b.CellPrefab
                && a.MaxMapSize == b.MaxMapSize
                && a.TopToBottom == b.TopToBottom;
        }
        public static bool operator!=(SpawnMapStuff a, SpawnMapStuff b)
        {
            return !(a == b);
        }
        public void Sync(SpawnMapStuff other)
        {
            this.Corners = other.Corners;
            this.MapParent = other.MapParent;
            this.CellPrefab = other.CellPrefab;
            this.MaxMapSize = other.MaxMapSize;
            this.TopToBottom = other.TopToBottom;
        }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Corners.GetHashCode();
                hash = hash * 23 + MapParent.GetHashCode();
                hash = hash * 23 + CellPrefab.GetHashCode();
                hash = hash * 23 + MaxMapSize.GetHashCode();
                hash = hash * 23 + TopToBottom.GetHashCode();
                return hash;
            }
        }
        public override bool Equals(object other)
        {
            return other is SpawnMapStuff a && this == a;
        }
        public SpawnMapStuff Copy => this;
    }
}

#pragma warning restore
