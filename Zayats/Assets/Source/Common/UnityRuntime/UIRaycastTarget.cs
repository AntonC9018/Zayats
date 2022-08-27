using UnityEngine;
using UnityEngine.UI;

namespace Common.Unity
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIRaycastTarget : Graphic
    {
        public new void OnValidate()
        {
            color = new Color(0, 0, 0, 0);
            base.OnValidate();
        }
    }
}