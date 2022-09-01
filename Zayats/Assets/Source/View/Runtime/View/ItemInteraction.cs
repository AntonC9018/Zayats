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
            
            // This could fail if ChangeItems is called twice in a row, which would make the calls overlap.
            // I don't think it would crash or anything, but it would look funky for sure.
            view.LastAnimationSequence.Join(s);
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
            assert(view.State.Selection.InteractionKind == SelectionInteractionKind.None);

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
            selection.InteractionKind = SelectionInteractionKind.Item;
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
                if (selection.ValidTargetCount < activatedItem.RequiredTargetCount)
                {
                    view.DisplayTip($"Not enough {Subject()} (required {activatedItem.RequiredTargetCount}, available {selection.ValidTargetCount}).");
                    return false;
                }

                view.StartSelecting(ref selection);
                view.HandleEvent(ViewEvents.OnItemInteraction.Started, new()
                {
                    Item = itemH,
                    Selection = view.State.Selection,
                });
            }
            
            return true;
        }

        public static void HighlightObjectsOfSelection(this ViewContext view, in SelectionState selection)
        {
            view.HighlightObjects(
                view.GetTargetObjects(selection.TargetKind, selection.ValidTargets));
        }

        public static bool MaybeConfirmItemUse(this ViewContext view)
        {
            ref var itemH = ref view.State.ItemHandling;
            if (view.State.Selection.TargetCount != itemH.ActivatedItem.RequiredTargetCount)
                return false;

            ConfirmItemUse(view);
            return true;
        }

        public static void CancelOrFinalizeItemInteraction(this ViewContext view, ref ViewEvents.ItemHandlingContext context)
        {
            assert(context.Selection.InteractionKind != SelectionInteractionKind.None);
            view.HandleEvent(ViewEvents.OnItemInteraction.CancelledOrFinalized, ref context);
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
                // Item = view.Game.GetComponentProxy(Components.ActivatedItemId, itemH.ThingId),
                SelectedTargets = selection.TargetIndices.Select(i => validTargets[i]).ToArray(),
            });

            var context = new ViewEvents.ItemHandlingContext
            {
                Item = itemH,
                Selection = view.State.Selection,
            };
            view.ResetItemInteraction();
            view.HandleEvent(ViewEvents.OnItemInteraction.Finalized, ref context);
            view.CancelOrFinalizeItemInteraction(ref context);
        }
        
        public static void CancelHandlingCurrentItemInteraction(this ViewContext view)
        {
            assert(view.State.Selection.InteractionKind == SelectionInteractionKind.Item);
            
            var context = new ViewEvents.ItemHandlingContext
            {
                Item = view.State.ItemHandling,
                Selection = view.State.Selection,
            };
            view.ResetItemInteraction();
            view.HandleEvent(ViewEvents.OnItemInteraction.Cancelled, ref context);
            view.CancelOrFinalizeItemInteraction(ref context);
        }
    }
}