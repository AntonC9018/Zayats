using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zayats.Core;
using DG.Tweening;
using Common.Unity;
using Common;
using Newtonsoft.Json;
using UnityEditor;

namespace Zayats.Unity.View
{
    using static PointerEventData.InputButton;
    using static Assert;

    [Serializable]
    public struct ButtonOverlay
    {
        public GameObject OuterObject;
        public RectTransform OuterTransform => (RectTransform) OuterObject.transform;
        public Button Button;
    }

    public class ItemContainers : IPointerEnterIndex, IPointerExitIndex, IPointerClickIndex
    {
        private int _itemCount;
        public int ItemCount
        {
            get => _itemCount;
            set
            {
                // for (int i = value; i < _itemCount; i++)
                //     _uiHolderInfos[i].OuterObject.SetActive(false);
                _itemCount = value;
            }
        }

        private List<UIHolderInfo> _uiHolderInfos;
        private int _currentlyHoveredItem;
        private ViewContext _viewContext;
        private ItemScrollUIReferences _ui;
        
        // private List<Tween> _rotationTweens;
        // private List<Tween> _usableGraphicFade;
        // private GameObject _buttonOverlay;
        // private Action<int> _overlayButtonClickedAction;

        public ItemContainers(
            ViewContext viewContext,
            ItemScrollUIReferences ui
                // , ButtonOverlay buttonOverlay, Action<int> overlayButtonClickedAction
        )
        {
            _ui = ui;
            assert(viewContext != null);

            _viewContext = viewContext;
            _uiHolderInfos = new();

            // viewContext.GetEventProxy(ViewEvents.OnItemInteractionCancelled)

            // _buttonOverlay = buttonOverlay.OuterObject;
            // buttonOverlay.Button.onClick.AddListener(() => _overlayButtonClickedAction(_currentlyHoveredItem));
        }

        private UIHolderInfo MaybeInitializeAt(int i)
        {
            UIHolderInfo holder;
            if (_uiHolderInfos.Count <= i)
            {
                holder = GameObject.Instantiate(_ui.HolderPrefab);
                {
                    var handler = holder.ItemFrameObject.AddComponent<PointerEnter>();
                    handler.Initialize(i, this);
                }
                {
                    var handler = holder.ItemFrameObject.AddComponent<PointerExit>();
                    handler.Initialize(i, this);
                }
                {
                    var handler = holder.ItemFrameObject.AddComponent<PointerClick>();
                    handler.Initialize(i, this);
                }
                _uiHolderInfos.Add(holder);
                holder.OuterObject.SetActive(false);
                holder.OuterTransform.SetParent(parent: _ui.ScrollRect.content, worldPositionStays: false);
                
                // Scroll rects only update on the next frame,
                // so we need to calculate the position of the next child manually...
                if (i > 0)
                {
                    var t = _uiHolderInfos[i - 1].OuterTransform;
                    var p = t.localPosition;
                    if (_ui.ScrollRect.horizontal)
                        p.x += t.rect.width;
                    else if (_ui.ScrollRect.vertical)
                        p.y -= t.rect.height;
                
                    holder.OuterTransform.localPosition = p;
                }
                
                holder.InitializeAnimatedContainer(_ui.ContainerTransform, i);
            }
            else
            {
                holder = _uiHolderInfos[i];
            }
            return holder;
        }

        private static readonly Vector3[] _WorldCornersCache = new Vector3[4];
        private static readonly List<Transform> _GetChildrenCache = new();

        private (Vector3 CCenter, float MinRatio, Vector3 CenterOffset, float ZOffset) CalculateOffsets(Transform item, UIHolderInfo holder)
        {
            var info = ViewLogic.GetInfo(item);
            var size = info.Size;
            var (ccenter, csize) = holder.ItemFrameTransform.GetWorldSpaceRect();
            var minRatio = Vector2.Scale(csize, size.xy().Inverse()).Min();
            var targetCenterOffset = info.Center * minRatio;

            // Must be a child of ItemFrameTransform
            var offset = - targetCenterOffset;

            return (ccenter, minRatio, offset, -1f);
        }

        public void ChangeItems(
            Transform[] itemsToStore,
            Sequence animationSequence,
            float animationSpeed)
        {
            for (int i = 0; i < itemsToStore.Length; i++)
            {
                var item = itemsToStore[i];
                var holder = MaybeInitializeAt(i);
                var o = CalculateOffsets(item, holder);

                var ct = holder.CenteringTransform;
                {
                    var t = o.CCenter + o.CenterOffset;
                    t.z += o.ZOffset;
                    var tween = item.DOMove(t, animationSpeed);
                    animationSequence.Join(tween);
                }
                {
                    // var scale = Vector3.one * o.MinRatio;
                    // var tween = item.DOScale(scale, animationSpeed);
                    // animationSequence.Join(tween);
                }
            }

            animationSequence.AppendCallback(() =>
            {
                for (int j = 0; j < _itemCount; j++)
                    _uiHolderInfos[j].StoredItem.parent = _ui.ParentForOldItems;

                for (int i = 0; i < itemsToStore.Length; i++)
                {
                    var item = itemsToStore[i];
                    item.GetComponentsInChildren<Transform>(_GetChildrenCache);
                    foreach (var ch in _GetChildrenCache)
                        ch.gameObject.layer = LayerIndex.UI;

                    var holder = _uiHolderInfos[i];
                    var o = CalculateOffsets(item, holder);
                    
                    {
                        var centering = holder.CenteringTransform;
                        item.parent = centering;
                        centering.localPosition = o.CenterOffset;

                        centering.localScale = Vector3.one * o.MinRatio;
                        item.localScale = Vector3.one;
                        item.localPosition = Vector3.zero;
                    }
                    {
                        var lp = holder.AnimatedTransform.localPosition;
                        lp.z = o.ZOffset;
                        holder.AnimatedTransform.localPosition = lp;
                    }

                    holder.OuterObject.SetActive(true);
                }
                
                ItemCount = itemsToStore.Length;
            });
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

        public void OnPointerClick(int index, PointerEventData eventData)
        {
            if (eventData.button == Left)
                _viewContext.MaybeTryStartHandlingItemInteraction(index);
        }

        public void RemoveItemAt(int itemIndex, Sequence animationSequence, float animationSpeed)
        {
            animationSequence.AppendCallback(() =>
            {
                var first = _uiHolderInfos[itemIndex];
                var last = _uiHolderInfos[ItemCount - 1];

                var storedItem = first.StoredItem;
                storedItem.parent = _ui.ParentForOldItems;
                storedItem.localScale = Vector3.one;
                
                var ft = first.AnimatedTransform;

                for (int i = itemIndex + 1; i < _itemCount; i++)
                    _uiHolderInfos[i - 1].AnimatedTransform = _uiHolderInfos[i].AnimatedTransform;
                    
                last.OuterObject.SetActive(false);

                last.AnimatedTransform = ft;
                assert(!last.HasStoredItem);
                _itemCount--;
            });
        }

        public void ResetUsabilityColors(
            IEnumerable<Color> colors,
            Sequence animationSequence)
        {
            animationSequence.AppendCallback(() =>
            {
                int i = 0;
                foreach (var color in colors)
                {
                    assert(i < _itemCount, i + " " + _itemCount);
                    _uiHolderInfos[i].UsabilityGraphic.color = color;
                    i++;
                }
            });
        }
    }
}