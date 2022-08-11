using Zayats.Core;
using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Zayats.Unity.View.Generated;
using TMPro;
using UnityEngine.UI;
using Kari.Plugins.AdvancedEnum;
using System.Linq;
using static Zayats.Core.Assert;
using Common.Unity;
using Common;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
    using UnityEditor;
#endif

namespace Zayats.Unity.View
{
    [Serializable]
    public class ViewContext : IGetEvents
    {
        public ViewState State;
        public GameContext Game;
        public SetupConfiguration SetupConfiguration;
        public ref VisualConfiguration Visual => ref SetupConfiguration.Visual; 
        public UIContext UI;

        public LinkedList<Sequence> AnimationSequences;
        public Sequence LastAnimationSequence => AnimationSequences.Last.Value;

        public Events.Storage Events { get; set; }
    }

    [System.Serializable]
    public struct VisualCell
    {
        public Transform Transform;
    }

    [GenerateArrayWrapper("GameplayButtonArray")]
    public enum GameplayButtonKind
    {
        Roll,
        Settings,
        Restart,
        TempBuy,
    }

    [GenerateArrayWrapper("GameplayTextArray")]
    public enum GameplayTextKind
    {
        Win,
        Seed,
        CoinCounter,
        RollValue,
    }

    [Serializable]
    public struct ActivatedItemHandling
    {
        public readonly bool InProgress => ThingId != -1;
        public int Index;
        public int ThingId;
        public Components.ActivatedItem ActivatedItem;
    }

    [Serializable]
    public struct ForcedItemDropHandling
    {
        public readonly bool InProgress => false;
    }

    [Serializable]
    public struct SelectionState
    {
        public readonly bool InProgress => TargetKind != TargetKind.None;
        public TargetKind TargetKind;
        public List<int> ValidTargets;
        public List<int> TargetIndices;
    }

    [Serializable]
    public struct ViewState
    {
        public ActivatedItemHandling ItemHandling;
        public ForcedItemDropHandling ForcedItemDropHandling;
        public BatchedMaterial HighlightMaterial;
        public SelectionState Selection;
        // public List<GameObject> HighlightedUIObjects;
    }

    [Serializable]
    public struct DynamicUI
    {
        public GameObject[] ThingGameObjects;
        public List<GameObject> ItemBuyButtons;
        public ItemContainers ItemContainers;
    }

    [Serializable]
    public struct ItemScrollUIReferences
    {
        public ScrollRect ScrollRect;
        public Transform ParentForOldItems;
        public UIHolderInfo HolderPrefab;
        public Transform ContainerTransform;
    }

    [Serializable]
    public struct UIReferences
    {
        public Transform[] VisualCells;
        public GameplayButtonArray<Button> GameplayButtons;
        public GameplayTextArray<TMP_Text> GameplayText;
        public GameObject BuyButtonPrefab;
        public GameObject ScreenOverlayObject;
        public ItemScrollUIReferences ItemScrollUI;
    }

    [Serializable]
    public partial struct UIContext
    {
        public DynamicUI Dynamic;
        public UIReferences Static;

        // Since the forward plugin doesn't work with unity references yet, and with generated code,
        // I'm doing this manually here.
        public ItemContainers ItemContainers { readonly get => Dynamic.ItemContainers; set => Dynamic.ItemContainers = value; }
        public GameObject[] ThingGameObjects { readonly get => Dynamic.ThingGameObjects; set => Dynamic.ThingGameObjects = value; }
        public List<GameObject> ItemBuyButtons { readonly get => Dynamic.ItemBuyButtons; set => Dynamic.ItemBuyButtons = value; }
        public readonly Transform[] VisualCells { get => Static.VisualCells; }
        public readonly GameplayButtonArray<Button> GameplayButtons { get => Static.GameplayButtons; }
        public readonly GameplayTextArray<TMP_Text> GameplayText { get => Static.GameplayText; }
        public readonly GameObject BuyButtonPrefab { get => Static.BuyButtonPrefab; }
        public readonly GameObject ScreenOverlayObject { get => Static.ScreenOverlayObject; }
    }

    public static class ViewLogic
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
            view.UI.ItemContainers.ChangeItems(
                view.Game.State.Players[playerIndex].Items.Select(
                    id => view.UI.ThingGameObjects[id].transform),
                view.LastAnimationSequence,
                animationSpeed: 0);
            view.ResetUsabilityColors(playerIndex);
        }

        public static void ResetUsabilityColors(this ViewContext view, int playerIndex)
        {
            var colors = view.GetItemUsabilityColors(playerIndex).ToArray();
            view.UI.ItemContainers.ResetUsabilityColors(colors, view.LastAnimationSequence);
        }

        public static void DisplayTip(this ViewContext context, string text)
        {
            // TODO
            Debug.Log(text);
        }

        public static void HighlightObjects(this ViewContext view, IEnumerable<Transform> objs)
        {
            ref var highlightMaterial = ref view.State.HighlightMaterial;
            var materialPaths = objs.SelectMany(c => c.GetObject(ObjectHierarchy.ModelInfo).Value.MaterialPaths).ToArray();
            var propertyNames = BatchedMaterial.DefaultPropertyNames;
            
            highlightMaterial.Reset(materialPaths, propertyNames);

            var intensity = view.Visual.HighlightEmissionIntensity;
            highlightMaterial.EmissionColor = new Color(intensity, intensity, intensity, 1);
            highlightMaterial.Apply();
        }

        public static void CancelHighlighting(this ViewContext view)
        {
            view.State.HighlightMaterial.Reset();
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

        public static void HighlightObjectsOfItemInteraction(this ViewContext view, in SelectionState selection)
        {
            view.HighlightObjects(
                view.GetObjectsValidForSelection(selection.TargetKind, selection.ValidTargets));
        }

        public static void ChangeLayerOnTargets(IEnumerable<Transform> targets, int layer)
        {
            foreach (var t in targets)
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

            {
                var selected = selection.TargetIndices;
                if (!selected.Contains(targetIndex))
                {
                    selected.Add(targetIndex);

                    view.HandleEvent(ViewEvents.OnSelectionProgress, ref selection);
                }
            }

            view.MaybeConfirmItemUse();
        }

        public static bool MaybeConfirmItemUse(this ViewContext view)
        {
            ref var itemH = ref view.State.ItemHandling;
            if (view.State.Selection.TargetIndices.Count != itemH.ActivatedItem.RequiredTargetCount)
                return false;

            ConfirmItemUse(view);
            return true;
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
            view.HandleEvent(ViewEvents.OnItemInteractionFinalized, ref context);
            view.HandleEvent(ViewEvents.OnItemInteractionCancelledOrFinalized, ref context);
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

        public static void CancelHandlingCurrentItemInteraction(this ViewContext view)
        {
            ref var itemH = ref view.State.ItemHandling;
            assert(itemH.InProgress);
            
            var context = new ViewEvents.ItemHandlingContext
            {
                Item = itemH,
                Selection = view.State.Selection,
            };
            view.HandleEvent(ViewEvents.OnItemInteractionCancelled, ref context);
            view.HandleEvent(ViewEvents.OnItemInteractionCancelledOrFinalized, ref context);
        }

        public static Sequence MaybeBeginAnimationEpoch(this ViewContext view)
        {
            var sequences = view.AnimationSequences;
            if (sequences.Count == 0)
                return view.BeginAnimationEpoch();
            return sequences.Last.Value;
        }

        public static Sequence BeginAnimationEpoch(this ViewContext view)
        {
            var sequences = view.AnimationSequences;
            var s = DOTween.Sequence()
                .OnComplete(() =>
                {
                    sequences.RemoveFirst();
                    if (sequences.Count > 0)
                        sequences.First.Value.Play();
                });
            if (sequences.Count != 0)
                s.Pause();
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

                assert(k == s?.Next);
            }
        }

        [Serializable]
        public struct VisualInfo
        {
            public Transform OuterObject;
            public MeshRenderer MeshRenderer;
            public Vector3 Size;
            public Vector3 Center;
            public Vector3 Normal;
            public readonly Vector3 GetTopOffset() => -Center + Size.y / 2 * Normal;
            public readonly Vector3 GetTop() => OuterObject.position + GetTopOffset();
        }

        public static VisualInfo GetInfo(Transform outerObject)
        {
            var (modelTransform, model) = outerObject.GetObject(ObjectHierarchy.Model);
            var bounds = model.localBounds;
            var normal = outerObject.up;

            var trs = modelTransform.GetLocalTRS();

            return new VisualInfo
            {
                OuterObject = outerObject,
                MeshRenderer = model,
                Size = Vector3.Scale(bounds.size, modelTransform.localScale),
                Center = trs.MultiplyPoint3x4(bounds.center),
                Normal = normal,
            };
        }

        public static VisualInfo GetCellVisualInfo(this ViewContext context, int cellIndex)
        {
            var cell = context.UI.VisualCells[cellIndex];
            return GetInfo(cell);
        }

        public static VisualInfo GetThingVisualInfo(this ViewContext context, int thingIndex)
        {
            var thing = context.UI.ThingGameObjects[thingIndex];
            return GetInfo(thing.transform);
        }

        public static ViewContext CreateView(SetupConfiguration config, UIReferences ui)
        {
            var view = new ViewContext()
            {
                AnimationSequences = new(),
                SetupConfiguration = config,
                State = new(),
                UI = new()
                {
                    Static = ui,
                },
                Events = ViewEvents.CreateStorage(),
            };

            view.UI.ItemBuyButtons = new List<GameObject>();
            view.UI.ItemContainers = new ItemContainers(view, ui.ItemScrollUI);
            view.State.HighlightMaterial = new BatchedMaterial();
            view.State.Selection.ValidTargets = new List<int>();
            view.State.Selection.TargetIndices = new List<int>();

            return view;
        }

        // meh
        public static GameObject InstantiateThing(this ViewContext view, ThingCreationProxy create, ThingKind thingKind)
        {
            var obj = GameObject.Instantiate(view.SetupConfiguration.Game.PrefabsToSpawn[(int) thingKind]);
            view.UI.ThingGameObjects[create.Id] = obj;

            {
                var c = view.SetupConfiguration.Game.ItemCosts[(int) thingKind];
                if (c > 0)
                    create.AddComponent(Components.CurrencyCostId) = c;
            }

            {
                obj.name = create.Id + "_" + (thingKind).ToString() + "_" + create.Id;
            }

            return obj;
        }

        public static void ArrangeThingsOnCell(
            this ViewContext view,
            int cellIndex,
            Sequence animationSequence,
            float animationSpeed)
        {
            var things = view.Game.State.Cells[cellIndex];
            var cellInfo = view.GetCellVisualInfo(cellIndex);
            Vector3 currentPos = cellInfo.GetTop();

            var lastTime = animationSequence.Duration();

            foreach (var thingId in things)
            {
                var thingInfo = view.GetThingVisualInfo(thingId);
                var p = currentPos - thingInfo.Center + thingInfo.Size.y / 2 * cellInfo.Normal;
                var tween = thingInfo.OuterObject.DOMove(p, animationSpeed);

                var thingObject = thingInfo.OuterObject;
                var cellObject = cellInfo.OuterObject;
                tween.OnComplete(() => thingObject.parent = cellObject);

                animationSequence.Insert(lastTime, tween);

                currentPos += thingInfo.Size.y * cellInfo.Normal;
            }
        }
    }
    

    public static class ObjectHierarchy
    {
        public static (Transform Transform, T Value) GetObject<T>(this Transform transform, TypedIdentifier<T> id)
        {
            var t = transform.GetChild(id.Id);
            return (t, t.GetComponent<T>());
        }

        #if UNITY_EDITOR
            public static void RestoreHierarchy(this Transform transform)
            {
                while (transform.childCount < ChildCount)
                {
                    var g = new GameObject(_Names[transform.childCount]);
                    Undo.RegisterCreatedObjectUndo(g, _Names[transform.childCount]);
                    g.transform.parent = transform;
                }
            }
        #endif

        private static readonly string[] _Names = { "model", "collider", }; 
        public static readonly TypedIdentifier<MeshRenderer> Model = new(0);
        public static readonly TypedIdentifier<ModelInfo> ModelInfo = new(0);
        public static readonly TypedIdentifier<Collider> Collider = new(1);
        public const int ChildCount = 2;
    }
    
    public static partial class ViewEvents
    {
        public static Events.Storage CreateStorage() => new(Count);

        public struct ItemHandlingContext
        {
            public ActivatedItemHandling Item;
            public SelectionState Selection;
        }
        public static readonly TypedIdentifier<ItemHandlingContext> OnItemInteractionStarted = new(0);
        // public static readonly TypedIdentifier<ItemHandlingContext> OnItemInteractionProgress = new(1);
        public static readonly TypedIdentifier<ItemHandlingContext> OnItemInteractionCancelled = new(2);
        public static readonly TypedIdentifier<ItemHandlingContext> OnItemInteractionFinalized = new(3);
        public static readonly TypedIdentifier<ItemHandlingContext> OnItemInteractionCancelledOrFinalized = new(4);


        public struct PointerEvent : Events.IContinue
        {
            public void Consume() => Continue = false;
            public bool Continue { get; set; }
            public PointerEventData Data;
        }
        public static readonly TypedIdentifier<PointerEvent> OnPointerClick = new(5);
        public static readonly TypedIdentifier<SelectionState> OnSelectionProgress = new(6);

        public const int Count = 7;
    }
}