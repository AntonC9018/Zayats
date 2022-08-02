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
using UnityEngine.EventSystems;

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
        UseItem,
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

    public class UIHolderInfo : MonoBehaviour
    {
        public RectTransform OuterTransform => (RectTransform) transform;
        public GameObject OuterObject => gameObject;
        public RectTransform ItemFrameTransform;
        public GameObject ItemFrameObject => ItemFrameTransform.gameObject;
        public PointerEnterExit<MyHorizontalScrollRect> Handler;
        public Transform StoredItem => ItemFrameTransform.GetChild(0);
    }

    [Serializable]
    public struct ButtonOverlay
    {
        public GameObject OuterObject;
        public RectTransform OuterTransform => (RectTransform) OuterObject.transform;
        public Button Button;
    }

    public interface IItemActionHandler
    {
        bool IsOperationInProgress { get; }
        int ItemIndexInOperation { get; }
        void StartHandling(int itemIndex, IItemHandlingProgressHandler progressHandler);
    }

    public interface IItemHandlingProgressHandler
    {
        void OnProgress(int stage);
        void OnCancelled();
    }

    public class MyHorizontalScrollRect : 
        IPointerEnterIndex, IPointerExitIndex, IPointerClickHandler,
        IItemHandlingProgressHandler
    {
        private List<UIHolderInfo> _uiHolderInfos;
        private UIHolderInfo _prefab;
        private int _itemCount;
        private int _currentlyHoveredItem;
        private IItemActionHandler _itemActionHandler;
        private IItemHandlingProgressHandler _itemHandlingProgressHandler;

        // private GameObject _buttonOverlay;
        // private Action<int> _overlayButtonClickedAction;

        public void Initialize(UIHolderInfo holderPrefab
                // , ButtonOverlay buttonOverlay, Action<int> overlayButtonClickedAction
        )
        {
            _prefab = holderPrefab;
            _uiHolderInfos = new();

            // _buttonOverlay = buttonOverlay.OuterObject;
            // buttonOverlay.Button.onClick.AddListener(() => _overlayButtonClickedAction(_currentlyHoveredItem));
        }

        private UIHolderInfo MaybeInitializeAt(int i)
        {
            UIHolderInfo holder;
            if (_uiHolderInfos.Count <= i)
            {
                holder = GameObject.Instantiate(_prefab);
                var handler = holder.ItemFrameObject.AddComponent<PointerEnterExit<MyHorizontalScrollRect>>();
                handler.Initialize(i, this);
                _uiHolderInfos.Add(holder);
            }
            else
            {
                holder = _uiHolderInfos[i];
            }
            return holder;
        }

        public void ChangeItems(
            IEnumerable<MeshRenderer> itemsToStore,
            Transform newParentForOldItems)
        {
            {
                for (int i = 0; i < _itemCount; i++)
                    _uiHolderInfos[i].StoredItem.SetParent(newParentForOldItems, worldPositionStays: false);
            }
            {
                int i = 0;
                foreach (var item in itemsToStore)
                {
                    var holder = MaybeInitializeAt(i);
                    holder.OuterObject.SetActive(true);
                    
                    // TODO:
                    // measuring stuff, perhaps actually putting items in a completely different hierarchy and just
                    // make their positions follow.
                    {
                        var t = item.transform;
                        t.SetParent(holder.ItemFrameTransform, worldPositionStays: false);
                        t.localPosition = holder.ItemFrameTransform.rect.center;
                    }
                    i++;
                }

                for (int j = i; j < _itemCount; j++)
                    _uiHolderInfos[j].OuterObject.SetActive(false);
                
                _itemCount = i;
            }
        }

        public void OnPointerEnter(int index, PointerEventData eventData)
        {
            _currentlyHoveredItem = index;

            // if (_itemActionHandler.IsOperationInProgress)
            //     return;
            // {
            //     var t = _buttonOverlay.transform;
            //     t.SetParent(_uiHolderInfos[index].OuterTransform, worldPositionStays: false);
            //     t.localPosition = Vector2.zero;
            // }
            // _buttonOverlay.SetActive(true);
        }
        public void OnPointerExit(int index, PointerEventData eventData)
        {
            _currentlyHoveredItem = -1;
            
            // if (_itemActionHandler.IsOperationInProgress)
            //     return;
            // _buttonOverlay.SetActive(false);
        }

        public void OnProgress(int stage)
        {
            throw new NotImplementedException();
        }

        public void OnCancelled()
        {
            throw new NotImplementedException();
        }
    }

    public class ViewContext
    {
        public GameContext Game;
        public GameObject[] ThingGameObjects;
        public MyHorizontalScrollRect ItemHolder;
        public List<GameObject> ItemBuyButtons;
    }

    public class View : MonoBehaviour
    {
        public Transform[] VisualCells;
        public ThingArray<int> CountsToSpawn;
        public ThingArray<GameObject> PrefabsToSpawn;
        public ThingArray<int> ItemCosts;
        public GameplayButtonArray<Button> GameplayButtons;
        public GameplayTextArray<TMP_Text> GameplayText;
        public Transform HolderPrefab;
        public Transform ItemHolderHolder;
        public Color[] PlayerCharacterColors;
        public int[] ShopPositions;
        public GameObject BuyButtonPrefab;
        private GameObject[] _thingGameObjects;
        private Transform[] _itemHolders;
        private GameContext _game;
        private List<GameObject> _itemBuyButtons;
        private ObjectPool<GameObject> _buyButtonsPool;

        private int _seed;
        private int Seed
        {
            get => _seed;
            set
            {
                _seed = value;
                GameplayText.Seed.text = value.ToString();
            }
        }

        public void OnValidate()
        {
            static bool IsNullOrEmpty<T>(T[] arr)
            {
                return arr is null || arr.Length == 0;
            }

            if (IsNullOrEmpty(CountsToSpawn.Array))
                CountsToSpawn = ThingArray<int>.Create();
            if (IsNullOrEmpty(ItemCosts.Array))
                ItemCosts = ThingArray<int>.Create();
            if (IsNullOrEmpty(PrefabsToSpawn.Array))
                PrefabsToSpawn = ThingArray<GameObject>.Create();
            if (IsNullOrEmpty(GameplayButtons.Array))
                GameplayButtons = GameplayButtonArray<Button>.Create();
            if (IsNullOrEmpty(GameplayText.Array))
                GameplayText = GameplayTextArray<TMP_Text>.Create();
            if (PlayerCharacterColors is null || PlayerCharacterColors.Length < CountsToSpawn.Player)
                Array.Resize(ref PlayerCharacterColors, CountsToSpawn.Player);
            CountsToSpawn.RespawnPoint = CountsToSpawn.Tower;
        }

        public class Random : IRandom
        {
            private UnityEngine.Random.State _randomState;

            public Random(UnityEngine.Random.State state)
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

        private GameContext InitializeGame()
        {
            _game = Initialization.CreateGame(cellCountNotIncludingStartAndFinish: VisualCells.Length - 2, CountsToSpawn.Player);

            int seed = Seed;
            UnityEngine.Random.InitState(seed);
            var gameRandom = new Random(UnityEngine.Random.state);
            _game.Random = gameRandom;

            _game.Logger = new UnityLogger();

            _game.State.Shop.CellsWhereAccessible = ShopPositions;

            UnityEngine.Random.InitState(seed + 1);
            var spawnRandom = new Random(UnityEngine.Random.state);
            
            _thingGameObjects = new GameObject[CountsToSpawn.Array.Sum()];

            int mineCount = CountsToSpawn.EternalMine + CountsToSpawn.RegularMine;
            var costStorage = Components.InitializeStorage(_game, Components.CurrencyCostId, mineCount);
            var playerStorage = Components.InitializeStorage(_game, Components.PlayerId, CountsToSpawn.Player);
            var coinStorage = Components.InitializeStorage(_game, Components.CurrencyId, CountsToSpawn.Coin);
            Components.InitializeStorage(_game, Components.ThingSpecificEventsId);
            var respawnPointIdsStorage = Components.InitializeStorage(_game, Components.RespawnPointIdsId, CountsToSpawn.Player);
            var respawnPositionStorage = Components.InitializeStorage(_game, Components.RespawnPositionId, CountsToSpawn.RespawnPoint);
            var pickupStorage = Components.InitializeStorage(_game, Components.PickupId);
            var pickupDelegateStorage = Components.InitializeStorage(_game, Components.AttachedPickupDelegateId);
            var respawnPointIdStorage = Components.InitializeStorage(_game, Components.RespawnPointIdId, CountsToSpawn.Tower);
            var flagsStorage = Components.InitializeStorage(_game, Components.FlagsId, mineCount);
            assertNoneNull(_game.State.ComponentsByType);

            var regularMinePickup = new MinePickup.Regular;
            var eternalMinePickup = new MinePickup.Eternal;

            var rabbitPickup = new AddStatPickup(Stats.RollAdditiveBonus, 1);
            var horsePickup = new AddStatPickup(Stats.JumpAfterMoveCapacity, 1);


            void ArrangeThings(int position)
            {
                var things = _game.State.Cells[position];
                var spriteRenderer = VisualCells[position].gameObject.GetComponent<SpriteRenderer>();
                var bounds = spriteRenderer.bounds;
                float availableSpaceY = spriteRenderer.bounds.extents.y * 2;
                float offsetIncrement = -availableSpaceY / (things.Count + 1);
                float offsetStart = offsetIncrement + availableSpaceY / 2;

                for (int i = 0; i < things.Count; i++)
                {
                    var transform = _thingGameObjects[things[i]].transform;
                    Vector3 cellPosition = VisualCells[position].position;
                    
                    Vector3 thingPosition;
                    thingPosition.x = cellPosition.x;
                    thingPosition.y = cellPosition.y + offsetStart + offsetIncrement * i;
                    thingPosition.z = -(i + 1);

                    transform.position = thingPosition;
                }
            }

            void SpawnOn(int position, int id, GameObject obj)
            {
                var things = _game.State.Cells[position];
                things.Add(id);
                obj.transform.parent = VisualCells[position];
            }

            int SpawnRandomly(IRandom random, int id, GameObject obj)
            {
                int randomPos = spawnRandom.GetUnoccupiedCellIndex(_game);
                if (randomPos != -1)
                    SpawnOn(randomPos, id, obj);
                return randomPos;
            }

            int currentId = 0;
            for (int kindIndex = 0; kindIndex < CountsToSpawn.Length; kindIndex++)
            for (int instanceIndex = 0; instanceIndex < CountsToSpawn[kindIndex]; instanceIndex++)
            {
                ref int id = ref currentId;
                var obj = GameObject.Instantiate(PrefabsToSpawn[kindIndex]);
                _thingGameObjects[id] = obj;

                if (ItemCosts[kindIndex] > 0)
                    costStorage.Add(id).Value = ItemCosts[kindIndex];

                switch ((ThingKind) kindIndex)
                {
                    default: panic($"Unhandled thing kind {(ThingKind) kindIndex}"); break;

                    case ThingKind.Player:
                    {
                        _game.InitializePlayer(index: instanceIndex, thingId: id, playerStorage);
                        respawnPointIdsStorage.Add(id).Value = new Stack<int>();
                        
                        {
                            var stats = _game.State.Players[instanceIndex].Stats; 
                            stats.Set(Stats.RollAdditiveBonus, 0);
                            stats.Set(Stats.JumpAfterMoveCapacity, 0);
                        }

                        obj.GetComponent<SpriteRenderer>().color = PlayerCharacterColors[instanceIndex];

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
                        _game.AddThingToShop(id);
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
                    SpawnOn(respawnPositionStorage.GetProxy(idsOfPositions[i]).Value, refereeId, _thingGameObjects[refereeId]);
                }
            }

            for (int cellIndex = 0; cellIndex < VisualCells.Length; cellIndex++)
                ArrangeThings(cellIndex);

            _game.GetEventProxy(Events.OnPositionChanged).Add(
                (GameContext game, ref Events.PlayerPositionChangedContext context) =>
                {
                    var player = game.State.Players[context.PlayerIndex];

                    var targetTransform = VisualCells[player.Position];
                    var playerTransform = _thingGameObjects[player.ThingId].transform;
                    playerTransform.parent = targetTransform;

                    int position = player.Position;
                    int count = game.State.Cells[position].Count;
                    Debug.LogFormat("There are {0} things at {1}", count, position);
                    
                    ArrangeThings(context.InitialPosition);
                    ArrangeThings(position);
                });

            _game.GetEventProxy(Events.OnPlayerWon).Add(
                (GameContext game, ref int playerId) =>
                {
                    GameplayText.Win.text = $"{playerId} wins.";
                    GameplayText.Win.gameObject.SetActive(true);
                });

            void AlignItemsInInventory(Transform holder)
            {
                if (holder.childCount == 0)
                    return;
                var oneThingSize = holder.GetChild(0).GetComponent<SpriteRenderer>().bounds.size;
                // float offsetSize = Mathf.Min(oneThingSize, holder.transform
                for (int i = 0; i < holder.childCount; i++)
                {
                    var v = holder.transform.position;
                    v.x += oneThingSize.x * i;
                    // v.y += oneThingSize.y / 2;
                    holder.GetChild(i).transform.position = v;
                }
            }

            _game.GetEventProxy(Events.OnItemAddedToInventory).Add(
                (GameContext game, ref ItemInteractionInfo context) =>
                {
                    var holder = _itemHolders[context.PlayerIndex];
                    _thingGameObjects[context.ThingId].transform.SetParent(holder, worldPositionStays: false);
                    AlignItemsInInventory(holder);
                });

            _game.GetEventProxy(Events.OnItemRemovedFromInventory).Add(
                (GameContext game, ref ItemInteractionInfo context) =>
                {
                    var holder = _itemHolders[context.PlayerIndex];
                    _thingGameObjects[context.ThingId].transform.SetParent(null);
                    AlignItemsInInventory(holder);
                });

            _game.GetEventProxy(Events.OnNextTurn).Add(
                (GameContext game, ref NextTurnContext context) =>
                {
                    _itemHolders[context.PreviousPlayerIndex].gameObject.SetActive(false);
                    _itemHolders[context.CurrentPlayerIndex].gameObject.SetActive(true);

                    {
                        var currencyStorage = game.GetComponentStorage(Components.CurrencyId);
                        int totalAmount = 0;
                        foreach (var currency in game.GetDataInItems(Components.CurrencyId, context.CurrentPlayerIndex))
                        {
                            // The component contains the amount that the coin represents.
                            totalAmount += currency.Value;
                        }
                        GameplayText.CoinCounter.text = totalAmount.ToString();
                    }
                });

            _game.GetEventProxy(Events.OnCellContentChanged).Add(
                (GameContext game, ref CellContentChangedContext context) =>
                {
                    // This is ok, because it will be eventually animated.
                    ArrangeThings(context.CellPosition);
                });

            _game.GetEventProxy(Events.OnAmountRolled).Add(
                (GameContext game, ref AmountRolledContext context) =>
                {
                    string val = context.RolledAmount.ToString();
                    if (context.BonusAmount > 0)
                        val += " (+" + context.BonusAmount.ToString() + ")";
                    GameplayText.RollValue.text = val;
                });

            _itemHolders[0].gameObject.SetActive(true);
            GameplayText.Win.gameObject.SetActive(false);
            GameplayText.CoinCounter.text = "0";

            // _game.GetEventProxy(Events.OnItemRemovedFromInventory).Add(
            //     (GameContext game, ref ItemInteractionInfo context) =>
            //     {
            //         var holder = _itemHolders[context.PlayerIndex];
            //         _things[context.ThingId].transform.SetParent(holder, worldPositionStays: false);
            //     });

            return _game;
        }

        public void Start()
        {
            const int initialSeed = 5;
            Seed = initialSeed;

            GameplayText.Win.gameObject.SetActive(false);

            _itemHolders = new Transform[CountsToSpawn.Player];
            foreach (ref var holder in _itemHolders.AsSpan())
            {
                holder = GameObject.Instantiate(HolderPrefab);
                holder.SetParent(ItemHolderHolder, worldPositionStays: false);
                holder.gameObject.SetActive(false);
            }
            _itemHolders[0].gameObject.SetActive(true);

            _buyButtonsPool = new(() => GameObject.Instantiate(BuyButtonPrefab));

            InitializeGame();

            GameplayButtons.Roll.onClick.AddListener(() =>
            {
                _game.ExecuteCurrentPlayersTurn();
            });

            GameplayButtons.Settings.onClick.AddListener(() =>
            {
                Debug.Log("Open Settings");
            });

            GameplayButtons.UseItem.onClick.AddListener(() =>
            {
                Debug.Log("Use Item Clicked");
            });

            GameplayButtons.Restart.onClick.AddListener(() =>
            {
                foreach (var thing in _thingGameObjects)
                    Destroy(thing);
                Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                InitializeGame();
            });

            GameplayButtons.TempBuy.onClick.AddListener(async () =>
            {
                async Task DoBuying()
                {
                    assert(_game.State.Shop.Items.Count > 0);
                    
                    int itemIndex = 0;
                    var context = _game.StartBuyingThingFromShop(new()
                    {
                        PlayerIndex = _game.State.CurrentPlayerIndex,
                        ThingShopIndex = itemIndex,
                    });
                    
                    if (context.NotEnoughCoins)
                    {
                        Debug.Log("Not enough money");
                        return;
                    }

                    var cellsSlice = _game.State.IntermediateCells;
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
                            int result = await PromptForCellPlacement(_game);
                            if (result == -1)
                                panic("Unimplemented");
                            positions.Add(result);
                        }
                    }

                    _game.EndBuyingThingFromShop(context, positions);
                }
                await DoBuying();
            });
        }
    }

    public enum UIState
    {
        Normal,
        SelectingCell,
    }

    public class UIContext
    {
        public MonoBehaviour WaitObject;
    }

    public static class UI
    {
        // public static Task<int> Select()
    }
}