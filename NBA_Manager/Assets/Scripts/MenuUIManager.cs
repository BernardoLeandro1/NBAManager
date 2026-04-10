using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NBAManager
{
    // ── Menu UI Manager ───────────────────────────────────────────────────────
    // Lives in the Menu scene. Controls three panels:
    //   1. MainMenuPanel     — title screen with New Game / Continue / Quit
    //   2. SaveSlotPanel     — slot picker (reused for both new and continue)
    //   3. NewGameSetupPanel — team selection before generating the league

    public class MenuUIManager : MonoBehaviour
    {
        // ── Panel references ──────────────────────────────────────────────────

        [Header("Panels")]
        public GameObject mainMenuPanel;
        public GameObject saveSlotPanel;
        public GameObject newGameSetupPanel;

        // ── Main menu ─────────────────────────────────────────────────────────

        [Header("Main Menu")]
        public TextMeshProUGUI gameTitleText;
        public Button          newGameButton;
        public Button          continueButton;
        public Button          quitButton;

        // ── Save slot panel ───────────────────────────────────────────────────

        [Header("Save Slot Panel")]
        public TextMeshProUGUI    slotPanelTitle;   // "Select a slot" or "Load game"
        public SaveSlotUI[]       slotUIs;          // assign 3 SaveSlotUI components

        // ── New game setup ────────────────────────────────────────────────────

        [Header("New Game Setup")]
        public TextMeshProUGUI    setupTitleText;
        public TMP_Dropdown       conferenceFilter; // East / West / All
        public Transform          teamListContent;  // ScrollView content parent
        public GameObject         teamListItemPrefab;
        public TextMeshProUGUI    selectedTeamName;
        public TextMeshProUGUI    selectedTeamCity;
        public TextMeshProUGUI    selectedTeamConference;
        public Image              selectedTeamPrimaryColor;
        public Image              selectedTeamSecondaryColor;
        public Button             confirmTeamButton;
        public Button             backToSlotButton;

        // ── State ─────────────────────────────────────────────────────────────

        private bool   _isNewGame;
        private int    _selectedSlot = -1;
        private string _selectedTeamId;
        private SaveSlotInfo[] _slotInfos;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            // Wire main menu buttons
            newGameButton.onClick.AddListener(OnNewGameClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
            quitButton.onClick.AddListener(OnQuitClicked);
            confirmTeamButton.onClick.AddListener(OnConfirmTeamClicked);
            backToSlotButton.onClick.AddListener(() => ShowPanel(saveSlotPanel));
            conferenceFilter.onValueChanged.AddListener(_ => PopulateTeamList());

            // Continue is only interactable if at least one slot has a save
            _slotInfos = SaveSystem.GetAllSlotInfos();
            continueButton.interactable = HasAnySave();

            ShowPanel(mainMenuPanel);
        }

        // ── Panel switching ───────────────────────────────────────────────────

        private void ShowPanel(GameObject panel)
        {
            mainMenuPanel.SetActive(false);
            saveSlotPanel.SetActive(false);
            newGameSetupPanel.SetActive(false);
            panel.SetActive(true);
        }

        // ── Main menu callbacks ───────────────────────────────────────────────

        private void OnNewGameClicked()
        {
            _isNewGame = true;
            slotPanelTitle.text = "Choose a Save Slot";
            RefreshSlotUIs(showEmpty: true);
            ShowPanel(saveSlotPanel);
        }

        private void OnContinueClicked()
        {
            _isNewGame = false;
            slotPanelTitle.text = "Load Game";
            RefreshSlotUIs(showEmpty: false);
            ShowPanel(saveSlotPanel);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── Save slot panel ───────────────────────────────────────────────────

        private void RefreshSlotUIs(bool showEmpty)
        {
            _slotInfos = SaveSystem.GetAllSlotInfos();

            for (int i = 0; i < slotUIs.Length; i++)
            {
                var info = i < _slotInfos.Length ? _slotInfos[i] : null;
                slotUIs[i].Populate(info, i, showEmpty, OnSlotSelected);
            }
        }

        // Called by SaveSlotUI when a slot is clicked
        private void OnSlotSelected(int slotIndex)
        {
            _selectedSlot = slotIndex;

            if (_isNewGame)
            {
                // If slot has existing save, confirm overwrite
                if (_slotInfos[slotIndex] != null && !_slotInfos[slotIndex].isEmpty)
                {
                    ConfirmOverwrite(slotIndex);
                }
                else
                {
                    // Empty slot — go straight to team selection
                    ShowNewGameSetup();
                }
            }
            else
            {
                // Load existing save
                LoadGame(slotIndex);
            }
        }

        private void ConfirmOverwrite(int slotIndex)
        {
            // For now just overwrite — you can add a confirmation dialog later
            Debug.Log($"Overwriting slot {slotIndex}");
            ShowNewGameSetup();
        }

        // ── New game setup ────────────────────────────────────────────────────

        private void ShowNewGameSetup()
        {
            PopulateTeamList();
            confirmTeamButton.interactable = false;
            selectedTeamName.text       = "—";
            selectedTeamCity.text       = "";
            selectedTeamConference.text = "";
            ShowPanel(newGameSetupPanel);
        }

        private void PopulateTeamList()
        {
            // Clear existing items
            foreach (Transform child in teamListContent)
                Destroy(child.gameObject);

            // Filter by conference dropdown (0 = All, 1 = East, 2 = West)
            int filter = conferenceFilter.value;
            var teams  = LeagueData.DefaultTeams;

            foreach (var template in teams)
            {
                if (filter == 1 && template.conference != Conference.East) continue;
                if (filter == 2 && template.conference != Conference.West) continue;

                var item = Instantiate(teamListItemPrefab, teamListContent);
                var ui   = item.GetComponent<TeamListItemUI>();
                if (ui != null)
                    ui.Populate(template, OnTeamSelected);
            }
        }

        private void OnTeamSelected(string teamId)
        {
            _selectedTeamId = teamId;

            var template = LeagueData.DefaultTeams
                .Find(t => t.teamId == teamId);

            if (template == null) return;

            selectedTeamName.text       = template.teamName;
            selectedTeamCity.text       = template.city;
            selectedTeamConference.text = $"{template.conference} — {template.division}";
            selectedTeamPrimaryColor.color   = template.primaryColor;
            selectedTeamSecondaryColor.color = template.secondaryColor;
            confirmTeamButton.interactable   = true;
        }

        private void OnConfirmTeamClicked()
        {
            if (string.IsNullOrEmpty(_selectedTeamId) || _selectedSlot < 0) return;

            // Generate the league
            var teams = LeagueData.InitialiseLeague();

            // Mark the selected team as the player's team
            var playerTeam = teams.Find(t => t.teamId == _selectedTeamId);
            if (playerTeam != null) playerTeam.isPlayerTeam = true;

            // Hand off to GameManager
            GameManager.Instance.allTeams = teams;
            GameManager.Instance.StartNewSeason(System.DateTime.Now.Year);

            // Auto-save to selected slot
            SaveSystem.Save(_selectedSlot, GameManager.Instance);

            // Load hub scene
            SceneLoader.Instance.LoadHubScene();
        }

        // ── Load game ─────────────────────────────────────────────────────────

        private void LoadGame(int slotIndex)
        {
            bool success = SaveSystem.Load(slotIndex, GameManager.Instance);

            if (success)
                SceneLoader.Instance.LoadHubScene();
            else
                Debug.LogError($"Failed to load slot {slotIndex}.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private bool HasAnySave()
        {
            foreach (var info in _slotInfos)
                if (info != null && !info.isEmpty) return true;
            return false;
        }
    }

}