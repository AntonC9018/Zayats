using Common.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using Zayats.Core;

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
                    return;
                }
            }
            else if (eventData.button == Left)
            {
                if (_viewContext.State.ItemHandling.InProgress)
                {
                    _viewContext.SelectObject(eventData.position);
                    return;
                }
            }

            var context = new ViewEvents.PointerEvent
            {
                Continue = true,
                Data = eventData,
            };
            _viewContext.GetEventProxy(ViewEvents.OnPointerClick)
                .HandleWithContinueCheck(_viewContext, ref context);

            if (context.Continue)
                eventData.Bubble(ExecuteEvents.pointerClickHandler, gameObject);
        }
    }
}