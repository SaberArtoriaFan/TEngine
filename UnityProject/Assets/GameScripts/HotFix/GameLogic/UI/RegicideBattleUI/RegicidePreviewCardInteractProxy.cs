using UnityEngine;
using UnityEngine.EventSystems;

namespace GameLogic
{
    [DisallowMultipleComponent]
    public sealed class RegicidePreviewCardInteractProxy : MonoBehaviour, IPointerClickHandler
    {
        private RegicideBattleUI _owner;
        private int _slotIndex = -1;

        public void Bind(RegicideBattleUI owner, int slotIndex)
        {
            _owner = owner;
            _slotIndex = slotIndex;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                _owner?.OnPreviewCardRightClicked(_slotIndex);
            }
        }
    }
}
