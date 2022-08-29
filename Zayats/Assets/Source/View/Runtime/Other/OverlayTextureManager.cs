using System;
using System.Collections.Generic;
using Common.Unity;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Zayats.Unity.View
{
    using static Zayats.Core.Assert;

    [Serializable]
    public struct Overlay3DContext
    {
        public Camera RenderCamera;
        public Material[] MaterialsWithTheTexture;
        public Canvas[] RootCanvases;
    }

    public class OverlayTextureManager
    {
        private static readonly List<Graphic> _GraphicsCache = new();
        private Overlay3DContext _refs;
        private RenderTexture _renderTexture;
        private CanvasResolutionService _resolution;
        
        private Vector2Int TextureSize => new(_renderTexture.width, _renderTexture.height);

        public OverlayTextureManager(Overlay3DContext refs, CanvasResolutionService resolution)
        {
            _refs = refs;
            _resolution = resolution;

            assert(_renderTexture == null, "Can't initialize twice.");
            
            var cam = refs.RenderCamera;
            assert(cam);
            cam.enabled = true;

            var camData = cam.GetUniversalAdditionalCameraData();
            assert(camData, "Must be using URP");
            camData.renderType = CameraRenderType.Base;

            _InitializeRenderTexture(resolution.Resolution);
            resolution.OnResolutionChanged += _MaybeResetTexture;
        }

        private void _InitializeRenderTexture(Vector2Int screenSize)
        {
            _renderTexture = new RenderTexture(width: screenSize.x, height: screenSize.y, depth: 32);

            var cam = _refs.RenderCamera;
            
            var canvasTransform = (RectTransform) _resolution.Canvas.transform;
            var p = canvasTransform.position;
            var (center, size) = canvasTransform.GetWorldSpaceRect();

            const float far = 20;
            cam.projectionMatrix = Matrix4x4.Ortho(
                left: -size.x / 2,
                right: size.x / 2,
                top: size.y / 2,
                bottom: -size.y / 2,
                zNear: 0,
                zFar: far);
            
            // p.x += screenSize.x / 2;
            // p.y += screenSize.y / 2;
            cam.transform.position = center - canvasTransform.forward * far;
            cam.targetTexture = _renderTexture;

            foreach (var material in _refs.MaterialsWithTheTexture)
                material.mainTexture = _renderTexture;
        }

        private void _MaybeResetTexture(Vector2Int newSize)
        {
            RenderTexture.Destroy(_renderTexture);
            _InitializeRenderTexture(newSize);

            // Need to invalidate the material on any graphics, otherwise it would try to render an empty texture.
            foreach (var rootCanvas in _refs.RootCanvases)
            {
                rootCanvas.GetComponentsInChildren(_GraphicsCache);
                foreach (var g in _GraphicsCache)
                {
                    if (g.mainTexture == _renderTexture)
                        g.SetMaterialDirty();
                }
                _GraphicsCache.Clear();
            }

        }
    }
}