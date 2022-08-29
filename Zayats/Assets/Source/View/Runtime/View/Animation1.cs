using System;
using DG.Tweening;
using UnityEngine;
using static Zayats.Core.Assert;
using Common.Unity;

#if UNITY_EDITOR
    using UnityEditor;
#endif

namespace Zayats.Unity.View
{
    public static partial class ViewLogic
    {
        public static Sequence MaybeBeginAnimationEpoch(this ViewContext view)
        {
            var sequences = view.AnimationSequences;
            if (sequences.Count == 0)
                return view.BeginAnimationEpoch();
            return sequences.Last.Value;
        }

        public static Sequence BeginAnimationEpoch(this ViewContext view)
        {
            view.State.AnimationEpoch++;

            var sequences = view.AnimationSequences;
            var s = DOTween.Sequence()
                .OnComplete(() =>
                {
                    sequences.RemoveFirst();
                    if (sequences.Count > 0)
                        sequences.First.Value.Play();
                });
            // if (sequences.Count != 0)
            //     s.Pause();
            sequences.AddLast(s);
            return s;
        }

        public static void SkipAnimations(this ViewContext view)
        {            
            var s = view.AnimationSequences.First;
            while (s is not null)
            {
                var t = s.Value;

                // Stopping the sequence will delete the first node,
                // which will set Next to null. (I have checked).
                s = s.Next;

                // It will not run the callback of the next sequence if it's empty,
                // unless it's killed first. We do have manual control here. (I have checked).
                var k = s?.Next;
                t.Complete(withCallbacks: true);

                // autokill is on
                // t.Kill();

                assert(k == s?.Next, "List node has been modified as a result of the complete call. "
                    + "This means the sequence complete callback got executed after all?");
            }
        }

        [Serializable]
        public struct VisualInfo
        {
            public Transform OuterObject;
            public Transform ModelTransform;
            public MeshRenderer MeshRenderer;
            public Vector3 Size;
            public Vector3 Center;
            public Matrix4x4 ModelTRSInParent;
            public readonly Vector3 GetTopOffset(Vector3 up) => -Center + Size.y / 2 * up;
            public readonly Vector3 GetTop(Vector3 up) => OuterObject.position + GetTopOffset(up);
            public readonly Vector3 GetTop() => GetTop(OuterObject.up);
            public readonly Vector3 GetBottomOffset(Vector3 up) => -Center - Size.y / 2 * up;
            public readonly Vector3 GetBottom(Vector3 up) => OuterObject.position + GetBottomOffset(up);
        }

        public static VisualInfo GetVisualInfo(this Transform outerObject)
        {
            var (modelTransform, model) = outerObject.GetObject(ObjectHierarchy.Model);
            var trs = modelTransform.GetLocalTRS();
            var bounds = model.localBounds;

            return new VisualInfo
            {
                OuterObject = outerObject,
                MeshRenderer = model,
                ModelTRSInParent = trs,
                // This only works for full rotations, like 90 degrees ones, otherwise the up and down kinda lose meaning.
                Size = trs.MultiplyVector(bounds.size),
                Center = trs.MultiplyPoint3x4(bounds.center),
            };
        }

        public static VisualInfo GetCellVisualInfo(this ViewContext view, int cellIndex)
        {
            var cell = view.GetCell(cellIndex);
            return GetVisualInfo(cell);
        }

        public static VisualInfo GetThingVisualInfo(this ViewContext view, int thingIndex)
        {
            var thing = view.GetThing(thingIndex);
            return GetVisualInfo(thing.transform);
        }
        
        public static Vector3 GetCellTopPosition(this ViewContext view, int cellIndex)
        {
            var things = view.Game.State.Cells[cellIndex];
            var cellInfo = view.GetCellVisualInfo(cellIndex);
            var up = cellInfo.OuterObject.up;
            var top = cellInfo.GetTop(up);

            foreach (var thingId in things)
            {
                var thingInfo = view.GetThingVisualInfo(thingId);
                top += thingInfo.Size.y * up;
            }
            return top;
        }

        public static Tween JumpAnimation(this ViewContext view, Transform t, Vector3 pos)
        {
            return t.DOJump(
                pos,
                duration: view.Visual.AnimationSpeed.Game,
                jumpPower: view.Visual.MovementJumpPower,
                numJumps: 1);
        }

        public static Tween MoveAnimation(this ViewContext view, Transform t, Vector3 pos)
        {
            return t.DOMove(pos, view.Visual.AnimationSpeed.Game);
        }

        public static Tween MoveAnimationAdapter(this ViewContext view, int thingId, Transform t, Vector3 pos)
        {
            return MoveAnimation(view, t, pos);
        }

        public delegate Tween GetThingAnimation(ViewContext view, int thingId, Transform t, Vector3 pos);

        public static void ArrangeThingsOnCell(
            this ViewContext view,
            int cellIndex,
            Sequence animationSequence,
            GetThingAnimation getAnimation)
        {
            var things = view.Game.State.Cells[cellIndex];
            var cellInfo = view.GetCellVisualInfo(cellIndex);
            var up = cellInfo.OuterObject.up;
            Vector3 currentPos = cellInfo.GetTop(up);

            var lastTime = animationSequence.Duration();

            foreach (var thingId in things)
            {
                var thingInfo = view.GetThingVisualInfo(thingId);
                var p = currentPos + thingInfo.GetTopOffset(up);
                var tween = getAnimation(view, thingId, thingInfo.OuterObject, p);
                {
                    var thingObject = thingInfo.OuterObject;
                    var cellObject = cellInfo.OuterObject;
                    tween.OnComplete(() => thingObject.parent = cellObject);
                }
                animationSequence.Insert(lastTime, tween);

                currentPos += thingInfo.Size.y * up;
            }
        }

        public static ref Transform GetThing(this ViewContext view, int id)
        {
            return ref view.UI.ThingGameObjects[id];
        }

        public static ref Transform GetCell(this ViewContext view, int cellIndex)
        {
            return ref view.UI.VisualCells[cellIndex];
        }
    }
}