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

    public struct ActivatedItemHandling
    {
        public readonly bool InProgress => Progress != 0;
        public int Progress;
        public int Index;
        public int ThingId;
        public Components.ActivatedItem ActivatedItem; 
    }

    public struct ViewState
    {
        public ActivatedItemHandling ItemHandling;
        // public List<GameObject> HighlightedGameObjects;
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
    public struct UIReferences
    {
        public Transform[] VisualCells;
        public GameplayButtonArray<Button> GameplayButtons;
        public GameplayTextArray<TMP_Text> GameplayText;
        public ScrollRect ItemScrollRect;
        public GameObject BuyButtonPrefab;
        public UIHolderInfo ItemHolderPrefab;
        public Transform ParentForOldItems;
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
        public readonly ScrollRect ItemScrollRect { get => Static.ItemScrollRect; }
        public readonly GameObject BuyButtonPrefab { get => Static.BuyButtonPrefab; }
        public readonly Transform ParentForOldItems { get => Static.ParentForOldItems; }
    }

    [GenerateArrayWrapper]
    public enum ItemUsability
    {
        None,
        NotEnoughSpots,
        Usable,
    }

    public static class ViewLogic
    {
        public static void SetItemsForPlayer(this ViewContext view, int playerIndex)
        {
            view.UI.ItemContainers.ChangeItems(
                view.Game.State.Players[playerIndex].Items.Select(id =>
                {
                    var t = view.UI.ThingGameObjects[id].transform;

                    ItemUsability usability;
                    if (!view.Game.TryGetComponent(Components.ActivatedItemId, id, out var proxy)
                        || proxy.Value.Action is null)
                    {
                        usability = ItemUsability.None;
                    }
                    else if (proxy.Value.Filter.GetValid(view.Game, new()
                        {
                            PlayerIndex = playerIndex,
                            Position = view.Game.State.Players[playerIndex].Position,
                            ThingId = id,
                        }).Take(proxy.Value.Count).Count() < proxy.Value.Count)
                    {
                        usability = ItemUsability.NotEnoughSpots;
                    }
                    else
                    {
                        usability = ItemUsability.Usable;
                    }
                    
                    return (view.Visual.ItemUsabilityColors.Get(usability), t);
                }),
                view.UI.ParentForOldItems,
                view.LastAnimationSequence,
                animationSpeed: 0);
        }
        public static void DisplayTip(this ViewContext context, string text)
        {
            // TODO
        }

        public static void HighlightCells(this ViewContext context, IEnumerable<Transform> cells)
        {
            // TODO
        }

        public static bool TryStartHandlingItemInteraction(this ViewContext view, int itemIndex)
        {
            ref var itemH = ref view.State.ItemHandling;
            assert(itemH.Progress == 0);

            int thingItemId = view.Game.State.CurrentPlayer.Items[itemIndex];
            if (view.Game.TryGetComponent(Components.ActivatedItemId, thingItemId, out var activatedItemProxy))
            {
                ref var state = ref view.Game.State;
                var itemContext = new ItemInterationContext
                {
                    ItemId = state.CurrentPlayer.Items[itemIndex],
                    PlayerIndex = state.CurrentPlayerIndex,
                    Position = state.CurrentPlayer.Position,
                };

                var activatedItem = activatedItemProxy.Value;
                var filter = activatedItem.Filter;
                var targetKind = filter.Kind;

                if (targetKind == TargetKind.None)
                {
                    // Immediately activate the item.
                    view.Game.UseItem(new()
                    {
                        Interaction = itemContext,
                        Item = activatedItemProxy,
                        SelectedTargets = Array.Empty<int>(),
                    });
                }
                else
                {
                    var valid = filter.GetValid(view.Game, itemContext).ToArray();

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
                    if (valid.Length < activatedItem.Count)
                    {
                        view.DisplayTip($"Not enough {Subject()} (required {activatedItem.Count}, available {valid.Length}).");
                        return false;
                    }

                    switch (targetKind)
                    {
                        default: panic($"Unimplemented case: {targetKind}"); break;

                        case TargetKind.Cell:
                        {
                            // TODO: enumerate into a reusable buffer.
                            var cells = valid;
                            view.HighlightCells(cells.Select(c => view.UI.VisualCells[c]));
                            break;
                        }
                        case TargetKind.Player:
                        {
                            var players = valid;
                            break;
                        }
                        case TargetKind.Thing:
                        {
                            panic("Unimplemented");
                            break;
                        }
                    }
                    
                    itemH.ThingId = thingItemId;
                    itemH.Progress = 1;
                    itemH.Index = itemIndex;
                    itemH.ActivatedItem = activatedItem;

                    view.HandleEvent(ViewEvents.OnItemInteractionStarted, ref itemH);
                }
                
                return true;
            }

            return false;
        }

        public static void CancelHandlingCurrentItemInteraction(this ViewContext context)
        {
            ref var itemH = ref context.State.ItemHandling;
            assert(itemH.Progress != 0);
            context.HandleEvent(ViewEvents.OnItemInteractionCancelled, ref itemH);
            itemH.Progress = 0;
        }

        public static Sequence BeginAnimationEpoch(this ViewContext context)
        {
            var sequences = context.AnimationSequences;
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

        public static Bounds GetCellOrThingBounds(Transform thing)
        {
            assert(thing.localScale == Vector3.one);
            var (modelTransform, model) = thing.GetObject(ObjectHierarchy.Model);
            var meshFilter = model.GetComponent<MeshFilter>();
            var mesh = meshFilter.sharedMesh;
            var bounds = mesh.bounds;
            var localScale = modelTransform.localScale;
            var b = new Bounds(
                Vector3.Scale(bounds.center, localScale) + modelTransform.localPosition,
                Vector3.Scale(bounds.size, localScale));
            return b;
        }

        [Serializable]
        public struct VisualInfo
        {
            public Transform OuterObject;
            public MeshRenderer MeshRenderer;
            public Vector3 Size;
            public Vector3 Center;
            public Vector3 Normal;
            public readonly Vector3 GetTopOffset() => Center + Normal * Size.y / 2;
            public readonly Vector3 GetTop() => OuterObject.position + GetTopOffset();
        }

        public static VisualInfo GetInfo(Transform outerObject)
        {
            var (modelTransform, model) = outerObject.GetObject(ObjectHierarchy.Model);
            // var meshFilter = model.GetComponent<MeshFilter>();
            // var mesh = meshFilter.sharedMesh;
            // var bounds = mesh.bounds;

            // var scale = modelTransform.localScale;
            // Vector3 Invert(Vector3 a)
            // {
            //     Vector3 result;
            //     result.x = 1 / a.x;
            //     result.y = 1 / a.y;
            //     result.z = 1 / a.z;
            //     return result;
            // }

            var bounds = model.bounds;
            var normal = outerObject.up;

            return new VisualInfo
            {
                OuterObject = outerObject,
                MeshRenderer = model,
                Size = bounds.size,
                Center = bounds.center - modelTransform.position,
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
            view.UI.ItemContainers = new ItemContainers(view, ui.ItemHolderPrefab, ui.ItemScrollRect.content); 

            return view;
        }
    }
    

    public static class ObjectHierarchy
    {
        public static (Transform Transform, T Value) GetObject<T>(this Transform transform, TypedIdentifier<T> id)
        {
            var t = transform.GetChild(id.Id);
            return (t, t.GetComponent<T>());
        }

        public static readonly TypedIdentifier<MeshRenderer> Model = new(0);
    }
    
    public static partial class ViewEvents
    {
        public static Events.Storage CreateStorage() => new(Count);

        public static readonly TypedIdentifier<ActivatedItemHandling> OnItemInteractionStarted = new(0);
        public static readonly TypedIdentifier<ActivatedItemHandling> OnItemInteractionProgress = new(1);
        public static readonly TypedIdentifier<ActivatedItemHandling> OnItemInteractionCancelled = new(2);
        public const int Count = 3;
    }
}