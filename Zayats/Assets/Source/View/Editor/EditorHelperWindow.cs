using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Zayats.Core;
using System.Linq;
using Common;
using Common.Unity;
using Common.Editor;
using System;
using System.IO;

namespace Zayats.Unity.View.Editor
{
    using static Assert;

    public class EditorHelperWindow : EditorWindow
    {
        private List<string> _diagnostics;
        private bool _shouldRequeryMaterials;
        private bool _shouldClearPreviewMaterial;

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
            static GameObject[] CleanSelection()
            {
                var ts = Selection.gameObjects;
                HashSet<GameObject> result = new();
                foreach (var t_ in ts)
                {
                    var t = t_;

                    assert(t != null, "Not sure if this is even possible");
                    
                    if (PrefabUtility.IsPartOfAnyPrefab(t))
                        t = PrefabUtility.GetOutermostPrefabInstanceRoot(t);
                    
                    // It's the actual prefab. In this case, we get the root object of the prefab.
                    if (t == null)
                    {
                        t = t_;
                        var transform = t.transform;
                        Transform parent;
                        while ((parent = transform.parent) != null)
                            transform = parent;
                        t = transform.gameObject;
                    }
                    
                    result.Add(t);
                }

                if (ts.Length != result.Count
                    || ts.Any(t => !result.Contains(t)))
                {
                    ts = result.ToArray();
                }

                Selection.objects = ts;
                return ts;
            }

            static IEnumerable<Transform> GetThings()
            {
                foreach (var t in CleanSelection().Select(a => a.transform))
                {
                    Debug.Log(t.name);
                    t.RestoreHierarchy();
                    yield return t;
                }
            }

            static (Transform Transform, ModelInfo Model) GetOrCreateModel(Transform t)
            {
                var (child, modelInfo) = t.GetObject(ObjectHierarchy.ModelInfo);
                if (modelInfo == null)
                {
                    modelInfo = child.gameObject.AddComponent<ModelInfo>();
                    Undo.RegisterCreatedObjectUndo(modelInfo, "add model info");
                    EditorUtility.SetDirty(child);
                }
                return (child, modelInfo);
            }

            static (Transform Transform, Collider Collider) GetOrCreateCollider(Transform t)
            {
                var (colliderTransform, collider) = t.GetObject(ObjectHierarchy.Collider);
                if (collider == null)
                {
                    collider = colliderTransform.gameObject.AddComponent<BoxCollider>();
                    Undo.RegisterCreatedObjectUndo(collider, "add collider");
                }
                return (colliderTransform, collider);
            }

            int group = Undo.GetCurrentGroup();
         
            _shouldRequeryMaterials = EditorGUILayout.Toggle("force requery the materials", _shouldRequeryMaterials);

            if (GUILayout.Button("Add material infos"))
            {
                var sharedMaterials = new List<Material>();
                var allSharedMaterials = new HashSet<Material>();
                var temp = new List<MaterialMapping>();
                
                foreach (var t in GetThings())
                {
                    var (child, modelInfo) = GetOrCreateModel(t);
                    Undo.RegisterCompleteObjectUndo(modelInfo, "change model info");

                    var meshRenderers = modelInfo.gameObject.GetComponentsInChildren<MeshRenderer>();
                    modelInfo.MeshRenderers = meshRenderers;

                    if (modelInfo.Config == null)
                    {
                        modelInfo.Config = AssetDatabaseHelper.CreateObjectOrLoadExistingWithDefaults<ModelInfoScriptableObject>(
                            folderWhereToSaveRelativeToAssets: "Game/Content/Things/Items",
                            fileNameWithoutExtension: t.name + "_ModelInfo");
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
                    allSharedMaterials.Clear();

                    // NOTE: Used to be a loop
                    int i = (int) MaterialKind.Default;
                    
                    SetPaths();
                    void SetPaths()
                    {
                        ref var material = ref modelConfig.Materials[i];
                        if (material == null
                            || _shouldRequeryMaterials)
                        {
                            if (allMaterials.Length == 0)
                                return;
                            int index = Math.Min(i, allMaterials.Length - 1);
                            modelConfig.Materials[i] = allMaterials[index];
                        }

                        for (int ri = 0; ri < meshRenderers.Length; ri++)
                        {
                            meshRenderers[ri].GetSharedMaterials(sharedMaterials);

                            // TODO: what happens if it's found multiple times within the same mesh renderer??
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
                        temp.Clear();
                    }

                    EditorUtility.SetDirty(modelConfig);
                    EditorUtility.SetDirty(modelInfo);
                }
            }

            _shouldClearPreviewMaterial = EditorGUILayout.Toggle("recreate preview materials", _shouldClearPreviewMaterial);
            

            if (GUILayout.Button("Make default preview materials"))
            {
                foreach (var t in GetThings())
                {
                    var (child, modelInfo) = GetOrCreateModel(t);
                    
                    var config = modelInfo.Config;
                    if (config == null)
                    {
                        Debug.LogWarning("Initialize the config prior to creating the preview material");
                        continue;
                    }
                    
                    if (config.Materials.Preview != null
                        && !_shouldClearPreviewMaterial)
                    {
                        Debug.Log("Skipping, because a preview material already exists");
                        continue;
                    }

                    var defaultMaterial = config.Materials.Default;
                    if (defaultMaterial == null)
                    {
                        Debug.LogWarningFormat("No default material for object {0}", t.name);
                        continue;
                    }

                    void SetPreviewMaterial(Material m)
                    {
                        Undo.RegisterCreatedObjectUndo(config, "Setting preview material");
                        config.Materials.Preview = m;
                        m.SetSurfaceType(SurfaceType.Transparent);
                        EditorUtility.SetDirty(config);
                    }

                    var path = AssetDatabase.GetAssetPath(defaultMaterial).AsSpan();
                    int dotIndex = path.LastIndexOf(".");
                    var newName = StringHelper.Concat(path[..dotIndex], "_preview.mat");
                    var fullPath = Path.Join(Application.dataPath, "..", newName);
                    if (File.Exists(fullPath))
                    {
                        if (_shouldClearPreviewMaterial)
                        {
                            AssetDatabase.DeleteAsset(newName);
                        }
                        else
                        {
                            Debug.Log("Skipping, because a preview material already exists");
                            SetPreviewMaterial(AssetDatabase.LoadAssetAtPath<Material>(newName));
                            continue;
                        }
                    }

                    var materialCopy = Material.Instantiate(defaultMaterial);
                    Undo.RegisterCreatedObjectUndo(materialCopy, "Create preview material");

                    var c = materialCopy.color;
                    c.a *= 0.5f;
                    materialCopy.color = c;

                    AssetDatabase.CreateAsset(materialCopy, newName);
                    SetPreviewMaterial(materialCopy);
                }
            }
            if (GUILayout.Button("Add colliders"))
            {
                foreach (var t in GetThings())
                {
                    var (colliderTransform, collider) = GetOrCreateCollider(t);

                    if (collider is MeshCollider meshCollider)
                    {
                        Undo.RegisterCompleteObjectUndo(meshCollider, "change collider properties");
                        Undo.RegisterCompleteObjectUndo(colliderTransform, "change transform properties");

                        var (modelTransform, model) = GetOrCreateModel(t);
                        meshCollider.sharedMesh = model.gameObject.GetComponent<MeshFilter>().sharedMesh;
                        colliderTransform.SetLocalTransform(modelTransform);
                        
                        EditorUtility.SetDirty(meshCollider);
                        EditorUtility.SetDirty(colliderTransform);
                    }
                    else if (collider is BoxCollider boxCollider)
                    {
                        Undo.RegisterCompleteObjectUndo(boxCollider, "change collider properties");
                        var bounds = ViewLogic.GetVisualInfo(t);
                        boxCollider.center = bounds.Center;
                        boxCollider.size = bounds.Size;
                        colliderTransform.ResetLocalPositionRotationScale();

                        EditorUtility.SetDirty(boxCollider);
                    }
                    EditorUtility.SetDirty(t.gameObject);
                }
            }
            if (GUILayout.Button("Clear colliders"))
            {
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
            }
            Undo.CollapseUndoOperations(group);
        }
    }

    public static class StringHelper
    {
        // https://github.com/dotnet/runtime/blob/4f9ae42d861fcb4be2fcd5d3d55d5f227d30e723/src/libraries/Microsoft.IO.Redist/src/Microsoft/IO/StringExtensions.cs#L49-L78
        public static unsafe string Concat(ReadOnlySpan<char> str0, ReadOnlySpan<char> str1)
        {
            var result = new string('\0', checked(str0.Length + str1.Length));
            fixed (char* resultPtr = result)
            {
                var resultSpan = new Span<char>(resultPtr, result.Length);

                str0.CopyTo(resultSpan);
                resultSpan = resultSpan[str0.Length..];

                str1.CopyTo(resultSpan);
            }
            return result;
        }

        public static unsafe string Concat(ReadOnlySpan<char> str0, ReadOnlySpan<char> str1, ReadOnlySpan<char> str2)
        {
            var result = new string('\0', checked(str0.Length + str1.Length + str2.Length));
            fixed (char* resultPtr = result)
            {
                var resultSpan = new Span<char>(resultPtr, result.Length);

                str0.CopyTo(resultSpan);
                resultSpan = resultSpan[str0.Length..];

                str1.CopyTo(resultSpan);
                resultSpan = resultSpan[str1.Length..];

                str2.CopyTo(resultSpan);
            }
            return result;
        }
    }
}