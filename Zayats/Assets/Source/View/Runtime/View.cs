using UnityEngine;
using Zayats.Core;
using Kari.Plugins.AdvancedEnum;
using Zayats.Unity.View.Generated;
using static Zayats.Core.Assert;
using Zayats.Core.Generated;
using System.Linq;
using System;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using static Zayats.Core.Events;
using UnityEngine.Pool;
using System.Threading.Tasks;
using Common;
using static Zayats.Core.GameEvents;
using Kari.Plugins.Forward;
using DG.Tweening;

namespace Zayats.Unity.View
{
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

    public static class ViewLogic
    {
        public static void SetItemsForPlayer(this ViewContext view, int playerIndex)
        {
            view.UI.ItemContainers.ChangeItems(
                view.Game.State.CurrentPlayer.Items.Select(
                    id => view.UI.ThingGameObjects[id].transform),
                view.UI.ParentForOldItems,
                view.LastAnimationSequence,
                view.Visual.AnimationSpeed.UI);
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

        public static ViewContext CreateView(this SetupConfiguration config)
        {
            var view = new ViewContext()
            {
                AnimationSequences = new(),
                SetupConfiguration = config,
                State = new(),
                UI = new()
                {
                    Static = config.UI,
                },
                Events = ViewEvents.CreateStorage(),
            };

            view.UI.ItemBuyButtons = new List<GameObject>();
            view.UI.ItemContainers = new ItemContainers(view, config.UI.ItemHolderPrefab, config.UI.ItemScrollRect.viewport); 

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

    [Serializable]
    public struct GameConfiguration
    {
        public ThingArray<GameObject> PrefabsToSpawn;
        public ThingArray<int> CountsToSpawn;
        public ThingArray<int> ItemCosts;
        public Color[] PlayerCharacterColors;
        public int[] ShopPositions;
    }

    [GenerateArrayWrapper("AnimationArray")]
    public enum AnimationKind
    {
        UI,
        Game,
    }

    [Serializable]
    public struct VisualConfiguration
    {
        // [Range(0.0f, 2.0f)]
        public AnimationArray<float> AnimationSpeed;

        [Range(0.0f, 2.0f)]
        public float ToastTimeout;
    }

    [Serializable]
    public class SetupConfiguration
    {
        public UIReferences UI;
        public VisualConfiguration Visual;
        public GameConfiguration Game;
    }

    public class UnityRandom : IRandom
    {
        private UnityEngine.Random.State _randomState;

        public UnityRandom(UnityEngine.Random.State state)
        {
            _randomState = state;
        }

        private int GetIntInternal(int lower, int upperInclusive)
        {
            return Mathf.FloorToInt(UnityEngine.Random.Range(lower, upperInclusive + 1));
        }

        public int GetInt(int lower, int upperInclusive)
        {
            UnityEngine.Random.state = _randomState;
            var t = GetIntInternal(lower, upperInclusive);
            _randomState = UnityEngine.Random.state;
            return t;
        }
    }

    public class UnityLogger : Core.ILogger
    {
        public void Debug(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        public void Debug(string format, object value)
        {
            UnityEngine.Debug.LogFormat(format, value);
        }
    }

    public class View : MonoBehaviour
    {
        [SerializeField] private SetupConfiguration _config;
        public ViewContext _view;
        private GameContext Game => _view.Game;
        private ref UIContext UI => ref _view.UI;

        private int _seed;
        private int Seed
        {
            get => _seed;
            set
            {
                _seed = value;
                UI.GameplayText.Seed.text = value.ToString();
            }
        }

        private List<UnityEngine.Object> _toDestroy;

        public void OnValidate()
        {
            ref var gameConfig = ref _config.Game;

            if (gameConfig.PlayerCharacterColors is null
                || gameConfig.PlayerCharacterColors.Length < gameConfig.CountsToSpawn.Player)
            {
                Array.Resize(ref gameConfig.PlayerCharacterColors, gameConfig.CountsToSpawn.Player);
            }
            gameConfig.CountsToSpawn.RespawnPoint = gameConfig.CountsToSpawn.Tower;
        }
        

        private GameContext InitializeGame()
        {
            ref var gameConfig = ref _config.Game;
            ref var uiConfig = ref _config.UI;
            var countsToSpawn = gameConfig.CountsToSpawn;

            _view.Game = Initialization.CreateGame(cellCountNotIncludingStartAndFinish: uiConfig.VisualCells.Length - 2, countsToSpawn.Player);

            int seed = Seed;
            UnityEngine.Random.InitState(seed);
            var gameRandom = new UnityRandom(UnityEngine.Random.state);
            Game.Random = gameRandom;

            Game.Logger = new UnityLogger();

            Game.State.Shop.CellsWhereAccessible = gameConfig.ShopPositions.ToArray();

            UnityEngine.Random.InitState(seed + 1);
            var spawnRandom = new UnityRandom(UnityEngine.Random.state);
            
            UI.ThingGameObjects = new GameObject[countsToSpawn.Array.Sum()];

            int mineCount = countsToSpawn.EternalMine + countsToSpawn.RegularMine;
            var costStorage = Components.InitializeStorage(Game, Components.CurrencyCostId, mineCount);
            var playerStorage = Components.InitializeStorage(Game, Components.PlayerId, countsToSpawn.Player);
            var coinStorage = Components.InitializeStorage(Game, Components.CurrencyId, countsToSpawn.Coin);
            Components.InitializeStorage(Game, Components.ThingSpecificEventsId);
            var respawnPointIdsStorage = Components.InitializeStorage(Game, Components.RespawnPointIdsId, countsToSpawn.Player);
            var respawnPositionStorage = Components.InitializeStorage(Game, Components.RespawnPositionId, countsToSpawn.RespawnPoint);
            var pickupStorage = Components.InitializeStorage(Game, Components.PickupId);
            var pickupDelegateStorage = Components.InitializeStorage(Game, Components.AttachedPickupDelegateId);
            var respawnPointIdStorage = Components.InitializeStorage(Game, Components.RespawnPointIdId, countsToSpawn.Tower);
            var flagsStorage = Components.InitializeStorage(Game, Components.FlagsId, mineCount);
            var activatedItemStorage = Components.InitializeStorage(Game, Components.ActivatedItemId);

            assertNoneNull(Game.State.ComponentsByType);

            var regularMinePickup = MinePickup.Regular;
            var eternalMinePickup = MinePickup.Eternal;

            var rabbitPickup = new AddStatPickup(Stats.RollAdditiveBonus, 1);
            var horsePickup = new AddStatPickup(Stats.JumpAfterMoveCapacity, 1);


            void ArrangeThings(int position, Sequence animationSequence)
            {
                var things = _view.Game.State.Cells[position];
                var cellInfo = _view.GetCellVisualInfo(position);
                Vector3 currentPos = cellInfo.OuterObject.position + cellInfo.Normal * cellInfo.Size.y / 2;

                for (int i = 0; i < things.Count; i++)
                {
                    var thing = _view.GetThingVisualInfo(things[i]);
                    var tween = thing.OuterObject.DOMove(currentPos, _view.Visual.AnimationSpeed.Game);
                    animationSequence.Join(tween);

                    currentPos += thing.Size.y * cellInfo.Normal;
                }
            }

            void SpawnOn(int position, int id, GameObject obj)
            {
                var things = Game.State.Cells[position];
                things.Add(id);
                obj.transform.parent = UI.VisualCells[position];
            }

            int SpawnRandomly(IRandom random, int id, GameObject obj)
            {
                int randomPos = spawnRandom.GetUnoccupiedCellIndex(Game);
                if (randomPos != -1)
                    SpawnOn(randomPos, id, obj);
                return randomPos;
            }

            int currentId = 0;
            for (int kindIndex = 0; kindIndex < countsToSpawn.Length; kindIndex++)
            for (int instanceIndex = 0; instanceIndex < countsToSpawn[kindIndex]; instanceIndex++)
            {
                ref int id = ref currentId;
                var obj = GameObject.Instantiate(gameConfig.PrefabsToSpawn[kindIndex]);
                UI.ThingGameObjects[id] = obj;

                {
                    var c = gameConfig.ItemCosts[kindIndex];
                    if (c > 0)
                        costStorage.Add(id).Value = c;
                }

                switch ((ThingKind) kindIndex)
                {
                    default: panic($"Unhandled thing kind {(ThingKind) kindIndex}"); break;

                    case ThingKind.Player:
                    {
                        Game.InitializePlayer(index: instanceIndex, thingId: id, playerStorage);
                        respawnPointIdsStorage.Add(id).Value = new Stack<int>();
                        
                        {
                            var stats = Game.State.Players[instanceIndex].Stats; 
                            stats.Set(Stats.RollAdditiveBonus, 0);
                            stats.Set(Stats.JumpAfterMoveCapacity, 0);
                        }

                        var (_, renderer) = obj.transform.GetObject(ObjectHierarchy.Model);
                        var material = renderer.material;
                        // Instance materials need to be cleaned up.
                        // TODO: bring in the material search script.
                        _toDestroy.Add(material);
                        material.color = gameConfig.PlayerCharacterColors[instanceIndex];

                        SpawnOn(position: 0, id, obj);
                        break;
                    }
                    case ThingKind.EternalMine:
                    {
                        pickupStorage.Add(id).Value = eternalMinePickup;
                        flagsStorage.Add(id).Value = ThingFlags.Solid;
                        SpawnRandomly(spawnRandom, id, obj);
                        break;
                    }
                    case ThingKind.RegularMine:
                    {
                        pickupStorage.Add(id).Value = regularMinePickup;
                        flagsStorage.Add(id).Value = ThingFlags.Solid;
                        SpawnRandomly(spawnRandom, id, obj);
                        break;
                    }
                    case ThingKind.Coin:
                    {
                        coinStorage.Add(id).Value = 1;
                        pickupStorage.Add(id).Value = PlayerInventoryPickup.Instance;
                        SpawnRandomly(spawnRandom, id, obj);
                        break;
                    }
                    case ThingKind.RespawnPoint:
                    {
                        int position = SpawnRandomly(spawnRandom, id, obj);
                        respawnPositionStorage.Add(id).Value = position;
                        break;
                    }
                    case ThingKind.Totem:
                    {
                        var pickup = TotemPickup.Instance;
                        pickupStorage.Add(id).Value = pickup;
                        pickupDelegateStorage.Add(id);
                        SpawnRandomly(spawnRandom, id, obj);
                        break;
                    }
                    case ThingKind.Rabbit:
                    {
                        pickupStorage.Add(id).Value = rabbitPickup;
                        // SpawnRandomly(spawnRandom, id, obj);
                        Game.AddThingToShop(id);
                        break;
                    }
                    case ThingKind.Tower:
                    {
                        pickupStorage.Add(id).Value = TowerPickup.Instance;
                        respawnPointIdStorage.Add(id);
                        break;
                    }
                    case ThingKind.Horse:
                    {
                        pickupStorage.Add(id).Value = horsePickup;
                        SpawnRandomly(spawnRandom, id, obj);
                        break;
                    }
                    case ThingKind.Snake:
                    {
                        activatedItemStorage.Add(id).Value = new()
                        {
                            Filter = NearbyOtherPlayersFilter.Instance,
                            Action = new KillPlayersAction(Reasons.PoisonId),
                            Count = 1,
                            InitialUses = short.MaxValue,
                            UsesLeft = short.MaxValue,
                        };
                        pickupStorage.Add(id).Value = PlayerInventoryPickup.Instance;
                        Game.AddThingToShop(id);
                        break;
                    }
                }
                id++;
            }

            // Associate respawn points with the items.
            {
                int[] idsOfPositions = respawnPositionStorage.MapThingIdToIndex.Keys.ToArray();
                int[] idsOfPointStorage = respawnPointIdStorage.MapThingIdToIndex.Keys.ToArray();
                assert(idsOfPositions.Length >= idsOfPointStorage.Length, "why and also how?");
                for (int i = 0; i < idsOfPointStorage.Length; i++)
                {
                    int refereeId = idsOfPointStorage[i];
                    respawnPointIdStorage.GetProxy(refereeId).Value = idsOfPositions[i];
                    SpawnOn(respawnPositionStorage.GetProxy(idsOfPositions[i]).Value, refereeId, UI.ThingGameObjects[refereeId]);
                }
            }

            var s = _view.BeginAnimationEpoch();
            for (int cellIndex = 0; cellIndex < UI.VisualCells.Length; cellIndex++)
                ArrangeThings(cellIndex, s);

            Game.GetEventProxy(OnPositionChanged).Add(() => _view.BeginAnimationEpoch());

            Game.GetEventProxy(OnPositionChanged).Add(
                (GameContext game, ref PlayerPositionChangedContext context) =>
                {
                    var player = game.State.Players[context.PlayerIndex];

                    var targetTransform = UI.VisualCells[player.Position];
                    var playerTransform = UI.ThingGameObjects[player.ThingId].transform;
                    playerTransform.parent = targetTransform;

                    int position = player.Position;
                    int count = game.State.Cells[position].Count;
                    Debug.LogFormat("There are {0} things at {1}", count, position);
                    
                    var s = _view.LastAnimationSequence;
                    ArrangeThings(context.InitialPosition, s);
                    ArrangeThings(position, s);
                });

            Game.GetEventProxy(OnPlayerWon).Add(
                (GameContext game, int playerId) =>
                {
                    var win = UI.GameplayText.Win;
                    win.gameObject.SetActive(true);
                    win.text = $"{playerId} wins.";
                    win.alpha = 0.0f;
                    var fade = win.DOFade(255.0f, _view.Visual.AnimationSpeed.UI);

                    _view.LastAnimationSequence.Join(fade);
                });

            Game.GetEventProxy(GameEvents.OnItemAddedToInventory).Add(
                (GameContext game, ref ItemInterationContext context) =>
                {
                    _view.SetItemsForPlayer(game.State.CurrentPlayerIndex);
                });

            Game.GetEventProxy(GameEvents.OnItemRemovedFromInventory).Add(
                (GameContext game, ref ItemRemovedContext context) =>
                {
                    UI.ItemContainers.RemoveItemAt(context.ItemIndex);
                });

            Game.GetEventProxy(GameEvents.OnNextTurn).Add(
                (GameContext game, ref NextTurnContext context) =>
                {
                    _view.SetItemsForPlayer(context.CurrentPlayerIndex);

                    {
                        var currencyStorage = game.GetComponentStorage(Components.CurrencyId);
                        int totalAmount = 0;
                        foreach (var currency in game.GetDataInItems(Components.CurrencyId, context.CurrentPlayerIndex))
                        {
                            // The component contains the amount that the coin represents.
                            totalAmount += currency.Value;
                        }
                        var text = totalAmount.ToString();
                        _view.LastAnimationSequence.AppendCallback(
                            () => UI.GameplayText.CoinCounter.text = text);
                    }
                });

            Game.GetEventProxy(GameEvents.OnCellContentChanged).Add(
                (GameContext game, ref CellContentChangedContext context) =>
                {
                    // This is ok, because it will be eventually animated.
                    ArrangeThings(context.CellPosition, _view.LastAnimationSequence);
                });

            Game.GetEventProxy(GameEvents.OnAmountRolled).Add(
                (GameContext game, ref AmountRolledContext context) =>
                {
                    string val = context.RolledAmount.ToString();
                    if (context.BonusAmount > 0)
                        val += " (+" + context.BonusAmount.ToString() + ")";
                        
                    var s = _view.LastAnimationSequence;
                    // Placeholder rotation animation
                    // s.AppendInterval(_view.Visual.AnimationSpeed.UI);
                    s.AppendCallback(() => UI.GameplayText.RollValue.text = val);
                });

            _view.GetEventProxy(ViewEvents.OnItemInteractionStarted).Add(
                (ViewContext view, ref ActivatedItemHandling itemH) =>
                {
                    // view.BeginAnimationEpoch();
                    // view.LastAnimationSequence.Join(t);

                    var scrollRect = view.UI.ItemScrollRect;
                    var targetPos = scrollRect.GetContentLocalPositionToScrollChildIntoView(itemH.Index);
                    var t = scrollRect.content.DOLocalMove(targetPos, view.Visual.AnimationSpeed.UI);
                });

            _view.GetEventProxy(ViewEvents.OnItemInteractionCancelled).Add(
                (ViewContext view, ref ActivatedItemHandling itemH) =>
                {
                    Debug.Log("Cancelled");
                });

            UI.GameplayText.Win.gameObject.SetActive(false);
            UI.GameplayText.CoinCounter.text = "0";
            _view.SetItemsForPlayer(0);

            // _game.GetEventProxy(GameEvents.OnItemRemovedFromInventory).Add(
            //     (GameContext game, ref ItemInteractionInfo context) =>
            //     {
            //         var holder = _itemHolders[context.PlayerIndex];
            //         _things[context.ThingId].transform.SetParent(holder, worldPositionStays: false);
            //     });

            return Game;
        }

        void Start()
        {
            DOTween.Init(
                recycleAllByDefault: true,
                useSafeMode: false,
                logBehaviour: LogBehaviour.Verbose);
            _view = ViewLogic.CreateView(_config);
            _toDestroy = new();

            const int initialSeed = 5;
            Seed = initialSeed;

            UI.GameplayText.Win.gameObject.SetActive(false);

            InitializeGame();

            var buttons = UI.GameplayButtons;
            buttons.Roll.onClick.AddListener(() =>
            {
                _view.BeginAnimationEpoch();
                Game.ExecuteCurrentPlayersTurn();
            });

            buttons.Settings.onClick.AddListener(() =>
            {
                Debug.Log("Open Settings");
            });

            buttons.Restart.onClick.AddListener(() =>
            {
                {
                    var s = _view.AnimationSequences.First;
                    while (s is not null)
                    {
                        var t = s.Value;

                        // Stopping the sequence will delete the first node,
                        // which will set Next to null. (I checked).
                        s = s.Next;

                        // It will not run the callback of the next sequence if it's empty,
                        // unless it's killed first. We do have manual control here. (I checked).
                        t.Kill(complete: true);
                    }
                }
                foreach (var thing in UI.ThingGameObjects)
                    Destroy(thing);
                Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                InitializeGame();
            });

            buttons.TempBuy.onClick.AddListener(async () =>
            {
                async Task DoBuying()
                {
                    _view.BeginAnimationEpoch();
                    assert(Game.State.Shop.Items.Count > 0);
                    
                    int itemIndex = 0;
                    var context = Game.StartBuyingThingFromShop(new()
                    {
                        PlayerIndex = Game.State.CurrentPlayerIndex,
                        ThingShopIndex = itemIndex,
                    });
                    
                    if (context.NotEnoughCoins)
                    {
                        Debug.Log("Not enough money");
                        return;
                    }

                    var cellsSlice = Game.State.IntermediateCells;
                    int availableCellCount = cellsSlice.Count(c => c.Count == 0);
                    List<int> positions = new();
                    if (availableCellCount == context.Coins.Count)
                    {
                        cellsSlice.Indices(c => c.Count == 0, positions);
                    }
                    else if (availableCellCount < context.Coins.Count)
                    {
                        panic("Unimplemented");
                    }
                    else if (availableCellCount == 0)
                    {
                        panic("Unimplemented");
                    }
                    else
                    {
                        int coinCount = context.Coins.Count;
                        Task<int> PromptForCellPlacement(GameContext game)
                        {
                            return Task.FromResult(game.Random.GetUnoccupiedCellIndex(game));
                        }
                        for (int i = 0; i < coinCount; i++)
                        {
                            int result = await PromptForCellPlacement(Game);
                            if (result == -1)
                                panic("Unimplemented");
                            positions.Add(result);
                        }
                    }

                    _view.BeginAnimationEpoch();
                    Game.EndBuyingThingFromShop(context, positions);
                }
                await DoBuying();
            });
        }
    
        void OnDestroy()
        {
            foreach (var obj in _toDestroy)
                Destroy(obj);
        }
    }
}