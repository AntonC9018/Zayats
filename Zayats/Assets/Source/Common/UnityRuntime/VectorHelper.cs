using System.Collections.Generic;
using UnityEngine;

namespace Common.Unity
{
    public static class VectorHelper
    {
        public static Vector2 Inverse(this Vector2 v)
        {
            v.x = 1.0f / v.x;
            v.y = 1.0f / v.y;
            return v;
        }

        public static Vector2 Inverse(this Vector3 v)
        {
            v.x = 1.0f / v.x;
            v.y = 1.0f / v.y;
            v.z = 1.0f / v.z;
            return v;
        }

        public static float Min(this Vector2 v)
        {
            return Mathf.Min(v.x, v.y);
        }

        public static float Max(this Vector2 v)
        {
            return Mathf.Max(v.x, v.y);
        }

        public static float Min(this Vector3 v)
        {
            float m = Mathf.Min(v.x, v.y);
            return Mathf.Min(m, v.z);
        }

        private static readonly Vector3[] _WorldCornersCache = new Vector3[4];
        public static (Vector3 Center, Vector2 Size) GetWorldSpaceRect(this RectTransform t)
        {
            t.GetWorldCorners(_WorldCornersCache);
            var center = (_WorldCornersCache[0] + _WorldCornersCache[2]) * 0.5f;
            var size = new Vector2(
                x : (_WorldCornersCache[1] - _WorldCornersCache[0]).magnitude,
                y : (_WorldCornersCache[3] - _WorldCornersCache[0]).magnitude);
            return (center, size);
        }

        public static Vector2 xy(this Vector3 v) => new(v.x, v.y);

        public static void SetLocalTransform(this Transform t, Transform from)
        {
            t.localScale = from.localScale;
            t.localRotation = from.localRotation;
            t.localPosition = from.localPosition;
        }
    }
}