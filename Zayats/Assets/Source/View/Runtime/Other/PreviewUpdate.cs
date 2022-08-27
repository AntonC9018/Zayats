using UnityEngine;
using UnityEngine.Assertions;
using Zayats.Core;

namespace Zayats.Unity.View
{
    using static Zayats.Core.Assert;
    
    public class PreviewUpdate : MonoBehaviour
    {
        private ViewContext _view;

        public void Initialize(ViewContext view)
        {
            _view = view;
            enabled = false;
            _view.GetEventProxy(ViewEvents.OnForcedItemDrop.Started).Add(() => enabled = true);
            _view.GetEventProxy(ViewEvents.OnForcedItemDrop.CancelledOrFinalized).Add(() => enabled = false);
        }
        
        void Update()
        {
            if (_view == null)
            {
                enabled = false;
                panic("View should have been initialized at this point.");
                return;
            }

            ref var drop = ref _view.State.ForcedItemDropHandling; 
            assert(drop.InProgress, "Should have been deactivated.");
            
            // - try raycast cell
            // If there is a cell under selection, then it's already been raycasted and the position
            // of the coin is already correct.
            // TODO:
            // this raycasting should be done in the background, perhaps lazily, and always available.
            // need one more layer of abstraction here.
            var ray = ViewLogic.GetScreenToPointRay(Input.mousePosition);

            {
                var r = ViewLogic.RaycastRaycastable(ray);
                // Pointing at a cell
                if (r.HasValue)
                {
                    assert(_view.State.Selection.TargetKind == TargetKind.Cell);
                    
                    var cellIndex = _view.GetCellIndex(r.Value.GameObject);
                    if (_view.TrySetLastCoinPositionToCell(ref drop, cellIndex))
                        return;
                }
            }

            // - when outside cell, get the intersection of ray with the cell plane, set the position to there
            // _view.TrySetCoinPositionToCell();
            // TODO: This also needs an abstraction. Imagine the cells are not on the same plane.
            int testCellIndex = 0;
            var cellInfo = _view.GetCellVisualInfo(testCellIndex);
            var cellTopPlane = new Plane(inNormal: cellInfo.OuterObject.up, inPoint: cellInfo.GetTop());
            
            if (cellTopPlane.Raycast(ray, out float distance))
            {
                var position = ray.GetPoint(distance);
                _view.SetLastCoinPositionOutsideCell(ref drop, position);
            }
        }
    }
}