using System;
using Common.Unity;
using UnityEngine;

namespace Zayats.Unity.View
{
    using static Zayats.Core.Assert;

    public interface IResolutionService
    {
        Vector2Int Resolution { get; }
        event Action<Vector2Int> OnResolutionChanged;
    }

    public class CanvasResolutionService : MonoBehaviour, IResolutionService
    {
        public Canvas Canvas { get; private set; }
        private Vector2Int _previousResolution;
        public event Action<Vector2Int> OnResolutionChanged;

        public void Initialize(Canvas resolutionCanvas)
        {
            assert(resolutionCanvas != null
                && resolutionCanvas.renderMode == RenderMode.ScreenSpaceCamera);
            
            Canvas = resolutionCanvas;
            _previousResolution = _GetResolution();
        }

        public Vector2Int Resolution => _previousResolution;

        private Vector2Int _GetResolution() => Canvas.renderingDisplaySize.FloorToInt();

        void Update()
        {
            var r = _GetResolution();
            if (_previousResolution != r)
            {
                Debug.Log("Resolution changed to " + r);
                _previousResolution = r;
                OnResolutionChanged?.Invoke(r);
            }
        }
    }
}