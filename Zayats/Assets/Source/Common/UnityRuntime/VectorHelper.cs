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
        public static Vector2 xz(this Vector3 v) => new(v.x, v.z);

        public static void SetLocalTransform(this Transform t, Transform from)
        {
            t.localScale = from.localScale;
            t.localPosition = from.localPosition;
            t.localRotation = from.localRotation;
        }

        public static Matrix4x4 GetLocalTRS(this Transform t)
        {
            return Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
        }

        public static void ResetLocalPositionRotationScale(this Transform t)
        {
            t.localScale = Vector3.one;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
        }

        public static Vector2Int FloorToInt(this Vector2 v)
        {
            return new Vector2Int((int) v.x, (int) v.y);
        }

        public static Vector2Int RoundUpToPowersOf2(this Vector2Int v)
        {
            static uint RoundUpToPowerOf2(uint a)
            {
                if (a == 0)
                    return 0;

                uint msb = unchecked((uint) 1 << (sizeof(uint) * 8 - 1));

                // Go throught all 32 bits from end to start.
                // We know we'll find at least one bit, so we can ignore the stop condition.
                for (uint i = msb;; i >>= 1)
                {
                    if ((a & i) != 0)
                    {
                        // i is the only set bit.
                        if (a == i)
                            return i;

                        // highest set bit * 2
                        return unchecked(i << 1);
                    }
                }
            }
            return new Vector2Int(
                (int) RoundUpToPowerOf2((uint) v.x),
                (int) RoundUpToPowerOf2((uint) v.y));
        }
    }
}