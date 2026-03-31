using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAManager
{
    // ── Shot type ────────────────────────────────────────────────────────────

    public enum ShotType
    {
        AtRim,       // Layup, dunk — high percentage, finishing-dependent
        MidRange,    // Mid-range jumper
        ThreePoint,  // Three-point attempt
        FreeThrow    // From the line
    }

    // ── Possession outcome ───────────────────────────────────────────────────

    public enum PossessionOutcome
    {
        MadeShot,
        MissedShot,
        Turnover,
        FoulDrawn,
        TechnicalFoul
    }

    // ── Simulation mode ──────────────────────────────────────────────────────

    public enum SimulationMode
    {
        Instant,            // Full game simulated at once, no coroutine
        PossessionByPossession  // Stepped simulation, speed controllable
    }

    // ── Game ─────────────────────────────────────────────────────────────────

    [Serializable]
    public class Game
    {
        // ── Match info ───────────────────────────────────────────────────────

        public string        gameId;
        public Team          homeTeam;
        public Team          awayTeam;
        public ScheduledGame scheduledGame;
        public bool          isComplete;

        // ── Score ────────────────────────────────────────────────────────────

        public int[] homeQuarterScores = new int[4];  // index 0 = Q1
        public int[] awayQuarterScores = new int[4];
        public int   homeOvertimeScore;
        public int   awayOvertimeScore;
        public bool  wentToOvertime;

        public int HomeScore => homeQuarterScores.Sum() + homeOvertimeScore;
        public int AwayScore => awayQuarterScores.Sum() + awayOvertimeScore;
        public Team Winner   => HomeScore >= AwayScore ? homeTeam : awayTeam;
        public Team Loser    => HomeScore <  AwayScore ? homeTeam : awayTeam;

        // ── Stats ────────────────────────────────────────────────────────────

        public List<PlayerGameStats> homeStats = new List<PlayerGameStats>();
        public List<PlayerGameStats> awayStats = new List<PlayerGameStats>();

        public List<PlayerGameStats> AllStats =>
            homeStats.Concat(awayStats).ToList();

        public PlayerGameStats GetStats(Player p) =>
            AllStats.FirstOrDefault(s => s.player == p);

        // ── Play log ─────────────────────────────────────────────────────────

        public List<PlayLogEntry> playLog = new List<PlayLogEntry>();

        // ── Simulation state (not serialized — runtime only) ─────────────────

        [NonSerialized] public SimulationMode mode = SimulationMode.Instant;
        [NonSerialized] public float          simulationSpeed = 1f; // 0.25x to 4x
        [NonSerialized] public bool           isPaused;

        // Runtime state
        [NonSerialized] private int   _currentQuarter;   // 1–4 (5 = OT)
        [NonSerialized] private float _gameClock;        // seconds remaining in quarter
        [NonSerialized] private int   _homeScore;
        [NonSerialized] private int   _awayScore;
        [NonSerialized] private float _homeMomentum;     // -1 to 1, positive = home on a run
        [NonSerialized] private float _awayMomentum;
        [NonSerialized] private int   _consecutiveHomePoints;
        [NonSerialized] private int   _consecutiveAwayPoints;

        // Active rosters for this game (excludes injured / fouled out players)
        [NonSerialized] private List<PlayerGameStats> _homeActive;
        [NonSerialized] private List<PlayerGameStats> _awayActive;

        // Minutes budget remaining per player this game
        [NonSerialized] private Dictionary<Player, float> _minutesRemaining;

        // Constants
        private const float QuarterLength      = 720f;  // 12 minutes in seconds
        private const float OvertimeLength     = 300f;  // 5 minutes
        private const int   FoulOutLimit       = 6;
        private const float InjuryBaseChance   = 0.002f; // per possession
        private const float TechFoulBaseChance = 0.008f; // per possession
        private const int   MomentumRunThreshold = 8;    // points before "run" is logged

        // ── Initialise ───────────────────────────────────────────────────────

        public Game(ScheduledGame scheduled)
        {
            gameId        = Guid.NewGuid().ToString();
            homeTeam      = scheduled.homeTeam;
            awayTeam      = scheduled.awayTeam;
            scheduledGame = scheduled;
            isComplete    = false;
        }

        private void Initialise()
        {
            _currentQuarter          = 1;
            _gameClock               = QuarterLength;
            _homeScore               = 0;
            _awayScore               = 0;
            _homeMomentum            = 0f;
            _awayMomentum            = 0f;
            _consecutiveHomePoints   = 0;
            _consecutiveAwayPoints   = 0;

            homeQuarterScores        = new int[4];
            awayQuarterScores        = new int[4];
            homeOvertimeScore        = 0;
            awayOvertimeScore        = 0;
            wentToOvertime           = false;
            playLog.Clear();

            // Build active player lists from rotation
            _minutesRemaining = new Dictionary<Player, float>();
            homeStats.Clear();
            awayStats.Clear();

            InitTeamStats(homeTeam, homeStats);
            InitTeamStats(awayTeam, awayStats);

            _homeActive = homeStats.Where(s => !s.didNotPlay).ToList();
            _awayActive = awayStats.Where(s => !s.didNotPlay).ToList();
        }

        private void InitTeamStats(Team team, List<PlayerGameStats> statsList)
        {
            foreach (var slot in team.rotation)
            {
                var stats = new PlayerGameStats(slot.player, team);

                if (slot.player.isInjured || slot.minutesPerGame == 0)
                {
                    stats.didNotPlay = true;
                    statsList.Add(stats);
                    continue;
                }

                _minutesRemaining[slot.player] = slot.minutesPerGame * 60f; // convert to seconds
                statsList.Add(stats);
            }
        }

        // ── Public entry points ───────────────────────────────────────────────

        // Instant mode: simulate the entire game synchronously
        public void SimulateInstant()
        {
            mode = SimulationMode.Instant;
            Initialise();

            // Calculate possessions per team based on pace
            int homePossessions = CalculatePace(homeTeam);
            int awayPossessions = CalculatePace(awayTeam);
            int totalPossessions = (homePossessions + awayPossessions);

            // Simulate quarter by quarter
            for (int q = 1; q <= 4; q++)
            {
                _currentQuarter = q;
                _gameClock      = QuarterLength;
                int possPerQuarter = totalPossessions / 4;

                SimulateQuarter(possPerQuarter, q);

                LogQuarterEnd(q);
            }

            // Overtime if tied
            if (_homeScore == _awayScore)
                SimulateOvertime();

            Finalise();
        }

        // Possession-by-possession mode: returns a coroutine to run in Unity
        public IEnumerator SimulatePossessionByPossession(Action<PlayLogEntry> onEvent,
                                                           Action onComplete)
        {
            mode = SimulationMode.PossessionByPossession;
            Initialise();

            int homePossessions  = CalculatePace(homeTeam);
            int awayPossessions  = CalculatePace(awayTeam);
            int totalPossessions = homePossessions + awayPossessions;
            int possPerQuarter   = totalPossessions / 4;

            for (int q = 1; q <= 4; q++)
            {
                _currentQuarter = q;
                _gameClock      = QuarterLength;

                bool homePossession = UnityEngine.Random.value > 0.5f; // tip-off sim

                for (int p = 0; p < possPerQuarter; p++)
                {
                    if (isPaused) yield return new WaitUntil(() => !isPaused);

                    SimulateSinglePossession(
                        homePossession ? homeTeam : awayTeam,
                        homePossession ? awayTeam : homeTeam,
                        homePossession ? _homeActive : _awayActive,
                        homePossession ? _awayActive : _homeActive
                    );

                    // Advance clock
                    float possessionTime = UnityEngine.Random.Range(8f, 24f);
                    _gameClock = Mathf.Max(0f, _gameClock - possessionTime);

                    // Fire latest log entry to UI
                    if (playLog.Count > 0)
                        onEvent?.Invoke(playLog[playLog.Count - 1]);

                    homePossession = !homePossession;

                    // Wait based on simulation speed (1f = real time compressed, 0.25f = slow)
                    yield return new WaitForSeconds(0.05f / simulationSpeed);
                }

                LogQuarterEnd(q);
                onEvent?.Invoke(playLog[playLog.Count - 1]);

                yield return new WaitForSeconds(0.3f / simulationSpeed);
            }

            if (_homeScore == _awayScore)
            {
                yield return SimulateOvertimeCoroutine(onEvent);
            }

            Finalise();
            onComplete?.Invoke();
        }

        // ── Pace calculation ─────────────────────────────────────────────────

        // Base pace ~90 possessions, modified by FastBreak and PackThePaint priorities
        private int CalculatePace(Team team)
        {
            float basePace = 90f;

            // FastBreak at rank 1 adds up to +8 possessions, rank 5 subtracts up to -4
            float fastBreakWeight = team.offensiveTactics.GetPlayWeight(OffensivePlay.FastBreak);
            basePace += Mathf.Lerp(-4f, 8f, fastBreakWeight / 0.35f);

            // Pack the Paint defense slows the game down
            if (team.defensiveTactics.activeScheme == DefensiveScheme.PackThePaint)
                basePace -= 4f;

            // Press defense can increase pace (more live-ball turnovers)
            if (team.defensiveTactics.activeScheme == DefensiveScheme.PressDefense)
                basePace += 3f;

            return Mathf.RoundToInt(basePace + UnityEngine.Random.Range(-3f, 3f));
        }

        // ── Quarter simulation (instant mode) ────────────────────────────────

        private void SimulateQuarter(int possessions, int quarter)
        {
            bool homePossession = quarter % 2 != 0; // home starts Q1 and Q3

            for (int p = 0; p < possessions; p++)
            {
                var offTeam    = homePossession ? homeTeam    : awayTeam;
                var defTeam    = homePossession ? awayTeam    : homeTeam;
                var offActive  = homePossession ? _homeActive : _awayActive;
                var defActive  = homePossession ? _awayActive : _homeActive;

                SimulateSinglePossession(offTeam, defTeam, offActive, defActive);

                float possessionTime = UnityEngine.Random.Range(8f, 24f);
                _gameClock = Mathf.Max(0f, _gameClock - possessionTime);

                homePossession = !homePossession;
            }

            // Commit quarter scores
            if (quarter <= 4)
            {
                homeQuarterScores[quarter - 1] = _homeScore - homeQuarterScores.Take(quarter - 1).Sum();
                awayQuarterScores[quarter - 1] = _awayScore - awayQuarterScores.Take(quarter - 1).Sum();
            }
        }

        // ── Single possession ─────────────────────────────────────────────────

        private void SimulateSinglePossession(
            Team offTeam, Team defTeam,
            List<PlayerGameStats> offActive, List<PlayerGameStats> defActive)
        {
            if (offActive.Count == 0) return;

            // Pick ball handler weighted by minutes and playmaking
            var ballHandler = PickPlayerWeighted(offActive,
                s => s.player.ratings.PlaymakingRating * (s.player.GetGamePerformanceMultiplier()));

            // Pick shot taker based on offensive play priorities
            var shooter = PickShooter(offTeam, offActive, ballHandler);
            if (shooter == null) return;

            // Pick defender
            var defender = PickDefender(defActive, shooter);

            // Check for turnover first
            float turnoverChance = CalculateTurnoverChance(offTeam, defTeam, ballHandler, defActive);
            if (UnityEngine.Random.value < turnoverChance)
            {
                ballHandler.turnovers++;
                if (defender != null) defender.steals++;
                UpdateMomentum(defTeam, 0);
                CheckTechnicalFoul(offTeam, offActive);
                return;
            }

            // Determine shot type from tactics
            ShotType shotType = DetermineShotType(offTeam, shooter);

            // Calculate shot probability
            float makeChance = CalculateMakeChance(shotType, shooter, defender, offTeam, defTeam);

            // Foul check — happens before shot or on the shot
            float foulChance = CalculateFoulChance(shotType, shooter, defender, defTeam);
            bool  foulDrawn  = UnityEngine.Random.value < foulChance;

            if (foulDrawn && defender != null)
            {
                HandleFoul(defender, defTeam, defActive, shooter, offTeam, shotType, makeChance);
                return;
            }

            // Simulate the shot
            bool madeShot = UnityEngine.Random.value < makeChance;
            shooter.AddFieldGoalAttempt(shotType == ShotType.ThreePoint, madeShot);

            if (madeShot)
            {
                // Assist: credited if a pass directly led to the shot
                float assistChance = CalculateAssistChance(offTeam, ballHandler, shooter);
                if (UnityEngine.Random.value < assistChance && ballHandler != shooter)
                    ballHandler.assists++;

                int pts = shotType == ShotType.ThreePoint ? 3 : 2;
                AddScore(offTeam, pts, shooter);
                UpdateMomentum(offTeam, pts);
            }
            else
            {
                // Rebound
                HandleRebound(offTeam, defTeam, offActive, defActive);
                UpdateMomentum(defTeam, 0);
            }

            // Injury check (rare)
            CheckGameInjury(shooter, offTeam, offActive);

            // Technical foul check (very rare)
            CheckTechnicalFoul(offTeam, offActive);

            // Fatigue — reduce minutes remaining
            float timeUsed = UnityEngine.Random.Range(8f, 24f);
            if (_minutesRemaining.ContainsKey(shooter.player))
            {
                _minutesRemaining[shooter.player] -= timeUsed;
                shooter.minutesPlayed             += timeUsed / 60f;

                if (_minutesRemaining[shooter.player] <= 0)
                    SubstituteOut(shooter, offTeam, offActive);
            }
        }

        // ── Player selection helpers ──────────────────────────────────────────

        // Weighted random player selection
        private PlayerGameStats PickPlayerWeighted(
            List<PlayerGameStats> active,
            Func<PlayerGameStats, float> weightFn)
        {
            if (active.Count == 0) return null;

            float total = active.Sum(weightFn);
            if (total <= 0f) return active[UnityEngine.Random.Range(0, active.Count)];

            float roll = UnityEngine.Random.Range(0f, total);
            float cumulative = 0f;

            foreach (var s in active)
            {
                cumulative += weightFn(s);
                if (roll <= cumulative) return s;
            }

            return active[active.Count - 1];
        }

        // Picks the shooter based on offensive play priorities
        private PlayerGameStats PickShooter(
            Team offTeam,
            List<PlayerGameStats> offActive,
            PlayerGameStats ballHandler)
        {
            var plays = offTeam.offensiveTactics.GetPriorityOrder();
            var topPlay = plays[0];

            switch (topPlay)
            {
                case OffensivePlay.Isolation:
                    // Best scorer gets the ball
                    return PickPlayerWeighted(offActive,
                        s => s.player.ratings.ScoringRating * s.player.GetGamePerformanceMultiplier());

                case OffensivePlay.PickAndRoll:
                    // Ball handler or best finishing big
                    return UnityEngine.Random.value > 0.5f
                        ? ballHandler
                        : PickPlayerWeighted(offActive.Where(s =>
                            s.player.position == Position.Center ||
                            s.player.position == Position.PowerForward).ToList(),
                            s => s.player.ratings.finishing);

                case OffensivePlay.SpacingAndCuts:
                    // Best three-point shooter gets the look
                    return PickPlayerWeighted(offActive,
                        s => s.player.ratings.threePoint * s.player.GetGamePerformanceMultiplier());

                case OffensivePlay.PostUp:
                    // Best big man
                    return PickPlayerWeighted(
                        offActive.Where(s =>
                            s.player.position == Position.Center ||
                            s.player.position == Position.PowerForward).ToList(),
                        s => (s.player.ratings.finishing + s.player.ratings.strength) * 0.5f)
                        ?? ballHandler;

                case OffensivePlay.FastBreak:
                    // Fastest player with the ball
                    return PickPlayerWeighted(offActive,
                        s => s.player.ratings.speed * s.player.GetGamePerformanceMultiplier());

                default:
                    return ballHandler;
            }
        }

        private PlayerGameStats PickDefender(
            List<PlayerGameStats> defActive, PlayerGameStats shooter)
        {
            if (defActive.Count == 0) return null;

            // Prefer defender at same position
            var samePos = defActive
                .Where(s => s.player.position == shooter.player.position)
                .ToList();

            return samePos.Count > 0
                ? samePos.OrderByDescending(s => s.player.ratings.DefensiveRating).First()
                : defActive.OrderByDescending(s => s.player.ratings.DefensiveRating).First();
        }

        // ── Shot type determination ───────────────────────────────────────────

        private ShotType DetermineShotType(Team offTeam, PlayerGameStats shooter)
        {
            var tactics = offTeam.offensiveTactics;

            // Base probabilities modified by play weights
            float rimWeight   = 0.35f + tactics.GetPlayWeight(OffensivePlay.PickAndRoll)  * 0.3f
                                      + tactics.GetPlayWeight(OffensivePlay.FastBreak)    * 0.2f;
            float midWeight   = 0.25f + tactics.GetPlayWeight(OffensivePlay.Isolation)    * 0.3f
                                      + tactics.GetPlayWeight(OffensivePlay.PostUp)       * 0.4f;
            float threeWeight = 0.40f + tactics.GetPlayWeight(OffensivePlay.SpacingAndCuts) * 0.5f;

            // Adjust by shooter's skills
            rimWeight   *= (shooter.player.ratings.finishing  / 99f + 0.5f);
            midWeight   *= (shooter.player.ratings.midRange   / 99f + 0.5f);
            threeWeight *= (shooter.player.ratings.threePoint / 99f + 0.5f);

            float total = rimWeight + midWeight + threeWeight;
            float roll  = UnityEngine.Random.Range(0f, total);

            if (roll < rimWeight)            return ShotType.AtRim;
            if (roll < rimWeight + midWeight) return ShotType.MidRange;
            return ShotType.ThreePoint;
        }

        // ── Make chance ───────────────────────────────────────────────────────

        private float CalculateMakeChance(
            ShotType shot, PlayerGameStats shooter,
            PlayerGameStats defender, Team offTeam, Team defTeam)
        {
            // Base percentages by shot type
            float baseChance = shot switch
            {
                ShotType.AtRim      => 0.62f,
                ShotType.MidRange   => 0.44f,
                ShotType.ThreePoint => 0.36f,
                _                   => 0.50f
            };

            // Shooter skill modifier
            float shooterSkill = shot switch
            {
                ShotType.AtRim      => shooter.player.ratings.finishing  / 99f,
                ShotType.MidRange   => shooter.player.ratings.midRange   / 99f,
                ShotType.ThreePoint => shooter.player.ratings.threePoint / 99f,
                _                   => 0.5f
            };

            // Defender resistance
            float defSkill = 0f;
            if (defender != null)
            {
                defSkill = shot == ShotType.AtRim
                    ? defender.player.ratings.interior / 99f
                    : defender.player.ratings.perimeter / 99f;
            }

            // Defensive scheme modifier
            float schemeMod = defTeam.defensiveTactics.activeScheme switch
            {
                DefensiveScheme.PackThePaint    => shot == ShotType.AtRim      ? -0.08f : +0.04f,
                DefensiveScheme.PressDefense    => shot == ShotType.ThreePoint ? +0.03f : -0.02f,
                DefensiveScheme.SwitchEverything => -0.03f,
                DefensiveScheme.ZoneDefense     => shot == ShotType.MidRange   ? -0.06f : +0.02f,
                _                               => 0f
            };

            // Momentum modifier — team on a run gets a small boost
            float momentumMod = offTeam == homeTeam
                ? _homeMomentum * 0.04f
                : _awayMomentum * 0.04f;

            // Clutch factor — 4th quarter, close game
            float clutchMod = 0f;
            if (_currentQuarter == 4 && Mathf.Abs(_homeScore - _awayScore) <= 5)
                clutchMod = (shooter.player.ratings.clutch - 50f) / 99f * 0.06f;

            // B2B fatigue penalty
            float b2bMod = 0f;
            bool onB2B = offTeam == homeTeam
                ? scheduledGame.homeTeamOnB2B
                : scheduledGame.awayTeamOnB2B;
            if (onB2B) b2bMod = -0.03f;

            // Combine
            float finalChance = baseChance
                + (shooterSkill - 0.5f) * 0.20f
                - (defSkill     - 0.5f) * 0.12f
                + schemeMod + momentumMod + clutchMod + b2bMod;

            // Performance multiplier (morale + consistency variance)
            finalChance *= shooter.player.GetGamePerformanceMultiplier();

            return Mathf.Clamp(finalChance, 0.15f, 0.80f);
        }

        // ── Turnover chance ───────────────────────────────────────────────────

        private float CalculateTurnoverChance(
            Team offTeam, Team defTeam,
            PlayerGameStats ballHandler, List<PlayerGameStats> defActive)
        {
            float baseTOV = 0.14f;

            // Better ball handling = fewer turnovers
            baseTOV -= (ballHandler.player.ratings.ballHandling / 99f) * 0.06f;

            // Press defense forces more turnovers
            if (defTeam.defensiveTactics.activeScheme == DefensiveScheme.PressDefense)
                baseTOV += 0.05f;

            // High steal ratings in defense active players
            float avgSteals = defActive.Count > 0
                ? (float)defActive.Average(s => s.player.ratings.stealing) / 99f
                : 0f;
            baseTOV += avgSteals * 0.03f;

            return Mathf.Clamp(baseTOV, 0.05f, 0.30f);
        }

        // ── Foul chance ───────────────────────────────────────────────────────

        private float CalculateFoulChance(
            ShotType shot, PlayerGameStats shooter,
            PlayerGameStats defender, Team defTeam)
        {
            float baseFoul = shot switch
            {
                ShotType.AtRim      => 0.18f,
                ShotType.MidRange   => 0.08f,
                ShotType.ThreePoint => 0.05f,
                _                   => 0.10f
            };

            // Aggressive defense fouls more
            baseFoul += defTeam.defensiveTactics.foulAggression / 99f * 0.08f;

            // Defender in foul trouble is more careful
            if (defender != null && defender.personalFouls >= 4)
                baseFoul -= 0.05f;

            return Mathf.Clamp(baseFoul, 0.02f, 0.35f);
        }

        // ── Assist chance ─────────────────────────────────────────────────────

        private float CalculateAssistChance(
            Team offTeam, PlayerGameStats passer, PlayerGameStats shooter)
        {
            float baseAssist = 0.55f;

            // Playmaking-heavy offense generates more assists
            baseAssist += offTeam.offensiveTactics.GetPlayWeight(OffensivePlay.SpacingAndCuts) * 0.2f;
            baseAssist += offTeam.offensiveTactics.GetPlayWeight(OffensivePlay.PickAndRoll)    * 0.1f;

            // Isolation-heavy offense: fewer assists
            baseAssist -= offTeam.offensiveTactics.GetPlayWeight(OffensivePlay.Isolation)      * 0.3f;

            // Passer's passing rating
            baseAssist += (passer.player.ratings.passing - 50f) / 99f * 0.15f;

            return Mathf.Clamp(baseAssist, 0.20f, 0.85f);
        }

        // ── Foul handling ─────────────────────────────────────────────────────

        private void HandleFoul(
            PlayerGameStats defender, Team defTeam, List<PlayerGameStats> defActive,
            PlayerGameStats shooter, Team offTeam, ShotType shot, float makeChance)
        {
            defender.AddFoul(false);

            // Log foul trouble at 4 and 5 fouls
            if (defender.personalFouls == 4 || defender.personalFouls == 5)
                LogEvent(PlayLogType.FoulCalled, _currentQuarter, _gameClock,
                    $"{defender.player.FullName} in foul trouble ({defender.personalFouls} fouls)",
                    defTeam, defender.player);

            if (defender.fouledOut)
            {
                defActive.Remove(defender);
                LogEvent(PlayLogType.PlayerFouledOut, _currentQuarter, _gameClock,
                    $"{defender.player.FullName} has fouled out!",
                    defTeam, defender.player);
            }

            // Free throws: 2 shots (or 3 if fouled on a 3pt attempt)
            int attempts = shot == ShotType.ThreePoint ? 3 : 2;

            // And-1: if shot was going in, player gets 1 FT instead
            bool andOne = shot != ShotType.ThreePoint && UnityEngine.Random.value < makeChance;
            if (andOne)
            {
                shooter.AddFieldGoalAttempt(false, true);
                AddScore(offTeam, 2, shooter);
                attempts = 1;
            }

            // Simulate free throws
            int ftMade = 0;
            for (int i = 0; i < attempts; i++)
            {
                float ftChance = shooter.player.ratings.freeThrow / 99f;
                if (UnityEngine.Random.value < ftChance) ftMade++;
            }

            shooter.AddFreeThrows(attempts, ftMade);
            AddScore(offTeam, ftMade, shooter);

            LogEvent(PlayLogType.FoulCalled, _currentQuarter, _gameClock,
                $"{shooter.player.FullName} goes to the line ({ftMade}/{attempts} FT)",
                offTeam, shooter.player);
        }

        // ── Rebound handling ─────────────────────────────────────────────────

        private void HandleRebound(
            Team offTeam, Team defTeam,
            List<PlayerGameStats> offActive, List<PlayerGameStats> defActive)
        {
            // Offensive rebound chance ~27% base
            float orebChance = 0.27f;

            // Bigs on offense help offensive rebounding
            float offSize = offActive
                .Where(s => s.player.position == Position.Center ||
                            s.player.position == Position.PowerForward)
                .Average(s => (float)s.player.ratings.strength) / 99f;

            // Pack the Paint defense crashes the boards more
            if (defTeam.defensiveTactics.activeScheme == DefensiveScheme.PackThePaint)
                orebChance -= 0.05f;

            orebChance = Mathf.Clamp(orebChance + offSize * 0.05f, 0.15f, 0.40f);

            bool offensiveRebound = UnityEngine.Random.value < orebChance;

            if (offensiveRebound)
            {
                var rebounder = PickPlayerWeighted(offActive,
                    s => s.player.ratings.strength + s.player.ratings.verticalLeap);
                if (rebounder != null) rebounder.offRebounds++;
            }
            else
            {
                var rebounder = PickPlayerWeighted(defActive,
                    s => s.player.ratings.strength + s.player.ratings.verticalLeap);
                if (rebounder != null) rebounder.defRebounds++;
            }
        }

        // ── Scoring ───────────────────────────────────────────────────────────

        private void AddScore(Team team, int points, PlayerGameStats scorer)
        {
            if (team == homeTeam) _homeScore += points;
            else                  _awayScore += points;

            LogEvent(PlayLogType.Score, _currentQuarter, _gameClock,
                $"{scorer.player.FullName} scores {points}pts " +
                $"({homeTeam.abbreviation} {_homeScore} – {_awayScore} {awayTeam.abbreviation})",
                team, scorer.player);
        }

        // ── Momentum ─────────────────────────────────────────────────────────

        private void UpdateMomentum(Team scoringTeam, int points)
        {
            if (scoringTeam == homeTeam)
            {
                _consecutiveHomePoints += points;
                _consecutiveAwayPoints  = 0;
                _homeMomentum = Mathf.Clamp(_homeMomentum + points * 0.1f, -1f, 1f);
                _awayMomentum = Mathf.Clamp(_awayMomentum - points * 0.05f, -1f, 1f);

                if (_consecutiveHomePoints >= MomentumRunThreshold)
                {
                    LogEvent(PlayLogType.MomentumSwing, _currentQuarter, _gameClock,
                        $"{homeTeam.FullName} on a {_consecutiveHomePoints}-0 run!",
                        homeTeam, null);
                    _consecutiveHomePoints = 0;
                }
            }
            else if (scoringTeam == awayTeam)
            {
                _consecutiveAwayPoints += points;
                _consecutiveHomePoints  = 0;
                _awayMomentum = Mathf.Clamp(_awayMomentum + points * 0.1f, -1f, 1f);
                _homeMomentum = Mathf.Clamp(_homeMomentum - points * 0.05f, -1f, 1f);

                if (_consecutiveAwayPoints >= MomentumRunThreshold)
                {
                    LogEvent(PlayLogType.MomentumSwing, _currentQuarter, _gameClock,
                        $"{awayTeam.FullName} on a {_consecutiveAwayPoints}-0 run!",
                        awayTeam, null);
                    _consecutiveAwayPoints = 0;
                }
            }
            else
            {
                // Turnover / defensive stop — both teams' momentum decays slightly
                _homeMomentum = Mathf.Clamp(_homeMomentum * 0.85f, -1f, 1f);
                _awayMomentum = Mathf.Clamp(_awayMomentum * 0.85f, -1f, 1f);
                _consecutiveHomePoints = 0;
                _consecutiveAwayPoints = 0;
            }
        }

        // ── Injury ───────────────────────────────────────────────────────────

        private void CheckGameInjury(PlayerGameStats player, Team team,
                                      List<PlayerGameStats> active)
        {
            float chance = InjuryBaseChance;
            chance += player.player.progression.injuryRisk / 100f * 0.004f;

            // B2B increases injury risk
            bool onB2B = team == homeTeam
                ? scheduledGame.homeTeamOnB2B
                : scheduledGame.awayTeamOnB2B;
            if (onB2B) chance += 0.001f;

            if (UnityEngine.Random.value < chance)
            {
                player.injuredDuringGame = true;
                active.Remove(player);

                // Set injury duration (1–20 games depending on severity)
                int severity = UnityEngine.Random.Range(1, 21);
                player.player.isInjured             = true;
                player.player.injuryGamesRemaining  = severity;

                LogEvent(PlayLogType.InjuryOccurred, _currentQuarter, _gameClock,
                    $"{player.player.FullName} leaves the game injured! " +
                    $"(Est. {severity} game{(severity > 1 ? "s" : "")})",
                    team, player.player);
            }
        }

        // ── Technical foul ────────────────────────────────────────────────────

        private void CheckTechnicalFoul(Team team, List<PlayerGameStats> active)
        {
            if (UnityEngine.Random.value > TechFoulBaseChance) return;

            var offender = active[UnityEngine.Random.Range(0, active.Count)];
            offender.AddFoul(true);

            // Technical: opponent shoots 1 FT
            var opponent     = team == homeTeam ? awayTeam  : homeTeam;
            var oppActive    = team == homeTeam ? _awayActive : _homeActive;
            var ftShooter    = PickPlayerWeighted(oppActive,
                s => s.player.ratings.freeThrow);

            if (ftShooter != null)
            {
                bool made = UnityEngine.Random.value < ftShooter.player.ratings.freeThrow / 99f;
                ftShooter.AddFreeThrows(1, made ? 1 : 0);
                if (made) AddScore(opponent, 1, ftShooter);
            }

            LogEvent(PlayLogType.TechnicalFoul, _currentQuarter, _gameClock,
                $"Technical foul on {offender.player.FullName}!",
                team, offender.player);
        }

        // ── Substitution ─────────────────────────────────────────────────────

        private void SubstituteOut(PlayerGameStats outPlayer, Team team,
                                    List<PlayerGameStats> active)
        {
            active.Remove(outPlayer);

            // Find bench player with minutes remaining
            var allStats = team == homeTeam ? homeStats : awayStats;
            var sub = allStats
                .Where(s => !s.didNotPlay && !s.fouledOut &&
                            !s.injuredDuringGame && !active.Contains(s) &&
                            _minutesRemaining.TryGetValue(s.player, out float m) && m > 0)
                .OrderByDescending(s => s.player.ratings.overall)
                .FirstOrDefault();

            if (sub != null) active.Add(sub);
        }

        // ── Overtime ─────────────────────────────────────────────────────────

        private void SimulateOvertime()
        {
            wentToOvertime  = true;
            _currentQuarter = 5;
            _gameClock      = OvertimeLength;

            int possessions = Mathf.RoundToInt(CalculatePace(homeTeam) * (OvertimeLength / QuarterLength));

            bool homePossession = UnityEngine.Random.value > 0.5f;

            for (int p = 0; p < possessions; p++)
            {
                var offTeam   = homePossession ? homeTeam    : awayTeam;
                var defTeam   = homePossession ? awayTeam    : homeTeam;
                var offActive = homePossession ? _homeActive : _awayActive;
                var defActive = homePossession ? _awayActive : _homeActive;

                SimulateSinglePossession(offTeam, defTeam, offActive, defActive);
                homePossession = !homePossession;

                if (_homeScore != _awayScore) break;
            }

            homeOvertimeScore = _homeScore - homeQuarterScores.Sum();
            awayOvertimeScore = _awayScore - awayQuarterScores.Sum();
        }

        private IEnumerator SimulateOvertimeCoroutine(Action<PlayLogEntry> onEvent)
        {
            wentToOvertime  = true;
            _currentQuarter = 5;
            _gameClock      = OvertimeLength;

            int possessions = Mathf.RoundToInt(CalculatePace(homeTeam) * (OvertimeLength / QuarterLength));
            bool homePossession = UnityEngine.Random.value > 0.5f;

            for (int p = 0; p < possessions; p++)
            {
                if (isPaused) yield return new WaitUntil(() => !isPaused);

                var offTeam   = homePossession ? homeTeam    : awayTeam;
                var defTeam   = homePossession ? awayTeam    : homeTeam;
                var offActive = homePossession ? _homeActive : _awayActive;
                var defActive = homePossession ? _awayActive : _homeActive;

                SimulateSinglePossession(offTeam, defTeam, offActive, defActive);

                if (playLog.Count > 0)
                    onEvent?.Invoke(playLog[playLog.Count - 1]);

                homePossession = !homePossession;
                yield return new WaitForSeconds(0.05f / simulationSpeed);

                if (_homeScore != _awayScore) break;
            }

            homeOvertimeScore = _homeScore - homeQuarterScores.Sum();
            awayOvertimeScore = _awayScore - awayQuarterScores.Sum();
        }

        // ── Quarter end log ───────────────────────────────────────────────────

        private void LogQuarterEnd(int quarter)
        {
            LogEvent(PlayLogType.QuarterEnd, quarter, 0f,
                $"End of Q{quarter} — {homeTeam.abbreviation} {_homeScore} – " +
                $"{_awayScore} {awayTeam.abbreviation}",
                null, null);
        }

        // ── Finalise ──────────────────────────────────────────────────────────

        private void Finalise()
        {
            isComplete            = true;
            scheduledGame.isPlayed = true;

            // Commit game stats to season averages
            foreach (var stats in AllStats)
            {
                if (stats.didNotPlay) continue;

                // Find or create current season stats on the player
                var season = stats.player.careerStats
                    .FirstOrDefault(s => s.season == DateTime.Now.Year);

                if (season == null)
                {
                    season = new SeasonStats { season = DateTime.Now.Year };
                    var list = new List<SeasonStats>(stats.player.careerStats) { season };
                    stats.player.careerStats = list.ToArray();
                }

                stats.CommitToSeasonStats(season);

                // Tick injury recovery for players who played
                stats.player.TickInjury();
            }

            // Update plus/minus
            int scoreDiff = HomeScore - AwayScore;
            foreach (var s in homeStats.Where(s => !s.didNotPlay))
                s.plusMinus = scoreDiff;
            foreach (var s in awayStats.Where(s => !s.didNotPlay))
                s.plusMinus = -scoreDiff;
        }

        // ── Log helper ────────────────────────────────────────────────────────

        private void LogEvent(PlayLogType type, int quarter, float clock,
                              string description, Team team, Player player)
        {
            playLog.Add(new PlayLogEntry(
                type, quarter, clock, description,
                team, player, _homeScore, _awayScore));
        }
    }
}