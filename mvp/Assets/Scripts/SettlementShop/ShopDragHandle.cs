using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mvp.SettlementShop
{
    public sealed class ShopDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        SettlementShopController _owner;
        string _cardId, _sourceCommander;
        int _sourceSlot;
        GameObject _ghost;

        public void Configure(SettlementShopController owner, string cardId, string commander, int slot)
        { _owner = owner; _cardId = cardId; _sourceCommander = commander; _sourceSlot = slot; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _ghost = new GameObject("CardDragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            _ghost.transform.SetParent(_owner.transform, false);
            var source = GetComponent<Image>();
            var image = _ghost.GetComponent<Image>(); image.sprite = source != null ? source.sprite : null; image.color = new Color(1, 1, 1, 0.82f);
            var rt = _ghost.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(150, 80); rt.position = eventData.position;
            _ghost.GetComponent<CanvasGroup>().blocksRaycasts = false;
            var text = GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                var clone = Instantiate(text, _ghost.transform); clone.rectTransform.anchorMin = Vector2.zero;
                clone.rectTransform.anchorMax = Vector2.one; clone.rectTransform.offsetMin = clone.rectTransform.offsetMax = Vector2.zero;
            }
        }

        public void OnDrag(PointerEventData eventData) { if (_ghost != null) _ghost.transform.position = eventData.position; }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_ghost != null) Destroy(_ghost);
            var target = eventData.pointerCurrentRaycast.gameObject;
            var drop = target != null ? target.GetComponentInParent<ShopDropTarget>() : null;
            if (drop != null) drop.Accept(_cardId, _sourceCommander, _sourceSlot);
        }
    }
}
