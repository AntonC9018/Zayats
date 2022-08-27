using UnityEngine.EventSystems;

namespace Zayats.Unity.View
{
    public class PointerEnter : PointerEventHelper<IPointerEnterIndex>, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData) => Target.OnPointerEnter(Index, eventData);
    }
}