using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAManager
{
    // ── Enums ────────────────────────────────────────────────────────────────

    public enum SeasonPhase
    {
        Preseason,        // Setup, roster building, no standings impact
        RegularSeason,    // 82 games, standings accumulate
        PlayIn,           // Seeds 7-10 each conference compete for final 2 playoff spots
        Playoffs,         // 16 teams, best-of-7 bracket
        Offseason,        // Free agency, trades, contract renewals
        Draft             // Annual draft — 2 rounds, 60 picks
    }

    public enum PlayoffRound
    {
        FirstRound,       // 16 → 8
        ConferenceSemis,  // 8 → 4
        ConferenceFinals, // 4 → 2
        Finals            // 2 → 1 champion
    }

    // ── Schedule entry ───────────────────────────────────────────────────────

    [Serializable]
    public class ScheduledGame
    {
        public string gameId;
        public Team homeTeam;
        public Team awayTeam;
        public int week;               // which week of the season (1–24 approx)
        public int dayOfSeason;        // day number within the season (1–180 approx)
        public bool isPlayIn;
        public bool isPlayoff;
        public PlayoffRound playoffRound;
        public bool isPlayed;
        public bool isPlayerGame;      // true if the human team is involved

        // Back-to-back flags — set during schedule generation
        public bool homeTeamOnB2B;     // home team played yesterday
        public bool awayTeamOnB2B;     // away team played yesterday

        public ScheduledGame(Team home, Team away, int week, int day)
        {
            gameId           = Guid.NewGuid().ToString();
            homeTeam         = home;
            awayTeam         = away;
            this.week        = week;
            this.dayOfSeason = day;
            isPlayed         = false;
            isPlayIn         = false;
            isPlayoff        = false;
            homeTeamOnB2B    = false;
            awayTeamOnB2B    = false;
        }
    }

    // ── Standings entry ──────────────────────────────────────────────────────

    [Serializable]
    public class StandingsEntry
    {
        public Team team;
        public int wins;
        public int losses;
        public int conferenceWins;
        public int conferenceLosses;
        public int divisionWins;
        public int divisionLosses;
        public int pointsFor;          // total points scored this season
        public int pointsAgainst;      // total points conceded this season
        public int streak;             // positive = win streak, negative = losing streak
        public List<bool> last10 = new List<bool>(); // true = win, false = loss

        public int    GamesPlayed   => wins + losses;
        public float  WinPct        => GamesPlayed == 0 ? 0f : (float)wins / GamesPlayed;
        public float  PointDiff     => GamesPlayed == 0 ? 0f : (float)(pointsFor - pointsAgainst) / GamesPlayed;
        public string StreakLabel   => streak == 0 ? "-" : (streak > 0 ? $"W{streak}" : $"L{Mathf.Abs(streak)}");
        public string Last10Label
        {
            get
            {
                int w = last10.Count(r => r);
                return $"{w}-{last10.Count - w}";
            }
        }

        public void RegisterResult(bool won, bool isConference, bool isDivision, int ptsFor, int ptsAgainst)
        {
            if (won) wins++;   else losses++;
            if (isConference) { if (won) conferenceWins++;   else conferenceLosses++; }
            if (isDivision)   { if (won) divisionWins++;     else divisionLosses++;   }

            pointsFor     += ptsFor;
            pointsAgainst += ptsAgainst;
            streak         = won ? (streak > 0 ? streak + 1 :  1)
                                 : (streak < 0 ? streak - 1 : -1);

            last10.Add(won);
            if (last10.Count > 10) last10.RemoveAt(0);
        }
    }

    // ── Playoff series ───────────────────────────────────────────────────────

    [Serializable]
    public class PlayoffSeries
    {
        public string seriesId;
        public Team   higherSeed;
        public Team   lowerSeed;
        public int    higherSeedWins;
        public int    lowerSeedWins;
        public PlayoffRound round;
        public bool   isComplete;
        public Team   winner;
        public List<ScheduledGame> games = new List<ScheduledGame>();

        // Best-of-7: first to 4 wins
        public bool CheckComplete()
        {
            if (higherSeedWins == 4) { winner = higherSeed; isComplete = true; }
            if (lowerSeedWins  == 4) { winner = lowerSeed;  isComplete = true; }
            return isComplete;
        }

        public string SeriesScore =>
            $"{higherSeed.abbreviation} {higherSeedWins} – {lowerSeedWins} {lowerSeed.abbreviation}";
    }

    // ── Play-in entry ─────────────────────────────────────────────────────────

    [Serializable]
    public class PlayInMatchup
    {
        public Team   teamA;           // higher seed
        public Team   teamB;           // lower seed
        public bool   isElimination;   // true = loser is eliminated
        public Team   winner;
        public bool   isPlayed;
        public ScheduledGame game;
    }

    // ── Draft ────────────────────────────────────────────────────────────────

    [Serializable]
    public class DraftEntry
    {
        public int    overall;         // pick number 1–60
        public int    round;           // 1 or 2
        public int    pickInRound;     // 1–30
        public Team   pickOwner;       // team who owns this pick (may differ from original)
        public Player draftedPlayer;   // filled once pick is made
        public bool   isSelected;
    }

    [Serializable]
    public class DraftClass
    {
        public int            year;
        public List<Player>   prospects  = new List<Player>(); // generated pre-draft
        public List<DraftEntry> pickOrder = new List<DraftEntry>();

        // Returns the next unpicked entry
        public DraftEntry NextPick => pickOrder.FirstOrDefault(e => !e.isSelected);

        public bool IsComplete => pickOrder.All(e => e.isSelected);
    }

    // ── Season ───────────────────────────────────────────────────────────────

    [CreateAssetMenu(fileName = "NewSeason", menuName = "NBA Manager/Season")]
    public class Season : ScriptableObject
    {
        // ── Constants ────────────────────────────────────────────────────────

        public const int TeamsInLeague       = 30;
        public const int TeamsPerConference  = 15;
        public const int GamesPerSeason      = 82;
        public const int PlayoffTeams        = 16;  // 8 per conference
        public const int PlayInTeams         = 8;   // seeds 7-10 per conference
        public const int DraftRounds         = 2;
        public const int PicksPerRound       = 30;
        public const int TotalDraftPicks     = DraftRounds * PicksPerRound; // 60

        // ── Identity ─────────────────────────────────────────────────────────

        [Header("Season Info")]
        public int year;
        public SeasonPhase currentPhase;
        public int currentDay;         // day within the current phase
        public int currentWeek;        // week of the season (used for schedule display)

        // ── Teams ─────────────────────────────────────────────────────────────

        [Header("League")]
        public List<Team> allTeams     = new List<Team>();
        public List<Team> eastTeams    = new List<Team>();
        public List<Team> westTeams    = new List<Team>();

        // ── Schedule ──────────────────────────────────────────────────────────

        [Header("Schedule")]
        public List<ScheduledGame> regularSeasonSchedule = new List<ScheduledGame>();
        public List<ScheduledGame> playInSchedule        = new List<ScheduledGame>();
        public List<ScheduledGame> playoffSchedule       = new List<ScheduledGame>();

        // All upcoming games for the player's team — used to drive the main UI
        public List<ScheduledGame> PlayerTeamSchedule =>
            regularSeasonSchedule
                .Concat(playInSchedule)
                .Concat(playoffSchedule)
                .Where(g => g.isPlayerGame)
                .OrderBy(g => g.dayOfSeason)
                .ToList();

        public ScheduledGame NextPlayerGame =>
            PlayerTeamSchedule.FirstOrDefault(g => !g.isPlayed);

        // ── Standings ─────────────────────────────────────────────────────────

        [Header("Standings")]
        public List<StandingsEntry> eastStandings = new List<StandingsEntry>();
        public List<StandingsEntry> westStandings = new List<StandingsEntry>();

        // Sorted standings by win % then point differential as tiebreaker
        public List<StandingsEntry> EastSorted =>
            eastStandings
                .OrderByDescending(e => e.WinPct)
                .ThenByDescending(e => e.PointDiff)
                .ToList();

        public List<StandingsEntry> WestSorted =>
            westStandings
                .OrderByDescending(e => e.WinPct)
                .ThenByDescending(e => e.PointDiff)
                .ToList();

        // ── Play-in ───────────────────────────────────────────────────────────

        [Header("Play-In")]
        // Per conference: two matchups
        // Game 1: seed 7 vs seed 8 — winner gets 7th playoff spot
        // Game 2: seed 9 vs seed 10 — loser eliminated
        // Game 3: loser of Game 1 vs winner of Game 2 — winner gets 8th spot
        public List<PlayInMatchup> eastPlayIn = new List<PlayInMatchup>();
        public List<PlayInMatchup> westPlayIn = new List<PlayInMatchup>();

        // Final 8 playoff qualifiers per conference (filled after play-in)
        public List<Team> eastPlayoffTeams = new List<Team>();
        public List<Team> westPlayoffTeams = new List<Team>();

        // ── Playoffs ──────────────────────────────────────────────────────────

        [Header("Playoffs")]
        public List<PlayoffSeries> allSeries     = new List<PlayoffSeries>();
        public Team                champion;

        // Active series in current round
        public List<PlayoffSeries> ActiveSeries =>
            allSeries.Where(s => !s.isComplete).ToList();

        public List<PlayoffSeries> SeriesInRound(PlayoffRound round) =>
            allSeries.Where(s => s.round == round).ToList();

        // ── Draft ─────────────────────────────────────────────────────────────

        [Header("Draft")]
        public DraftClass currentDraft;

        // ── Free agents ───────────────────────────────────────────────────────

        [Header("Free Agency")]
        public List<Player> freeAgents       = new List<Player>();
        public List<Player> retiredPlayers   = new List<Player>();

        // ── Phase management ─────────────────────────────────────────────────

        public void AdvancePhase()
        {
            switch (currentPhase)
            {
                case SeasonPhase.Preseason:
                    currentPhase = SeasonPhase.RegularSeason;
                    GenerateRegularSeasonSchedule();
                    break;

                case SeasonPhase.RegularSeason:
                    currentPhase = SeasonPhase.PlayIn;
                    SetupPlayIn();
                    break;

                case SeasonPhase.PlayIn:
                    currentPhase = SeasonPhase.Playoffs;
                    SetupPlayoffBracket();
                    break;

                case SeasonPhase.Playoffs:
                    currentPhase = SeasonPhase.Offseason;
                    BeginOffseason();
                    break;

                case SeasonPhase.Offseason:
                    currentPhase = SeasonPhase.Draft;
                    SetupDraft();
                    break;

                case SeasonPhase.Draft:
                    // Season over — caller (GameManager) starts a new Season
                    break;
            }

            currentDay = 0;
        }

        // ── Schedule generation ───────────────────────────────────────────────

        // Generates a balanced 82-game schedule for all 30 teams.
        // Each team plays:
        //   - 4 games vs each division opponent      (4 × 4  = 16)
        //   - 3 games vs rest of same conference     (~36)
        //   - 2 games vs all opposite conference     (2 × 15 = 30)
        // Total = 82
        //
        // Game days are variable — some nights have 2 games, some have 13,
        // mirroring the real NBA calendar. Back-to-backs are tracked per team.
        public void GenerateRegularSeasonSchedule()
        {
            regularSeasonSchedule.Clear();
            var matchups = new List<(Team home, Team away)>();

            // Build matchup pool — each pair added once, home/away determined later
            var pairs = new HashSet<string>();
            foreach (var team in allTeams)
            {
                var division  = allTeams.Where(t => t != team && t.division   == team.division);
                var sameConf  = allTeams.Where(t => t != team && t.conference == team.conference && t.division != team.division);
                var otherConf = allTeams.Where(t => t.conference != team.conference);

                foreach (var opp in division)
                {
                    string key = string.Compare(team.teamId, opp.teamId) < 0
                        ? $"{team.teamId}_{opp.teamId}" : $"{opp.teamId}_{team.teamId}";
                    if (pairs.Add(key))
                        for (int i = 0; i < 4; i++)
                            matchups.Add(i % 2 == 0 ? (team, opp) : (opp, team));
                }

                foreach (var opp in sameConf)
                {
                    string key = string.Compare(team.teamId, opp.teamId) < 0
                        ? $"{team.teamId}_{opp.teamId}" : $"{opp.teamId}_{team.teamId}";
                    if (pairs.Add(key))
                        for (int i = 0; i < 3; i++)
                            matchups.Add(i % 2 == 0 ? (team, opp) : (opp, team));
                }

                foreach (var opp in otherConf)
                {
                    string key = string.Compare(team.teamId, opp.teamId) < 0
                        ? $"{team.teamId}_{opp.teamId}" : $"{opp.teamId}_{team.teamId}";
                    if (pairs.Add(key))
                        for (int i = 0; i < 2; i++)
                            matchups.Add(i % 2 == 0 ? (team, opp) : (opp, team));
                }
            }

            // Shuffle matchups
            matchups = matchups.OrderBy(_ => UnityEngine.Random.value).ToList();

            // ── Variable game day distribution ────────────────────────────────
            // Real NBA nights have anywhere from 1 to 13 games.
            // We use a weighted random pattern:
            //   ~30% of days:  2–4  games  (quiet nights)
            //   ~45% of days:  5–8  games  (standard nights)
            //   ~20% of days:  9–11 games  (busy nights)
            //   ~5%  of days: 12–15 games  (big nights — Christmas, MLK etc.)

            var dayGameCounts = new List<int>(); // how many games each day will hold
            int remaining = matchups.Count;

            while (remaining > 0)
            {
                float roll = UnityEngine.Random.value;
                int count;

                if (roll < 0.30f)      count = UnityEngine.Random.Range(2, 5);   // 2–4
                else if (roll < 0.75f) count = UnityEngine.Random.Range(5, 9);   // 5–8
                else if (roll < 0.95f) count = UnityEngine.Random.Range(9, 12);  // 9–11
                else                   count = UnityEngine.Random.Range(12, 16); // 12–15

                count = Mathf.Min(count, remaining);
                dayGameCounts.Add(count);
                remaining -= count;
            }

            // ── Assign games to days and tag back-to-backs ────────────────────
            // lastGameDay[team] = last day that team played — used for B2B detection
            var lastGameDay = new Dictionary<Team, int>();

            int matchupIndex = 0;
            int day          = 1;

            foreach (int gamesThisDay in dayGameCounts)
            {
                int week = Mathf.CeilToInt(day / 7f);

                for (int g = 0; g < gamesThisDay; g++)
                {
                    var (home, away) = matchups[matchupIndex++];
                    var game         = new ScheduledGame(home, away, week, day);

                    // Back-to-back: did this team play yesterday?
                    game.homeTeamOnB2B = lastGameDay.TryGetValue(home, out int hLast) && hLast == day - 1;
                    game.awayTeamOnB2B = lastGameDay.TryGetValue(away, out int aLast) && aLast == day - 1;

                    // Tag player team involvement
                    game.isPlayerGame = home.isPlayerTeam || away.isPlayerTeam;

                    regularSeasonSchedule.Add(game);

                    lastGameDay[home] = day;
                    lastGameDay[away] = day;
                }

                day++;

                // Occasional rest day (no games) — ~1 in every 8 days
                if (UnityEngine.Random.value < 0.12f && remaining > 0)
                    day++;
            }

            currentWeek = 1;
            InitialiseStandings();
        }

        // Returns all games on a specific day of the season
        public List<ScheduledGame> GetGamesOnDay(int day) =>
            regularSeasonSchedule.Where(g => g.dayOfSeason == day).ToList();

        // Returns the next unplayed day number in the schedule
        public int NextScheduledDay =>
            regularSeasonSchedule
                .Where(g => !g.isPlayed)
                .Select(g => g.dayOfSeason)
                .DefaultIfEmpty(0)
                .Min();

        // Returns how many back-to-backs a team has this season (useful for UI)
        public int GetBackToBackCount(Team team) =>
            regularSeasonSchedule.Count(g =>
                (g.homeTeam == team && g.homeTeamOnB2B) ||
                (g.awayTeam == team && g.awayTeamOnB2B));

        // ── Standings ─────────────────────────────────────────────────────────

        public void InitialiseStandings()
        {
            eastStandings.Clear();
            westStandings.Clear();

            foreach (var team in eastTeams)
                eastStandings.Add(new StandingsEntry { team = team });

            foreach (var team in westTeams)
                westStandings.Add(new StandingsEntry { team = team });
        }

        public void RegisterGameResult(Team winner, Team loser, int winnerPts, int loserPts, ScheduledGame game)
        {
            bool isConference = winner.conference == loser.conference;
            bool isDivision   = winner.division   == loser.division;

            var winEntry  = GetStandingsEntry(winner);
            var lossEntry = GetStandingsEntry(loser);

            winEntry?.RegisterResult(true,  isConference, isDivision, winnerPts, loserPts);
            lossEntry?.RegisterResult(false, isConference, isDivision, loserPts, winnerPts);

            game.isPlayed = true;

            // Sync back to the Team's own record too
            winner.record.RegisterResult(true,  game.homeTeam == winner, isConference);
            loser.record.RegisterResult(false, game.homeTeam == loser,  isConference);
        }

        public StandingsEntry GetStandingsEntry(Team team)
        {
            return eastStandings.FirstOrDefault(e => e.team == team)
                ?? westStandings.FirstOrDefault(e => e.team == team);
        }

        // ── Play-in setup ─────────────────────────────────────────────────────

        // Seeds 7-10 per conference enter the play-in tournament.
        // Structure per conference:
        //   Round 1 — Game A: 7 vs 8 (winner = 7th seed), Game B: 9 vs 10
        //   Round 2 — Game C: loser of A vs winner of B (winner = 8th seed)
        public void SetupPlayIn()
        {
            eastPlayIn.Clear();
            westPlayIn.Clear();

            SetupConferencePlayIn(EastSorted, eastPlayIn, playInSchedule);
            SetupConferencePlayIn(WestSorted, westPlayIn, playInSchedule);
        }

        private void SetupConferencePlayIn(
            List<StandingsEntry> sorted,
            List<PlayInMatchup> matchups,
            List<ScheduledGame> schedule)
        {
            if (sorted.Count < 10) return;

            var seed7  = sorted[6].team;
            var seed8  = sorted[7].team;
            var seed9  = sorted[8].team;
            var seed10 = sorted[9].team;

            // Game A: 7 vs 8
            var gameA = new ScheduledGame(seed7, seed8, 0, currentDay + 1) { isPlayIn = true };
            gameA.isPlayerGame = seed7.isPlayerTeam || seed8.isPlayerTeam;
            schedule.Add(gameA);
            matchups.Add(new PlayInMatchup { teamA = seed7, teamB = seed8, isElimination = false, game = gameA });

            // Game B: 9 vs 10
            var gameB = new ScheduledGame(seed9, seed10, 0, currentDay + 1) { isPlayIn = true };
            gameB.isPlayerGame = seed9.isPlayerTeam || seed10.isPlayerTeam;
            schedule.Add(gameB);
            matchups.Add(new PlayInMatchup { teamA = seed9, teamB = seed10, isElimination = true, game = gameB });

            // Game C placeholder — filled after A and B are played
            matchups.Add(new PlayInMatchup { isElimination = true });
        }

        // Call after Game A and Game B are resolved to set up Game C
        public void ResolvePlayInRound1(Conference conf)
        {
            var matchups = conf == Conference.East ? eastPlayIn : westPlayIn;
            var sorted   = conf == Conference.East ? EastSorted : WestSorted;

            var gameA = matchups[0];
            var gameB = matchups[1];
            var gameC = matchups[2];

            if (!gameA.isPlayed || !gameB.isPlayed) return;

            // Winner of A → 7th playoff seed (added directly to playoff teams)
            var playoffList = conf == Conference.East ? eastPlayoffTeams : westPlayoffTeams;
            playoffList.Add(gameA.winner);

            // Game C: loser of A vs winner of B
            var loserA   = gameA.winner == gameA.teamA ? gameA.teamB : gameA.teamA;
            var winnerB  = gameB.winner;
            var gameCScheduled = new ScheduledGame(loserA, winnerB, 0, currentDay + 2) { isPlayIn = true };
            gameCScheduled.isPlayerGame = loserA.isPlayerTeam || winnerB.isPlayerTeam;
            playInSchedule.Add(gameCScheduled);

            gameC.teamA = loserA;
            gameC.teamB = winnerB;
            gameC.game  = gameCScheduled;
        }

        // Call after Game C is resolved
        public void ResolvePlayInRound2(Conference conf)
        {
            var matchups    = conf == Conference.East ? eastPlayIn    : westPlayIn;
            var playoffList = conf == Conference.East ? eastPlayoffTeams : westPlayoffTeams;

            var gameC = matchups[2];
            if (!gameC.isPlayed) return;

            // Winner of C → 8th playoff seed
            playoffList.Add(gameC.winner);
        }

        // ── Playoff bracket ───────────────────────────────────────────────────

        // Fills eastPlayoffTeams and westPlayoffTeams with seeds 1-6 from standings
        // (seeds 7-8 are added by play-in resolution above)
        public void CollectPlayoffSeeds()
        {
            var eastTop6 = EastSorted.Take(6).Select(e => e.team);
            var westTop6 = WestSorted.Take(6).Select(e => e.team);

            eastPlayoffTeams.InsertRange(0, eastTop6);
            westPlayoffTeams.InsertRange(0, westTop6);
        }

        // Generates the first-round playoff bracket (1v8, 2v7, 3v6, 4v5 per conf)
        public void SetupPlayoffBracket()
        {
            allSeries.Clear();

            CollectPlayoffSeeds();

            CreateConferenceBracket(eastPlayoffTeams, Conference.East);
            CreateConferenceBracket(westPlayoffTeams, Conference.West);
        }

        private void CreateConferenceBracket(List<Team> seeds, Conference conf)
        {
            // 1v8, 2v7, 3v6, 4v5
            var matchupSeeds = new[] { (0, 7), (1, 6), (2, 5), (3, 4) };

            foreach (var (hi, lo) in matchupSeeds)
            {
                if (seeds.Count <= lo) continue;
                CreateSeries(seeds[hi], seeds[lo], PlayoffRound.FirstRound);
            }
        }

        private PlayoffSeries CreateSeries(Team higher, Team lower, PlayoffRound round)
        {
            var series = new PlayoffSeries
            {
                seriesId      = Guid.NewGuid().ToString(),
                higherSeed    = higher,
                lowerSeed     = lower,
                round         = round,
                higherSeedWins = 0,
                lowerSeedWins  = 0,
                isComplete    = false
            };

            // Generate up to 7 games (home court: H H A A H A H)
            bool[] homeSeedIsHigher = { true, true, false, false, true, false, true };
            for (int g = 0; g < 7; g++)
            {
                var home = homeSeedIsHigher[g] ? higher : lower;
                var away = homeSeedIsHigher[g] ? lower  : higher;
                var game = new ScheduledGame(home, away, 0, currentDay + g + 1)
                {
                    isPlayoff     = true,
                    playoffRound  = round,
                    isPlayerGame  = higher.isPlayerTeam || lower.isPlayerTeam
                };
                series.games.Add(game);
                playoffSchedule.Add(game);
            }

            allSeries.Add(series);
            return series;
        }

        // Call after each playoff game to update series and advance bracket
        public void RegisterPlayoffResult(PlayoffSeries series, Team winner)
        {
            if (series.isComplete) return;

            if (winner == series.higherSeed) series.higherSeedWins++;
            else                             series.lowerSeedWins++;

            if (series.CheckComplete())
                TryAdvanceBracket(series);
        }

        private void TryAdvanceBracket(PlayoffSeries completedSeries)
        {
            var nextRound = completedSeries.round switch
            {
                PlayoffRound.FirstRound       => PlayoffRound.ConferenceSemis,
                PlayoffRound.ConferenceSemis  => PlayoffRound.ConferenceFinals,
                PlayoffRound.ConferenceFinals => PlayoffRound.Finals,
                _                             => (PlayoffRound?)null
            };

            if (nextRound == null)
            {
                // Finals complete — crown champion
                champion = completedSeries.winner;
                return;
            }

            // Find a completed series in the same round to pair with
            var sameRound = allSeries
                .Where(s => s.round == completedSeries.round && s.isComplete && s != completedSeries)
                .ToList();

            // Pair winners together if a partner series is ready
            // (simplified: pair in completion order — works for standard bracket)
            var unpaired = sameRound
                .Where(s => !allSeries.Any(ns =>
                    ns.round == nextRound &&
                    (ns.higherSeed == s.winner || ns.lowerSeed == s.winner)))
                .FirstOrDefault();

            if (unpaired != null)
            {
                var teamA = completedSeries.winner;
                var teamB = unpaired.winner;

                // Higher seed determined by original seeding position
                var higher = eastPlayoffTeams.Contains(teamA) ?
                    (eastPlayoffTeams.IndexOf(teamA) < eastPlayoffTeams.IndexOf(teamB) ? teamA : teamB) :
                    (westPlayoffTeams.IndexOf(teamA) < westPlayoffTeams.IndexOf(teamB) ? teamA : teamB);
                var lower = higher == teamA ? teamB : teamA;

                CreateSeries(higher, lower, nextRound.Value);
            }
        }

        // ── Offseason ─────────────────────────────────────────────────────────

        public void BeginOffseason()
        {
            // Collect all players whose contracts expired
            foreach (var team in allTeams)
            {
                var expired = team.roster.Where(p => p.contract.yearsRemaining <= 0).ToList();
                foreach (var p in expired)
                {
                    team.roster.Remove(p);
                    p.isFreeAgent = true;
                    freeAgents.Add(p);
                }
            }

            // Handle retirements (players over 38 with low overall have a chance to retire)
            var retiring = freeAgents.Where(p => p.age >= 38 &&
                UnityEngine.Random.value > (p.ratings.overall / 99f)).ToList();

            foreach (var p in retiring)
            {
                freeAgents.Remove(p);
                retiredPlayers.Add(p);
            }
        }

        // ── Draft setup ───────────────────────────────────────────────────────

        // Draft order: worst record picks first (lottery for top 4, then by record)
        public void SetupDraft()
        {
            currentDraft = new DraftClass { year = year };

            // Combine east + west sorted by win% ascending (worst first)
            var draftOrder = allTeams
                .OrderBy(t => GetStandingsEntry(t)?.WinPct ?? 0f)
                .ToList();

            int overall = 1;
            for (int round = 1; round <= DraftRounds; round++)
            {
                // Round 2 reverses slightly (protected picks may reorder — simplified here)
                var roundOrder = round == 1 ? draftOrder : draftOrder.AsEnumerable().Reverse().ToList();

                foreach (var team in roundOrder)
                {
                    // Check if the team traded away this pick
                    var ownedPick = team.ownedPicks.FirstOrDefault(p => p.year == year && p.round == round);
                    var pickOwner = ownedPick != null ? ownedPick.originalOwner : team;

                    currentDraft.pickOrder.Add(new DraftEntry
                    {
                        overall      = overall,
                        round        = round,
                        pickInRound  = overall - (round - 1) * PicksPerRound,
                        pickOwner    = pickOwner,
                        isSelected   = false
                    });
                    overall++;
                }
            }

            // Generate prospect pool (60 prospects + extras for teams to evaluate)
            GenerateDraftProspects(80);
        }

        // Generates a pool of draft-eligible players with rookie-level ratings
        private void GenerateDraftProspects(int count)
        {
            currentDraft.prospects.Clear();

            string[] firstNames = { "James", "Marcus", "Tyler", "Devon", "Andre", "Malik",
                                    "Jordan", "Chris", "Kevin", "Darius", "Zion", "Cole" };
            string[] lastNames  = { "Williams", "Johnson", "Davis", "Brown", "Smith", "Jones",
                                    "Taylor", "Wilson", "Moore", "Harris", "Martin", "Clark" };

            for (int i = 0; i < count; i++)
            {
                // Top prospects are better; talent drops off after pick ~20
                float talentTier = Mathf.Clamp01(1f - (i / (float)count));

                var prospect = ScriptableObject.CreateInstance<Player>();
                prospect.playerId  = Guid.NewGuid().ToString();
                prospect.firstName = firstNames[UnityEngine.Random.Range(0, firstNames.Length)];
                prospect.lastName  = lastNames[UnityEngine.Random.Range(0,  lastNames.Length)];
                prospect.age       = UnityEngine.Random.Range(18, 22);
                prospect.position  = (Position)UnityEngine.Random.Range(0, 5);
                prospect.isRookie  = true;
                prospect.isFreeAgent = true;

                // Ratings: top prospects 65-80 overall, late picks 50-65
                int baseRating = Mathf.RoundToInt(Mathf.Lerp(52f, 78f, talentTier));
                int variance   = UnityEngine.Random.Range(-5, 6);

                prospect.ratings = new PlayerRatings();
                prospect.ratings.overall = Mathf.Clamp(baseRating + variance, 45, 82);

                // Potential: lottery picks could become stars (85–99), late picks 65–80
                prospect.progression = new PlayerProgression
                {
                    potential       = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(65f, 97f, talentTier))
                                        + UnityEngine.Random.Range(-5, 6), 60, 99),
                    peakAgeStart    = UnityEngine.Random.Range(24, 27),
                    peakAgeEnd      = UnityEngine.Random.Range(29, 33),
                    developmentRate = Mathf.Lerp(0.8f, 2.0f, talentTier)
                };

                prospect.contract = new Contract
                {
                    annualSalary   = Mathf.Lerp(1.1f, 10.0f, talentTier),
                    yearsRemaining = 4,
                    isRookieDeal   = true
                };

                currentDraft.prospects.Add(prospect);
            }

            // Sort by overall descending so the board is ordered like a real draft board
            currentDraft.prospects = currentDraft.prospects
                .OrderByDescending(p => p.ratings.overall)
                .ThenByDescending(p => p.progression.potential)
                .ToList();
        }

        // Make a draft selection — assigns a prospect to a pick and team
        public bool MakeDraftPick(DraftEntry pick, Player prospect)
        {
            if (pick.isSelected)
            {
                Debug.LogWarning($"Pick {pick.overall} has already been used.");
                return false;
            }
            if (!currentDraft.prospects.Contains(prospect))
            {
                Debug.LogWarning($"{prospect.FullName} is no longer available.");
                return false;
            }

            pick.draftedPlayer = prospect;
            pick.isSelected    = true;

            prospect.isFreeAgent = false;
            prospect.isRookie    = true;
            currentDraft.prospects.Remove(prospect);

            pick.pickOwner.roster.Add(prospect);
            pick.pickOwner.finances.RecalculateCapUsed(pick.pickOwner.roster);

            return true;
        }

        // ── Season initialisation ─────────────────────────────────────────────

        public void Initialise(int seasonYear, List<Team> teams)
        {
            year         = seasonYear;
            currentPhase = SeasonPhase.Preseason;
            currentDay   = 0;
            currentWeek  = 1;
            champion     = null;
            allSeries.Clear();
            freeAgents.Clear();
            eastPlayoffTeams.Clear();
            westPlayoffTeams.Clear();

            allTeams  = teams;
            eastTeams = teams.Where(t => t.conference == Conference.East).ToList();
            westTeams = teams.Where(t => t.conference == Conference.West).ToList();

            InitialiseStandings();
        }
    }
}