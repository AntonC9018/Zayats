using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zayats.Core;

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

        public void Initialize(ViewContext viewContext, UIHolderInfo holderPrefab, Transform viewport
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