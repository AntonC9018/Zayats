using Zayats.Core;
using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using System.Linq;
using static Zayats.Core.Assert;
using Common;

#if UNITY_EDITOR
    using UnityEditor;
#endif

namespace Zayats.Unity.View
{
    [Serializable]
    public struct ActivatedItemHandling
    {
        public readonly bool InProgress => ThingId != -1;
        public int Index;
        public int ThingId;
        public Components.ActivatedItem ActivatedItem;
    }

    public static partial class ViewLogic
    {
        public static IEnumerable<Color> GetItemUsabilityColors(this ViewContext view, int playerIndex)
        {
            return view.Game.State.Players[playerIndex].Items.Select(it =>
            {
                var usability = view.Game.GetItemUsability(playerIndex, it);
                return view.Visual.ItemUsabilityColors.Get(usability);
            });
        }

        public static void SetItemsForPlayer(this ViewContext view, int playerIndex)
        {
            var s = DOTween.Sequence();
            view.UI.ItemContainers.ChangeItems(
                view.Game.State.Players[playerIndex].Items.Select(
                    id => view.UI.ThingGameObjects[id].transform).ToArray(),
                s,
                animationSpeed: view.Visual.AnimationSpeed.UI);
            view.ResetUsabilityColors(playerIndex, s);
            view.LastAnimationSequence.Append(s);
        }

        public static void ResetUsabilityColors(this ViewContext view, int playerIndex, Sequence s)
        {
            var colors = view.GetItemUsabilityColors(playerIndex).ToArray();
            view.UI.ItemContainers.ResetUsabilityColors(colors, s);
        }

        public static bool MaybeTryStartHandlingItemInteraction(this ViewContext view, int itemIndex)
        {
            if (!view.State.Selection.InProgress)
                return TryStartHandlingItemInteraction(view, itemIndex);
            return false;
        }

        public static bool TryStartHandlingItemInteraction(this ViewContext view, int itemIndex)
        {
            assert(!view.State.Selection.InProgress);

            ref var itemH = ref view.State.ItemHandling;
            var items = view.Game.State.CurrentPlayer.Items;
            if (items.Count <= itemIndex)
                return false;

            int thingItemId = items[itemIndex];
            if (!view.Game.TryGetComponent(Components.ActivatedItemId, thingItemId, out var activatedItemProxy))
                return false;

            var activatedItem = activatedItemProxy.Value;
            var filter = activatedItem.Filter;
            var targetKind = filter.Kind;

            ref var selection = ref view.State.Selection;
            selection.TargetKind = targetKind;
            selection.TargetIndices.Clear();
            selection.ValidTargets.Clear();

            itemH.ThingId = thingItemId;
            itemH.Index = itemIndex;
            itemH.ActivatedItem = activatedItem;

            if (targetKind == TargetKind.None)
            {
                view.ConfirmItemUse();

                // Makes sense for it to not return true,
                // since the interaction has already ended at this point.
                return false;
            }

            {
                var itemContext = view.Game.GetItemInteractionContextForCurrentPlayer(itemIndex);
                filter.GetValid(view.Game, itemContext).Overwrite(selection.ValidTargets);

                string Subject()
                {
                    switch (targetKind)
                    {
                        default: panic("?" + targetKind); return null;
                        case TargetKind.Cell:   return "cells";
                        case TargetKind.Player: return "players";
                        case TargetKind.Thing:  return "things";
                    }
                }

                // Payload in this case means cell count
                if (selection.ValidTargets.Count < activatedItem.RequiredTargetCount)
                {
                    view.DisplayTip($"Not enough {Subject()} (required {activatedItem.RequiredTargetCount}, available {selection.ValidTargets.Count}).");
                    return false;
                }

                view.HandleEvent(ViewEvents.OnItemInteractionStarted, new()
                {
                    Item = itemH,
                    Selection = view.State.Selection,
                });
            }
            
            return true;
        }

        public static void HighlightObjectsOfItemInteraction(this ViewContext view, in SelectionState selection)
        {
            view.HighlightObjects(
                view.GetObjectsValidForSelection(selection.TargetKind, selection.ValidTargets));
        }

        public static bool MaybeConfirmItemUse(this ViewContext view)
        {
            ref var itemH = ref view.State.ItemHandling;
            if (view.State.Selection.TargetIndices.Count != itemH.ActivatedItem.RequiredTargetCount)
                return false;

            ConfirmItemUse(view);
            return true;
        }

        public static void CancelOrFinalizeItemInteraction(this ViewContext view, ref ViewEvents.ItemHandlingContext context)
        {
            view.HandleEvent(ViewEvents.OnItemInteractionCancelledOrFinalized, ref context);
            view.CancelOrFinalizeSelection(ref context.Selection);
        }

        public static void ConfirmItemUse(this ViewContext view)
        {
            ref var itemH = ref view.State.ItemHandling;
            var selection = view.State.Selection;
            var validTargets = selection.ValidTargets;

            view.Game.UseItem(new()
            {
                Interaction = view.Game.GetItemInteractionContextForCurrentPlayer(itemH.Index),
                Item = view.Game.GetComponentProxy(Components.ActivatedItemId, itemH.ThingId),
                SelectedTargets = selection.TargetIndices.Select(i => validTargets[i]).ToArray(),
            });

            var context = new ViewEvents.ItemHandlingContext
            {
                Item = itemH,
                Selection = view.State.Selection,
            };
            view.ResetItemInteraction();
            view.HandleEvent(ViewEvents.OnItemInteractionFinalized, ref context);
            view.CancelOrFinalizeItemInteraction(ref context);
        }
        
        public static void CancelHandlingCurrentItemInteraction(this ViewContext view)
        {
            ref var itemH = ref view.State.ItemHandling;
            assert(itemH.InProgress);
            
            var context = new ViewEvents.ItemHandlingContext
            {
                Item = itemH,
                Selection = view.State.Selection,
            };
            view.ResetItemInteraction();
            view.HandleEvent(ViewEvents.OnItemInteractionCancelled, ref context);
            view.CancelOrFinalizeItemInteraction(ref context);
        }
    }
}