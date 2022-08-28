using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Zayats.Unity.View
{
    public interface IPointerEnterIndex
    {
        void OnPointerEnter(int index, PointerEventData eventData);
    }
    public interface IPointerExitIndex
    {
        void OnPointerExit(int index, PointerEventData eventData);
    }
    public interface IPointerClickIndex
    {
        void OnPointerClick(int index, PointerEventData eventData);
    }

    public class PointerEventHelper<T> : MonoBehaviour
    {
        [NonSerialized] public int Index;
        [NonSerialized] public T Target;

        public void Initialize(int index, T target)
        {
            Index = index;
            Target = target;
        }
    }
}