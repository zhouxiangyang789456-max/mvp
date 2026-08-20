using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mvp.SettlementShop
{
    public enum ShopDropTargetType { Inventory, CommanderSlot, Sell }

    public sealed class ShopDropTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        SettlementShopController _owner;
        ShopDropTargetType _type;
        string _commanderId;
        int _slot;
        Image _image;
        Color _normal;

        public void Configure(SettlementShopController owner, ShopDropTargetType type, string commanderId, int slot)
        { _owner = owner; _type = type; _commanderId = commanderId; _slot = slot; _image = GetComponent<Image>(); if (_image != null) _normal = _image.color; }
        public void Accept(string cardId, string sourceCommander, int sourceSlot) =>
            _owner.DropCard(cardId, sourceCommander, sourceSlot, _type, _commanderId, _slot);
        public void OnPointerEnter(PointerEventData eventData) { if (_image != null) _image.color = new Color(0.75f, 1f, 0.65f, _normal.a); }
        public void OnPointerExit(PointerEventData eventData) { if (_image != null) _image.color = _normal; }
    }
}
