using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Common.Unity
{
    public static class EventHelper
    {
        private static readonly List<RaycastResult> _RaycastResults = new();
        public static void Bubble<T>(this PointerEventData eventData, ExecuteEvents.EventFunction<T> eventFunction, GameObject self)
            where T : IEventSystemHandler
        {
            var r = _RaycastResults;
            EventSystem.current.RaycastAll(eventData, r);

            int i = 0;
            while (r[i].gameObject == self)
            {
                i++;
                if (i == r.Count)
                    return;
            }
            while (!ExecuteEvents.Execute(r[i].gameObject, eventData, eventFunction))
            {
                i++;
                if (i == r.Count)
                    return;
            }
        }
    }
}