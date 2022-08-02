    
namespace Zayats.View
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

    public class PointerEnterExit<T> : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        where T : IPointerEnterIndex, IPointerExitIndex
    {
        public int Index;
        public T Target;

        public void Initialize(int index, T target)
        {
            Index = index;
            Target = target;
        }

        // public void OnPointerClick(PointerEventData eventData) => Target.OnPointerClick(Index, eventData);
        public void OnPointerEnter(PointerEventData eventData) => Target.OnPointerEnter(Index, eventData);
        public void OnPointerExit(PointerEventData eventData) => Target.OnPointerExit(Index, eventData);
    }
}