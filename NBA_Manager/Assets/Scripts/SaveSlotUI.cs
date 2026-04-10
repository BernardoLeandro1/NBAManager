using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────
// SaveSlotUI — attach to each of the 3 slot GameObjects in the save panel.
// Displays slot metadata and fires a callback when clicked.
// ─────────────────────────────────────────────────────────────────────────
namespace NBAManager
{
    public class SaveSlotUI : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI teamNameText;
        public TextMeshProUGUI recordText;
        public TextMeshProUGUI seasonPhaseText;
        public TextMeshProUGUI lastSavedText;
        public TextMeshProUGUI emptyLabel;
        public Button          slotButton;
        public GameObject      filledContent;   // shown when slot has data
        public GameObject      emptyContent;    // shown when slot is empty

        private int                      _slotIndex;
        private System.Action<int>       _onSelected;

        public void Populate(SaveSlotInfo info, int index,
                                bool allowEmpty, System.Action<int> onSelected)
        {
            _slotIndex  = index;
            _onSelected = onSelected;

            slotButton.onClick.RemoveAllListeners();

            bool isEmpty = info == null || info.isEmpty;

            filledContent.SetActive(!isEmpty);
            emptyContent.SetActive(isEmpty);

            if (!isEmpty)
            {
                teamNameText.text    = info.teamName;
                recordText.text      = $"{info.wins}W – {info.losses}L";
                seasonPhaseText.text = info.seasonPhase;
                lastSavedText.text   = FormatTimestamp(info.lastSavedAt);
            }

            // Slot is clickable if it has data (for Continue)
            // or always (for New Game, to pick empty or overwrite)
            bool interactable = !isEmpty || allowEmpty;
            slotButton.interactable = interactable;

            if (interactable)
                slotButton.onClick.AddListener(() => _onSelected?.Invoke(_slotIndex));
        }

        private string FormatTimestamp(string iso)
        {
            if (System.DateTime.TryParse(iso, out System.DateTime dt))
                return dt.ToLocalTime().ToString("MMM dd, yyyy  HH:mm");
            return iso;
        }
    }
}
