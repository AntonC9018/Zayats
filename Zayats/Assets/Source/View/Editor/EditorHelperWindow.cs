using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Zayats.Core;
using System.Linq;
using Common;
using Common.Unity;

namespace Zayats.Unity.View.Editor
{
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
                    while (t.childCount <= ObjectHierarchy.ModelInfo.Id)
                        new GameObject("???").transform.parent = t;

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
        }
     
    }
}