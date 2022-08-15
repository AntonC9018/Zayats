using UnityEngine;
using Zayats.Core;
using static Zayats.Core.Assert;
using System.Linq;
using System;
using System.Collections.Generic;
using static Zayats.Core.Events;
using Common;
using static Zayats.Core.GameEvents;
using DG.Tweening;
using Newtonsoft.Json;

namespace Zayats.Unity.View
{

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

        public void Dump(object obj)
        {
            UnityEngine.Debug.Log(JsonConvert.SerializeObject(obj, Formatting.Indented));
        }

        public void Debug(string format, object value)
        {
            UnityEngine.Debug.LogFormat(format, value);
        }
    }

    public class Initialization : MonoBehaviour
    {
        [SerializeField] private UIReferences _ui;
        [SerializeField] private SetupConfiguration _config;
        
        #if UNITY_EDITOR
            public
        #endif
        ViewContext _view;

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

        public enum WorldMode
        {
            Random,
            Set,
        }
        public WorldMode Mode;

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

        private void CustomCreateThings()
        {
            var creating = Game.StartCreating();
            
            {
                var p = creating.Create();
                _view.InstantiateThing(p, ThingKind.Player);
                p.Player(0).Place().At(0);
            }
            {
                var p = creating.Create();
                _view.InstantiateThing(p, ThingKind.Snake);
                p.Snake().Place().At(1); 
            }
            {
                var p = creating.Create();
                _view.InstantiateThing(p, ThingKind.Player);
                p.Player(1).Place().At(2);
            }
        }

        private void DefaultCreateThings(in GameConfiguration config)
        {
            UnityEngine.Random.InitState(Seed + 1);
            var spawnRandom = new UnityRandom(UnityEngine.Random.state);

            var creating = Game.StartCreating();
            for (int kindIndex = 0; kindIndex < config.CountsToSpawn.Length; kindIndex++)
            for (int instanceIndex = 0; instanceIndex < config.CountsToSpawn[kindIndex]; instanceIndex++)
            {
                var create = creating.Create();
                _view.InstantiateThing(create, (ThingKind) kindIndex);

                switch ((ThingKind) kindIndex)
                {
                    default: panic($"Unhandled thing kind {(ThingKind) kindIndex}"); break;

                    case ThingKind.Player:
                    {
                        create.Player(instanceIndex).Place().At(0);
                        break;
                    }
                    case ThingKind.EternalMine:
                    {
                        create.EternalMine().Place().Randomly(spawnRandom);
                        break;
                    }
                    case ThingKind.RegularMine:
                    {
                        create.RegularMine().Place().Randomly(spawnRandom);
                        break;
                    }
                    case ThingKind.Coin:
                    {
                        create.Coin().Place().Randomly(spawnRandom);
                        break;
                    }
                    case ThingKind.RespawnPoint:
                    {
                        int pos = create.Place().Randomly(spawnRandom);
                        create.RespawnPoint(pos);
                        break;
                    }
                    case ThingKind.Totem:
                    {
                        create.Totem().Place().Randomly(spawnRandom);
                        break;
                    }
                    case ThingKind.Rabbit:
                    {
                        create.Rabbit().Place().IntoShop();
                        break;
                    }
                    case ThingKind.Tower:
                    {
                        create.Tower().Place().IntoShop();
                        break;
                    }
                    case ThingKind.Horse:
                    {
                        create.Horse().Place().Randomly(spawnRandom);
                        break;
                    }
                    case ThingKind.Snake:
                    {
                        create.Snake().Place().IntoShop();
                        break;
                    }
                }
            }
        }

        private void InitializePlayerColor(Color[] playerCharacterColors)
        {
            for (int i = 0; i < Game.State.Players.Length; i++)
            {
                int thingId = Game.State.Players[i].ThingId;
                var (_, renderer) = UI.ThingGameObjects[thingId].transform.GetObject(ObjectHierarchy.Model);
                var material = renderer.material;
                // Instance materials need to be cleaned up.
                _toDestroy.Add(material);
                material.color = playerCharacterColors[i];
            }
        }

        private GameContext InitializeGame()
        {
            ref var gameConfig = ref _config.Game;
            var countsToSpawn = gameConfig.CountsToSpawn;

            _view.Game = Core.Initialization.CreateGame(cellCountNotIncludingStartAndFinish: UI.VisualCells.Length - 2, countsToSpawn.Player);

            UnityEngine.Random.InitState(Seed);
            var gameRandom = new UnityRandom(UnityEngine.Random.state);
            Game.Random = gameRandom;

            Game.Logger = new UnityLogger();
            Game.State.Shop.CellsWhereAccessible = gameConfig.ShopPositions.ToArray();
            Game.InitializeComponentStorages();
            
            {
                var t = UI.ThingGameObjects;
                Array.Resize(ref t, countsToSpawn.Array.Sum());
                UI.ThingGameObjects = t;
            }
            assertNoneNull(Game.State.ComponentsByType);

            if (Mode == WorldMode.Random)
                DefaultCreateThings(_config.Game);
            else
                CustomCreateThings();

            {    
                InitializePlayerColor(_config.Game.PlayerCharacterColors);
                // Associate respawn points with the items.
                Core.Initialization.AssociateRespawnPointIdsOneToOne(Game);
            }

            var s = _view.BeginAnimationEpoch();
            for (int cellIndex = 0; cellIndex < UI.VisualCells.Length; cellIndex++)
                _view.ArrangeThingsOnCell(cellIndex, s,
                    (view, _, transform, pos) => transform.DOMove(pos, view.Visual.AnimationSpeed.InitialThingSpawning));   

            Vector3 GetCellTopPosition(int cellIndex)
            {
                var things = _view.Game.State.Cells[cellIndex];
                var cellInfo = _view.GetCellVisualInfo(cellIndex);
                var up = cellInfo.OuterObject.up;
                var top = cellInfo.GetTop(up);

                foreach (var thingId in things)
                {
                    var thingInfo = _view.GetThingVisualInfo(thingId);
                    top += thingInfo.Size.y * up;
                }
                return top;
            }


            Game.GetEventProxy(OnPositionAboutToChange).Add(() => _view.BeginAnimationEpoch());

            Game.GetEventProxy(OnPositionChanging).Add(
                (GameContext game, ref PlayerPositionChangedContext context) =>
                {
                    // Only animate hopping
                    bool isHopping = context.MovementKind
                        is MovementKind.HopOverThing
                        or MovementKind.Normal
                        or MovementKind.ToppleOverPlayer;
                    if (!isHopping)
                        return;

                    ref var player = ref game.State.Players[context.PlayerIndex];
                    var playerTransform = UI.ThingGameObjects[player.ThingId].transform;

                    var moveSequence = DOTween.Sequence();
                    {
                        var playerVisualInfo = _view.GetThingVisualInfo(player.ThingId);
                        int direction = Math.Sign(context.TargetPosition - context.InitialPosition);
                        for (int i = context.InitialPosition + direction; i != context.TargetPosition; i += direction)
                        {
                            var cell = UI.VisualCells[i];
                            var pos = GetCellTopPosition(i);
                            var t = _view.JumpAnimation(playerTransform, pos);
                            t.OnComplete(() => playerTransform.SetParent(cell, worldPositionStays: true));
                            moveSequence.Append(t);
                        }
                    }
                    {
                        var s = _view.LastAnimationSequence;
                        s.Join(moveSequence);
                        // s.AppendInterval(0);
                    }
                });

            Game.GetEventProxy(OnPlayerWon).Add(
                (GameContext game, int playerId) =>
                {
                    var win = UI.GameplayText.Win;
                    win.gameObject.SetActive(true);
                    win.text = $"{playerId} wins.";
                    win.alpha = 0.0f;
                    var fade = win.DOFade(255.0f, _view.Visual.AnimationSpeed.UI);

                    _view.LastAnimationSequence.Append(fade);
                });

            Game.GetEventProxy(GameEvents.OnItemAddedToInventory).Add(
                (GameContext game, ref ItemInterationContext context) =>
                {
                    if (context.PlayerIndex == game.State.CurrentPlayerIndex)
                        _view.SetItemsForPlayer(context.PlayerIndex);
                });

            Game.GetEventProxy(GameEvents.OnItemRemovedFromInventory).Add(
                (GameContext game, ref ItemRemovedContext context) =>
                {
                    UI.ItemContainers.RemoveItemAt(
                        context.ItemIndex,
                        _view.MaybeBeginAnimationEpoch(),
                        _view.Visual.AnimationSpeed.UI);
                });

            Game.GetEventProxy(GameEvents.OnNextTurn).Add(
                (GameContext game, ref NextTurnContext context) =>
                {
                    _view.SetItemsForPlayer(context.CurrentPlayerIndex);

                    {
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
                    assert(game.State.Cells[context.CellPosition].ContainsAll(context.AddedThings));
                    assert(game.State.Cells[context.CellPosition].ContainsNone(context.RemovedThings));

                    var playerMovement = context.Reason.MatchPlayerMovement();

                    if (playerMovement.HasValue
                        && context.AddedThings.Count == 1
                        && context.AddedThings[0] == playerMovement.Value.PlayerId
                        // Death does not have an animation for now, so we just teleport the player in this case.
                        && playerMovement.Value.Kind != MovementKind.Death)
                    {
                        int playerId = context.AddedThings[0];

                        _view.ArrangeThingsOnCell(
                            context.CellPosition,
                            _view.LastAnimationSequence,
                            (view, thingId, transform, position) =>
                            {
                                if (playerId == thingId)
                                    return view.JumpAnimation(transform, position);
                                return view.MoveAnimation(transform, position);
                            });
                    }
                    else
                    {
                        _view.ArrangeThingsOnCell(
                            context.CellPosition,
                            _view.LastAnimationSequence,
                            ViewLogic.MoveAnimationAdapter);
                    }
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

            Game.GetEventProxy(GameEvents.OnPositionChanged).Add(
                (GameContext game, ref PlayerPositionChangedContext context) =>
                {
                    _view.ResetUsabilityColors(game.State.CurrentPlayerIndex, _view.LastAnimationSequence);
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

            DOTween.defaultAutoPlay = AutoPlay.None;

            _view = ViewLogic.CreateView(_config, _ui);
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
                // TODO: move into an event to clean this up?
                _view.UI.ItemContainers.ItemCount = 0;

                _view.SkipAnimations();
                foreach (var thing in UI.ThingGameObjects)
                    Destroy(thing);

                Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                InitializeGame();
            });

            buttons.TempBuy.onClick.AddListener(async () =>
            {
                _view.MaybeTryInitiateBuying(shopItemIndex: 0);
            });

            UI.ScreenOverlayObject.AddComponent<InputInterceptorOverlay>().Initialize(_view);

            _view.GetEventProxy(ViewEvents.OnSelectionProgress).Add(
                (ViewContext view, ref SelectionState state) =>
                {
                    view.MaybeConfirmItemUse();
                });

            _view.GetEventProxy(ViewEvents.OnItemInteraction.Started).Add(
                // Scroll the item into view on the scrollview.
                (ViewContext view, ref ViewEvents.ItemHandlingContext context) =>
                {
                    // view.BeginAnimationEpoch();
                    // view.LastAnimationSequence.Join(t);

                    var scrollRect = view.UI.Static.ItemScrollUI.ScrollRect;
                    var targetPos = scrollRect.GetContentLocalPositionToScrollChildIntoView(context.Item.Index);
                    var t = scrollRect.content.DOLocalMove(targetPos, view.Visual.AnimationSpeed.UI);
                });

            _view.GetEventProxy(ViewEvents.OnItemInteraction.Started).Add(
                (ViewContext view, ref ViewEvents.ItemHandlingContext context) =>
                {
                    view.HighlightObjectsOfItemInteraction(context.Selection);
                });
            _view.GetEventProxy(ViewEvents.OnItemInteraction.CancelledOrFinalized).Add(
                (ViewContext view) =>
                {
                    view.CancelHighlighting();
                });

            _view.GetEventProxy(ViewEvents.OnItemInteraction.Started).Add(
                (ViewContext view, ref ViewEvents.ItemHandlingContext context) =>
                {
                    view.ChangeLayerOnValidTargetsForRaycasts(
                        context.Selection.TargetKind, context.Selection.ValidTargets);
                });
            _view.GetEventProxy(ViewEvents.OnItemInteraction.CancelledOrFinalized).Add(
                (ViewContext view, ref ViewEvents.ItemHandlingContext context) =>
                {
                    view.ChangeLayerOnValidTargetsToDefault(
                        context.Selection.TargetKind, context.Selection.ValidTargets);
                });
            _view.GetEventProxy(ViewEvents.OnItemInteraction.CancelledOrFinalized).Add(
                (ViewContext view) =>
                {
                    view.State.Selection.TargetKind = TargetKind.None;
                    view.State.ItemHandling.ThingId = -1;
                });

            GetComponent<AnimationStart>().Initialize(_view);
        }
    
        void OnDestroy()
        {
            foreach (var obj in _toDestroy)
                Destroy(obj);
        }
    }
}