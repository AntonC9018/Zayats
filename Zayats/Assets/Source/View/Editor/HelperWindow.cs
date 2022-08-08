using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Zayats.Core;

namespace Zayats.Unity.View.Editor
{
    public class HelperWindow : EditorWindow
    {
        private ViewContext _view;
        private SerializedObject _viewHolderSerializedObject;
        private SerializedProperty _viewSerializedProperty;
        private List<string> _diagnostics;

        public void Initialize()
        {
            var v = GameObject.FindObjectOfType<Initialization>();
            _viewHolderSerializedObject = new(v);
            _viewSerializedProperty = _viewHolderSerializedObject.FindProperty(nameof(v._view));
            _view = v._view;
            _diagnostics ??= new();
        }

        // Add menu named "My Window" to the Window menu
        [MenuItem("Tools/Helper Window")]
        public static void BringUpWindow()
        {
            var window = EditorWindow.GetWindow<HelperWindow>();
            window.Initialize();
            window.Show();
        }
        

        void OnGUI()
        {
            if (_viewSerializedProperty is null)
                Initialize();
            EditorGUILayout.PropertyField(_viewSerializedProperty, new GUIContent("View"));

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("Enter play mode");
                return;
            }

            var selectedObjects = Selection.gameObjects;
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                var t = selectedObjects[i];
                while (t.GetComponent<MeshRenderer>() != null)
                    t = t.transform.parent.gameObject;
                selectedObjects[i] = t;
            }
            if (GUILayout.Button("KillPlayer"))
                MaybeKillPlayer(selectedObjects);

            foreach (var message in _diagnostics)
                EditorGUILayout.LabelField(message);
        }

        private int MapId(int unityId)
        {
            var l = _view.UI.ThingGameObjects;
            for (int i = 0; i < l.Length; i++)
            {
                if (l[i].GetInstanceID() == unityId)
                    return i;
            }
            return -1;
        }

        public bool MaybeKillPlayer(GameObject[] selectedObjects)
        {
            _diagnostics.Clear();
            if (selectedObjects.Length == 0)
            {
                _diagnostics.Add("No objects selected");
                return true;
            }

            List<int> players = new();
            foreach (var obj in selectedObjects)
            {
                int thingId = MapId(obj.GetInstanceID());
                if (thingId == -1)
                {
                    _diagnostics.Add($"{obj.name}, {obj.GetInstanceID()} is not a thing.");
                    continue;
                }
                if (!_view.Game.TryGetComponentValue(Components.PlayerId, thingId, out var p))
                {
                    _diagnostics.Add($"{obj.name}, {obj.GetInstanceID()} is not a player.");
                    continue;
                }
                players.Add(p.PlayerIndex);
            }

            if (_diagnostics.Count > 0)
                return false;

            foreach (var pIndex in players)
            {
                _view.Game.KillPlayer(new()
                {
                    Reason = Reasons.Debug,
                    PlayerIndex = pIndex,
                });
            }
            return true;
        }
    }
}