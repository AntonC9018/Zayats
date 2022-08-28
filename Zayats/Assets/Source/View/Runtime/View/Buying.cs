using System;
using DG.Tweening;
using UnityEngine;
using static Zayats.Core.Assert;
using Common.Unity;
using System.Collections.Generic;
using Zayats.Core;
using System.Linq;
using Common;
using Zayats.Unity.View.Generated;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zayats.Unity.View
{
    using static Logic.StartPurchaseResult;

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

    [Serializable]
    public struct ShopUIReferences
    {
        public Transform ShopRoot;
        public CornersArray<Transform> Corners;
    }

    [Serializable]
    public struct ShopState
    {
        public ViewLogic.GridCornersInfo Grid;
        public ViewLogic.SquareGridAlignmentInfo Alignment;
    }

    public static partial class ViewLogic
    {
        public static void MaybeTryInitiateBuying(this ViewContext view, int shopItemIndex)
        {
            if (view.State.Selection.InProgress)
                return;
            view.TryInitiateBuying(shopItemIndex);
        }

        private static void ConfirmBuying(this ViewContext view, ref ForcedItemDropHandling drop)
        {
            view.Game.EndBuyingThingFromShop(new()
            {
                CoinsToPayWith = drop.RemovedItems,
                Start = view.State.CurrentPurchase,
                SelectedCoinPlacementPositions = drop.SelectedPositions,
            });
        }

        public static bool TryInitiateBuying(this ViewContext view, int shopItemIndex)
        {
            ref var selection = ref view.State.Selection;

            assert(!selection.InProgress);
            assert(shopItemIndex >= 0);

            var shopItems = view.Game.State.Shop.Items;
            assert(shopItems.Count > shopItemIndex);

            var itemId = shopItems[shopItemIndex];
            
            ref var start = ref view.State.CurrentPurchase;
            start.PlayerIndex = view.Game.State.CurrentPlayerIndex;
            start.ThingShopIndex = shopItemIndex;

            ref var drop = ref view.State.ForcedItemDropHandling;
            drop.SelectedPositions.Clear();
            drop.PreviewObjects.Clear();
            drop.InProgress = true;

            {
                var result = view.Game.StartBuyingThingFromShop(start, outSpentCoins: drop.RemovedItems);

                if (result == NotEnoughCoins)
                {
                    view.DisplayTip("Not enough coins");
                    return false;
                }

                if (result == ItemIsFree)
                {
                    ConfirmBuyingItem(view, ref drop);
                    // Started + ended, so need not change anything (I guess).
                    return false;
                }

                assert(result == RequiresToSpendCoins);
            }

            int targetCellCount = drop.RemovedItems.Count;
            var unoccupiedCells = view.Game.GetUnoccupiedCellIndices();

            assert(!selection.InProgress);
                
            selection.TargetKind = TargetKind.Cell;
            selection.TargetIndices.Clear();
            unoccupiedCells.Overwrite(selection.ValidTargets);

            int unoccupiedCellCount = selection.ValidTargets.Count;

            // Resolve immediately
            if (unoccupiedCellCount == drop.RemovedItems.Count)
            {
                ConfirmBuying(view, ref drop);
                return true;
            }

            else if (unoccupiedCellCount < drop.RemovedItems.Count)
            {
                view.DisplayTip("Not enough empty spaces to drop the spent coins");
                return false;
            }

            else // if (unoccupiedCellCount > drop.RemovedItems.Count)
            {
                view.StartSelecting(ref selection);
                view.HandleEvent(ViewEvents.OnForcedItemDrop.Started, ref drop);
                view.PreviewSpawnNextCoin(ref drop);
                return true;
            }
        }

        public static void PreviewSpawnNextCoin(this ViewContext view, ref ForcedItemDropHandling drop)
        {
            assert(drop.InProgress);

            int coinsPlacedSoFar = drop.SelectedPositions.Count;
            assert(coinsPlacedSoFar < drop.RemovedItems.Count);
            
            if (coinsPlacedSoFar > 0)
                assert(drop.SelectedPositions[^1] != -1);

            var nextCoinId = drop.RemovedItems[coinsPlacedSoFar].ThingId;
            var nextCoinPrefab = view.UI.ThingGameObjects[nextCoinId];
            var nextCoinPreviewObject = GameObject.Instantiate(original: nextCoinPrefab).transform;
            
            drop.PreviewObjects.Add(nextCoinPreviewObject);
            drop.SelectedPositions.Add(-1);

            static void SetMaterial(Transform obj, MaterialKind kind)
            {
                var (_, modelInfo) = obj.GetObject(ObjectHierarchy.ModelInfo);
                modelInfo.SetSharedMaterial(kind);
            }
            SetMaterial(nextCoinPreviewObject, MaterialKind.Preview);
        }

        public static bool TrySetLastCoinPositionToCell(
            this ViewContext view,
            ref ForcedItemDropHandling drop,
            int cellIndex)
        {
            return view.TrySetCoinPositionToCell(
                ref drop,
                coinIndex: drop.PreviewObjects.Count - 1,
                cellIndex);
        }
        
        public static bool TrySetCoinPositionToCell(
            this ViewContext view,
            ref ForcedItemDropHandling drop,
            int coinIndex,
            int cellIndex)
        {
            assert(drop.InProgress);
            assert(drop.PreviewObjects.Count > coinIndex);

            var cellInfo = view.GetCellVisualInfo(cellIndex);
            var coinPreviewObject = drop.PreviewObjects[coinIndex];
            coinPreviewObject.parent = cellInfo.OuterObject;
            coinPreviewObject.position = cellInfo.GetTop();
            
            if (!view.State.Selection.ValidTargets.Contains(cellIndex))
                return false;

            var s = drop.SelectedPositions;
            if (s.Take(s.Count - 1).Contains(cellIndex))
                return false;     

            s[coinIndex] = cellIndex;
            return true;
        }

        public static void SetLastCoinPositionToLastSelectedPosition(
            this ViewContext view,
            ref ForcedItemDropHandling drop)
        {
            assert(drop.InProgress);
            var coinIndex = drop.PreviewObjects.Count - 1;
            ref var selection = ref view.State.Selection; 
            var targetIndex = selection.TargetIndices[^1];
            var cellIndex = selection.ValidTargets[targetIndex];
            var cellInfo = view.GetCellVisualInfo(cellIndex);
            var coinPreviewObject = drop.PreviewObjects[coinIndex];
            coinPreviewObject.parent = cellInfo.OuterObject;
            coinPreviewObject.position = cellInfo.GetTop();

            var s = drop.SelectedPositions;
            if (s.Take(s.Count - 1).Contains(cellIndex))
                panic("Repeated cell index?");
            
            s[coinIndex] = cellIndex;
        }

        public static void HandleNextForcedItemDropStateMachineStep(this ViewContext view, ref ForcedItemDropHandling drop)
        {
            // This discrepancy can possibly happen if the update on the animation
            // thing happens before the click input gets handled.
            // Which I think it does. Currently, there's no centralized raycast system,
            // so we can't just assume this works...
            view.SetLastCoinPositionToLastSelectedPosition(ref drop);
            // assert(drop.SelectedPositions[^1] != -1, "Should have been set by the PreviewUpdate script");
            
            if (drop.PreviewObjects.Count != drop.RemovedItems.Count)
                view.PreviewSpawnNextCoin(ref drop);
            else
                view.ConfirmBuyingItem(ref drop);
        }

        public static void SetLastCoinPositionOutsideCell(
            this ViewContext view,
            ref ForcedItemDropHandling drop,
            Vector3 position)
        {
            assert(drop.InProgress);
            var index = drop.PreviewObjects.Count - 1;
            var lastCoin = drop.PreviewObjects[index];
            lastCoin.position = position;
            lastCoin.parent = null;
            drop.SelectedPositions[index] = -1;
        }

        private static void DestroyPreviewObjects(this in ForcedItemDropHandling drop)
        {
            foreach (var coin in drop.PreviewObjects)
                GameObject.Destroy(coin.gameObject);
        }

        public static void CancelPurchase(this ViewContext view, ref ForcedItemDropHandling drop)
        {
            assert(drop.InProgress);

            DestroyPreviewObjects(drop);

            view.CancelSelection(ref view.State.Selection);
            view.HandleEvent(ViewEvents.OnForcedItemDrop.Cancelled, ref drop);
            view.HandleEvent(ViewEvents.OnForcedItemDrop.CancelledOrFinalized, ref drop);

            drop.PreviewObjects.Clear();
            drop.SelectedPositions.Clear();
            drop.RemovedItems.Clear();
            drop.InProgress = false;
        }

        public static void ConfirmCurrentCoinPlacement(this ref ForcedItemDropHandling drop)
        {
            assert(drop.InProgress);
            assert(drop.SelectedPositions[^1] != -1);
        }

        public static bool HaveAllCoinsBeenPlacedBeforePurchase(in SelectionState selection, in ForcedItemDropHandling drop)
        {
            assert(drop.InProgress);
            return drop.SelectedPositions.Count == drop.RemovedItems.Count
                && drop.SelectedPositions[^1] != -1;
        }

        public static void ConfirmBuyingItem(
            this ViewContext view,
            ref ForcedItemDropHandling drop)
        {
            assert(HaveAllCoinsBeenPlacedBeforePurchase(view.State.Selection, drop));

            DestroyPreviewObjects(drop);
            ConfirmBuying(view, ref drop);

            view.FinalizeSelection(ref view.State.Selection);
            view.HandleEvent(ViewEvents.OnForcedItemDrop.CancelledOrFinalized, ref drop);
            view.HandleEvent(ViewEvents.OnForcedItemDrop.Finalized, ref drop);
        }

        public struct GridCornersInfo
        {
            public Vector3 Origin;
            public Vector3 VX;
            public Vector3 VY;
            public Vector2 Size;
        }

        public static GridCornersInfo GetGridInfoForCorners(CornersArray<Transform> corners)
        {
            GridCornersInfo grid;
            Vector3 vwidth, vheight;
            (grid.Origin, vwidth, vheight) = corners.GetCornersInfo();
            grid.Size.x = vwidth.magnitude;
            grid.Size.y = vheight.magnitude;
            grid.VX = vwidth / grid.Size.x;
            grid.VY = vheight / grid.Size.y;
            return grid;
        }


        public struct SquareGridAlignmentInfo
        {
            public int WidthInBoxes;
            public Vector2 GapSize;
            public Vector2 BoxSize;

            public readonly int MaxItemCapacity => WidthInBoxes * WidthInBoxes;

            public readonly Vector3 GetPositionAt(Vector2 i, in GridCornersInfo grid)
            {
                var gap = Vector2.Scale(i, GapSize) + GapSize;
                var offset = Vector2.Scale(i, BoxSize);
                var position = gap + offset;
                Vector3 result = grid.Origin + position.x * grid.VX + position.y * grid.VY;
                return result;
            }

            public readonly Vector3 GetPositionAtIndex(int i, in GridCornersInfo grid)
            {
                int x = i % WidthInBoxes;
                int y = i / WidthInBoxes;
                return GetPositionAt(new Vector2(x, y), grid);
            }
        }

        public static SquareGridAlignmentInfo AlignInSquareGrid(
            in GridCornersInfo grid,
            int itemCount,
            float gapPercentage = 0.05f)
        {
            SquareGridAlignmentInfo alignment;

            // I want to do a square space for the items this time.
            int boxCount = itemCount;
            var closestApproximationSizeLength = 0;
            while (closestApproximationSizeLength * closestApproximationSizeLength < boxCount)
                closestApproximationSizeLength++;

            alignment.WidthInBoxes = closestApproximationSizeLength;

            Vector2 desiredBoxSize = grid.Size / alignment.WidthInBoxes;             

            int gapCount = closestApproximationSizeLength + 1;
            alignment.GapSize = gapPercentage * desiredBoxSize;

            alignment.BoxSize = (grid.Size - (alignment.WidthInBoxes + 1) * alignment.GapSize) / alignment.WidthInBoxes;

            return alignment;
        }

        public static Vector3 GetPositionForInventoryItem(
            in GridCornersInfo grid,
            ref SquareGridAlignmentInfo alignment,
            int numItems)
        {
            if (numItems + 1 > alignment.MaxItemCapacity)
                alignment = AlignInSquareGrid(grid, numItems + 1);
            return alignment.GetPositionAtIndex(numItems + 1, grid);
        }

        public static void OnItemAddedToShop(this ViewContext view, ref GameEvents.ThingAddedToShopContext context)
        {
            OnItemAddedToShop(view, ref view.State.Shop, context);
        }

        public static void OnItemAddedToShop(this ViewContext view, ref ShopState shop, in GameEvents.ThingAddedToShopContext context)
        {
            int thingId = context.ThingId;
            
            var thing = view.UI.ThingGameObjects[thingId].transform;
            ref var grid = ref shop.Grid;
            ref var alignment = ref shop.Alignment;

            var s = view.MaybeBeginAnimationEpoch();
            var position = GetPositionForInventoryItem(grid, ref alignment, view.Game.State.Shop.Items.Count);
            var speeds = view.Visual.AnimationSpeed;
            var tween = thing.DOMove(position,
                duration: context.Reason.Id == Reasons.PlacementId
                ? speeds.InitialThingSpawning
                : speeds.Game);
            tween.OnComplete(() =>
            {
                thing.ChangeLayer(LayerIndex.RaycastTarget);
                thing.parent = view.UI.Static.ShopUI.ShopRoot;
            });
            s.Append(tween);
        }

        public static void ArrangeShopItems(this ViewContext view, Sequence animationSequence, float animationSpeed)
        {
            var shopRoot = view.UI.Static.ShopUI.ShopRoot;
            ref var grid = ref view.State.Shop.Grid;

            ref var alignment = ref view.State.Shop.Alignment;
            var shopItems = view.Game.State.Shop.Items;
            if (shopItems.Count > alignment.MaxItemCapacity)
                alignment = AlignInSquareGrid(grid, shopItems.Count);

            for (int i = 0; i < shopItems.Count; i++)
            {
                var itemId = shopItems[i];
                var item = view.UI.ThingGameObjects[itemId].transform;
                var position = alignment.GetPositionAtIndex(i, grid);
                var itemInfo = item.GetVisualInfo();
                var p = position + itemInfo.GetTopOffset(Vector3.up);
                var tween = item.DOMove(p, animationSpeed);
                animationSequence.Insert(0, tween);
            }
        }
    }
}