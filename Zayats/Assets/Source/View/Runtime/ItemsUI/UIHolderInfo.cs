using UnityEngine;
using UnityEngine.UI;

namespace Zayats.Unity.View
{
    public class UIHolderInfo : MonoBehaviour
    {
        public RectTransform ItemFrameTransform;
        public Graphic UsabilityGraphic;

        public RectTransform OuterTransform => (RectTransform) transform;
        public GameObject OuterObject => gameObject;
        public GameObject ItemFrameObject => ItemFrameTransform.gameObject;
        public Transform StoredItem => ItemFrameTransform.GetChild(0);
    }
}