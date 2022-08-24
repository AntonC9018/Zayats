using System;
using Kari.Plugins.AdvancedEnum;
using UnityEngine;
using Zayats.Unity.View.Generated;

#if UNITY_EDITOR
    using UnityEditor;
#endif

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
            [Tooltip("These only exist in the editor, useful for setting things up.")]
            public Material[] AllMaterials;
        #endif

        public MaterialArray<Material> Materials;

        // Map child mesh renderers to a material that should be changed when
        // highlighting or just switching from the default material in any way.
        // The mesh renderers are indexed as they appear in the hierarchy.
        public MaterialMapping[] MaterialMappings;
    }
}