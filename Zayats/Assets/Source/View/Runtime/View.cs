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
        public static void SetItemsForPlayer(this ViewContext context, int playerIndex)
        {
            context.UI.ItemContainers.ChangeItems(
                context.Game.State.CurrentPlayer.Items.Select(
                    id => context.UI.ThingGameObjects[id].GetComponent<MeshRenderer>()),
                context.UI.ParentForOldItems);
        }
        public static void DisplayTip(this ViewContext context, string text)
        {
            // TODO
        }

        public static void HighlightCells(this ViewContext context, IEnumerable<Transform> cells)
        {
            // TODO
        }

        public static bool TryStartHandlingItemInteraction(this ViewContext context, int itemIndex)
        {
            ref var itemH = ref context.State.ItemHandling;
            assert(itemH.Progress == 0);

            int thingItemId = context.Game.State.CurrentPlayer.Items[itemIndex];
            if (context.Game.TryGetComponentValue(Components.ActivatedItemId, thingItemId, out var activatedItem))
            {
                switch (activatedItem.Kind)
                {
                    case ActivatedItemKind.None:
                    {
                        break;
                    }
                    case ActivatedItemKind.SelectCell:
                    {
                        var cells = context.UI.VisualCells;
                        context.HighlightCells(cells);
                        break;
                    }
                    case ActivatedItemKind.SelectEmptyCell:
                    {
                        var emptyCells = context.UI.VisualCells
                            .Where((a, i) => context.Game.State.Cells[i].Count == 0)
                            // .Select((a, i) => (a, i))
                            .ToArray();

                        // Payload in this case means cell count
                        if (emptyCells.Length < activatedItem.Payload)
                        {
                            context.DisplayTip($"Not enough empty cells (required {activatedItem.Payload}).");
                            return false;
                        }
                        context.HighlightCells(emptyCells);
                        break;
                    }
                    case ActivatedItemKind.SelectPlayer:
                    {
                        break;
                    }
                    case ActivatedItemKind.SelectPlayerOtherThanSelf:
                    {
                        break;
                    }
                }
                
                itemH.ThingId = thingItemId;
                itemH.Progress = 1;
                itemH.Index = itemIndex;
                itemH.ActivatedItem = activatedItem;

                context.HandleEvent(ViewEvents.OnItemInteractionStarted, ref itemH);
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

    [Serializable]
    public struct VisualConfiguration
    {
        [Range(0.0f, 2.0f)]
        public float AnimationSpeed;
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
        private ViewContext _view;
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
            assertNoneNull(Game.State.ComponentsByType);

            var regularMinePickup = MinePickup.Regular;
            var eternalMinePickup = MinePickup.Eternal;

            var rabbitPickup = new AddStatPickup(Stats.RollAdditiveBonus, 1);
            var horsePickup = new AddStatPickup(Stats.JumpAfterMoveCapacity, 1);


            void ArrangeThings(int position)
            {
                var things = Game.State.Cells[position];
                var spriteRenderer = UI.VisualCells[position].gameObject.GetComponent<SpriteRenderer>();
                var bounds = spriteRenderer.bounds;
                float availableSpaceY = spriteRenderer.bounds.extents.y * 2;
                float offsetIncrement = -availableSpaceY / (things.Count + 1);
                float offsetStart = offsetIncrement + availableSpaceY / 2;

                for (int i = 0; i < things.Count; i++)
                {
                    var transform = UI.ThingGameObjects[things[i]].transform;
                    Vector3 cellPosition = UI.VisualCells[position].position;
                    
                    Vector3 thingPosition;
                    thingPosition.x = cellPosition.x;
                    thingPosition.y = cellPosition.y + offsetStart + offsetIncrement * i;
                    thingPosition.z = -(i + 1);

                    transform.position = thingPosition;
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

                        obj.GetComponent<SpriteRenderer>().color = gameConfig.PlayerCharacterColors[instanceIndex];

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

            for (int cellIndex = 0; cellIndex < UI.VisualCells.Length; cellIndex++)
                ArrangeThings(cellIndex);

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
                    
                    ArrangeThings(context.InitialPosition);
                    ArrangeThings(position);
                });

            Game.GetEventProxy(OnPlayerWon).Add(
                (GameContext game, ref int playerId) =>
                {
                    UI.GameplayText.Win.text = $"{playerId} wins.";
                    UI.GameplayText.Win.gameObject.SetActive(true);
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
                        UI.GameplayText.CoinCounter.text = totalAmount.ToString();
                    }
                });

            Game.GetEventProxy(GameEvents.OnCellContentChanged).Add(
                (GameContext game, ref CellContentChangedContext context) =>
                {
                    // This is ok, because it will be eventually animated.
                    ArrangeThings(context.CellPosition);
                });

            Game.GetEventProxy(GameEvents.OnAmountRolled).Add(
                (GameContext game, ref AmountRolledContext context) =>
                {
                    string val = context.RolledAmount.ToString();
                    if (context.BonusAmount > 0)
                        val += " (+" + context.BonusAmount.ToString() + ")";
                    UI.GameplayText.RollValue.text = val;
                });

            _view.GetEventProxy(ViewEvents.OnItemInteractionStarted).Add(
                (ViewContext view, ref ActivatedItemHandling itemH) =>
                {
                    var scrollRect = view.UI.ItemScrollRect;
                    var targetPos = scrollRect.GetContentLocalPositionToScrollChildIntoView(itemH.Index);
                    scrollRect.content.DOLocalMove(targetPos, view.Visual.AnimationSpeed);
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

        public void Start()
        {
            UI.ItemBuyButtons = new();

            const int initialSeed = 5;
            Seed = initialSeed;

            UI.GameplayText.Win.gameObject.SetActive(false);

            InitializeGame();

            var buttons = UI.GameplayButtons;
            buttons.Roll.onClick.AddListener(() =>
            {
                Game.ExecuteCurrentPlayersTurn();
            });

            buttons.Settings.onClick.AddListener(() =>
            {
                Debug.Log("Open Settings");
            });

            buttons.Restart.onClick.AddListener(() =>
            {
                foreach (var thing in UI.ThingGameObjects)
                    Destroy(thing);
                Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                InitializeGame();
            });

            buttons.TempBuy.onClick.AddListener(async () =>
            {
                async Task DoBuying()
                {
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

                    Game.EndBuyingThingFromShop(context, positions);
                }
                await DoBuying();
            });
        }
    }
}