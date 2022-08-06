using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zayats.Core;
using DG.Tweening;

namespace Zayats.Unity.View
{
    using static PointerEventData.InputButton;

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
        private Transform _viewport;
        private int _itemCount;
        private int _currentlyHoveredItem;
        private ViewContext _viewContext;

        // private GameObject _buttonOverlay;
        // private Action<int> _overlayButtonClickedAction;

        public ItemContainers(ViewContext viewContext, UIHolderInfo holderPrefab, Transform viewport
                // , ButtonOverlay buttonOverlay, Action<int> overlayButtonClickedAction
        )
        {
            _viewContext = viewContext;
            _viewport = viewport;
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
                holder = GameObject.Instantiate(_prefab, parent: _viewport);
                {
                    var handler = holder.ItemFrameObject.AddComponent<PointerEnter<ItemContainers>>();
                    handler.Initialize(i, this);
                }
                {
                    var handler = holder.ItemFrameObject.AddComponent<PointerExit<ItemContainers>>();
                    handler.Initialize(i, this);
                }
                {
                    var handler = holder.ItemFrameObject.AddComponent<PointerClick<ItemContainers>>();
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

        private static readonly Vector3[] _WorldCornersCache;

        public void ChangeItems(
            IEnumerable<Transform> itemsToStore,
            Transform newParentForOldItems,
            Sequence animationSequence,
            float animationSpeed)
        {
            animationSequence.AppendCallback(() =>
            {
                int i = 0;
                foreach (var item in itemsToStore)
                    item.SetParent(_uiHolderInfos[i++].ItemFrameTransform, worldPositionStays: true);
                _itemCount = i;
            });

            int i = 0;
            {
                foreach (var item in itemsToStore)
                {
                    var holder = MaybeInitializeAt(i++);
                    var itemFrame = holder.ItemFrameTransform;
                    itemFrame.GetWorldCorners(_WorldCornersCache);
                    var center = (_WorldCornersCache[0] + _WorldCornersCache[2]) * 0.5f;
                    var tween = item.DOMove(center, animationSpeed);
                    animationSequence.Join(tween);
                }
            }

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
                _viewContext.TryStartHandlingItemInteraction(index);
        }

        internal void RemoveItemAt(int itemIndex)
        {
            throw new NotImplementedException();
        }
    }
}