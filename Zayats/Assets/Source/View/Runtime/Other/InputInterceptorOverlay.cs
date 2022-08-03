using UnityEngine;
using UnityEngine.EventSystems;

namespace Zayats.Unity.View
{
    using static PointerEventData.InputButton;

    // Needs to span the entire screen, and not block the pointer events.
    public class InputInterceptorOverlay : MonoBehaviour, IPointerClickHandler
    {
        private ViewContext _viewContext;

        public void Initialize(ViewContext viewContext)
        {
            _viewContext = viewContext;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == Right)
            {
                if (_viewContext.State.ItemHandling.InProgress)
                {
                    _viewContext.CancelHandlingCurrentItemInteraction();
                    eventData.Use();
                }
            }
        }
    }
}