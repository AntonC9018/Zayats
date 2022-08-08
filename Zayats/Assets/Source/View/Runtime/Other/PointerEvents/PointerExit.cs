using UnityEngine.EventSystems;

namespace Zayats.Unity.View
{
    public class PointerExit : PointerEventHelper<IPointerExitIndex>, IPointerExitHandler
    {
        public void OnPointerExit(PointerEventData eventData) => Target.OnPointerExit(Index, eventData);
    }
}