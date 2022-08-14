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

    public static class ShaderHelper
    {
        public static readonly ShaderPropertyArray<string> DefaultPropertyNames;
        static ShaderHelper()
        {
            var a = ShaderPropertyArray<string>.Create();
            a.MainColor = "_BaseColor";
            a.BaseMap = "_BaseMap";
            a.EmissionColor = "_EmissionColor";
            DefaultPropertyNames = a;
        }
    }

    [Serializable]
    public struct BatchedMaterialBlock
    {
        public static readonly List<Material> _Cache = new();

        public ShaderPropertyArray<string> PropertyNames { get; set; }
        private List<MaterialPath> _materialPaths;
        private MaterialPropertyBlock _materialPropertyBlock;

        public static BatchedMaterialBlock Create()
        {
            return new()
            {
                _materialPropertyBlock = new(),
                _materialPaths = new List<MaterialPath>(),
                PropertyNames = ShaderHelper.DefaultPropertyNames,
            };
        }

        public void ClearPaths()
        {
            _materialPaths.Clear();
        }
        
        public void Reset()
        {
            foreach (var p in _materialPaths)
                p.MeshRenderer.SetPropertyBlock(null, p.Index);
            _materialPaths.Clear();
        }

        public void AddPaths(MaterialPath[] materialPaths)
        {
            foreach (var path in materialPaths)
                _materialPaths.Add(path);
        }

        public void AddPath(MaterialPath materialPath)
        {
            _materialPaths.Add(materialPath);
        }

        public void AddPaths(IEnumerable<MaterialPath> materialPaths)
        {
            _materialPaths.AddRange(materialPaths);
        }

        public void Reset(IEnumerable<MaterialPath> materialPaths)
        {
            foreach (var p in _materialPaths)
                p.MeshRenderer.SetPropertyBlock(null, p.Index);
            Initialize(materialPaths);
        }

        private void Initialize(IEnumerable<MaterialPath> materialPaths)
        {
            materialPaths.Overwrite(_materialPaths);
            if (_materialPaths.Count > 0)
            {
                var path = _materialPaths[0];
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
                Assert.IsTrue(_materialPaths.Count >= 1,
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
                var a = PropertyNames.MainColor;
                if (_materialPropertyBlock.HasColor(a))
                    return _materialPropertyBlock.GetColor(a);

                return OriginalColor;
            }
            set
            {
                _materialPropertyBlock.SetColor(PropertyNames.MainColor, value);
            }
        }

        public Color EmissionColor
        {
            get
            {
                var a = PropertyNames.EmissionColor;
                if (_materialPropertyBlock.HasColor(a))
                    return _materialPropertyBlock.GetColor(a);

                return Color.black;
            }
            set
            {
                var a = PropertyNames.EmissionColor;
                _materialPropertyBlock.SetColor(a, value);
            }
        }

        public void Apply()
        {
            foreach (var path in _materialPaths)
                ApplyTo(path);
        }

        public void ApplyTo(MaterialPath path)
        {
            path.MeshRenderer.SetPropertyBlock(_materialPropertyBlock, path.Index);
        }

        public void ApplyTo(MaterialPath[] paths)
        {
            foreach (var path in paths)
                ApplyTo(path);
        }
    }

    // [Serializable]
    // public struct MaterialSubstitutePath
    // {
    //     public MaterialPath[] Paths;
    //     public Material[] Materials;
    // }

    // [Serializable]
    // public struct BatchedMaterial
    // {
    //     public List<MaterialSubstitutePath> Values;
    // }

    public static class MaterialHelper
    {
        private static readonly List<Material> _SharedMaterialsCache = new();
        private static Material[][] _MaterialArraysCache = Array.Empty<Material[]>();

        public static Material[] GetCacheOfSize(int size)
        {
            // In order for this not to allocate too much memory,
            // we reuse buffers of different lengths.
            var cacheIndex = size - 2;
            if (_MaterialArraysCache.Length <= cacheIndex)
            {
                Array.Resize(ref _MaterialArraysCache, cacheIndex + 1);
                _MaterialArraysCache[cacheIndex] = new Material[size];
            }
            else if (_MaterialArraysCache[cacheIndex] == null)
            {
                _MaterialArraysCache[cacheIndex] = new Material[size];
            }
            var materialCache = _MaterialArraysCache[cacheIndex];
            return materialCache;
        }

        public readonly ref struct TempCache
        {
            internal readonly List<Material> _cache;

            public TempCache(List<Material> list)
            {
                _cache = list;
            }

            public int Length => _cache.Count;

            public Material this[int index]
            {
                get => _cache[index];
                set => _cache[index] = value;
            }
        }

        public static TempCache GetSharedMaterialsTemp(this MeshRenderer meshRenderer)
        {
            meshRenderer.GetSharedMaterials(_SharedMaterialsCache);
            return new (_SharedMaterialsCache);
        }

        public static void SetSharedMaterials(this TempCache cache, MeshRenderer meshRenderer)
        {
            var materials = GetCacheOfSize(cache.Length);
            cache._cache.CopyTo(materials);
            meshRenderer.sharedMaterials = materials;
        }

        public static void SetSharedMaterialAtIndex(this MeshRenderer meshRenderer, int index, Material sharedMaterial)
        {
            meshRenderer.GetSharedMaterials(_SharedMaterialsCache);
            var materials = GetCacheOfSize(_SharedMaterialsCache.Count);
            _SharedMaterialsCache.CopyTo(materials);
            materials[index] = sharedMaterial;
            meshRenderer.sharedMaterials = materials;
        }
    }
}