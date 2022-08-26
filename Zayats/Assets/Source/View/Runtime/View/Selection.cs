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
        public readonly int TargetCount => TargetIndices.Count;
        public readonly int ValidTargetCount => ValidTargets.Count;

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

        public static Ray GetScreenToPointRay(Vector2 positionOfInteractionOnScreen)
        {
            Ray ray = Camera.main.ScreenPointToRay(positionOfInteractionOnScreen);
            return ray;
        }

        public static (Transform Transform, GameObject GameObject)? RaycastThing(Ray ray, int layerMask)
        {
            RaycastHit hit;

            if (!Physics.Raycast(ray, out hit, maxDistance: float.PositiveInfinity, layerMask: layerMask))
                return null;
                
            var t = hit.collider.transform.parent;
            GameObject obj;
            while ((obj = t.gameObject).GetComponent<Collider>() != null)
                t = t.parent;

            return (t, obj);
        }

        public static (Transform Transform, GameObject GameObject)? RaycastRaycastable(Ray ray)
        {
            return RaycastThing(ray, layerMask: LayerBits.RaycastTarget);
        }

        public static bool MaybeSelectObject(this ViewContext view, Vector3 positionOfInteractionOnScreen)
        {
            var ray = GetScreenToPointRay(positionOfInteractionOnScreen);
            var raycastResult = RaycastRaycastable(ray);
            if (!raycastResult.HasValue)
                return false;
            var (transform, obj) = raycastResult.Value;
                
            // Buying shop item.
            // TODO: should be decoupled.
            int GetIndexInShop()
            {
                var game = view.Game;
                return game.IsShoppingAvailable(game.State.CurrentPlayerIndex)
                    ? game.State.Shop.Items.Select(id => view.UI.ThingGameObjects[id]).IndexOf(obj)
                    : -1;
            }

            var shopItemIndex = GetIndexInShop();
            bool isSelecting = view.State.Selection.InProgress;

            if (shopItemIndex != -1 && isSelecting)
                return false;

            if (isSelecting)
                view.AddOrRemoveObjectToSelection(obj);
            else if (shopItemIndex != -1)
                view.TryInitiateBuying(shopItemIndex);

            return true;
        }

        public static IEnumerable<GameObject> GetPlayerObjects(this ViewContext view)
        {
            return view.Game.State.Players.Select(p => view.UI.ThingGameObjects[p.ThingId]);
        }

        public static int GetCellIndex(this ViewContext view, GameObject raycastHitObject)
        {
            return Array.IndexOf(view.UI.VisualCells, raycastHitObject.transform);
        }

        public static (int TargetIndex, int Target) GetSelectionTargetInfo(this ViewContext view, GameObject raycastHitObject)
        {
            ref var selection = ref view.State.Selection;
            assert(view.State.Selection.InProgress);

            int target;
            switch (selection.TargetKind)
            {
                case TargetKind.None:
                {
                    panic("Should not come to this");
                    return (-1, -1);
                }
                default:
                {
                    panic($"Unimplemented case: {selection.TargetKind}.");
                    return (-1, -1);
                }
                case TargetKind.Cell:
                {
                    target = view.GetCellIndex(raycastHitObject);
                    break;
                }
                case TargetKind.Player:
                {
                    target = view.GetPlayerObjects().IndexOf(raycastHitObject);
                    break;
                }
                case TargetKind.Thing:
                {
                    target = Array.IndexOf(view.UI.ThingGameObjects, raycastHitObject);
                    break;
                }
            }

            int targetIndex = selection.ValidTargets.IndexOf(target);
            return (targetIndex, target);
        }

        public static void AddOrRemoveObjectToSelection(this ViewContext view, GameObject hitObject)
        {
            var (targetIndex, target) = view.GetSelectionTargetInfo(hitObject);
            assert(targetIndex != -1);

            ref var selection = ref view.State.Selection;
            var selected = selection.TargetIndices;
            if (!selected.Contains(targetIndex))
            {
                selected.Add(targetIndex);
                view.HandleEvent(ViewEvents.OnSelection.Progress, ref selection);
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

        public static void StartSelecting(this ViewContext view, ref SelectionState context)
        {
            view.HandleEvent(ViewEvents.OnSelection.Started, ref context);
        }

        public static void CancelSelection(this ViewContext view, ref SelectionState context)
        {
            view.CancelOrFinalizeSelection(ref context);
            view.HandleEvent(ViewEvents.OnSelection.Cancelled, ref context);
        }

        public static void FinalizeSelection(this ViewContext view, ref SelectionState context)
        {
            view.CancelOrFinalizeSelection(ref context);
            view.HandleEvent(ViewEvents.OnSelection.Finalized, ref context);
        }

        public static void CancelOrFinalizeSelection(this ViewContext view, ref SelectionState context)
        {
            view.HandleEvent(ViewEvents.OnSelection.CancelledOrFinalized, ref context);
        }
    }
}