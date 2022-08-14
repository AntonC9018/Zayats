using System;
using DG.Tweening;
using UnityEngine;
using static Zayats.Core.Assert;
using Common.Unity;
using System.Collections.Generic;
using Zayats.Core;
using System.Linq;
using Common;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zayats.Unity.View
{
    [Serializable]
    public struct ForcedItemDropHandling
    {
        public bool InProgress;
        
        // Always Game.CurrentPlayerIndex
        // public int PlayerIndex;

        public List<(int Index, int ThingId)> RemovedItems;
        public List<int> SelectedPositions;

        public List<Transform> PreviewObjects;
    }

    public static partial class ViewLogic
    {
        public static void MaybeTryInitiateBuying(this ViewContext view, int shopItemIndex)
        {
            if (view.State.Selection.InProgress)
                return;
            view.TryInitiateBuying(shopItemIndex);
        }

        public static bool TryInitiateBuying(this ViewContext view, int shopItemIndex)
        {
            assert(!view.State.Selection.InProgress);
            assert(shopItemIndex > 0);

            var shopItems = view.Game.State.Shop.Items;
            assert(shopItems.Count > shopItemIndex);

            var itemId = shopItems[shopItemIndex];
            var context = view.Game.StartBuyingThingFromShop(new()
            {
                PlayerIndex = view.Game.State.CurrentPlayerIndex,
                ThingShopIndex = shopItemIndex,
            });

            if (context.NotEnoughCoins)
            {
                view.DisplayTip("Not enough coins");
                return false;
            }

            ref var drop = ref view.State.ForcedItemDropHandling;
            // context.Coins.Overwrite(drop.RemovedItems);
            drop.RemovedItems = context.Coins;
            drop.SelectedPositions.Clear();
            drop.PreviewObjects.Clear();
            drop.InProgress = true;

            int targetCellCount = context.Coins.Count;
            var unoccupiedCells = view.Game.GetUnoccupiedCellIndices();

            ref var selection = ref view.State.Selection;
            assert(!selection.InProgress);
                
            selection.TargetKind = TargetKind.Cell;
            unoccupiedCells.Overwrite(selection.ValidTargets);
            selection.TargetIndices.Clear();


            int unoccupiedCellCount = selection.ValidTargets.Count;

            // Resolve immediately
            if (unoccupiedCellCount == drop.RemovedItems.Count)
            {
                ConfirmBuyingItem(view, ref drop, ref context);
                return true;
            }

            else if (unoccupiedCellCount < drop.RemovedItems.Count)
            {
                view.DisplayTip("Not enough empty spaces to drop the spent coins");
                return false;
            }

            else // if (unoccupiedCellCount > drop.RemovedItems.Count)
            {
                view.HandleEvent(ViewEvents.OnForcedItemDrop.Started, ref drop);
                return true;
            }
        }

        public static void PreviewPlaceNextCoin(this ViewContext view, ref ForcedItemDropHandling drop, int cellIndex)
        {
            assert(drop.InProgress);
            assert(!drop.SelectedPositions.Contains(cellIndex));

            int coinsPlacedSoFar = drop.SelectedPositions.Count;
            assert(coinsPlacedSoFar < drop.RemovedItems.Count);

            var nextCoinId = drop.RemovedItems[coinsPlacedSoFar].ThingId;
            var nextCoinPrefab = view.UI.ThingGameObjects[nextCoinId].gameObject;
            var cellInfo = view.GetCellVisualInfo(cellIndex);
            var nextCoinPreviewObject = GameObject.Instantiate(
                original: nextCoinPrefab,
                position: cellInfo.GetTop(),
                rotation: Quaternion.identity,
                parent: cellInfo.OuterObject).transform;
            
            drop.PreviewObjects.Add(nextCoinPreviewObject);
            drop.SelectedPositions.Add(cellIndex);
            
            static void SetMaterial(Transform obj, MaterialKind kind)
            {
                var (_, modelInfo) = obj.GetObject(ObjectHierarchy.ModelInfo);
                modelInfo.SetSharedMaterial(kind);
            }
            SetMaterial(nextCoinPreviewObject, MaterialKind.Preview);
        }

        public static bool TrySetCoinPositionToCell(
            this ViewContext view,
            ref ForcedItemDropHandling drop,
            int coinIndex,
            int cellIndex)
        {
            assert(drop.InProgress);
            assert(drop.PreviewObjects.Count > coinIndex);

            if (!view.State.Selection.ValidTargets.Contains(cellIndex))
                return false;

            var cellInfo = view.GetCellVisualInfo(cellIndex);
            var coinPreviewObject = drop.PreviewObjects[coinIndex];
            coinPreviewObject.parent = cellInfo.OuterObject;
            coinPreviewObject.position = cellInfo.GetTop();

            // This should probably also use a different material.            
            if (drop.SelectedPositions.Contains(cellIndex))
                return false;

            drop.SelectedPositions[coinIndex] = cellIndex;
            return true;
        }

        public static void SetCoinPosition(this ViewContext view, ref ForcedItemDropHandling drop, Vector3 p)
        {
            
        }

        public static void CancelPurchase(this ViewContext view, ref ForcedItemDropHandling drop)
        {
            assert(drop.InProgress);

            foreach (var coin in drop.PreviewObjects)
                GameObject.Destroy(coin);

            view.HandleEvent(ViewEvents.OnForcedItemDrop.Cancelled, ref drop);

            drop.PreviewObjects.Clear();
            drop.SelectedPositions.Clear();
            drop.RemovedItems.Clear();
            drop.InProgress = false;
        }

        public static void ConfirmBuyingItem(
            this ViewContext view,
            ref ForcedItemDropHandling drop,
            ref Logic.PurchaseContext purchase)
        {

        }
    }
}