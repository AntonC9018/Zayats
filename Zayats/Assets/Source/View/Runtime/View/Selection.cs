using Zayats.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using static Zayats.Core.Assert;
using Common.Unity;
using Common;

#if UNITY_EDITOR
    using UnityEditor;
#endif

namespace Zayats.Unity.View
{
    [Serializable]
    public struct SelectionState
    {
        public readonly bool InProgress => TargetKind != TargetKind.None;
        public TargetKind TargetKind;
        public List<int> ValidTargets;
        public List<int> TargetIndices;
    }

    public static partial class ViewLogic
    {
        public static void HighlightObjects(this ViewContext view, IEnumerable<Transform> objs)
        {
            ref var highlightMaterial = ref view.State.HighlightMaterial;
            var materialPaths = objs.SelectMany(c => c.GetObject(ObjectHierarchy.ModelInfo).Value.MaterialPaths);
            
            highlightMaterial.Reset(materialPaths);

            var intensity = view.Visual.HighlightEmissionIntensity;
            highlightMaterial.EmissionColor = new Color(intensity, intensity, intensity, 1);
            highlightMaterial.Apply();
        }

        public static void CancelHighlighting(this ViewContext view)
        {
            view.State.HighlightMaterial.Reset();
        }

        public static IEnumerable<Transform> GetObjectsValidForSelection(this ViewContext view, TargetKind targetKind, List<int> validTargets)
        {
            switch (targetKind)
            {
                default: panic($"Unimplemented case: {targetKind}"); return null;

                case TargetKind.Cell:
                {
                    var cells = validTargets;
                    return cells.Select(c => view.UI.VisualCells[c]);
                }
                case TargetKind.Player:
                {
                    var players = validTargets;
                    Transform GetPlayerTransform(int playerIndex)
                    {
                        int id = view.Game.State.Players[playerIndex].ThingId;
                        return view.UI.ThingGameObjects[id].transform;
                    }
                    return players.Select(GetPlayerTransform);
                }
                case TargetKind.Thing:
                {
                    return validTargets.Select(id => view.UI.ThingGameObjects[id].transform);
                }
            }
        }

        public static void ChangeLayerOnTargets(IEnumerable<Transform> targets, int layer)
        {
            foreach (var t in targets)
                ChangeLayer(t, layer);
        }

        public static void ChangeLayer(this Transform t, int layer)
        {
            t.GetChild(ObjectHierarchy.Collider.Id).gameObject.layer = layer;
        }

        public static void ChangeLayerOnValidTargets(this ViewContext view, TargetKind targetKind, List<int> validTargets, int layer)
        {
            ChangeLayerOnTargets(view.GetObjectsValidForSelection(targetKind, validTargets), layer);
        }

        public static void ChangeLayerOnValidTargetsForRaycasts(this ViewContext view, TargetKind targetKind, List<int> validTargets)
        {
            ChangeLayerOnValidTargets(view, targetKind, validTargets, LayerIndex.RaycastTarget);
        }

        public static void ChangeLayerOnValidTargetsToDefault(this ViewContext view, TargetKind targetKind, List<int> validTargets)
        {
            ChangeLayerOnValidTargets(view, targetKind, validTargets, LayerIndex.Default);
        }

        public static void SelectObject(this ViewContext view, Vector3 positionOfInteractionOnScreen)
        {
            assert(view.State.Selection.InProgress, "Didn't get disabled??");

            int layerMask = LayerBits.RaycastTarget;

            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(positionOfInteractionOnScreen);

            if (!Physics.Raycast(ray, out hit, layerMask))
                return;

            {
                var t = hit.collider.transform.parent;
                GameObject obj;
                while ((obj = t.gameObject).GetComponent<Collider>() != null)
                    t = t.parent;
                
                view.AddOrRemoveObjectToSelection(obj);
            }
        }

        public static IEnumerable<GameObject> GetPlayerObjects(this ViewContext view)
        {
            return view.Game.State.Players.Select(p => view.UI.ThingGameObjects[p.ThingId]);
        }

        public static void AddOrRemoveObjectToSelection(this ViewContext view, GameObject hitObject)
        {
            ref var selection = ref view.State.Selection;
            assert(view.State.Selection.InProgress);

            int target;
            switch (selection.TargetKind)
            {
                case TargetKind.None:
                {
                    panic("Should not come to this");
                    return;
                }
                default:
                {
                    panic($"Unimplemented case: {selection.TargetKind}.");
                    return;
                }
                case TargetKind.Cell:
                {
                    target = Array.IndexOf(view.UI.VisualCells, hitObject.transform);
                    break;
                }
                case TargetKind.Player:
                {
                    target = view.GetPlayerObjects().IndexOf(hitObject);
                    break;
                }
                case TargetKind.Thing:
                {
                    target = Array.IndexOf(view.UI.ThingGameObjects, hitObject);
                    break;
                }
            }

            int targetIndex = selection.ValidTargets.IndexOf(target);
            assert(targetIndex != -1);

            var selected = selection.TargetIndices;
            if (!selected.Contains(targetIndex))
            {
                selected.Add(targetIndex);

                view.HandleEvent(ViewEvents.OnSelectionProgress, ref selection);
            }
        }

        public static void ResetSelection(this ViewContext view)
        {
            view.State.Selection.TargetKind = TargetKind.None;
        }

        public static void ResetItemInteraction(this ViewContext view)
        {
            view.State.ItemHandling.ThingId = -1;
            view.State.ItemHandling.Index = -1;
            view.ResetSelection();
        }

        public static void CancelOrFinalizeSelection(this ViewContext view, ref SelectionState context)
        {
            view.HandleEvent(ViewEvents.OnSelectionCancelledOrFinalized, ref context);
        }

        public static void CancelCurrentSelectionInteraction(this ViewContext view)
        {
            assert(view.State.Selection.InProgress);

            // Might want an enum + switch for this, or an interface.
            if (view.State.ItemHandling.InProgress)
            {
                view.CancelHandlingCurrentItemInteraction();
            }
            else if (view.State.ForcedItemDropHandling.InProgress)
            {
                // view.State.
            }
            else panic("Unimplemented?");

        }
    }
}