using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zayats.Core;
using DG.Tweening;
using Common.Unity;

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
        private List<UIHolderInfo> _uiHolderInfos;
        private UIHolderInfo _prefab;
        private Transform _content;
        private int _itemCount;
        private int _currentlyHoveredItem;
        private ViewContext _viewContext;
        // private List<Tween> _rotationTweens;
        // private List<Tween> _usableGraphicFade;
        // private GameObject _buttonOverlay;
        // private Action<int> _overlayButtonClickedAction;

        public ItemContainers(ViewContext viewContext, UIHolderInfo holderPrefab, Transform content
                // , ButtonOverlay buttonOverlay, Action<int> overlayButtonClickedAction
        )
        {
            assert(viewContext != null);
            assert(content != null);
            assert(holderPrefab != null);

            _viewContext = viewContext;
            _content = content;
            _prefab = holderPrefab;
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
                holder = GameObject.Instantiate(_prefab, parent: _content);
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
            }
            else
            {
                holder = _uiHolderInfos[i];
            }
            return holder;
        }

        private static readonly Vector3[] _WorldCornersCache = new Vector3[4];
        private static readonly List<Transform> _GetChildrenCache = new();

        public void ChangeItems(
            IEnumerable<Transform> itemsToStore,
            Sequence animationSequence,
            float animationSpeed)
        {
            int i = 0;
            {
                foreach (var item in itemsToStore)
                {
                    var holder = MaybeInitializeAt(i++);
                    var itemFrame = holder.ItemFrameTransform;
                    var (itemFrameCenter, itemFrameSize) = itemFrame.GetWorldSpaceRect();
                    var info = ViewLogic.GetInfo(item);
                    float smallerFactor = Vector2.Scale(info.Size.xy().Inverse(), itemFrameSize).Min();
                    Vector3 adjustedModelCenterOffset = info.Size.y / 2 * -smallerFactor * itemFrame.up;
                    Vector3 bringModelForwardOffset = info.Size.z * -smallerFactor * itemFrame.forward;
                    var position = itemFrameCenter + adjustedModelCenterOffset + bringModelForwardOffset;
                    var tween = item.DOMove(position, animationSpeed);
                    animationSequence.Join(tween);
                }
            }

            animationSequence.AppendCallback(() =>
            {
                int i = 0;

                for (int j = 0; j < _itemCount; j++)
                    _uiHolderInfos[j].ItemFrameTransform.GetChild(0).parent = _viewContext.UI.ParentForOldItems;

                foreach (var item in itemsToStore)
                {
                    item.GetComponentsInChildren<Transform>(_GetChildrenCache);
                    foreach (var ch in _GetChildrenCache)
                        ch.gameObject.layer = LayerIndex.UI;

                    var h = _uiHolderInfos[i++];
                    item.SetParent(h.ItemFrameTransform, worldPositionStays: true);
                    h.OuterObject.SetActive(true);
                }
                _itemCount = i;
            });

            animationSequence.AppendCallback(() =>
            {
                for (int j = i; j < _uiHolderInfos.Count; j++)
                    _uiHolderInfos[j].OuterObject.SetActive(false);
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
                _uiHolderInfos[itemIndex].StoredItem.parent = _viewContext.UI.ParentForOldItems;
                for (int i = itemIndex + 1; i < _itemCount; i++)
                    _uiHolderInfos[i].StoredItem.parent = _uiHolderInfos[i - 1].ItemFrameTransform;
                _uiHolderInfos[_itemCount - 1].OuterObject.SetActive(false);

                assert(_uiHolderInfos[_itemCount - 1].StoredItem == null);
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
                    assert(i < _itemCount);
                    _uiHolderInfos[i].UsabilityGraphic.color = color;
                    i++;
                }
            });
        }
    }
}