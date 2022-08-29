using System;
using UnityEngine;
using UnityEngine.UI;
using Common.Unity;

#if UNITY_EDITOR
using Common.Editor;
using UnityEditor;
#endif

namespace Zayats.Experiments
{
    [Serializable]
    public struct CameraItems
    {
        public Camera MainCamera;
        public Camera Overlay3DCamera;
        public Camera UICamera;
        public Transform Overlay3DItemsContainer;
        public Canvas Canvas;
        public Graphic[] RenderersWithTheMaterial;
        public Material ReadFromTextureMaterial;
    }

    public class CameraLogic : MonoBehaviour
    {
        #if UNITY_EDITOR
            public void GetItems()
            {
                _refs.MainCamera = GameObject.Find("main_camera").GetComponent<Camera>();
                _refs.Overlay3DCamera = GameObject.Find("overlay_texture_camera").GetComponent<Camera>();
                _refs.UICamera = GameObject.Find("ui_camera").GetComponent<Camera>();
                _refs.Overlay3DItemsContainer = GameObject.Find("3d_overlay").transform;

                var canvas = GameObject.Find("canvas");
                _refs.Canvas = canvas.GetComponent<Canvas>();
                var image = canvas.transform.Find("image").GetComponent<Graphic>();
                _refs.RenderersWithTheMaterial = new[] { image };

                _refs.ReadFromTextureMaterial = image.material;
            }
            [ContextMenuItem(nameof(GetItems), nameof(GetItems))]
        #endif
        [SerializeField] private CameraItems _refs;


        private RenderTexture _renderTexture;
        private Vector2Int TextureSize => new(_renderTexture.width, _renderTexture.height);
        private Vector2Int ScreenSize => _refs.Canvas.renderingDisplaySize.FloorToInt();
        
        void Start()
        {
            _refs.Overlay3DCamera.enabled = true;
            ResetRenderTexture(ScreenSize);
        }

        public void ResetRenderTexture(Vector2Int size)
        {
            _renderTexture = new RenderTexture(width: size.x, height: size.y, depth: 32);
            _refs.ReadFromTextureMaterial.mainTexture = _renderTexture;
            _refs.Overlay3DCamera.targetTexture = _renderTexture;

            // Need to invalidate the material, otherwise it would try to render an empty texture.
            foreach (var r in _refs.RenderersWithTheMaterial)
                r.SetMaterialDirty();
        }

        public void Update()
        {
            var screenSize = ScreenSize;

            if (TextureSize != screenSize)
            {
                Debug.Log("Resolution changed to " + screenSize);
                Destroy(_renderTexture);
                ResetRenderTexture(screenSize);
            }
        }
    }
}