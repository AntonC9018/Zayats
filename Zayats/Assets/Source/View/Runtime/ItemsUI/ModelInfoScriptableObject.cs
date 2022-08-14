using System;
using Kari.Plugins.AdvancedEnum;
using UnityEngine;
using Zayats.Unity.View.Generated;

namespace Zayats.Unity.View
{
    [GenerateArrayWrapper("MaterialArray")]
    public enum MaterialKind
    {
        Default,
        Preview,
    }

    [Serializable]
    public struct MaterialMapping
    {
        public int MeshRendererIndex;
        public int MaterialIndex;
    }

    public class ModelInfoScriptableObject : ScriptableObject
    {
#if UNITY_EDITOR
        public Material[] AllMaterials;
#endif
        public MaterialArray<Material> Materials;
        public MaterialMapping[] MaterialMappings;
    }
}