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
        public int Index;
        public T Target;

        public void Initialize(int index, T target)
        {
            Index = index;
            Target = target;
        }
    }

    public class PointerEnter<T> : PointerEventHelper<IPointerEnterIndex>, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData) => Target.OnPointerEnter(Index, eventData);
    }

    public class PointerExit<T> : PointerEventHelper<IPointerExitIndex>, IPointerExitHandler
    {
        public void OnPointerExit(PointerEventData eventData) => Target.OnPointerExit(Index, eventData);
    }

    public class PointerClick<T> : PointerEventHelper<IPointerClickIndex>, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData) => Target.OnPointerClick(Index, eventData);
    }
}