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
        private ViewContext _view;

        public void Initialize(ViewContext viewContext)
        {
            _view = viewContext;
        }

        // TODO: Some more decoupled input system, but this works for now. 
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == Right)
            {
                if (_view.State.Selection.InProgress)
                {
                    _view.CancelCurrentSelectionInteraction();
                    return;
                }
            }
            else if (eventData.button == Left)
            {
                if (_view.MaybeSelectObject(eventData.position))
                    return;
            }

            var context = new ViewEvents.PointerEvent
            {
                Continue = true,
                Data = eventData,
            };
            _view.GetEventProxy(ViewEvents.OnPointerClick)
                .HandleWithContinueCheck(_view, ref context);

            if (context.Continue)
                eventData.Bubble(ExecuteEvents.pointerClickHandler, gameObject);
        }
    }
}