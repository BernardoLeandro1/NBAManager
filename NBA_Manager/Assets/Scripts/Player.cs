using System;
using UnityEngine;

namespace NBAManager
{
    public enum Position
    {
        PointGuard,       // PG
        ShootingGuard,    // SG
        SmallForward,     // SF
        PowerForward,     // PF
        Center            // C
    }

    public enum PlayerMood
    {
        Unhappy,
        Neutral,
        Happy,
        Thriving
    }

    public enum PlayerType
    {
        // Offense-focused
        Scorer,           // Volume scorer, gets buckets in many ways
        ShotCreator,      // Elite at creating their own shot off the dribble
        Slasher,          // Attacks the rim relentlessly, lives at the FT line
        StretchBig,       // Big man who spaces the floor with 3pt shooting

        // Defense-focused
        LockdownDefender, // Elite perimeter stopper, can guard 1-through-3
        RimProtector,     // Anchor in the paint, blocks shots, protects the basket

        // Balanced / role
        ThreeAndD,        // Hits 3s and guards — the ideal modern role player
        Playmaker,        // Pass-first, orchestrates the offense, high assists
        FloorGeneral,     // Smart PG, runs the team, prioritizes efficiency over scoring
        TwoWayPlayer,     // Contributes on both ends — no glaring weakness
        PointCenter,      // Big with elite passing and playmaking from the post
        GlueGuy           // High IQ, does the little things, elevates teammates
    }

    [Serializable]
    public class Contract
    {
        public float annualSalary;      // in millions
        public int yearsRemaining;
        public bool isMaxContract;
        public bool hasPlayerOption;    // player can opt out in final year
        public bool hasTeamOption;      // team can drop in final year
        public bool isRookieDeal;

        public float TotalValueRemaining => annualSalary * yearsRemaining;
    }

    [Serializable]
    public class PlayerRatings
    {
        [Header("Core Ratings (0-99)")]
        [Range(0, 99)] public int overall;

        [Header("Scoring (0-99)")]
        [Range(0, 99)] public int threePoint;    // three-point shooting
        [Range(0, 99)] public int midRange;      // mid-range shooting
        [Range(0, 99)] public int finishing;     // layups, dunks at the rim
        [Range(0, 99)] public int freeThrow;     // free throw shooting

        [Header("Playmaking (0-99)")]
        [Range(0, 99)] public int ballHandling;  // dribbling, ball control
        [Range(0, 99)] public int passing;       // vision and passing accuracy
        [Range(0, 99)] public int offIQ;         // court vision, off-ball reads

        [Header("Defense (0-99)")]
        [Range(0, 99)] public int perimeter;     // on-ball perimeter defense
        [Range(0, 99)] public int interior;      // post defense, rim protection
        [Range(0, 99)] public int stealing;      // hands, anticipation
        [Range(0, 99)] public int blocking;      // shot alteration / blocking
        [Range(0, 99)] public int defIQ;         // rotations, help defense

        [Header("Physical (0-99)")]
        [Range(0, 99)] public int speed;         // straight-line speed
        [Range(0, 99)] public int agility;       // lateral movement
        [Range(0, 99)] public int strength;      // physical presence
        [Range(0, 99)] public int verticalLeap;
        [Range(0, 99)] public int stamina;       // how well they hold up over minutes

        [Header("Mental (0-99)")]
        [Range(0, 99)] public int clutch;        // performance in close games / 4th quarter
        [Range(0, 99)] public int consistency;   // how much ratings vary game to game
        [Range(0, 99)] public int leadership;    // impact on team morale

        // ── Computed category ratings (shown on player card UI) ──────────

        // Weighted: finishing counts most (at-rim threat), then 3pt and midrange
        public int ScoringRating => Mathf.RoundToInt(
            finishing   * 0.35f +
            threePoint  * 0.30f +
            midRange    * 0.20f +
            freeThrow   * 0.15f);

        // Passing is the core, ball handling enables creation, IQ rounds it out
        public int PlaymakingRating => Mathf.RoundToInt(
            passing      * 0.45f +
            ballHandling * 0.35f +
            offIQ        * 0.20f);

        // Interior + perimeter weighted equally; IQ/steals/blocks add nuance
        public int DefensiveRating => Mathf.RoundToInt(
            perimeter * 0.25f +
            interior  * 0.25f +
            defIQ     * 0.25f +
            stealing  * 0.15f +
            blocking  * 0.10f);

        // Athleticism snapshot — speed + agility most visible, stamina matters long-term
        public int PhysicalRating => Mathf.RoundToInt(
            speed       * 0.30f +
            agility     * 0.25f +
            verticalLeap* 0.20f +
            strength    * 0.15f +
            stamina     * 0.10f);

        // Recalculate overall using weights tuned to the player's archetype.
        // Call this whenever sub-ratings change or type is reassigned.
        public void RecalculateOverall(PlayerType type)
        {
            float s = ScoringRating;
            float p = PlaymakingRating;
            float d = DefensiveRating;
            float ph = PhysicalRating;

            // Each archetype defines (scoringW, playmakingW, defenseW, physicalW).
            // Weights sum to 1.0 — the dominant skill(s) get the heaviest share.
            float ws, wp, wd, wph;

            switch (type)
            {
                // ── Offense-focused ──────────────────────────────────────
                case PlayerType.Scorer:
                    // All-around scoring machine; some physical needed to create space
                    (ws, wp, wd, wph) = (0.55f, 0.15f, 0.15f, 0.15f);
                    break;

                case PlayerType.ShotCreator:
                    // Needs scoring AND playmaking — creates for self and others
                    (ws, wp, wd, wph) = (0.40f, 0.35f, 0.10f, 0.15f);
                    break;

                case PlayerType.Slasher:
                    // Finishing + athleticism define value; 3pt irrelevant
                    // Override ScoringRating with finishing-heavy sub-calc
                    s = Mathf.RoundToInt(finishing * 0.65f + midRange * 0.15f + freeThrow * 0.20f);
                    (ws, wp, wd, wph) = (0.40f, 0.10f, 0.15f, 0.35f);
                    break;

                case PlayerType.StretchBig:
                    // 3pt is everything; size means some interior defense still counts
                    s = Mathf.RoundToInt(threePoint * 0.70f + midRange * 0.20f + freeThrow * 0.10f);
                    (ws, wp, wd, wph) = (0.45f, 0.10f, 0.25f, 0.20f);
                    break;

                // ── Defense-focused ──────────────────────────────────────
                case PlayerType.LockdownDefender:
                    // Defense almost entirely defines value; perimeter + defIQ weighted up
                    d = Mathf.RoundToInt(perimeter * 0.40f + defIQ * 0.30f + stealing * 0.20f + interior * 0.10f);
                    (ws, wp, wd, wph) = (0.10f, 0.05f, 0.65f, 0.20f);
                    break;

                case PlayerType.RimProtector:
                    // Interior + blocking is the whole job; physical matters too
                    d = Mathf.RoundToInt(interior * 0.45f + blocking * 0.35f + defIQ * 0.20f);
                    (ws, wp, wd, wph) = (0.10f, 0.05f, 0.60f, 0.25f);
                    break;

                // ── Balanced / role ──────────────────────────────────────
                case PlayerType.ThreeAndD:
                    // Must do BOTH — 3pt shooting and perimeter defense, equally valued
                    s = Mathf.RoundToInt(threePoint * 0.80f + freeThrow * 0.20f);
                    d = Mathf.RoundToInt(perimeter * 0.50f + defIQ * 0.30f + stealing * 0.20f);
                    (ws, wp, wd, wph) = (0.35f, 0.05f, 0.45f, 0.15f);
                    break;

                case PlayerType.Playmaker:
                    // Passing and vision define this player; scoring almost irrelevant
                    (ws, wp, wd, wph) = (0.10f, 0.55f, 0.20f, 0.15f);
                    break;

                case PlayerType.FloorGeneral:
                    // Like Playmaker but leans more on IQ; offIQ boosted in sub-calc
                    p = Mathf.RoundToInt(passing * 0.40f + offIQ * 0.40f + ballHandling * 0.20f);
                    (ws, wp, wd, wph) = (0.10f, 0.55f, 0.20f, 0.15f);
                    break;

                case PlayerType.TwoWayPlayer:
                    // Truly balanced — no category neglected, offense edges defense slightly
                    (ws, wp, wd, wph) = (0.30f, 0.20f, 0.35f, 0.15f);
                    break;

                case PlayerType.PointCenter:
                    // Big man who passes: playmaking + scoring in equal measure, some defense
                    p = Mathf.RoundToInt(passing * 0.50f + offIQ * 0.35f + ballHandling * 0.15f);
                    (ws, wp, wd, wph) = (0.25f, 0.40f, 0.20f, 0.15f);
                    break;

                case PlayerType.GlueGuy:
                    // IQ and leadership carry overall; no single flashy skill needed
                    // Use a mental bonus: blend leadership + consistency into each category
                    float mentalBonus = (leadership * 0.6f + consistency * 0.4f);
                    s  = Mathf.RoundToInt(s  * 0.75f + mentalBonus * 0.25f);
                    p  = Mathf.RoundToInt(p  * 0.75f + mentalBonus * 0.25f);
                    d  = Mathf.RoundToInt(d  * 0.75f + mentalBonus * 0.25f);
                    ph = Mathf.RoundToInt(ph * 0.75f + mentalBonus * 0.25f);
                    (ws, wp, wd, wph) = (0.20f, 0.25f, 0.30f, 0.25f);
                    break;

                default:
                    (ws, wp, wd, wph) = (0.25f, 0.25f, 0.25f, 0.25f);
                    break;
            }

            overall = Mathf.Clamp(Mathf.RoundToInt(s * ws + p * wp + d * wd + ph * wph), 0, 99);
        }
    }

    [Serializable]
    public class PlayerProgression
    {
        [Range(0, 99)] public int potential;         // ceiling overall rating
        public int peakAgeStart;                     // age when peak begins (typically 25-27)
        public int peakAgeEnd;                       // age when decline starts (typically 30-33)
        public float developmentRate;                // how fast they improve (0.5 = slow, 2.0 = fast)
        public bool isInjuryProne;
        [Range(0, 100)] public int injuryRisk;       // 0 = never injured, 100 = always hurt
    }

    [Serializable]
    public class SeasonStats
    {
        public int season;
        public int gamesPlayed;
        public float pointsPerGame;
        public float assistsPerGame;
        public float reboundsPerGame;
        public float stealsPerGame;
        public float blocksPerGame;
        public float turnoversPerGame;
        public float fieldGoalPct;
        public float threePointPct;
        public float freeThrowPct;
        public float minutesPerGame;
        public float playerEfficiencyRating;  // PER
        public float winSharesPerFortyEight;  // WS/48
    }

    [CreateAssetMenu(fileName = "NewPlayer", menuName = "NBA Manager/Player")]
    public class Player : ScriptableObject
    {
        [Header("Identity")]
        public string playerId;
        public string firstName;
        public string lastName;
        public int age;
        public int heightCm;       // e.g. 198
        public float weightKg;     // e.g. 100.5
        public string nationality;
        public Position position;
        public PlayerType playerType;
        public Sprite portrait;    // optional 2D portrait for UI

        [Header("Ratings")]
        public PlayerRatings ratings;

        [Header("Progression")]
        public PlayerProgression progression;

        [Header("Contract")]
        public Contract contract;

        [Header("Status")]
        public bool isFreeAgent;
        public bool isRookie;
        public bool isInjured;
        public int injuryGamesRemaining;   // how many games left on injury
        public PlayerMood mood;

        [Header("Season History")]
        public SeasonStats[] careerStats;  // one entry per season

        // ── Computed helpers ──────────────────────────────────────────────

        public string FullName => $"{firstName} {lastName}";

        public string PositionAbbreviation => position switch
        {
            Position.PointGuard     => "PG",
            Position.ShootingGuard  => "SG",
            Position.SmallForward   => "SF",
            Position.PowerForward   => "PF",
            Position.Center         => "C",
            _                       => "??"
        };

        public bool IsInPrimeYears => age >= progression.peakAgeStart && age <= progression.peakAgeEnd;

        public bool IsDeclinig => age > progression.peakAgeEnd;

        // Returns a 0–1 float representing how much morale affects performance
        // Used by the simulation engine to tweak ratings slightly up or down
        public float MoraleMultiplier => mood switch
        {
            PlayerMood.Unhappy   => 0.92f,
            PlayerMood.Neutral   => 1.00f,
            PlayerMood.Happy     => 1.04f,
            PlayerMood.Thriving  => 1.08f,
            _                    => 1.00f
        };

        // Roll a random performance variance based on consistency rating
        // consistency 99 = very little variance, 0 = very unpredictable
        public float GetGamePerformanceMultiplier()
        {
            float varianceRange = Mathf.Lerp(0.20f, 0.02f, ratings.consistency / 99f);
            float variance = UnityEngine.Random.Range(-varianceRange, varianceRange);
            return Mathf.Clamp(1f + variance + (MoraleMultiplier - 1f), 0.70f, 1.30f);
        }

        // End-of-season progression: call once per season in GameManager
        public void ApplySeasonProgression()
        {
            if (IsInPrimeYears)
            {
                // Young-to-prime: slight improvement
                int gain = Mathf.RoundToInt(UnityEngine.Random.Range(0f, 2f) * progression.developmentRate);
                ratings.overall = Mathf.Min(ratings.overall + gain, progression.potential);
            }
            else if (IsDeclinig)
            {
                // Veteran decline
                int drop = Mathf.RoundToInt(UnityEngine.Random.Range(0f, 2f));
                ratings.overall = Mathf.Max(ratings.overall - drop, 55);
            }
            else
            {
                // Pre-prime: bigger improvement window
                int gain = Mathf.RoundToInt(UnityEngine.Random.Range(1f, 4f) * progression.developmentRate);
                ratings.overall = Mathf.Min(ratings.overall + gain, progression.potential);
            }

            // Re-derive overall from sub-ratings using this player's archetype
            ratings.RecalculateOverall(playerType);

            age++;
            contract.yearsRemaining = Mathf.Max(0, contract.yearsRemaining - 1);

            if (contract.yearsRemaining == 0)
                isFreeAgent = true;
        }

        // Resolve injury at end of each simulated game
        public void TickInjury()
        {
            if (!isInjured) return;
            injuryGamesRemaining--;
            if (injuryGamesRemaining <= 0)
            {
                isInjured = false;
                injuryGamesRemaining = 0;
            }
        }
    }
}