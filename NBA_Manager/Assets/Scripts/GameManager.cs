using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAManager
{
    // ── Game manager state ───────────────────────────────────────────────────

    public enum ManagerState
    {
        Idle,
        PreGame,              // Matchup preview + lineup setup
        SimulatingInstant,    // Full game simulating in background
        SimulatingLive,       // Possession-by-possession coroutine running
        QuarterBreak,         // Between quarters — lineup adjustment screen
        PostGame,             // Box score + log summary
        DailySummary,         // All results from today
        MidSeasonEvent,       // Trade offer, injury report, etc.
        Offseason,            // Free agency / trades
        Draft,                // Draft board screen
        SeasonComplete        // Champion crowned, prompt new season
    }

    // ── Mid-season event ─────────────────────────────────────────────────────

    public enum MidSeasonEventType
    {
        TradeOffer,
        InjuryReport,
        ContractExpiring,
        PlayerUnhappy,        // Player mood dropped to Unhappy
        FreeAgentAvailable    // Notable free agent hits the market
    }

    [Serializable]
    public class MidSeasonEvent
    {
        public MidSeasonEventType type;
        public string             title;
        public string             description;
        public Team               involvedTeam;
        public Player             involvedPlayer;
        public TradeOffer         tradeOffer;      // filled for TradeOffer events
        public bool               isResolved;
    }

    // ── Day result summary ───────────────────────────────────────────────────

    [Serializable]
    public class DayResult
    {
        public int              day;
        public List<Game>       gamesPlayed   = new List<Game>();
        public List<MidSeasonEvent> events    = new List<MidSeasonEvent>();

        public Game PlayerGame =>
            gamesPlayed.FirstOrDefault(g =>
                g.homeTeam.isPlayerTeam || g.awayTeam.isPlayerTeam);
    }

    // ── GameManager ──────────────────────────────────────────────────────────

    public class GameManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────

        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── References ───────────────────────────────────────────────────────

        [Header("League Data")]
        public Season      currentSeason;
        public List<Team>  allTeams = new List<Team>();

        // ── State ────────────────────────────────────────────────────────────

        [Header("Runtime State")]
        public ManagerState        state          = ManagerState.Idle;
        public int                 currentDay;
        public Game                currentGame;
        public DayResult           currentDayResult;
        public List<MidSeasonEvent> pendingEvents = new List<MidSeasonEvent>();

        // Simulation settings (bound to UI sliders / buttons)
        public SimulationMode simulationMode  = SimulationMode.Instant;
        public float          simulationSpeed = 1f;

        // ── Events (UI subscribes to these) ──────────────────────────────────

        public event Action<ManagerState>    OnStateChanged;
        public event Action<PlayLogEntry>    OnPossessionPlayed;    // live sim feed
        public event Action<Game>            OnGameComplete;
        public event Action<DayResult>       OnDayComplete;
        public event Action<MidSeasonEvent>  OnMidSeasonEvent;
        public event Action<int>             OnQuarterBreak;        // quarter number just ended
        public event Action                  OnSeasonComplete;

        // ── Quarter break state ───────────────────────────────────────────────

        private int  _lastReportedQuarter;
        private bool _waitingForQuarterResume;

        // ── Entry point ───────────────────────────────────────────────────────

        public void StartNewSeason(int year)
        {
            currentSeason = ScriptableObject.CreateInstance<Season>();
            currentSeason.Initialise(year, allTeams);
            currentSeason.AdvancePhase(); // Preseason → RegularSeason + schedule generation
            currentDay = currentSeason.NextScheduledDay;

            pendingEvents.Clear();
            SetState(ManagerState.Idle);
        }

        // ── Main loop — called by UI "Advance Day" button ─────────────────────

        public void AdvanceDay()
        {
            if (state != ManagerState.Idle) return;

            currentDayResult = new DayResult { day = currentDay };

            var gamesThisDay = currentSeason.GetGamesOnDay(currentDay);

            if (gamesThisDay.Count == 0)
            {
                // Rest day — check for mid-season events, then move on
                GenerateMidSeasonEvents();
                DispatchPendingEvents();
                return;
            }

            StartCoroutine(ProcessDay(gamesThisDay));
        }

        // ── Day processing coroutine ──────────────────────────────────────────

        private IEnumerator ProcessDay(List<ScheduledGame> gamesThisDay)
        {
            // Separate player game from AI games
            var playerGame = gamesThisDay.FirstOrDefault(g => g.isPlayerGame);
            var aiGames    = gamesThisDay.Where(g => !g.isPlayerGame).ToList();

            // 1. Simulate all AI games instantly
            foreach (var scheduled in aiGames)
            {
                var game = new Game(scheduled);
                game.SimulateInstant();
                currentSeason.RegisterGameResult(
                    game.Winner, game.Loser,
                    game.HomeScore, game.AwayScore, scheduled);
                currentDayResult.gamesPlayed.Add(game);
                ApplyPostGameEffects(game);
            }

            // 2. Handle player game
            if (playerGame != null)
            {
                // Pre-game screen
                SetState(ManagerState.PreGame);
                yield return new WaitUntil(() => state != ManagerState.PreGame);

                // Run the game
                currentGame = new Game(playerGame);
                currentGame.simulationSpeed = simulationSpeed;

                if (simulationMode == SimulationMode.Instant)
                {
                    SetState(ManagerState.SimulatingInstant);
                    currentGame.SimulateInstant();
                    currentDayResult.gamesPlayed.Add(currentGame);
                    OnGameComplete?.Invoke(currentGame);

                    // Post-game screen
                    SetState(ManagerState.PostGame);
                    yield return new WaitUntil(() => state != ManagerState.PostGame);
                }
                else
                {
                    SetState(ManagerState.SimulatingLive);
                    _lastReportedQuarter = 0;
                    _waitingForQuarterResume = false;

                    bool gameFinished = false;

                    yield return currentGame.SimulatePossessionByPossession(
                        onEvent: entry =>
                        {
                            OnPossessionPlayed?.Invoke(entry);

                            // Quarter break — pause after each quarter ends
                            if (entry.type == PlayLogType.QuarterEnd &&
                                entry.quarter != _lastReportedQuarter &&
                                entry.quarter < 4)
                            {
                                _lastReportedQuarter     = entry.quarter;
                                _waitingForQuarterResume = true;
                                currentGame.isPaused     = true;
                                SetState(ManagerState.QuarterBreak);
                                OnQuarterBreak?.Invoke(entry.quarter);
                            }
                        },
                        onComplete: () => gameFinished = true
                    );

                    // Wait if we're at a quarter break when the coroutine pauses
                    yield return new WaitUntil(() => !_waitingForQuarterResume);
                    yield return new WaitUntil(() => gameFinished);

                    currentDayResult.gamesPlayed.Add(currentGame);
                    OnGameComplete?.Invoke(currentGame);

                    // Post-game screen
                    SetState(ManagerState.PostGame);
                    yield return new WaitUntil(() => state != ManagerState.PostGame);
                }

                currentSeason.RegisterGameResult(
                    currentGame.Winner, currentGame.Loser,
                    currentGame.HomeScore, currentGame.AwayScore, playerGame);

                ApplyPostGameEffects(currentGame);
            }

            // 3. Generate and queue mid-season events
            GenerateMidSeasonEvents();

            // 4. Daily summary
            SetState(ManagerState.DailySummary);
            OnDayComplete?.Invoke(currentDayResult);
            yield return new WaitUntil(() => state != ManagerState.DailySummary);

            // 5. Dispatch any pending mid-season events one at a time
            yield return DispatchPendingEventsCoroutine();

            // 6. Advance to next day
            AdvanceToNextDay();
        }

        // ── Quarter break resume ──────────────────────────────────────────────

        // Called by UI "Resume" button after lineup adjustments
        public void ResumeFromQuarterBreak()
        {
            if (state != ManagerState.QuarterBreak) return;

            _waitingForQuarterResume = false;
            currentGame.isPaused     = false;
            SetState(ManagerState.SimulatingLive);
        }

        // ── Pre/post game confirmation ────────────────────────────────────────

        // Called by UI when the player confirms lineup and is ready to play
        public void ConfirmPreGame()
        {
            if (state != ManagerState.PreGame) return;

            var result = GetPlayerTeam()?.ValidateRotation();
            if (result != null && !result)
            {
                Debug.LogWarning("Rotation invalid:\n" +
                    string.Join("\n", result.errors));
                // UI should surface the errors — don't advance yet
                return;
            }

            SetState(ManagerState.Idle); // coroutine WaitUntil clears
        }

        // Called by UI "Continue" button on post-game screen
        public void ConfirmPostGame()
        {
            if (state != ManagerState.PostGame) return;
            SetState(ManagerState.Idle);
        }

        // Called by UI "Continue" button on daily summary screen
        public void ConfirmDailySummary()
        {
            if (state != ManagerState.DailySummary) return;
            SetState(ManagerState.Idle);
        }

        // ── Mid-season events ─────────────────────────────────────────────────

        private void GenerateMidSeasonEvents()
        {
            var playerTeam = GetPlayerTeam();
            if (playerTeam == null) return;

            // Trade offers from AI teams (~5% chance per day)
            if (UnityEngine.Random.value < 0.05f)
            {
                var aiTeam = allTeams
                    .Where(t => !t.isPlayerTeam)
                    .OrderBy(_ => UnityEngine.Random.value)
                    .FirstOrDefault();

                if (aiTeam != null && aiTeam.roster.Count > 0 && playerTeam.roster.Count > 0)
                {
                    // Pick a random player from each team
                    var offered    = aiTeam.roster[UnityEngine.Random.Range(0, aiTeam.roster.Count)];
                    var requested  = playerTeam.roster[UnityEngine.Random.Range(0, playerTeam.roster.Count)];

                    var offer = new TradeOffer
                    {
                        offeredBy        = aiTeam,
                        targetTeam       = playerTeam,
                        playersOffered   = new List<Player> { offered },
                        playersRequested = new List<Player> { requested },
                        aiEvalScore      = aiTeam.EvaluatePlayerValue(offered)
                                         - aiTeam.EvaluatePlayerValue(requested)
                    };

                    QueueMidSeasonEvent(new MidSeasonEvent
                    {
                        type           = MidSeasonEventType.TradeOffer,
                        title          = "Trade Offer",
                        description    = $"{aiTeam.FullName} wants {requested.FullName} " +
                                         $"for {offered.FullName}",
                        involvedTeam   = aiTeam,
                        involvedPlayer = offered,
                        tradeOffer     = offer
                    });
                }
            }

            // Injury reports — already generated during game simulation,
            // but surface any newly injured players on the player's roster
            foreach (var player in playerTeam.roster.Where(p => p.isInjured))
            {
                bool alreadyQueued = pendingEvents.Any(e =>
                    e.type == MidSeasonEventType.InjuryReport &&
                    e.involvedPlayer == player &&
                    !e.isResolved);

                if (!alreadyQueued)
                {
                    QueueMidSeasonEvent(new MidSeasonEvent
                    {
                        type           = MidSeasonEventType.InjuryReport,
                        title          = "Injury Update",
                        description    = $"{player.FullName} is out for " +
                                         $"{player.injuryGamesRemaining} game(s).",
                        involvedTeam   = playerTeam,
                        involvedPlayer = player
                    });
                }
            }

            // Unhappy player alerts
            foreach (var player in playerTeam.roster.Where(p => p.mood == PlayerMood.Unhappy))
            {
                bool alreadyQueued = pendingEvents.Any(e =>
                    e.type == MidSeasonEventType.PlayerUnhappy &&
                    e.involvedPlayer == player &&
                    !e.isResolved);

                if (!alreadyQueued)
                {
                    QueueMidSeasonEvent(new MidSeasonEvent
                    {
                        type           = MidSeasonEventType.PlayerUnhappy,
                        title          = "Player Unhappy",
                        description    = $"{player.FullName} is unhappy. " +
                                         $"Consider adjusting minutes or finding a trade.",
                        involvedTeam   = playerTeam,
                        involvedPlayer = player
                    });
                }
            }

            // Expiring contracts (warn with 10 days left in season)
            int gamesLeft = currentSeason.regularSeasonSchedule.Count(g => !g.isPlayed);
            if (gamesLeft <= 10)
            {
                foreach (var player in playerTeam.roster
                    .Where(p => p.contract.yearsRemaining == 1))
                {
                    bool alreadyQueued = pendingEvents.Any(e =>
                        e.type == MidSeasonEventType.ContractExpiring &&
                        e.involvedPlayer == player);

                    if (!alreadyQueued)
                    {
                        QueueMidSeasonEvent(new MidSeasonEvent
                        {
                            type           = MidSeasonEventType.ContractExpiring,
                            title          = "Contract Expiring",
                            description    = $"{player.FullName}'s contract expires " +
                                             $"at the end of this season.",
                            involvedTeam   = playerTeam,
                            involvedPlayer = player
                        });
                    }
                }
            }
        }

        private void QueueMidSeasonEvent(MidSeasonEvent ev)
        {
            pendingEvents.Add(ev);
            currentDayResult?.events.Add(ev);
        }

        private void DispatchPendingEvents()
        {
            foreach (var ev in pendingEvents.Where(e => !e.isResolved))
                OnMidSeasonEvent?.Invoke(ev);
        }

        private IEnumerator DispatchPendingEventsCoroutine()
        {
            var unresolved = pendingEvents.Where(e => !e.isResolved).ToList();

            foreach (var ev in unresolved)
            {
                SetState(ManagerState.MidSeasonEvent);
                OnMidSeasonEvent?.Invoke(ev);
                yield return new WaitUntil(() => ev.isResolved || state != ManagerState.MidSeasonEvent);
            }
        }

        // Called by UI after player dismisses or responds to a mid-season event
        public void ResolveMidSeasonEvent(MidSeasonEvent ev)
        {
            ev.isResolved = true;
            pendingEvents.Remove(ev);
            SetState(ManagerState.Idle);
        }

        // ── Trade resolution ──────────────────────────────────────────────────

        public bool AcceptTrade(TradeOffer offer)
        {
            var playerTeam = GetPlayerTeam();
            if (playerTeam == null) return false;

            // Validate both sides have the players
            if (!offer.playersOffered.All(p => offer.offeredBy.roster.Contains(p)))  return false;
            if (!offer.playersRequested.All(p => playerTeam.roster.Contains(p)))      return false;

            // Salary check (simplified — both teams just need to be under cap after trade
            // or were already over cap)
            float incomingSalary  = offer.playersOffered.Sum(p => p.contract.annualSalary);
            float outgoingSalary  = offer.playersRequested.Sum(p => p.contract.annualSalary);

            // Execute trade
            foreach (var p in offer.playersOffered)
            {
                offer.offeredBy.roster.Remove(p);
                playerTeam.roster.Add(p);
                playerTeam.rotation.Add(new PlayerLineupSlot(p, 0, false));
            }

            foreach (var p in offer.playersRequested)
            {
                playerTeam.roster.Remove(p);
                playerTeam.rotation.RemoveAll(s => s.player == p);
                offer.offeredBy.roster.Add(p);
            }

            playerTeam.finances.RecalculateCapUsed(playerTeam.roster);
            offer.offeredBy.finances.RecalculateCapUsed(offer.offeredBy.roster);

            Debug.Log($"Trade accepted: {string.Join(", ", offer.playersOffered.Select(p => p.FullName))} " +
                      $"→ {playerTeam.FullName}");
            return true;
        }

        public void DeclineTrade(TradeOffer offer)
        {
            Debug.Log($"Trade declined: {offer.offeredBy.FullName}'s offer.");
        }

        // ── Post-game effects ─────────────────────────────────────────────────

        private void ApplyPostGameEffects(Game game)
        {
            bool homeWon = game.HomeScore > game.AwayScore;

            // Morale shifts
            game.homeTeam.ApplyMoraleShiftWithCoach(homeWon ?  1 : -1);
            game.awayTeam.ApplyMoraleShiftWithCoach(homeWon ? -1 :  1);

            // Tick injury recovery for all players (one game passed)
            foreach (var team in new[] { game.homeTeam, game.awayTeam })
                foreach (var player in team.roster)
                    player.TickInjury();

            // AI teams auto-adjust rotation if someone fouled out or got injured
            foreach (var team in new[] { game.homeTeam, game.awayTeam })
                if (!team.isPlayerTeam && team.roster.Any(p => p.isInjured))
                    team.AutoSetRotation();
        }

        // ── Phase transitions ─────────────────────────────────────────────────

        private void AdvanceToNextDay()
        {
            // Check if all regular season games are done
            if (currentSeason.currentPhase == SeasonPhase.RegularSeason &&
                currentSeason.regularSeasonSchedule.All(g => g.isPlayed))
            {
                currentSeason.AdvancePhase(); // → PlayIn
                HandlePlayIn();
                return;
            }

            // Check playoffs done
            if (currentSeason.currentPhase == SeasonPhase.Playoffs &&
                currentSeason.champion != null)
            {
                SetState(ManagerState.SeasonComplete);
                OnSeasonComplete?.Invoke();
                return;
            }

            // Check if play-in is done
            if (currentSeason.currentPhase == SeasonPhase.PlayIn &&
                currentSeason.playInSchedule.All(g => g.isPlayed))
            {
                currentSeason.AdvancePhase(); // → Playoffs
                currentDay = currentSeason.NextScheduledDay;
                SetState(ManagerState.Idle);
                return;
            }

            currentDay = currentSeason.NextScheduledDay;
            SetState(ManagerState.Idle);
        }

        private void HandlePlayIn()
        {
            // Simulate play-in games day by day (same loop as regular season)
            // Play-in games are already in currentSeason.playInSchedule
            currentDay = currentSeason.playInSchedule
                .OrderBy(g => g.dayOfSeason)
                .FirstOrDefault()?.dayOfSeason ?? currentDay + 1;

            SetState(ManagerState.Idle);
        }

        // Called by UI after champion screen — starts new season
        public void StartNextSeason()
        {
            if (state != ManagerState.SeasonComplete) return;

            // Apply end-of-season progression to all teams
            foreach (var team in allTeams)
                team.ApplyEndOfSeason();

            StartNewSeason(currentSeason.year + 1);
        }

        // ── Offseason / Draft (called by UI navigation) ───────────────────────

        public void EnterOffseason()
        {
            currentSeason.AdvancePhase(); // → Offseason
            SetState(ManagerState.Offseason);
        }

        public void EnterDraft()
        {
            currentSeason.AdvancePhase(); // → Draft
            SetState(ManagerState.Draft);
        }

        // Player makes a draft pick
        public bool MakePlayerDraftPick(int pickOverall, Player prospect)
        {
            if (state != ManagerState.Draft) return false;

            var pick = currentSeason.currentDraft.pickOrder
                .FirstOrDefault(p => p.overall == pickOverall && !p.isSelected);

            if (pick == null) return false;

            bool success = currentSeason.MakeDraftPick(pick, prospect);

            // If draft is complete, advance phase
            if (currentSeason.currentDraft.IsComplete)
                SetState(ManagerState.SeasonComplete);

            return success;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        public Team GetPlayerTeam() =>
            allTeams.FirstOrDefault(t => t.isPlayerTeam);

        public List<StandingsEntry> GetPlayerConferenceStandings()
        {
            var team = GetPlayerTeam();
            if (team == null) return new List<StandingsEntry>();

            return team.conference == Conference.East
                ? currentSeason.EastSorted
                : currentSeason.WestSorted;
        }

        public List<ScheduledGame> GetUpcomingPlayerGames(int count = 5) =>
            currentSeason.PlayerTeamSchedule
                .Where(g => !g.isPlayed)
                .Take(count)
                .ToList();

        private void SetState(ManagerState newState)
        {
            state = newState;
            OnStateChanged?.Invoke(newState);
        }

        // ── Simulation controls (bound to UI buttons) ─────────────────────────

        public void SetSimulationMode(SimulationMode mode)   => simulationMode  = mode;
        public void SetSimulationSpeed(float speed)           => simulationSpeed = Mathf.Clamp(speed, 0.25f, 4f);
        public void PauseSimulation()  { if (currentGame != null) currentGame.isPaused = true;  }
        public void ResumeSimulation() { if (currentGame != null) currentGame.isPaused = false; }
    }
}