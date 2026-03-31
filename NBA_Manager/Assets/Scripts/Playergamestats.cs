using System;
using UnityEngine;

namespace NBAManager
{
    // ── Play log entry ────────────────────────────────────────────────────────
    // Stored in Game.playLog — only key events are logged (scores, fouls, injuries)

    public enum PlayLogType
    {
        Score,           // A basket was made
        FoulCalled,      // Personal foul
        TechnicalFoul,   // Technical foul
        InjuryOccurred,  // Player injured during game
        MomentumSwing,   // Team goes on a run (e.g. 8-0)
        QuarterEnd,      // End of a quarter
        Timeout,         // Timeout called (auto or manual)
        PlayerFouledOut  // Player disqualified with 6 fouls
    }

    [Serializable]
    public class PlayLogEntry
    {
        public PlayLogType type;
        public int         quarter;
        public float       gameClock;      // seconds remaining in quarter
        public string      description;   // human-readable text for UI
        public Team        team;          // team this event belongs to
        public Player      player;        // player involved (can be null)
        public int         homeScore;     // score at time of event
        public int         awayScore;

        public string ClockLabel =>
            $"Q{quarter} {Mathf.FloorToInt(gameClock / 60f)}:{(gameClock % 60f):00}";

        public PlayLogEntry(PlayLogType t, int q, float clock, string desc,
                            Team team, Player player, int home, int away)
        {
            type        = t;
            quarter     = q;
            gameClock   = clock;
            description = desc;
            this.team   = team;
            this.player = player;
            homeScore   = home;
            awayScore   = away;
        }
    }

    // ── Per-player game stats ─────────────────────────────────────────────────

    [Serializable]
    public class PlayerGameStats
    {
        public Player player;
        public Team   team;

        // ── Box score ────────────────────────────────────────────────────────
        public int   points;
        public int   assists;
        public int   offRebounds;
        public int   defRebounds;
        public int   steals;
        public int   blocks;
        public int   turnovers;
        public int   personalFouls;
        public int   technicalFouls;
        public int   fgMade;
        public int   fgAttempted;
        public int   fg3Made;
        public int   fg3Attempted;
        public int   ftMade;
        public int   ftAttempted;
        public float minutesPlayed;
        public int   plusMinus;

        // ── Status flags ─────────────────────────────────────────────────────
        public bool  fouledOut;          // 6 fouls
        public bool  injuredDuringGame;
        public bool  didNotPlay;         // DNP — injury, coach decision, etc.

        // ── Computed ─────────────────────────────────────────────────────────
        public int   Rebounds       => offRebounds + defRebounds;
        public float FGPct          => fgAttempted  == 0 ? 0f : (float)fgMade  / fgAttempted;
        public float FG3Pct         => fg3Attempted == 0 ? 0f : (float)fg3Made / fg3Attempted;
        public float FTPct          => ftAttempted  == 0 ? 0f : (float)ftMade  / ftAttempted;

        // Game score — a single-number performance summary (similar to real NBA metric)
        // Points + 0.4*FGM - 0.7*FGA - 0.4*(FTA-FTM) + 0.7*OReb + 0.3*DReb
        // + STL + 0.7*AST + 0.7*BLK - 0.4*PF - TOV
        public float GameScore =>
            points
            + 0.4f  * fgMade
            - 0.7f  * fgAttempted
            - 0.4f  * (ftAttempted - ftMade)
            + 0.7f  * offRebounds
            + 0.3f  * defRebounds
            + steals
            + 0.7f  * assists
            + 0.7f  * blocks
            - 0.4f  * personalFouls
            - turnovers;

        public PlayerGameStats(Player p, Team t)
        {
            player = p;
            team   = t;
        }

        // Adds free throw attempts and makes based on whether shots are made
        public void AddFreeThrows(int attempts, int made)
        {
            ftAttempted += attempts;
            ftMade      += made;
            points      += made;
        }

        // Records a field goal attempt — handles 2pt and 3pt separately
        public void AddFieldGoalAttempt(bool isThree, bool made)
        {
            fgAttempted++;
            if (isThree) fg3Attempted++;

            if (made)
            {
                fgMade++;
                points += isThree ? 3 : 2;
                if (isThree) fg3Made++;
            }
        }

        public void AddFoul(bool isTechnical)
        {
            if (isTechnical) technicalFouls++;
            else             personalFouls++;

            fouledOut = personalFouls >= 6;
        }

        // Transfers this game's stats into the player's running season averages
        public void CommitToSeasonStats(SeasonStats season)
        {
            if (didNotPlay) return;

            int g = season.gamesPlayed + 1;

            season.pointsPerGame    = UpdateAvg(season.pointsPerGame,    points,        g);
            season.assistsPerGame   = UpdateAvg(season.assistsPerGame,   assists,       g);
            season.reboundsPerGame  = UpdateAvg(season.reboundsPerGame,  Rebounds,      g);
            season.stealsPerGame    = UpdateAvg(season.stealsPerGame,    steals,        g);
            season.blocksPerGame    = UpdateAvg(season.blocksPerGame,    blocks,        g);
            season.turnoversPerGame = UpdateAvg(season.turnoversPerGame, turnovers,     g);
            season.minutesPerGame   = UpdateAvg(season.minutesPerGame,   minutesPlayed, g);
            season.fieldGoalPct     = UpdateAvg(season.fieldGoalPct,     FGPct,         g);
            season.threePointPct    = UpdateAvg(season.threePointPct,    FG3Pct,        g);
            season.freeThrowPct     = UpdateAvg(season.freeThrowPct,     FTPct,         g);
            season.gamesPlayed      = g;
        }

        // Running average update: newAvg = (oldAvg * (n-1) + newVal) / n
        private float UpdateAvg(float oldAvg, float newVal, int n) =>
            (oldAvg * (n - 1) + newVal) / n;
    }
}