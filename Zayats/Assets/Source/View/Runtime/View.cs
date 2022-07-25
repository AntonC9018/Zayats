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
    }

    [GenerateArrayWrapper("GameplayTextArray")]
    public enum GameplayTextKind
    {
        Win,
        Seed,
        CoinCounter,
        RollValue,
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
        private GameObject[] _thingGameObjects;
        private Transform[] _itemHolders;
        private GameContext _game;

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
            _game = Initialization.CreateGameContext(cellCountNotIncludingFirstAndLast: VisualCells.Length - 2);

            int seed = Seed;
            UnityEngine.Random.InitState(seed);
            var gameRandom = new Random(UnityEngine.Random.state);
            _game.Random = gameRandom;

            _game.Logger = new UnityLogger();

            var initializationContext = new Initialization.GameInitializationContext
            {
                Game = _game,
                CurrentId = 0,
            };

            Initialization.InitializePlayers(ref initializationContext, CountsToSpawn.Player);
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
            var pickupStorage = Components.InitializeStorage(_game, Components.PickupActionId);
            var pickupDelegateStorage = Components.InitializeStorage(_game, Components.AttachedPickupDelegateId);
            var respawnPointIdStorage = Components.InitializeStorage(_game, Components.RespawnPointIdId, CountsToSpawn.Tower);
            var flagsStorage = Components.InitializeStorage(_game, Components.FlagsId, mineCount);
            assertNoneNull(_game.State.ComponentsByType);

            var regularMinePickup = new MinePickup(new()
            {
                DestroyOnDetonation = false,
                PutInInventoryOnDetonation = true,
                RemoveOnDetonation = true,
            });

            var eternalMinePickup = new MinePickup(new()
            {
                DestroyOnDetonation = false,
                PutInInventoryOnDetonation = false,
                RemoveOnDetonation = false,
            });

            var rabbitPickup = new AddStatPickup(Stats.RollAdditiveBonus, 1);
            var horsePickup = new AddStatPickup(Stats.JumpAfterMoveCapacity, 1);


            void ArrangeThings(int position)
            {
                var things = _game.State.Board.Cells[position].Things;
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
                var things = _game.State.Board.Cells[position].Things;
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


            for (int kindIndex = 0; kindIndex < CountsToSpawn.Length; kindIndex++)
            for (int instanceIndex = 0; instanceIndex < CountsToSpawn[kindIndex]; instanceIndex++)
            {
                ref int id = ref initializationContext.CurrentId;
                var obj = GameObject.Instantiate(PrefabsToSpawn[kindIndex]);
                _thingGameObjects[id] = obj;

                if (ItemCosts[kindIndex] > 0 && kindIndex != (int) ThingKind.Player)
                    costStorage.Add(id).Value = ItemCosts[kindIndex];

                switch ((ThingKind) kindIndex)
                {
                    default: assert(false, $"Unhandled thing kind {(ThingKind) kindIndex}"); break;

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
                        SpawnRandomly(spawnRandom, id, obj);
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
                    int count = game.State.Board.Cells[position].Things.Count;
                    Debug.LogFormat("There are {0} things at {1}", count, position);
                    
                    ArrangeThings(context.StartingPosition);
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
        }
    }
}