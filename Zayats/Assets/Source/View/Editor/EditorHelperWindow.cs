using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Zayats.Core;
using System.Linq;
using Common;
using Common.Unity;
using Common.Editor;
using System;

namespace Zayats.Unity.View.Editor
{
    using static Assert;

    public class EditorHelperWindow : EditorWindow
    {
        private List<string> _diagnostics;

        public void Initialize()
        {
            _diagnostics ??= new();
        }

        // Add menu named "My Window" to the Window menu
        [MenuItem("Tools/EditorHelperWindow")]
        public static void BringUpWindow()
        {
            var window = EditorWindow.GetWindow<EditorHelperWindow>();
            window.Initialize();
            window.Show();
        }

        public void OnGUI()
        {
            if (GUILayout.Button("Add material infos"))
            {
                int group = Undo.GetCurrentGroup();

                var meshRenderers = new List<MeshRenderer>();
                var sharedMaterials = new List<Material>();
                var allSharedMaterials = new HashSet<Material>();
                var temp = new List<MaterialMapping>();
                
                foreach (var t in Selection.transforms)
                {
                    Debug.Log(t.name);
                    t.RestoreHierarchy();

                    var (child, modelInfo) = t.GetObject(ObjectHierarchy.ModelInfo);
                    if (modelInfo == null)
                    {
                        modelInfo = child.gameObject.AddComponent<ModelInfo>();
                        Undo.RegisterCreatedObjectUndo(modelInfo, "add model info");
                        EditorUtility.SetDirty(child);
                    }

                    Undo.RegisterCompleteObjectUndo(modelInfo, "change model info");
                    modelInfo.gameObject.GetComponentsInChildren<MeshRenderer>(meshRenderers);
                    modelInfo.MeshRenderers = meshRenderers.ToArray();

                    if (modelInfo.Config == null)
                    {
                        modelInfo.Config = AssetDatabaseHelper.CreateObjectWithDefaults<ModelInfoScriptableObject>();
                        Undo.RegisterCreatedObjectUndo(modelInfo.Config, "config creation");
                    }

                    var modelConfig = modelInfo.Config;
                    Undo.RegisterCompleteObjectUndo(modelConfig, "Setting stuff");

                    modelConfig.Materials.FixSize();

                    foreach (var renderer in meshRenderers)
                    {
                        renderer.GetSharedMaterials(sharedMaterials);
                        foreach (var sharedMaterial in sharedMaterials)
                            allSharedMaterials.Add(sharedMaterial);
                    }

                    var allMaterials = allSharedMaterials.OrderBy(t => t.name).ToArray();
                    // Useful for setting stuff in the editor, doesn't exist at runtime.
                    modelConfig.AllMaterials = allMaterials;

                    int i = (int) MaterialKind.Default;
                    
                    SetPaths();
                    void SetPaths()
                    {
                        var material = modelConfig.Materials[i];
                        if (material == null)
                        {
                            if (allMaterials.Length == 0)
                                return;
                            int index = Math.Min(i, allMaterials.Length - 1);
                            modelConfig.Materials[i] = allMaterials[index];
                        }

                        temp.Clear();
                        for (int ri = 0; ri < meshRenderers.Count; ri++)
                        {
                            meshRenderers[ri].GetSharedMaterials(sharedMaterials);

                            // TODO: what happens if it's found multiple times withing the same mesh renderer??
                            int indexOfMaterial = sharedMaterials.IndexOf(material);
                            if (indexOfMaterial == -1)
                                return;
                            temp.Add(new()
                            {
                                MaterialIndex = indexOfMaterial,
                                MeshRendererIndex = ri,
                            });
                        }
                        
                        modelConfig.MaterialMappings = temp.ToArray();
                    }

                    EditorUtility.SetDirty(modelConfig);
                    EditorUtility.SetDirty(modelInfo);
                }
                Undo.CollapseUndoOperations(group);
            }
            if (GUILayout.Button("Add colliders"))
            {
                int group = Undo.GetCurrentGroup();
                foreach (var t in Selection.transforms)
                {
                    Debug.Log(t.name);
                    t.RestoreHierarchy();

                    var (colliderTransform, collider) = t.GetObject(ObjectHierarchy.Collider);
                    if (collider == null)
                    {
                        collider = colliderTransform.gameObject.AddComponent<BoxCollider>();
                        Undo.RegisterCreatedObjectUndo(collider, "add collider");
                    }

                    var (modelTransform, model) = t.GetObject(ObjectHierarchy.Model);

                    if (collider is MeshCollider meshCollider)
                    {
                        Undo.RegisterCompleteObjectUndo(meshCollider, "change collider properties");
                        Undo.RegisterCompleteObjectUndo(colliderTransform, "change tranform properties");

                        meshCollider.sharedMesh = model.gameObject.GetComponent<MeshFilter>().sharedMesh;
                        colliderTransform.SetLocalTransform(modelTransform);
                        
                        EditorUtility.SetDirty(meshCollider);
                        EditorUtility.SetDirty(colliderTransform);
                    }
                    else if (collider is BoxCollider boxCollider)
                    {
                        Undo.RegisterCompleteObjectUndo(boxCollider, "change collider properties");
                        var bounds = model.bounds;
                        boxCollider.center = bounds.center;
                        boxCollider.size = bounds.size;
                        EditorUtility.SetDirty(boxCollider);
                    }
                    EditorUtility.SetDirty(t.gameObject);
                }
                Undo.CollapseUndoOperations(group);
            }
            if (GUILayout.Button("Clear colliders"))
            {
                int group = Undo.GetCurrentGroup();
                foreach (var t in Selection.transforms)
                {
                    t.RestoreHierarchy();
                    var (colliderTransform, collider) = t.GetObject(ObjectHierarchy.Collider);
                    if (collider != null)
                    {
                        Undo.DestroyObjectImmediate(collider);
                        EditorUtility.SetDirty(colliderTransform.gameObject);
                    }
                }
                Undo.CollapseUndoOperations(group);
            }
        }
     
    }
}