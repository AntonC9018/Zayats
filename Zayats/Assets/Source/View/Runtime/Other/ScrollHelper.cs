namespace Zayats.Unity.View
{
    using UnityEngine;
    using UnityEngine.UI;

    public static class ScrollHelper
    {
        public static void ScrollChildIntoView(this ScrollRect scrollRect, int childIndex)
        {
            ScrollChildIntoView(scrollRect, (RectTransform) scrollRect.content.GetChild(childIndex));
        }

        public static Vector3 GetContentLocalPositionToScrollChildIntoView(this ScrollRect scrollRect, int childIndex)
        {
            return GetContentLocalPositionToScrollChildIntoView(scrollRect, (RectTransform) scrollRect.content.GetChild(childIndex));
        }

        public static void ScrollChildIntoView(this ScrollRect scrollRect, RectTransform child)
        {
            scrollRect.content.localPosition = GetContentLocalPositionToScrollChildIntoView(scrollRect, child);
        }

        public static Vector3 GetContentLocalPositionToScrollChildIntoView(this ScrollRect scrollRect, RectTransform child)
        {
            var content = scrollRect.content;
            var viewport = scrollRect.viewport;

            var vsize = viewport.rect.size;
            var (cpos, csize) = TransformRectFromTo(content, viewport);
            var (ipos, isize) = TransformRectFromTo(child, viewport);

            var d1 = ipos;

            Vector2 desiredPosition;
            {
                bool isOffLeft = d1.x < 0;
                bool isOffRight = d1.x > vsize.x - isize.x;
                if (isOffLeft)
                    desiredPosition.x = 0;
                else if (isOffRight)
                    desiredPosition.x = vsize.x - isize.x;
                else
                    desiredPosition.x = ipos.x;
            }
            {
                bool isOffTop = d1.y > 0;
                bool isOffBottom = d1.y < vsize.y - isize.y;
                if (isOffTop)
                    desiredPosition.y = 0;
                else if (isOffBottom)
                    desiredPosition.y = vsize.y - isize.y;
                else
                    desiredPosition.y = ipos.y;
            }

            var currentItemPosition = ipos;
            var positionOffset = desiredPosition - currentItemPosition;

            var result = content.localPosition;
            var scale = content.localScale;
            result.x += positionOffset.x / scale.x;
            result.y += positionOffset.y / scale.y;
            
            return result;
        }

        private static readonly Vector3[] _FromWorldCornersCache = new Vector3[4];

        private static (Vector2 BottomLeft, Vector2 Size) TransformRectFromTo(RectTransform from, RectTransform to)
        {
            Vector3[] fromWorldCorners = _FromWorldCornersCache;
            from.GetWorldCorners(fromWorldCorners);

            Matrix4x4 toLocal = to.worldToLocalMatrix;
            Vector3 a = toLocal.MultiplyPoint3x4(fromWorldCorners[0]);
            Vector3 b = toLocal.MultiplyPoint3x4(fromWorldCorners[2]);

            Vector2 a0, b0;
            if (a.x < b.x)
            {
                a0.x = a.x;
                b0.x = b.x;
            }
            else
            {
                a0.x = b.x;
                b0.x = a.x;
            }
            if (a.y < b.y)
            {
                a0.y = a.y;
                b0.y = b.y;
            }
            else
            {
                a0.y = b.y;
                b0.y = a.y;
            }

            return (new Vector2(a0.x, b0.y), b0 - a0);
        }
    }
}