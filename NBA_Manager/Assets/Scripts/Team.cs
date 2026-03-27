using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAManager
{
    // ── Enums ────────────────────────────────────────────────────────────────

    public enum Conference { East, West }

    public enum Division
    {
        Atlantic, Central, Southeast,   // East
        Northwest, Pacific, Southwest    // West
    }

    public enum AIPersonality
    {
        Rebuilding,   // Prioritises young players, picks, future cap space
        Contending,   // All-in on winning now, trades picks for veterans
        Balanced,     // Moderate approach, mixes youth and experience
        Tanking       // Deliberately losing to secure high draft picks
    }

    // ── Offensive plays ──────────────────────────────────────────────────────
    // The team ranks all 5 from 1 (most used) to 5 (least used).
    // The simulation engine checks this order when generating possessions.

    public enum OffensivePlay
    {
        PickAndRoll,      // Ball handler + big, breeds mid-range and rim attempts
        Isolation,        // One-on-one creation, star-dependent
        SpacingAndCuts,   // 3pt heavy, off-ball movement, requires shooters
        PostUp,           // Big or wing backed down in the paint
        FastBreak         // Push pace, early offense, athleticism-dependent
    }

    public enum DefensiveScheme
    {
        ManToMan,         // Individual matchups, effort and athleticism-dependent
        ZoneDefense,      // Area coverage, disrupts opponent sets, hides weak defenders
        PressDefense,     // Full or half court pressure, forces turnovers, tiring
        SwitchEverything, // Modern scheme, requires versatile/IQ-heavy roster
        PackThePaint      // Sag off shooters, protect the rim, concedes 3s
    }

    // ── Coach ────────────────────────────────────────────────────────────────

    [Serializable]
    public class CoachRatings
    {
        [Range(0, 99)] public int overall;

        [Header("Strengths & Weaknesses (0-99)")]
        [Range(0, 99)] public int offensiveSchemeIQ;   // how well their offense translates
        [Range(0, 99)] public int defensiveSchemeIQ;   // how well their defense translates
        [Range(0, 99)] public int playerDevelopment;   // boosts young player progression rate
        [Range(0, 99)] public int inGameAdjustments;   // performance in close / playoff games
        [Range(0, 99)] public int lockerRoomManagement; // keeps morale high across the roster
        [Range(0, 99)] public int rotationManagement;  // how well they use bench depth

        public void RecalculateOverall()
        {
            overall = Mathf.Clamp(Mathf.RoundToInt(
                offensiveSchemeIQ      * 0.20f +
                defensiveSchemeIQ      * 0.20f +
                playerDevelopment      * 0.20f +
                inGameAdjustments      * 0.15f +
                lockerRoomManagement   * 0.15f +
                rotationManagement     * 0.10f), 0, 99);
        }
    }

    [CreateAssetMenu(fileName = "NewCoach", menuName = "NBA Manager/Coach")]
    public class Coach : ScriptableObject
    {
        [Header("Identity")]
        public string coachId;
        public string firstName;
        public string lastName;
        public int age;
        public Sprite portrait;

        [Header("Ratings")]
        public CoachRatings ratings;

        [Header("Progression")]
        [Range(0, 99)] public int potential;
        public bool isFreeAgent;

        // Preferred schemes — used by AI teams when auto-setting tactics
        public OffensivePlay preferredOffense;
        public DefensiveScheme preferredDefense;

        public string FullName => $"{firstName} {lastName}";

        // A multiplier applied to young players' developmentRate at season end
        public float DevelopmentBonus =>
            Mathf.Lerp(0.80f, 1.40f, ratings.playerDevelopment / 99f);

        // A multiplier applied to the simulation engine in clutch moments
        public float ClutchBonus =>
            Mathf.Lerp(0.90f, 1.15f, ratings.inGameAdjustments / 99f);

        public void ApplySeasonProgression()
        {
            // Coaches improve slightly until ~55, then plateau
            if (age < 55)
            {
                int gain = UnityEngine.Random.Range(0, 2);
                ratings.overall = Mathf.Min(ratings.overall + gain, potential);
            }
            age++;
        }
    }

    // ── Tactics ──────────────────────────────────────────────────────────────

    [Serializable]
    public class OffensiveTactics
    {
        // Each play is assigned a priority rank 1–5 by the player.
        // Rank 1 = called most often, rank 5 = rarely used.
        // No two plays can share the same rank (enforced in UI).
        [Range(1, 5)] public int pickAndRollPriority    = 1;
        [Range(1, 5)] public int isolationPriority      = 2;
        [Range(1, 5)] public int spacingAndCutsPriority = 3;
        [Range(1, 5)] public int postUpPriority         = 4;
        [Range(1, 5)] public int fastBreakPriority      = 5;

        // Returns plays ordered from most-used (rank 1) to least-used (rank 5)
        public List<OffensivePlay> GetPriorityOrder()
        {
            var ranked = new Dictionary<OffensivePlay, int>
            {
                { OffensivePlay.PickAndRoll,      pickAndRollPriority    },
                { OffensivePlay.Isolation,        isolationPriority      },
                { OffensivePlay.SpacingAndCuts,   spacingAndCutsPriority },
                { OffensivePlay.PostUp,           postUpPriority         },
                { OffensivePlay.FastBreak,        fastBreakPriority      }
            };
            return ranked.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
        }

        // Returns a 0–1 weight for a given play based on its rank
        // Rank 1 = 0.35, Rank 2 = 0.25, Rank 3 = 0.20, Rank 4 = 0.12, Rank 5 = 0.08
        public float GetPlayWeight(OffensivePlay play)
        {
            float[] weights = { 0.35f, 0.25f, 0.20f, 0.12f, 0.08f };
            int rank = play switch
            {
                OffensivePlay.PickAndRoll    => pickAndRollPriority,
                OffensivePlay.Isolation      => isolationPriority,
                OffensivePlay.SpacingAndCuts => spacingAndCutsPriority,
                OffensivePlay.PostUp         => postUpPriority,
                OffensivePlay.FastBreak      => fastBreakPriority,
                _                            => 5
            };
            return weights[Mathf.Clamp(rank - 1, 0, 4)];
        }
    }

    [Serializable]
    public class DefensiveTactics
    {
        public DefensiveScheme activeScheme = DefensiveScheme.ManToMan;

        // How aggressively to foul (0 = never, 99 = hack-a-shaq)
        [Range(0, 99)] public int foulAggression = 40;

        // How much to double-team the opponent's best player (0–99)
        [Range(0, 99)] public int doubleTeamTendency = 30;
    }

    // ── Lineup ───────────────────────────────────────────────────────────────

    [Serializable]
    public class PlayerLineupSlot
    {
        public Player player;

        // Minutes assigned per game (0–48). Starters typically 28–38, bench 8–20.
        [Range(0, 48)] public int minutesPerGame;

        // Whether this slot is in the starting five or bench rotation
        public bool isStarter;

        public PlayerLineupSlot(Player p, int minutes, bool starter)
        {
            player        = p;
            minutesPerGame = minutes;
            isStarter     = starter;
        }
    }

    public class RotationValidationResult
    {
        public bool isValid;
        public List<string> errors = new List<string>();
        public int totalMinutesAssigned;

        // Convenience so callers can do: if (!result) { ... }
        public static implicit operator bool(RotationValidationResult r) => r.isValid;
    }

    // ── Finances ─────────────────────────────────────────────────────────────

    [Serializable]
    public class TeamFinances
    {
        public float salaryCap       = 136.0f;  // in millions (2024-25 approx)
        public float luxuryTaxLine   = 165.0f;  // paying above this triggers tax
        public float minimumSalary   = 1.1f;    // league minimum

        // Derived at runtime — sum of all active contracts on the roster
        public float CapUsed;

        public float CapSpace        => Mathf.Max(0f, salaryCap - CapUsed);
        public bool  IsOverCap       => CapUsed > salaryCap;
        public bool  IsOverLuxuryTax => CapUsed > luxuryTaxLine;

        // Rough luxury tax bill (simplified: $1.50 per $1M over the line)
        public float LuxuryTaxBill   =>
            IsOverLuxuryTax ? (CapUsed - luxuryTaxLine) * 1.5f : 0f;

        public void RecalculateCapUsed(List<Player> roster)
        {
            CapUsed = (float)roster.Sum(p => p.contract.annualSalary);
        }
    }

    // ── Team record ──────────────────────────────────────────────────────────

    [Serializable]
    public class TeamRecord
    {
        public int wins;
        public int losses;
        public int homeWins;
        public int homeLosses;
        public int awayWins;
        public int awayLosses;
        public int conferenceWins;
        public int conferenceLosses;
        public int streak;          // positive = win streak, negative = losing streak

        public int GamesPlayed  => wins + losses;
        public float WinPct     => GamesPlayed == 0 ? 0f : (float)wins / GamesPlayed;
        public string StreakLabel =>
            streak == 0 ? "-" : (streak > 0 ? $"W{streak}" : $"L{Mathf.Abs(streak)}");

        public void RegisterResult(bool won, bool isHome, bool isConference)
        {
            if (won) { wins++;   if (isHome) homeWins++;   else awayWins++;   }
            else     { losses++; if (isHome) homeLosses++; else awayLosses++; }

            if (isConference) { if (won) conferenceWins++; else conferenceLosses++; }

            streak = won ? (streak > 0 ? streak + 1 : 1)
                         : (streak < 0 ? streak - 1 : -1);
        }

        public void Reset()
        {
            wins = losses = homeWins = homeLosses = 0;
            awayWins = awayLosses = conferenceWins = conferenceLosses = streak = 0;
        }
    }

    // ── Draft pick ───────────────────────────────────────────────────────────

    [Serializable]
    public class DraftPick
    {
        public int year;
        public int round;           // 1 or 2
        public Team originalOwner;  // team this pick originally belonged to
        public bool isProtected;
        public int protectionCutoff; // e.g. top-5 protected: if pick lands 1-5, it doesn't convey
    }

    // ── Team ─────────────────────────────────────────────────────────────────

    [CreateAssetMenu(fileName = "NewTeam", menuName = "NBA Manager/Team")]
    public class Team : ScriptableObject
    {
        [Header("Identity")]
        public string teamId;
        public string teamName;       // e.g. "Wildcards"
        public string city;           // e.g. "Boston"
        public string abbreviation;   // e.g. "BOS"
        public Conference conference;
        public Division division;
        public bool isPlayerTeam;     // true for the human-controlled franchise

        [Header("Colors")]
        public Color primaryColor;
        public Color secondaryColor;
        public Sprite logo;

        [Header("Roster")]
        public List<Player> roster = new List<Player>();  // all players under contract (max 15)

        [Header("Lineup & Rotation")]
        // Full rotation — starters first (isStarter = true), then bench.
        // The player sets each slot's minutesPerGame. Total across all slots must equal 240.
        public List<PlayerLineupSlot> rotation = new List<PlayerLineupSlot>();

        // Total minutes available per game (5 positions × 48 min)
        public const int TotalGameMinutes = 240;

        [Header("Coaching")]
        public Coach headCoach;

        [Header("Tactics")]
        public OffensiveTactics offensiveTactics = new OffensiveTactics();
        public DefensiveTactics defensiveTactics = new DefensiveTactics();

        [Header("Finances")]
        public TeamFinances finances = new TeamFinances();

        [Header("Season Record")]
        public TeamRecord record = new TeamRecord();

        [Header("Draft Assets")]
        public List<DraftPick> ownedPicks = new List<DraftPick>();

        [Header("AI")]
        public AIPersonality aiPersonality;

        // ── Computed helpers ─────────────────────────────────────────────────

        public string FullName => $"{city} {teamName}";

        public bool IsRosterFull => roster.Count >= 15;
        public int  RosterSize   => roster.Count;

        public List<PlayerLineupSlot> Starters =>
            rotation.Where(s => s.isStarter).ToList();

        public List<PlayerLineupSlot> Bench =>
            rotation.Where(s => !s.isStarter).ToList();

        // Average overall of starters — quick team strength indicator
        public float StartingFiveOverall =>
            Starters.Count == 0 ? 0f :
            (float)Starters.Average(s => s.player.ratings.overall);

        // Average overall of the full roster
        public float RosterOverall =>
            roster.Count == 0 ? 0f :
            (float)roster.Average(p => p.ratings.overall);

        // Team morale — average mood across all rostered players
        public float TeamMorale =>
            roster.Count == 0 ? 1f :
            (float)roster.Average(p => (float)p.mood);

        // Total minutes assigned across all rotation slots
        public int TotalAssignedMinutes =>
            rotation.Sum(s => s.minutesPerGame);

        // ── Roster management ────────────────────────────────────────────────

        public bool SignPlayer(Player player)
        {
            if (IsRosterFull)
            {
                Debug.LogWarning($"{FullName}: Cannot sign {player.FullName} — roster full.");
                return false;
            }
            if (finances.CapSpace < player.contract.annualSalary && !finances.IsOverCap)
            {
                Debug.LogWarning($"{FullName}: Not enough cap space to sign {player.FullName}.");
                return false;
            }

            roster.Add(player);
            player.isFreeAgent = false;
            finances.RecalculateCapUsed(roster);

            // Add to rotation with 0 minutes — player must manually assign
            rotation.Add(new PlayerLineupSlot(player, 0, false));
            return true;
        }

        public bool ReleasePlayer(Player player)
        {
            if (!roster.Contains(player))
            {
                Debug.LogWarning($"{FullName}: {player.FullName} is not on this roster.");
                return false;
            }

            roster.Remove(player);
            rotation.RemoveAll(s => s.player == player);
            player.isFreeAgent = true;
            finances.RecalculateCapUsed(roster);
            return true;
        }

        // ── Lineup management ────────────────────────────────────────────────

        // Set a player's minutes and starter status directly
        public bool SetPlayerMinutes(Player player, int minutes, bool isStarter)
        {
            var slot = rotation.FirstOrDefault(s => s.player == player);
            if (slot == null)
            {
                Debug.LogWarning($"{player.FullName} is not in the rotation.");
                return false;
            }

            slot.minutesPerGame = Mathf.Clamp(minutes, 0, 48);
            slot.isStarter      = isStarter;
            return true;
        }

        // Validate the rotation before simulating a game.
        // Returns a result object with a list of errors the UI can display.
        public RotationValidationResult ValidateRotation()
        {
            var result = new RotationValidationResult();
            result.totalMinutesAssigned = TotalAssignedMinutes;

            int starterCount = Starters.Count;
            if (starterCount != 5)
                result.errors.Add($"You need exactly 5 starters (currently {starterCount}).");

            if (result.totalMinutesAssigned != TotalGameMinutes)
                result.errors.Add(
                    $"Total minutes must equal {TotalGameMinutes} " +
                    $"(currently {result.totalMinutesAssigned}).");

            // No injured player should have minutes assigned
            foreach (var slot in rotation.Where(s => s.player.isInjured && s.minutesPerGame > 0))
                result.errors.Add($"{slot.player.FullName} is injured but has {slot.minutesPerGame} min assigned.");

            // Each starter should have at least 10 minutes
            foreach (var slot in Starters.Where(s => s.minutesPerGame < 10))
                result.errors.Add($"Starter {slot.player.FullName} has fewer than 10 min — consider adjusting.");

            result.isValid = result.errors.Count == 0;
            return result;
        }

        // Auto-build a balanced rotation: best 5 start with ~32 min each,
        // next 5 bench players share remaining 80 min evenly, rest get 0.
        public void AutoSetRotation()
        {
            var positions = new[]
            {
                Position.PointGuard, Position.ShootingGuard, Position.SmallForward,
                Position.PowerForward, Position.Center
            };

            rotation.Clear();

            // Pick starters — best available per position, skip injured
            var starters = new List<Player>();
            foreach (var pos in positions)
            {
                var best = roster
                    .Where(p => p.position == pos && !starters.Contains(p) && !p.isInjured)
                    .OrderByDescending(p => p.ratings.overall)
                    .FirstOrDefault()
                    ?? roster
                        .Where(p => !starters.Contains(p) && !p.isInjured)
                        .OrderByDescending(p => p.ratings.overall)
                        .FirstOrDefault();

                if (best != null) starters.Add(best);
            }

            // Assign 32 min each to starters (5 × 32 = 160)
            foreach (var p in starters)
                rotation.Add(new PlayerLineupSlot(p, 32, true));

            // Bench: next best healthy players share 80 min
            var bench = roster
                .Where(p => !starters.Contains(p) && !p.isInjured)
                .OrderByDescending(p => p.ratings.overall)
                .Take(5)
                .ToList();

            int benchMinutes = bench.Count > 0 ? 80 / bench.Count : 0;
            int remainder    = bench.Count > 0 ? 80 % bench.Count  : 0;

            for (int i = 0; i < bench.Count; i++)
            {
                int min = benchMinutes + (i == 0 ? remainder : 0); // give remainder to first bench player
                rotation.Add(new PlayerLineupSlot(bench[i], min, false));
            }

            // Remaining roster players get 0 minutes (inactive)
            foreach (var p in roster.Where(p => !starters.Contains(p) && !bench.Contains(p)))
                rotation.Add(new PlayerLineupSlot(p, 0, false));
        }

        // ── Trade logic ──────────────────────────────────────────────────────

        // Returns how valuable this team considers a player (used in trade eval)
        // AI trades that score positively for both sides are accepted
        public float EvaluatePlayerValue(Player player)
        {
            float baseValue = player.ratings.overall;

            // Younger players with high potential are worth more
            float ageBonus = Mathf.Lerp(10f, -5f, Mathf.Clamp01((player.age - 20f) / 15f));
            float potentialBonus = player.progression.potential - player.ratings.overall;

            // Contract value: cheaper contracts are worth more
            float contractPenalty = player.contract.annualSalary * 0.5f;

            // Personality modifier
            float personalityMod = aiPersonality switch
            {
                AIPersonality.Rebuilding => ageBonus * 1.5f + potentialBonus * 1.5f,
                AIPersonality.Contending => baseValue * 0.2f - contractPenalty * 0.5f,
                AIPersonality.Tanking    => ageBonus * 2.0f - baseValue * 0.3f,
                _                        => ageBonus + potentialBonus * 0.5f
            };

            return baseValue + personalityMod - contractPenalty;
        }

        // ── End of season ────────────────────────────────────────────────────

        public void ApplyEndOfSeason()
        {
            foreach (var player in roster)
            {
                if (headCoach != null)
                    player.progression.developmentRate *= headCoach.DevelopmentBonus;

                player.ApplySeasonProgression();

                if (headCoach != null)
                    player.progression.developmentRate /= headCoach.DevelopmentBonus;
            }

            headCoach?.ApplySeasonProgression();
            record.Reset();
            finances.RecalculateCapUsed(roster);

            // Reset all rotation slots to 0 minutes — player must re-set for new season
            // (roster changes over offseason make old minutes assignments stale)
            foreach (var slot in rotation)
                slot.minutesPerGame = 0;
        }

        // ── Morale ───────────────────────────────────────────────────────────

        // Call after wins/losses or trades to shift player moods
        public void ApplyMoraleShift(int delta)
        {
            foreach (var player in roster)
            {
                int current = (int)player.mood;
                int next    = Mathf.Clamp(current + delta, 0, 3);
                player.mood = (PlayerMood)next;
            }
        }

        // Head coach's locker room management softens morale drops
        public void ApplyMoraleShiftWithCoach(int delta)
        {
            if (headCoach != null)
            {
                float buffer = headCoach.ratings.lockerRoomManagement / 99f;
                delta = delta < 0
                    ? Mathf.RoundToInt(delta * (1f - buffer * 0.5f))
                    : delta;
            }
            ApplyMoraleShift(delta);
        }
    }
}