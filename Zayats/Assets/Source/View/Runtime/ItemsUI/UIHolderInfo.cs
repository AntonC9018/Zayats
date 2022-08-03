using UnityEngine;

namespace Zayats.Unity.View
{
    public class UIHolderInfo : MonoBehaviour
    {
        public RectTransform OuterTransform => (RectTransform) transform;
        public GameObject OuterObject => gameObject;
        public RectTransform ItemFrameTransform;
        public GameObject ItemFrameObject => ItemFrameTransform.gameObject;
        public Transform StoredItem => ItemFrameTransform.GetChild(0);
    }
}