using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Zayats.Core;
using System.Linq;
using Common;
using Common.Unity;

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

                    ref var matPaths = ref modelInfo.MaterialPaths;
                    if (matPaths == null || matPaths.Length == 0)
                    {
                        Undo.RegisterCompleteObjectUndo(modelInfo, "change model info");
                        matPaths = t.GetComponentsInChildren<MeshRenderer>()
                            // for now, just take the first material.
                            .Select(m => new MaterialPath(m, 0))
                            .ToArray();
                        EditorUtility.SetDirty(modelInfo);
                        EditorUtility.SetDirty(child);
                    }
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