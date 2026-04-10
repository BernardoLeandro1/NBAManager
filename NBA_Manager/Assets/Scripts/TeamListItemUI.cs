using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────
// TeamListItemUI — attach to the team list item prefab in the scroll view.
// ─────────────────────────────────────────────────────────────────────────
namespace NBAManager
{
    public class TeamListItemUI : MonoBehaviour
    {
        [Header("UI References")]
        public Image              primaryColorBar;
        public TextMeshProUGUI    cityText;
        public TextMeshProUGUI    teamNameText;
        public TextMeshProUGUI    conferenceText;
        public Button             selectButton;

        private string                   _teamId;
        private System.Action<string>    _onSelected;

        public void Populate(TeamTemplate template, System.Action<string> onSelected)
        {
            _teamId     = template.teamId;
            _onSelected = onSelected;

            primaryColorBar.color = template.primaryColor;
            cityText.text         = template.city;
            teamNameText.text     = template.teamName;
            conferenceText.text   = $"{template.conference}";

            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => _onSelected?.Invoke(_teamId));
        }
    }
}
