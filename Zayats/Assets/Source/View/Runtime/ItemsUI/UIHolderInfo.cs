using System;
using Common.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Zayats.Unity.View
{
    public class UIHolderInfo : MonoBehaviour
    {
        public RectTransform ItemFrameTransform;
        public Graphic UsabilityGraphic;
        [NonSerialized] public Transform AnimatedTransform;
        [NonSerialized] public float ItemSize;

        public float ZOffset
        {
            set
            {
                var lp = AnimatedTransform.localPosition;
                lp.z = value;
                AnimatedTransform.localPosition = lp;
            }
        }

        public RectTransform OuterTransform => (RectTransform) transform;
        public GameObject OuterObject => gameObject;
        public GameObject ItemFrameObject => ItemFrameTransform.gameObject;
        public Transform CenteringTransform => AnimatedTransform.GetChild(0);
        public bool HasStoredItem => CenteringTransform.childCount == 1;
        public Transform StoredItem => CenteringTransform.GetChild(0);

        public void InitializeAnimatedContainer(Transform parent, int index)
        {
            var container = new GameObject($"animated item container {index}").transform;
            container.parent = parent;
            AnimatedTransform = container;
            
            var container1 = new GameObject($"centering").transform;
            container1.parent = container;
            
            var (center, size) = ItemFrameTransform.GetWorldSpaceRect();
            AnimatedTransform.localPosition = new Vector3(center.x, center.y, 0);
        }
    }

}