using UnityEngine;

namespace NBAManager
{
    // ── Hub panel IDs ─────────────────────────────────────────────────────────

    public enum HubPanel
    {
        MainHub,
        Roster,
        PreGame,
        Trade,
        Draft,
        Standings,
        PlayerDetail,
        CoachSelect,
        SaveLoad
    }

    // ── Hub UI manager ────────────────────────────────────────────────────────
    // Lives in the Hub scene. Assign each panel's root GameObject
    // in the Inspector. Call ShowPanel() from any UI button.

    public class HubUIManager : MonoBehaviour
    {
        public static HubUIManager Instance { get; private set; }

        [Header("Panels — assign root GameObjects in Inspector")]
        public GameObject mainHubPanel;
        public GameObject rosterPanel;
        public GameObject preGamePanel;
        public GameObject tradePanel;
        public GameObject draftPanel;
        public GameObject standingsPanel;
        public GameObject playerDetailPanel;
        public GameObject coachSelectPanel;
        public GameObject saveLoadPanel;

        // Track history for a simple back-button
        private HubPanel _currentPanel;
        private HubPanel _previousPanel;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // Subscribe to GameManager state changes
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged += HandleManagerStateChange;

            ShowPanel(HubPanel.MainHub);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= HandleManagerStateChange;
        }

        // ── Panel navigation ──────────────────────────────────────────────────

        public void ShowPanel(HubPanel panel)
        {
            _previousPanel = _currentPanel;
            _currentPanel  = panel;

            // Hide all
            SetActive(mainHubPanel,      false);
            SetActive(rosterPanel,       false);
            SetActive(preGamePanel,      false);
            SetActive(tradePanel,        false);
            SetActive(draftPanel,        false);
            SetActive(standingsPanel,    false);
            SetActive(playerDetailPanel, false);
            SetActive(coachSelectPanel,  false);
            SetActive(saveLoadPanel,     false);

            // Show requested
            switch (panel)
            {
                case HubPanel.MainHub:      SetActive(mainHubPanel,      true); break;
                case HubPanel.Roster:       SetActive(rosterPanel,       true); break;
                case HubPanel.PreGame:      SetActive(preGamePanel,      true); break;
                case HubPanel.Trade:        SetActive(tradePanel,        true); break;
                case HubPanel.Draft:        SetActive(draftPanel,        true); break;
                case HubPanel.Standings:    SetActive(standingsPanel,    true); break;
                case HubPanel.PlayerDetail: SetActive(playerDetailPanel, true); break;
                case HubPanel.CoachSelect:  SetActive(coachSelectPanel,  true); break;
                case HubPanel.SaveLoad:     SetActive(saveLoadPanel,     true); break;
            }
        }

        public void GoBack() => ShowPanel(_previousPanel);

        // ── Button callbacks (wire these to UI buttons in the Inspector) ──────

        public void OnMainHubButton()   => ShowPanel(HubPanel.MainHub);
        public void OnRosterButton()    => ShowPanel(HubPanel.Roster);
        public void OnTradeButton()     => ShowPanel(HubPanel.Trade);
        public void OnStandingsButton() => ShowPanel(HubPanel.Standings);
        public void OnSaveLoadButton()  => ShowPanel(HubPanel.SaveLoad);
        public void OnBackButton()      => GoBack();

        // ── GameManager state listener ────────────────────────────────────────
        // Automatically switches panels when GameManager state changes

        private void HandleManagerStateChange(ManagerState state)
        {
            switch (state)
            {
                case ManagerState.PreGame:
                    ShowPanel(HubPanel.PreGame);
                    break;
                case ManagerState.Draft:
                    ShowPanel(HubPanel.Draft);
                    break;
                case ManagerState.MidSeasonEvent:
                    // Mid-season events show as overlays on the current panel
                    // handled by MidSeasonEventOverlay separately
                    break;
                case ManagerState.Idle:
                    ShowPanel(HubPanel.MainHub);
                    break;
            }
        }

        // ── Utility ───────────────────────────────────────────────────────────

        private void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }
    }
}