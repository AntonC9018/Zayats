namespace Zayats.Unity.View
{
    public static class LayerIndex
    {
        public const int UI = 5;
        public const int Default = 0;
        public const int RaycastTarget = 6;
    }

    public static class LayerBits
    {
        public const int UI = 1 << LayerIndex.UI;
        public const int Default = 1 << LayerIndex.Default;
        public const int RaycastTarget = 1 << LayerIndex.RaycastTarget;
    }
}