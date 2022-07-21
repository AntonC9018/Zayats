using UnityEngine;
using Zayats.Core;
using Kari.Plugins.AdvancedEnum;
using Zayats.Unity.View.Generated;
using static Zayats.Core.Assert;
using System.Linq;
using System;
using UnityEngine.UI;
using TMPro;

namespace Zayats.Unity.View
{
    [System.Serializable]
    public struct VisualCell
    {
        public Transform Transform;
    }

    [GenerateArrayWrapper("ThingArray")]
    public enum ThingKind
    {
        Player,
        EternalMine,
        RegularMine,
        Coin,
    }

    [GenerateArrayWrapper("GameplayButtonArray")]
    public enum GameplayButtonKind
    {
        Roll,
        UseItem,
        Settings,
    }

    [GenerateArrayWrapper("GameplayTextArray")]
    public enum GameplayTextKind
    {
        Win,
    }

    public class View : MonoBehaviour
    {
        public Transform[] VisualCells;
        public ThingArray<int> CountsToSpawn;
        public ThingArray<GameObject> PrefabsToSpawn;
        public GameplayButtonArray<Button> GameplayButtons;
        public GameplayTextArray<TMP_Text> GameplayText;
        public Transform HolderPrefab;
        public Transform ItemHolderHolder;
        private GameObject[] _things;
        private Transform[] _itemHolders;
        private GameContext _game;

        public void OnValidate()
        {
            static bool IsNullOrEmpty<T>(T[] arr)
            {
                return arr is null || arr.Length == 0;
            }

            if (IsNullOrEmpty(CountsToSpawn.Array))
                CountsToSpawn = ThingArray<int>.Create();
            if (IsNullOrEmpty(PrefabsToSpawn.Array))
                PrefabsToSpawn = ThingArray<GameObject>.Create();
            if (IsNullOrEmpty(GameplayButtons.Array))
                GameplayButtons = GameplayButtonArray<Button>.Create();
            if (IsNullOrEmpty(GameplayText.Array))
                GameplayText = GameplayTextArray<TMP_Text>.Create();
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

            public int GetUnoccupiedCellIndex(GameContext game)
            {
                UnityEngine.Random.state = _randomState;
                int lower = 1;
                var cells = game.State.Board.Cells;
                int upperInclusive = cells.Length - 2;
                int attemptCounter = 0;
                const int maxAttempts = 10;
                int t;
                bool maxAttemptsReached = false;
                do
                {
                    t = GetIntInternal(lower, upperInclusive);
                    attemptCounter++;
                }
                while (cells[t].Things.Count != 0
                    || (maxAttemptsReached = attemptCounter >= maxAttempts));

                _randomState = UnityEngine.Random.state;

                if (maxAttemptsReached)
                {
                    for (int i = 0; i < cells.Length; i++)
                    {
                        if (cells[i].Things.Count == 0)
                            return i;
                    }
                    return -1;
                }

                return t;
            }
        }

        public void Start()
        {
            GameplayText.Win.gameObject.SetActive(false);

            _itemHolders = new Transform[CountsToSpawn.Player];
            foreach (ref var holder in _itemHolders.AsSpan())
            {
                holder = GameObject.Instantiate(HolderPrefab);
                holder.SetParent(ItemHolderHolder, worldPositionStays: false);
                holder.gameObject.SetActive(false);
            }
            Debug.Log(_itemHolders[0]);
            _itemHolders[0].gameObject.SetActive(true);
            
            _game = Initialization.CreateGameContext(cellCountNotIncludingFirstAndLast: VisualCells.Length - 2);

            const int seed = 5;
            UnityEngine.Random.InitState(seed);
            var gameRandom = new Random(UnityEngine.Random.state);
            _game.Random = gameRandom;

            var initializationContext = new Initialization.GameInitializationContext
            {
                Game = _game,
                CurrentId = 0,
            };

            Initialization.InitializePlayers(ref initializationContext, CountsToSpawn.Player);
            UnityEngine.Random.InitState(seed + 1);
            var spawnRandom = new Random(UnityEngine.Random.state);
            
            _things = new GameObject[CountsToSpawn.Array.Sum()];

            var mineStorage = Components.InitializeStorage(_game, Components.MineId, CountsToSpawn.EternalMine + CountsToSpawn.RegularMine);
            var playerStorage = Components.InitializeStorage(_game, Components.PlayerId, CountsToSpawn.Player);
            var coinStorage = Components.InitializeStorage(_game, Components.CurrencyId, CountsToSpawn.Coin);

            Vector3 WithZ(Vector3 prev, float z)
            {
                return new Vector3(prev.x, prev.y, z);
            }

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
                    var transform = _things[things[i]].transform;
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

            void SpawnRandomly(IRandom random, int id, GameObject obj)
            {
                int randomPos = spawnRandom.GetUnoccupiedCellIndex(_game);
                if (randomPos != -1)
                    SpawnOn(randomPos, id, obj);
            }


            for (int kindIndex = 0; kindIndex < CountsToSpawn.Length; kindIndex++)
            for (int instanceIndex = 0; instanceIndex < CountsToSpawn[kindIndex]; instanceIndex++)
            {
                ref int id = ref initializationContext.CurrentId;
                var obj = GameObject.Instantiate(PrefabsToSpawn[kindIndex]);
                _things[id] = obj;

                switch ((ThingKind) kindIndex)
                {
                    case ThingKind.Player:
                    {
                        Initialization.InitializePlayer(instanceIndex, id, _game.State.Players, playerStorage);
                        SpawnOn(0, id, obj);
                        break;
                    }
                    case ThingKind.EternalMine:
                    {
                        ref var mine = ref mineStorage.Add(id).Component;
                        mine.DestroyOnDetonation = false;
                        mine.PutInInventoryOnDetonation = false;
                        mine.RemoveOnDetonation = false;
                        SpawnRandomly(spawnRandom, id, obj);
                        break;
                    }
                    case ThingKind.RegularMine:
                    {
                        ref var mine = ref mineStorage.Add(id).Component;
                        mine.DestroyOnDetonation = false;
                        mine.PutInInventoryOnDetonation = true;
                        mine.RemoveOnDetonation = true;
                        SpawnRandomly(spawnRandom, id, obj);
                        break;
                    }
                    case ThingKind.Coin:
                    {
                        ref var coin = ref coinStorage.Add(id).Component;
                        coin = 1;
                        SpawnRandomly(spawnRandom, id, obj);
                        break;
                    }
                }
                id++;
            }

            for (int cellIndex = 0; cellIndex < VisualCells.Length; cellIndex++)
                ArrangeThings(cellIndex);

            _game.GetEventProxy(Events.OnPositionChanged).Add(
                (GameContext game, ref Events.PlayerPositionChangedContext context) =>
                {
                    game.CollectCoinsAtCurrentPosition(context.PlayerId);
                    
                    var currencyStorage = game.GetComponentStorage(Components.CurrencyId);
                    int totalAmount = 0;
                    foreach (var currency in game.GetDataInItems(Components.CurrencyId, context.PlayerId))
                    {
                        // The component contains the amount that the coin represents.
                        totalAmount += currency.Component;
                    }
                    Debug.LogFormat("The player {0} now has {1} currency.", context.PlayerId, totalAmount);
                });

            _game.GetEventProxy(Events.OnPositionChanged).Add(
                (GameContext game, ref Events.PlayerPositionChangedContext context) =>
                {
                    var player = game.State.Players[context.PlayerId];

                    var targetTransform = VisualCells[player.Position];
                    var playerTransform = _things[player.ThingId].transform;
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

            _game.GetEventProxy(Events.OnAddedInventoryItem).Add(
                (GameContext game, ref Events.AddedInventoryItemContext context) =>
                {
                    var holder = _itemHolders[context.PlayerId];
                    _things[context.ThingId].transform.SetParent(holder, worldPositionStays: false);
                });


            GameplayButtons.Roll.onClick.AddListener(() =>
            {
                int playerId = _game.State.CurrentPlayerId;
                int roll = _game.RollAmount(playerId);
                _game.MovePlayer(new()
                {
                    PlayerId = playerId,
                    Amount = roll,
                    Details = Logic.MovementDetails.Normal,
                });
                {
                    ref var a = ref _game.State.CurrentPlayerId;
                    _itemHolders[a].gameObject.SetActive(false);
                    a = (a + 1) % _game.State.Players.Length;
                    _itemHolders[a].gameObject.SetActive(true);
                }
            });

            GameplayButtons.Settings.onClick.AddListener(() =>
            {
                Debug.Log("Open Settings");
            });

            GameplayButtons.UseItem.onClick.AddListener(() =>
            {
                Debug.Log("Use Item Clicked");
            });
        }
    }
}