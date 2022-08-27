using UnityEngine.EventSystems;

namespace Zayats.Unity.View
{
    public class PointerClick : PointerEventHelper<IPointerClickIndex>, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData) => Target.OnPointerClick(Index, eventData);
    }
}