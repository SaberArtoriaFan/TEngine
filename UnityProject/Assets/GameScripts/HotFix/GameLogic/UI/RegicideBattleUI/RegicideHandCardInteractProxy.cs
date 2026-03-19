using UnityEngine;
using UnityEngine.EventSystems;

namespace GameLogic
{
    [DisallowMultipleComponent]
    public sealed class RegicideHandCardInteractProxy : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private RegicideBattleUI _owner;
        private int _cardIndex = -1;

        public void Bind(RegicideBattleUI owner, int cardIndex)
        {
            _owner = owner;
            _cardIndex = cardIndex;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _owner?.OnCardPointerEnter(_cardIndex);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _owner?.OnCardPointerExit(_cardIndex);
        }
    }
}
