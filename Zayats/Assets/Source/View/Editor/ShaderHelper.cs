// TODO: support the standard shader with some #if's
using UnityEngine;

namespace Zayats.Unity.View.Editor
{
    using ShaderGUI = UnityEditor.BaseShaderGUI;
    
    // I have to copy the code here, because there is no type aliasing.
    // using SurfaceType = UnityEditor.BaseShaderGUI.SurfaceType;
    public enum SurfaceType
    {
        Opaque,
        Transparent
    }

    public static class ShaderHelper
    {
        public static void SetupMaterialBlendMode(Material material)
        {
            ShaderGUI.SetupMaterialBlendMode(material);
        }
        
        public static void SetSurfaceType(this Material material, SurfaceType surfaceType)
        {
            material.SetFloat(MaterialPropertyNames.SurfaceType, (float) surfaceType);
            SetupMaterialBlendMode(material);
        }
    }
    
    // It's internal in the sources.
    // https://github.com/needle-mirror/com.unity.render-pipelines.universal/blob/8de5d7f7c63a433f5134fc8ce941072a08371ddc/Editor/ShaderGraph/UniversalProperties.cs
    public static class MaterialPropertyNames
    {
        public static readonly string SpecularWorkflowMode = "_WorkflowMode";
        public static readonly string SurfaceType = "_Surface";
        public static readonly string BlendMode = "_Blend";
        public static readonly string AlphaClip = "_AlphaClip";
        public static readonly string SrcBlend = "_SrcBlend";
        public static readonly string DstBlend = "_DstBlend";
        public static readonly string ZWrite = "_ZWrite";
        public static readonly string CullMode = "_Cull";
        public static readonly string CastShadows = "_CastShadows";
        public static readonly string ReceiveShadows = "_ReceiveShadows";
        public static readonly string QueueOffset = "_QueueOffset";

        // for ShaderGraph shaders only
        public static readonly string ZTest = "_ZTest";
        public static readonly string ZWriteControl = "_ZWriteControl";
        public static readonly string QueueControl = "_QueueControl";

        // Global Illumination requires some properties to be named specifically:
        public static readonly string EmissionMap = "_EmissionMap";
        public static readonly string EmissionColor = "_EmissionColor";
    }
}