using System.Collections.Generic;
using Common.Unity;
using UnityEngine;

namespace Zayats.Unity.View
{
    public class ModelInfo : MonoBehaviour
    {
        public MeshRenderer[] MeshRenderers;
        public ModelInfoScriptableObject Config;

        public IEnumerable<MaterialPath> MaterialPaths
        {
            get
            {
                var mappings = Config.MaterialMappings;
                for (int i = 0; i < mappings.Length; i++)
                {
                    var p = mappings[i];
                    yield return new (MeshRenderers[p.MeshRendererIndex], p.MaterialIndex);
                }
            }
        }

        public void SetSharedMaterial(MaterialKind material)
        {
            foreach (var p in MaterialPaths)
            {
                var sharedMaterial = Config.Materials.Get(material);
                p.MeshRenderer.SetSharedMaterialAtIndex(p.Index, sharedMaterial);
            }
        }
    }
}