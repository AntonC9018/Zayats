using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Zayats.Core;
using System.Linq;
using Common;

namespace Zayats.Unity.View.Editor
{
    public class GameplayHelperWindow : EditorWindow
    {
        private View.Initialization _initialization;
        private ViewContext _view;
        private SerializedObject _viewHolderSerializedObject;
        private SerializedProperty _viewSerializedProperty;
        private List<string> _diagnostics;

        public void Initialize()
        {
            _initialization = GameObject.FindObjectOfType<Initialization>();
            if (_initialization == null)
                return;
            _viewHolderSerializedObject = new(_initialization);
            _viewSerializedProperty = _viewHolderSerializedObject.FindProperty(nameof(_initialization._view));
            _view = _initialization._view;
            _diagnostics ??= new();
        }

        // Add menu named "My Window" to the Window menu
        [MenuItem("Tools/GameplayHelperWindow")]
        public static void BringUpWindow()
        {
            var window = EditorWindow.GetWindow<GameplayHelperWindow>();
            window.Initialize();
            window.Show();
        }
        

        void OnGUI()
        {
            if (_initialization == null
                || _viewHolderSerializedObject == null
                || _viewSerializedProperty == null)
            {
                Initialize();
            }

            if (_initialization == null)
            {
                EditorGUILayout.LabelField("View context not found");
                return;
            }

            if (_viewHolderSerializedObject == null || _viewSerializedProperty == null)
            {
                EditorGUILayout.LabelField("???");
                return;
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("Enter play mode");
                return;
            }

            EditorGUILayout.PropertyField(_viewSerializedProperty, new GUIContent("View"));

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
            if (GUILayout.Button("Get Snake"))
                MaybeGetSnake(selectedObjects);
            
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current player walk");
            for (int i = 1; i <= 6; i++)
            {
                if (GUILayout.Button(i.ToString()))
                {
                    _view.BeginAnimationEpoch();
                    _view.Game.MovePlayer_DoPostMovementMechanics(new()
                    {
                        Amount = i,
                        Details = MovementKind.Normal,
                        PlayerIndex = _view.Game.State.CurrentPlayerIndex,
                    });
                }
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("End turn"))
            {
                _view.BeginAnimationEpoch();
                _view.Game.EndCurrentPlayersTurn();
            }

            if (GUILayout.Button("Skip animations"))
                _view.SkipAnimations();

            if (GUILayout.Button("Highlight 10 cells"))
                _view.HighlightObjects(_view.UI.VisualCells.Take(10));

            if (GUILayout.Button("Cancel highlighting"))
                _view.CancelHighlighting();


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

        public IEnumerable<(GameObject obj, int id)> GetThings(GameObject[] selectedObjects)
        {
            foreach (var obj in selectedObjects)
            {
                int thingId = MapId(obj.GetInstanceID());
                if (thingId == -1)
                {
                    _diagnostics.Add($"{obj.name}, {obj.GetInstanceID()} is not a thing.");
                    continue;
                }
                yield return (obj, thingId);
            }
        }

        public bool ShouldSelect(GameObject[] selectedObjects)
        {
            _diagnostics.Clear();
            if (selectedObjects.Length == 0)
            {
                _diagnostics.Add("No objects selected");
                return false;
            }
            return true;
        }

        public List<int> GetPlayers(List<(GameObject obj, int id)> things)
        {
            List<int> players = new();
            foreach (var (obj, id) in things)
            {
                if (!_view.Game.TryGetComponentValue(Components.PlayerId, id, out var p))
                {
                    _diagnostics.Add($"{obj.name}, {obj.GetInstanceID()} is not a player.");
                    continue;
                }
                players.Add(p.PlayerIndex);
            }
            return players;
        }

        public bool MaybeGetSnake(GameObject[] selectedObjects)
        {
            _view.BeginAnimationEpoch();
            var snake = _view.Game
                .GetComponentStorage(Components.ActivatedItemId)
                .Enumerate()
                .MaybeFirst(t => t.Proxy.Value.Action is KillPlayersAction
                    && !_view.Game.State.CurrentPlayer.Items.Contains(t.Id));
            
            if (!snake.HasValue)
            {
                _diagnostics.Add("No unequipped snakes. Cannot spawn things dynamically at this point...");
                return false;
            }
            _view.Game.AddItemToInventory_WithoutPickupEffect(new()
            {
                PlayerIndex = _view.Game.State.CurrentPlayerIndex,
                Position =  _view.Game.State.CurrentPlayer.Position,
                ThingId = snake.Value.Id,
            });

            return true;
        }

        public bool MaybeKillPlayer(GameObject[] selectedObjects)
        {
            if (!ShouldSelect(selectedObjects))
                return false;
            var players = GetPlayers(GetThings(selectedObjects).ToList());

            if (_diagnostics.Count > 0)
                return false;

            _view.BeginAnimationEpoch();
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