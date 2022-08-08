using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Assertions;
using Kari.Plugins.AdvancedEnum;
using Common.Unity.Generated;
using System;

namespace Common.Unity
{
    [System.Serializable]
    public struct MaterialPath
    {
        public MeshRenderer MeshRenderer;
        public int Index;

        public MaterialPath(MeshRenderer meshRenderer, int index)
        {
            MeshRenderer = meshRenderer;
            Index = index;
        }
    }

    [GenerateArrayWrapper]
    public enum ShaderProperty
    {
        MainColor,
        BaseMap,
        EmissionColor,
    }

    public class BatchedMaterial
    {
        public static readonly List<Material> _Cache = new();

        public static readonly ShaderPropertyArray<string> DefaultPropertyNames;
        static BatchedMaterial()
        {
            var a = ShaderPropertyArray<string>.Create();
            a.MainColor = "_BaseColor";
            a.BaseMap = "_BaseMap";
            a.EmissionColor = "_EmissionColor";
            DefaultPropertyNames = a;
        }


        private ShaderPropertyArray<string> _propertyNames;
        private MaterialPath[] _materialPaths;
        private MaterialPropertyBlock _materialPropertyBlock;


        public BatchedMaterial()
        {
            _materialPropertyBlock = new();
            _materialPaths = Array.Empty<MaterialPath>();
        }
        
        public void Reset()
        {
            foreach (var p in _materialPaths)
                p.MeshRenderer.SetPropertyBlock(null, p.Index);
            _materialPaths = Array.Empty<MaterialPath>();
        }

        public void Reset(MaterialPath[] materialPaths, ShaderPropertyArray<string> propertyNames)
        {
            foreach (var p in _materialPaths)
                p.MeshRenderer.SetPropertyBlock(null, p.Index);
            Initialize(materialPaths, propertyNames);
        }

        private void Initialize(MaterialPath[] materialPaths, ShaderPropertyArray<string> propertyNames)
        {
            _propertyNames = propertyNames;
            _materialPaths = materialPaths;
            if (materialPaths.Length > 0)
            {
                var path = materialPaths[0];
                path.MeshRenderer.GetPropertyBlock(_materialPropertyBlock, path.Index);
            }
        }

        public void SetTexture(string key, Texture value)
        {
            Assert.IsNotNull(value, "Cannot set texture to null. Use Texture2D.whiteTexture instead.");
            _materialPropertyBlock.SetTexture(key, value);
        }

        // NOTE: is not thread safe!
        public Color OriginalColor
        {
            get
            {
                Assert.IsTrue(_materialPaths.Length >= 1,
                    "At least one material path must be assigned in order to use this.");
                    
                var firstPath = _materialPaths[0];
                firstPath.MeshRenderer.GetSharedMaterials(_Cache);
                var material = _Cache[firstPath.Index];
                return material.color;
            }
        }

        public Color Color
        {
            get
            {
                var a = _propertyNames.MainColor;
                if (_materialPropertyBlock.HasColor(a))
                    return _materialPropertyBlock.GetColor(a);

                return OriginalColor;
            }
            set
            {
                _materialPropertyBlock.SetColor(_propertyNames.MainColor, value);
            }
        }

        public Color EmissionColor
        {
            get
            {
                var a = _propertyNames.EmissionColor;
                if (_materialPropertyBlock.HasColor(a))
                    return _materialPropertyBlock.GetColor(a);

                return Color.black;
            }
            set
            {
                var a = _propertyNames.EmissionColor;
                _materialPropertyBlock.SetColor(a, value);
            }
        }

        public void Apply()
        {
            foreach (var path in _materialPaths)
                path.MeshRenderer.SetPropertyBlock(_materialPropertyBlock, path.Index);
        }
    }
}