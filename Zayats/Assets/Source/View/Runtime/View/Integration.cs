using Zayats.Core;
using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Zayats.Unity.View.Generated;
using TMPro;
using UnityEngine.UI;
using Kari.Plugins.AdvancedEnum;
using Common.Unity;
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
    public struct ForcedItemDropHandling
    {
        public readonly bool InProgress => false;
    }

    [Serializable]
    public struct ViewState
    {
        public ActivatedItemHandling ItemHandling;
        public ForcedItemDropHandling ForcedItemDropHandling;
        public BatchedMaterial HighlightMaterial;
        public SelectionState Selection;
        public int AnimationEpoch;
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

    public static partial class ViewLogic
    {
        public static void DisplayTip(this ViewContext context, string text)
        {
            // TODO
            Debug.Log(text);
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
                var c = view.SetupConfiguration.Game.ItemCosts.Get(thingKind);
                if (c > 0)
                    create.AddComponent(Components.CurrencyCostId) = c;
            }

            {
                obj.name = create.Id + "_" + (thingKind).ToString() + "_" + create.Id;
            }

            return obj;
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
        public static readonly TypedIdentifier<SelectionState> OnSelectionCancelledOrFinalized = new(7);

        public const int Count = 8;
    }
}