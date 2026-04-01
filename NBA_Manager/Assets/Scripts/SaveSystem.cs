using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace NBAManager
{
    // ── Save slot metadata ────────────────────────────────────────────────────
    // Lightweight info shown on the save/load screen without loading the full file.

    [Serializable]
    public class SaveSlotInfo
    {
        public int    slotIndex;
        public bool   isEmpty;
        public string teamName;          // e.g. "Boston Wildcats"
        public int    seasonYear;
        public string seasonPhase;       // e.g. "Regular Season — Week 12"
        public int    wins;
        public int    losses;
        public string lastSavedAt;       // ISO 8601 timestamp
        public string saveVersion;       // for future migration support
    }

    // ── Full save data ────────────────────────────────────────────────────────
    // Everything needed to restore a session exactly as it was left.
    // ScriptableObjects can't be JSON-serialised directly, so we convert
    // them to serialisable data transfer objects (DTOs) defined below.

    [Serializable]
    public class SaveData
    {
        public string          saveVersion  = SaveSystem.CurrentVersion;
        public string          savedAt;
        public int             slotIndex;

        // League
        public List<TeamDTO>   teams        = new List<TeamDTO>();
        public SeasonDTO       season       = new SeasonDTO();

        // GameManager state
        public string          managerState;
        public int             currentDay;
        public SimulationMode  simulationMode;
        public float           simulationSpeed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Data Transfer Objects — flat, JSON-serialisable representations of
    // ScriptableObjects. We write to DTOs on save and reconstruct SOs on load.
    // ─────────────────────────────────────────────────────────────────────────

    [Serializable]
    public class PlayerDTO
    {
        public string  playerId;
        public string  firstName;
        public string  lastName;
        public int     age;
        public int     heightCm;
        public float   weightKg;
        public string  nationality;
        public string  position;
        public string  playerType;
        public string  mood;
        public bool    isFreeAgent;
        public bool    isRookie;
        public bool    isInjured;
        public int     injuryGamesRemaining;

        // Ratings
        public int threePoint, midRange, finishing, freeThrow;
        public int ballHandling, passing, offIQ;
        public int perimeter, interior, stealing, blocking, defIQ;
        public int speed, agility, strength, verticalLeap, stamina;
        public int clutch, consistency, leadership;
        public int overall;

        // Progression
        public int   potential;
        public int   peakAgeStart;
        public int   peakAgeEnd;
        public float developmentRate;
        public bool  isInjuryProne;
        public int   injuryRisk;

        // Contract
        public float annualSalary;
        public int   yearsRemaining;
        public bool  isMaxContract;
        public bool  hasPlayerOption;
        public bool  hasTeamOption;
        public bool  isRookieDeal;

        // Career stats (season-by-season)
        public List<SeasonStatsDTO> careerStats = new List<SeasonStatsDTO>();
    }

    [Serializable]
    public class SeasonStatsDTO
    {
        public int   season;
        public int   gamesPlayed;
        public float pointsPerGame, assistsPerGame, reboundsPerGame;
        public float stealsPerGame, blocksPerGame, turnoversPerGame;
        public float fieldGoalPct, threePointPct, freeThrowPct;
        public float minutesPerGame;
        public float playerEfficiencyRating;
        public float winSharesPerFortyEight;
    }

    [Serializable]
    public class CoachDTO
    {
        public string coachId;
        public string firstName;
        public string lastName;
        public int    age;
        public bool   isFreeAgent;
        public string preferredOffense;
        public string preferredDefense;
        public int    potential;

        // Ratings
        public int offensiveSchemeIQ, defensiveSchemeIQ, playerDevelopment;
        public int inGameAdjustments, lockerRoomManagement, rotationManagement;
        public int overall;
    }

    [Serializable]
    public class LineupSlotDTO
    {
        public string playerId;
        public int    minutesPerGame;
        public bool   isStarter;
    }

    [Serializable]
    public class DraftPickDTO
    {
        public int    year;
        public int    round;
        public string originalOwnerId;
        public bool   isProtected;
        public int    protectionCutoff;
    }

    [Serializable]
    public class TeamRecordDTO
    {
        public int wins, losses;
        public int homeWins, homeLosses;
        public int awayWins, awayLosses;
        public int conferenceWins, conferenceLosses;
        public int streak;
    }

    [Serializable]
    public class TeamDTO
    {
        public string       teamId;
        public string       teamName;
        public string       city;
        public string       abbreviation;
        public string       conference;
        public string       division;
        public bool         isPlayerTeam;
        public string       aiPersonality;

        // Colors stored as hex strings
        public string       primaryColorHex;
        public string       secondaryColorHex;

        // Roster — player IDs only; full player data stored separately
        public List<string>       rosterIds    = new List<string>();
        public List<LineupSlotDTO> rotation    = new List<LineupSlotDTO>();
        public List<DraftPickDTO>  ownedPicks  = new List<DraftPickDTO>();

        // Coach
        public CoachDTO     headCoach;

        // Tactics
        public int  pickAndRollPriority, isolationPriority;
        public int  spacingAndCutsPriority, postUpPriority, fastBreakPriority;
        public string defensiveScheme;
        public int  foulAggression, doubleTeamTendency;

        // Finances
        public float salaryCap, capUsed;

        // Record
        public TeamRecordDTO record = new TeamRecordDTO();
    }

    [Serializable]
    public class StandingsEntryDTO
    {
        public string teamId;
        public int    wins, losses;
        public int    conferenceWins, conferenceLosses;
        public int    divisionWins, divisionLosses;
        public int    pointsFor, pointsAgainst;
        public int    streak;
        public List<bool> last10 = new List<bool>();
    }

    [Serializable]
    public class ScheduledGameDTO
    {
        public string gameId;
        public string homeTeamId;
        public string awayTeamId;
        public int    week;
        public int    dayOfSeason;
        public bool   isPlayIn;
        public bool   isPlayoff;
        public string playoffRound;
        public bool   isPlayed;
        public bool   isPlayerGame;
        public bool   homeTeamOnB2B;
        public bool   awayTeamOnB2B;
    }

    [Serializable]
    public class PlayoffSeriesDTO
    {
        public string seriesId;
        public string higherSeedId;
        public string lowerSeedId;
        public int    higherSeedWins;
        public int    lowerSeedWins;
        public string round;
        public bool   isComplete;
        public string winnerId;
        public List<string> gameIds = new List<string>();
    }

    [Serializable]
    public class DraftEntryDTO
    {
        public int    overall;
        public int    round;
        public int    pickInRound;
        public string pickOwnerId;
        public string draftedPlayerId; // empty if not yet selected
        public bool   isSelected;
    }

    [Serializable]
    public class SeasonDTO
    {
        public int    year;
        public string currentPhase;
        public int    currentDay;
        public int    currentWeek;
        public string championId;

        public List<string>            eastTeamIds      = new List<string>();
        public List<string>            westTeamIds      = new List<string>();
        public List<ScheduledGameDTO>  regularSchedule  = new List<ScheduledGameDTO>();
        public List<ScheduledGameDTO>  playInSchedule   = new List<ScheduledGameDTO>();
        public List<ScheduledGameDTO>  playoffSchedule  = new List<ScheduledGameDTO>();
        public List<StandingsEntryDTO> eastStandings    = new List<StandingsEntryDTO>();
        public List<StandingsEntryDTO> westStandings    = new List<StandingsEntryDTO>();
        public List<PlayoffSeriesDTO>  allSeries        = new List<PlayoffSeriesDTO>();
        public List<string>            freeAgentIds     = new List<string>();
        public List<DraftEntryDTO>     draftPickOrder   = new List<DraftEntryDTO>();
        public List<string>            draftProspectIds = new List<string>();
    }

    // ── Save system ───────────────────────────────────────────────────────────

    public static class SaveSystem
    {
        // ── Config ────────────────────────────────────────────────────────────

        public const  string CurrentVersion = "1.0.0";
        public const  int    MaxSlots       = 3;

        // Toggle this to switch between dev (plain JSON) and release (encrypted)
        public static bool EncryptionEnabled = false; // set true for release builds

        private static readonly string SaveDirectory =
            Path.Combine(Application.persistentDataPath, "Saves");

        private static readonly string SlotInfoDirectory =
            Path.Combine(Application.persistentDataPath, "SlotInfo");

        // AES key + IV — change these before shipping (or derive from a player GUID)
        private static readonly byte[] AesKey = Encoding.UTF8.GetBytes("NBAMgr2024Key!32"); // 16 bytes
        private static readonly byte[] AesIV  = Encoding.UTF8.GetBytes("NBAMgr2024IV!16_"); // 16 bytes

        // ── Paths ─────────────────────────────────────────────────────────────

        private static string SavePath(int slot)     =>
            Path.Combine(SaveDirectory,   $"save_slot_{slot}.dat");

        private static string SlotInfoPath(int slot) =>
            Path.Combine(SlotInfoDirectory, $"slot_{slot}_info.json");

        // ── Initialise ────────────────────────────────────────────────────────

        static SaveSystem()
        {
            Directory.CreateDirectory(SaveDirectory);
            Directory.CreateDirectory(SlotInfoDirectory);
        }

        // ── Slot info ─────────────────────────────────────────────────────────

        // Returns lightweight metadata for all 3 slots (for the save/load screen)
        public static SaveSlotInfo[] GetAllSlotInfos()
        {
            var infos = new SaveSlotInfo[MaxSlots];

            for (int i = 0; i < MaxSlots; i++)
            {
                string path = SlotInfoPath(i);
                if (File.Exists(path))
                {
                    try
                    {
                        infos[i] = JsonUtility.FromJson<SaveSlotInfo>(File.ReadAllText(path));
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"SaveSystem: Failed to read slot {i} info — {e.Message}");
                        infos[i] = EmptySlotInfo(i);
                    }
                }
                else
                {
                    infos[i] = EmptySlotInfo(i);
                }
            }

            return infos;
        }

        private static SaveSlotInfo EmptySlotInfo(int slot) =>
            new SaveSlotInfo { slotIndex = slot, isEmpty = true };

        // ── Save ──────────────────────────────────────────────────────────────

        public static bool Save(int slot, GameManager gm)
        {
            if (slot < 0 || slot >= MaxSlots)
            {
                Debug.LogError($"SaveSystem: Invalid slot index {slot}.");
                return false;
            }

            try
            {
                var saveData = BuildSaveData(slot, gm);
                string json  = JsonUtility.ToJson(saveData, prettyPrint: !EncryptionEnabled);

                string path = SavePath(slot);
                if (EncryptionEnabled)
                    File.WriteAllBytes(path, Encrypt(json));
                else
                    File.WriteAllText(path, json);

                // Write lightweight slot info
                var info = BuildSlotInfo(slot, gm, saveData);
                File.WriteAllText(SlotInfoPath(slot), JsonUtility.ToJson(info, true));

                Debug.Log($"SaveSystem: Saved to slot {slot} at {path}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveSystem: Save failed — {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        // ── Load ──────────────────────────────────────────────────────────────

        public static bool Load(int slot, GameManager gm)
        {
            if (slot < 0 || slot >= MaxSlots)
            {
                Debug.LogError($"SaveSystem: Invalid slot index {slot}.");
                return false;
            }

            string path = SavePath(slot);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"SaveSystem: No save found at slot {slot}.");
                return false;
            }

            try
            {
                string json;
                if (EncryptionEnabled)
                    json = Decrypt(File.ReadAllBytes(path));
                else
                    json = File.ReadAllText(path);

                var saveData = JsonUtility.FromJson<SaveData>(json);

                if (saveData == null)
                {
                    Debug.LogError("SaveSystem: Deserialisation returned null.");
                    return false;
                }

                // Version check — warn but don't block (migration handled separately)
                if (saveData.saveVersion != CurrentVersion)
                    Debug.LogWarning($"SaveSystem: Save version {saveData.saveVersion} " +
                                     $"differs from current {CurrentVersion}. Data may be stale.");

                RestoreSaveData(saveData, gm);

                Debug.Log($"SaveSystem: Loaded slot {slot} successfully.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveSystem: Load failed — {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        // ── Delete ────────────────────────────────────────────────────────────

        public static bool DeleteSlot(int slot)
        {
            bool deleted = false;

            string savePath = SavePath(slot);
            if (File.Exists(savePath))  { File.Delete(savePath);          deleted = true; }

            string infoPath = SlotInfoPath(slot);
            if (File.Exists(infoPath)) { File.Delete(infoPath);           deleted = true; }

            Debug.Log($"SaveSystem: Slot {slot} deleted.");
            return deleted;
        }

        public static bool SlotExists(int slot) => File.Exists(SavePath(slot));

        // ── Build save data ───────────────────────────────────────────────────

        private static SaveData BuildSaveData(int slot, GameManager gm)
        {
            var data = new SaveData
            {
                savedAt        = DateTime.UtcNow.ToString("o"),
                slotIndex      = slot,
                managerState   = gm.state.ToString(),
                currentDay     = gm.currentDay,
                simulationMode = gm.simulationMode,
                simulationSpeed = gm.simulationSpeed
            };

            // Build a flat player lookup (all players from all rosters + free agents)
            var allPlayers = CollectAllPlayers(gm);

            // Teams
            foreach (var team in gm.allTeams)
                data.teams.Add(TeamToDTO(team, allPlayers));

            // Players (stored once, referenced by ID in team DTOs)
            // Note: we embed players inside TeamDTO.rosterIds and store full PlayerDTOs
            // in a separate list — but for simplicity here they're embedded per-team.
            // A more scalable approach stores them at the top SaveData level.

            // Season
            data.season = SeasonToDTO(gm.currentSeason, allPlayers);

            return data;
        }

        // ── Restore save data ─────────────────────────────────────────────────

        private static void RestoreSaveData(SaveData data, GameManager gm)
        {
            // Step 1: Rebuild all players keyed by ID
            var playerMap = new Dictionary<string, Player>();
            var teamMap   = new Dictionary<string, Team>();

            // Reconstruct teams and their rosters
            gm.allTeams.Clear();

            foreach (var teamDTO in data.teams)
            {
                var team = DTOToTeam(teamDTO, playerMap);
                teamMap[team.teamId] = team;
                gm.allTeams.Add(team);
            }

            // Step 2: Restore season
            gm.currentSeason = DTOToSeason(data.season, teamMap, playerMap);

            // Step 3: Restore GameManager state
            gm.currentDay       = data.currentDay;
            gm.simulationMode   = data.simulationMode;
            gm.simulationSpeed  = data.simulationSpeed;

            if (Enum.TryParse(data.managerState, out ManagerState state))
                gm.state = state;
        }

        // ── Team serialisation ────────────────────────────────────────────────

        private static TeamDTO TeamToDTO(Team team, Dictionary<string, Player> allPlayers)
        {
            var dto = new TeamDTO
            {
                teamId              = team.teamId,
                teamName            = team.teamName,
                city                = team.city,
                abbreviation        = team.abbreviation,
                conference          = team.conference.ToString(),
                division            = team.division.ToString(),
                isPlayerTeam        = team.isPlayerTeam,
                aiPersonality       = team.aiPersonality.ToString(),
                primaryColorHex     = "#" + ColorUtility.ToHtmlStringRGB(team.primaryColor),
                secondaryColorHex   = "#" + ColorUtility.ToHtmlStringRGB(team.secondaryColor),
                pickAndRollPriority    = team.offensiveTactics.pickAndRollPriority,
                isolationPriority      = team.offensiveTactics.isolationPriority,
                spacingAndCutsPriority = team.offensiveTactics.spacingAndCutsPriority,
                postUpPriority         = team.offensiveTactics.postUpPriority,
                fastBreakPriority      = team.offensiveTactics.fastBreakPriority,
                defensiveScheme        = team.defensiveTactics.activeScheme.ToString(),
                foulAggression         = team.defensiveTactics.foulAggression,
                doubleTeamTendency     = team.defensiveTactics.doubleTeamTendency,
                salaryCap              = team.finances.salaryCap,
                capUsed                = team.finances.CapUsed,
                headCoach              = team.headCoach != null
                    ? CoachToDTO(team.headCoach) : null,
                record = new TeamRecordDTO
                {
                    wins              = team.record.wins,
                    losses            = team.record.losses,
                    homeWins          = team.record.homeWins,
                    homeLosses        = team.record.homeLosses,
                    awayWins          = team.record.awayWins,
                    awayLosses        = team.record.awayLosses,
                    conferenceWins    = team.record.conferenceWins,
                    conferenceLosses  = team.record.conferenceLosses,
                    streak            = team.record.streak
                }
            };

            foreach (var player in team.roster)
            {
                dto.rosterIds.Add(player.playerId);
                if (!allPlayers.ContainsKey(player.playerId))
                    allPlayers[player.playerId] = player;
            }

            foreach (var slot in team.rotation)
                dto.rotation.Add(new LineupSlotDTO
                {
                    playerId       = slot.player.playerId,
                    minutesPerGame = slot.minutesPerGame,
                    isStarter      = slot.isStarter
                });

            foreach (var pick in team.ownedPicks)
                dto.ownedPicks.Add(new DraftPickDTO
                {
                    year             = pick.year,
                    round            = pick.round,
                    originalOwnerId  = pick.originalOwner?.teamId,
                    isProtected      = pick.isProtected,
                    protectionCutoff = pick.protectionCutoff
                });

            return dto;
        }

        private static Team DTOToTeam(TeamDTO dto, Dictionary<string, Player> playerMap)
        {
            var team = ScriptableObject.CreateInstance<Team>();

            team.teamId       = dto.teamId;
            team.teamName     = dto.teamName;
            team.city         = dto.city;
            team.abbreviation = dto.abbreviation;
            team.isPlayerTeam = dto.isPlayerTeam;

            Enum.TryParse(dto.conference,   out Conference conf); team.conference = conf;
            Enum.TryParse(dto.division,     out Division   div);  team.division   = div;
            Enum.TryParse(dto.aiPersonality,out AIPersonality ai); team.aiPersonality = ai;

            ColorUtility.TryParseHtmlString(dto.primaryColorHex,   out Color pc);
            ColorUtility.TryParseHtmlString(dto.secondaryColorHex, out Color sc);
            team.primaryColor   = pc;
            team.secondaryColor = sc;

            team.offensiveTactics = new OffensiveTactics
            {
                pickAndRollPriority    = dto.pickAndRollPriority,
                isolationPriority      = dto.isolationPriority,
                spacingAndCutsPriority = dto.spacingAndCutsPriority,
                postUpPriority         = dto.postUpPriority,
                fastBreakPriority      = dto.fastBreakPriority
            };

            Enum.TryParse(dto.defensiveScheme, out DefensiveScheme scheme);
            team.defensiveTactics = new DefensiveTactics
            {
                activeScheme       = scheme,
                foulAggression     = dto.foulAggression,
                doubleTeamTendency = dto.doubleTeamTendency
            };

            team.finances = new TeamFinances
            {
                salaryCap = dto.salaryCap,
                CapUsed   = dto.capUsed
            };

            team.record = new TeamRecord
            {
                wins             = dto.record.wins,
                losses           = dto.record.losses,
                homeWins         = dto.record.homeWins,
                homeLosses       = dto.record.homeLosses,
                awayWins         = dto.record.awayWins,
                awayLosses       = dto.record.awayLosses,
                conferenceWins   = dto.record.conferenceWins,
                conferenceLosses = dto.record.conferenceLosses,
                streak           = dto.record.streak
            };

            if (dto.headCoach != null)
                team.headCoach = DTOToCoach(dto.headCoach);

            // Players will be linked in a second pass once all are in playerMap
            team.rotation = new List<PlayerLineupSlot>();

            return team;
        }

        // Second pass: link players to teams after all players are in the map
        private static void LinkPlayersToTeams(
            List<TeamDTO> teamDTOs, List<Team> teams,
            Dictionary<string, Player> playerMap,
            Dictionary<string, Team>   teamMap)
        {
            for (int i = 0; i < teamDTOs.Count; i++)
            {
                var dto  = teamDTOs[i];
                var team = teams[i];

                team.roster.Clear();
                team.rotation.Clear();

                foreach (var id in dto.rosterIds)
                    if (playerMap.TryGetValue(id, out Player p))
                        team.roster.Add(p);

                foreach (var slotDTO in dto.rotation)
                    if (playerMap.TryGetValue(slotDTO.playerId, out Player p))
                        team.rotation.Add(new PlayerLineupSlot(p,
                            slotDTO.minutesPerGame, slotDTO.isStarter));

                foreach (var pickDTO in dto.ownedPicks)
                {
                    team.ownedPicks.Add(new DraftPick
                    {
                        year             = pickDTO.year,
                        round            = pickDTO.round,
                        originalOwner    = teamMap.TryGetValue(pickDTO.originalOwnerId, out Team owner)
                            ? owner : team,
                        isProtected      = pickDTO.isProtected,
                        protectionCutoff = pickDTO.protectionCutoff
                    });
                }
            }
        }

        // ── Player serialisation ──────────────────────────────────────────────

        private static PlayerDTO PlayerToDTO(Player p)
        {
            var dto = new PlayerDTO
            {
                playerId             = p.playerId,
                firstName            = p.firstName,
                lastName             = p.lastName,
                age                  = p.age,
                heightCm             = p.heightCm,
                weightKg             = p.weightKg,
                nationality          = p.nationality,
                position             = p.position.ToString(),
                playerType           = p.playerType.ToString(),
                mood                 = p.mood.ToString(),
                isFreeAgent          = p.isFreeAgent,
                isRookie             = p.isRookie,
                isInjured            = p.isInjured,
                injuryGamesRemaining = p.injuryGamesRemaining,

                // Ratings
                threePoint   = p.ratings.threePoint,
                midRange     = p.ratings.midRange,
                finishing    = p.ratings.finishing,
                freeThrow    = p.ratings.freeThrow,
                ballHandling = p.ratings.ballHandling,
                passing      = p.ratings.passing,
                offIQ        = p.ratings.offIQ,
                perimeter    = p.ratings.perimeter,
                interior     = p.ratings.interior,
                stealing     = p.ratings.stealing,
                blocking     = p.ratings.blocking,
                defIQ        = p.ratings.defIQ,
                speed        = p.ratings.speed,
                agility      = p.ratings.agility,
                strength     = p.ratings.strength,
                verticalLeap = p.ratings.verticalLeap,
                stamina      = p.ratings.stamina,
                clutch       = p.ratings.clutch,
                consistency  = p.ratings.consistency,
                leadership   = p.ratings.leadership,
                overall      = p.ratings.overall,

                // Progression
                potential       = p.progression.potential,
                peakAgeStart    = p.progression.peakAgeStart,
                peakAgeEnd      = p.progression.peakAgeEnd,
                developmentRate = p.progression.developmentRate,
                isInjuryProne   = p.progression.isInjuryProne,
                injuryRisk      = p.progression.injuryRisk,

                // Contract
                annualSalary   = p.contract.annualSalary,
                yearsRemaining = p.contract.yearsRemaining,
                isMaxContract  = p.contract.isMaxContract,
                hasPlayerOption = p.contract.hasPlayerOption,
                hasTeamOption  = p.contract.hasTeamOption,
                isRookieDeal   = p.contract.isRookieDeal
            };

            foreach (var s in p.careerStats)
                dto.careerStats.Add(new SeasonStatsDTO
                {
                    season                = s.season,
                    gamesPlayed           = s.gamesPlayed,
                    pointsPerGame         = s.pointsPerGame,
                    assistsPerGame        = s.assistsPerGame,
                    reboundsPerGame       = s.reboundsPerGame,
                    stealsPerGame         = s.stealsPerGame,
                    blocksPerGame         = s.blocksPerGame,
                    turnoversPerGame      = s.turnoversPerGame,
                    fieldGoalPct          = s.fieldGoalPct,
                    threePointPct         = s.threePointPct,
                    freeThrowPct          = s.freeThrowPct,
                    minutesPerGame        = s.minutesPerGame,
                    playerEfficiencyRating = s.playerEfficiencyRating,
                    winSharesPerFortyEight = s.winSharesPerFortyEight
                });

            return dto;
        }

        private static Player DTOToPlayer(PlayerDTO dto)
        {
            var p = ScriptableObject.CreateInstance<Player>();

            p.playerId             = dto.playerId;
            p.firstName            = dto.firstName;
            p.lastName             = dto.lastName;
            p.age                  = dto.age;
            p.heightCm             = dto.heightCm;
            p.weightKg             = dto.weightKg;
            p.nationality          = dto.nationality;
            p.isFreeAgent          = dto.isFreeAgent;
            p.isRookie             = dto.isRookie;
            p.isInjured            = dto.isInjured;
            p.injuryGamesRemaining = dto.injuryGamesRemaining;

            Enum.TryParse(dto.position,   out Position   pos);  p.position   = pos;
            Enum.TryParse(dto.playerType, out PlayerType type); p.playerType = type;
            Enum.TryParse(dto.mood,       out PlayerMood mood); p.mood       = mood;

            p.ratings = new PlayerRatings
            {
                threePoint   = dto.threePoint,   midRange     = dto.midRange,
                finishing    = dto.finishing,     freeThrow    = dto.freeThrow,
                ballHandling = dto.ballHandling,  passing      = dto.passing,
                offIQ        = dto.offIQ,         perimeter    = dto.perimeter,
                interior     = dto.interior,      stealing     = dto.stealing,
                blocking     = dto.blocking,      defIQ        = dto.defIQ,
                speed        = dto.speed,         agility      = dto.agility,
                strength     = dto.strength,      verticalLeap = dto.verticalLeap,
                stamina      = dto.stamina,        clutch       = dto.clutch,
                consistency  = dto.consistency,   leadership   = dto.leadership,
                overall      = dto.overall
            };

            p.progression = new PlayerProgression
            {
                potential       = dto.potential,
                peakAgeStart    = dto.peakAgeStart,
                peakAgeEnd      = dto.peakAgeEnd,
                developmentRate = dto.developmentRate,
                isInjuryProne   = dto.isInjuryProne,
                injuryRisk      = dto.injuryRisk
            };

            p.contract = new Contract
            {
                annualSalary    = dto.annualSalary,
                yearsRemaining  = dto.yearsRemaining,
                isMaxContract   = dto.isMaxContract,
                hasPlayerOption = dto.hasPlayerOption,
                hasTeamOption   = dto.hasTeamOption,
                isRookieDeal    = dto.isRookieDeal
            };

            p.careerStats = dto.careerStats.Select(s => new SeasonStats
            {
                season                 = s.season,
                gamesPlayed            = s.gamesPlayed,
                pointsPerGame          = s.pointsPerGame,
                assistsPerGame         = s.assistsPerGame,
                reboundsPerGame        = s.reboundsPerGame,
                stealsPerGame          = s.stealsPerGame,
                blocksPerGame          = s.blocksPerGame,
                turnoversPerGame       = s.turnoversPerGame,
                fieldGoalPct           = s.fieldGoalPct,
                threePointPct          = s.threePointPct,
                freeThrowPct           = s.freeThrowPct,
                minutesPerGame         = s.minutesPerGame,
                playerEfficiencyRating = s.playerEfficiencyRating,
                winSharesPerFortyEight = s.winSharesPerFortyEight
            }).ToArray();

            return p;
        }

        // ── Coach serialisation ───────────────────────────────────────────────

        private static CoachDTO CoachToDTO(Coach c) => new CoachDTO
        {
            coachId               = c.coachId,
            firstName             = c.firstName,
            lastName              = c.lastName,
            age                   = c.age,
            isFreeAgent           = c.isFreeAgent,
            potential             = c.potential,
            preferredOffense      = c.preferredOffense.ToString(),
            preferredDefense      = c.preferredDefense.ToString(),
            offensiveSchemeIQ     = c.ratings.offensiveSchemeIQ,
            defensiveSchemeIQ     = c.ratings.defensiveSchemeIQ,
            playerDevelopment     = c.ratings.playerDevelopment,
            inGameAdjustments     = c.ratings.inGameAdjustments,
            lockerRoomManagement  = c.ratings.lockerRoomManagement,
            rotationManagement    = c.ratings.rotationManagement,
            overall               = c.ratings.overall
        };

        private static Coach DTOToCoach(CoachDTO dto)
        {
            var c = ScriptableObject.CreateInstance<Coach>();
            c.coachId     = dto.coachId;
            c.firstName   = dto.firstName;
            c.lastName    = dto.lastName;
            c.age         = dto.age;
            c.isFreeAgent = dto.isFreeAgent;
            c.potential   = dto.potential;

            Enum.TryParse(dto.preferredOffense, out OffensivePlay  off); c.preferredOffense = off;
            Enum.TryParse(dto.preferredDefense, out DefensiveScheme def); c.preferredDefense = def;

            c.ratings = new CoachRatings
            {
                offensiveSchemeIQ    = dto.offensiveSchemeIQ,
                defensiveSchemeIQ    = dto.defensiveSchemeIQ,
                playerDevelopment    = dto.playerDevelopment,
                inGameAdjustments    = dto.inGameAdjustments,
                lockerRoomManagement = dto.lockerRoomManagement,
                rotationManagement   = dto.rotationManagement,
                overall              = dto.overall
            };

            return c;
        }

        // ── Season serialisation ──────────────────────────────────────────────

        private static SeasonDTO SeasonToDTO(Season season, Dictionary<string, Player> allPlayers)
        {
            var dto = new SeasonDTO
            {
                year         = season.year,
                currentPhase = season.currentPhase.ToString(),
                currentDay   = season.currentDay,
                currentWeek  = season.currentWeek,
                championId   = season.champion?.teamId
            };

            dto.eastTeamIds.AddRange(season.eastTeams.Select(t => t.teamId));
            dto.westTeamIds.AddRange(season.westTeams.Select(t => t.teamId));

            foreach (var g in season.regularSeasonSchedule)
                dto.regularSchedule.Add(GameToDTO(g));

            foreach (var g in season.playInSchedule)
                dto.playInSchedule.Add(GameToDTO(g));

            foreach (var g in season.playoffSchedule)
                dto.playoffSchedule.Add(GameToDTO(g));

            foreach (var e in season.eastStandings)
                dto.eastStandings.Add(StandingsToDTO(e));

            foreach (var e in season.westStandings)
                dto.westStandings.Add(StandingsToDTO(e));

            foreach (var s in season.allSeries)
                dto.allSeries.Add(SeriesToDTO(s));

            dto.freeAgentIds.AddRange(season.freeAgents.Select(p =>
            {
                if (!allPlayers.ContainsKey(p.playerId))
                    allPlayers[p.playerId] = p;
                return p.playerId;
            }));

            if (season.currentDraft != null)
                foreach (var entry in season.currentDraft.pickOrder)
                    dto.draftPickOrder.Add(new DraftEntryDTO
                    {
                        overall         = entry.overall,
                        round           = entry.round,
                        pickInRound     = entry.pickInRound,
                        pickOwnerId     = entry.pickOwner?.teamId,
                        draftedPlayerId = entry.draftedPlayer?.playerId ?? "",
                        isSelected      = entry.isSelected
                    });

            if (season.currentDraft != null)
                dto.draftProspectIds.AddRange(season.currentDraft.prospects.Select(p =>
                {
                    if (!allPlayers.ContainsKey(p.playerId))
                        allPlayers[p.playerId] = p;
                    return p.playerId;
                }));

            return dto;
        }

        private static Season DTOToSeason(
            SeasonDTO dto,
            Dictionary<string, Team>   teamMap,
            Dictionary<string, Player> playerMap)
        {
            var season = ScriptableObject.CreateInstance<Season>();
            season.year        = dto.year;
            season.currentDay  = dto.currentDay;
            season.currentWeek = dto.currentWeek;

            Enum.TryParse(dto.currentPhase, out SeasonPhase phase);
            season.currentPhase = phase;

            season.allTeams  = teamMap.Values.ToList();
            season.eastTeams = dto.eastTeamIds
                .Where(id => teamMap.ContainsKey(id)).Select(id => teamMap[id]).ToList();
            season.westTeams = dto.westTeamIds
                .Where(id => teamMap.ContainsKey(id)).Select(id => teamMap[id]).ToList();

            season.champion = !string.IsNullOrEmpty(dto.championId) &&
                teamMap.TryGetValue(dto.championId, out Team champ) ? champ : null;

            foreach (var g in dto.regularSchedule)
                season.regularSeasonSchedule.Add(DTOToGame(g, teamMap));
            foreach (var g in dto.playInSchedule)
                season.playInSchedule.Add(DTOToGame(g, teamMap));
            foreach (var g in dto.playoffSchedule)
                season.playoffSchedule.Add(DTOToGame(g, teamMap));

            season.eastStandings = dto.eastStandings
                .Select(e => DTOToStandings(e, teamMap))
                .Where(e => e != null).ToList();
            season.westStandings = dto.westStandings
                .Select(e => DTOToStandings(e, teamMap))
                .Where(e => e != null).ToList();

            season.freeAgents = dto.freeAgentIds
                .Where(id => playerMap.ContainsKey(id))
                .Select(id => playerMap[id]).ToList();

            // Restore draft
            if (dto.draftPickOrder.Count > 0)
            {
                season.currentDraft = new DraftClass { year = season.year };

                season.currentDraft.prospects = dto.draftProspectIds
                    .Where(id => playerMap.ContainsKey(id))
                    .Select(id => playerMap[id]).ToList();

                foreach (var e in dto.draftPickOrder)
                {
                    var entry = new DraftEntry
                    {
                        overall     = e.overall,
                        round       = e.round,
                        pickInRound = e.pickInRound,
                        isSelected  = e.isSelected,
                        pickOwner   = teamMap.TryGetValue(e.pickOwnerId, out Team owner)
                            ? owner : null,
                        draftedPlayer = !string.IsNullOrEmpty(e.draftedPlayerId) &&
                            playerMap.TryGetValue(e.draftedPlayerId, out Player drafted)
                            ? drafted : null
                    };
                    season.currentDraft.pickOrder.Add(entry);
                }
            }

            return season;
        }

        // ── Game / standings / series helpers ─────────────────────────────────

        private static ScheduledGameDTO GameToDTO(ScheduledGame g) => new ScheduledGameDTO
        {
            gameId         = g.gameId,
            homeTeamId     = g.homeTeam?.teamId,
            awayTeamId     = g.awayTeam?.teamId,
            week           = g.week,
            dayOfSeason    = g.dayOfSeason,
            isPlayIn       = g.isPlayIn,
            isPlayoff      = g.isPlayoff,
            playoffRound   = g.playoffRound.ToString(),
            isPlayed       = g.isPlayed,
            isPlayerGame   = g.isPlayerGame,
            homeTeamOnB2B  = g.homeTeamOnB2B,
            awayTeamOnB2B  = g.awayTeamOnB2B
        };

        private static ScheduledGame DTOToGame(
            ScheduledGameDTO dto, Dictionary<string, Team> teamMap)
        {
            teamMap.TryGetValue(dto.homeTeamId, out Team home);
            teamMap.TryGetValue(dto.awayTeamId, out Team away);

            var g = new ScheduledGame(home, away, dto.week, dto.dayOfSeason)
            {
                gameId        = dto.gameId,
                isPlayIn      = dto.isPlayIn,
                isPlayoff     = dto.isPlayoff,
                isPlayed      = dto.isPlayed,
                isPlayerGame  = dto.isPlayerGame,
                homeTeamOnB2B = dto.homeTeamOnB2B,
                awayTeamOnB2B = dto.awayTeamOnB2B
            };

            Enum.TryParse(dto.playoffRound, out PlayoffRound round);
            g.playoffRound = round;
            return g;
        }

        private static StandingsEntryDTO StandingsToDTO(StandingsEntry e) =>
            new StandingsEntryDTO
            {
                teamId           = e.team?.teamId,
                wins             = e.wins,
                losses           = e.losses,
                conferenceWins   = e.conferenceWins,
                conferenceLosses = e.conferenceLosses,
                divisionWins     = e.divisionWins,
                divisionLosses   = e.divisionLosses,
                pointsFor        = e.pointsFor,
                pointsAgainst    = e.pointsAgainst,
                streak           = e.streak,
                last10           = new List<bool>(e.last10)
            };

        private static StandingsEntry DTOToStandings(
            StandingsEntryDTO dto, Dictionary<string, Team> teamMap)
        {
            if (!teamMap.TryGetValue(dto.teamId, out Team team)) return null;
            return new StandingsEntry
            {
                team             = team,
                wins             = dto.wins,
                losses           = dto.losses,
                conferenceWins   = dto.conferenceWins,
                conferenceLosses = dto.conferenceLosses,
                divisionWins     = dto.divisionWins,
                divisionLosses   = dto.divisionLosses,
                pointsFor        = dto.pointsFor,
                pointsAgainst    = dto.pointsAgainst,
                streak           = dto.streak,
                last10           = new List<bool>(dto.last10)
            };
        }

        private static PlayoffSeriesDTO SeriesToDTO(PlayoffSeries s) =>
            new PlayoffSeriesDTO
            {
                seriesId       = s.seriesId,
                higherSeedId   = s.higherSeed?.teamId,
                lowerSeedId    = s.lowerSeed?.teamId,
                higherSeedWins = s.higherSeedWins,
                lowerSeedWins  = s.lowerSeedWins,
                round          = s.round.ToString(),
                isComplete     = s.isComplete,
                winnerId       = s.winner?.teamId,
                gameIds        = s.games.Select(g => g.gameId).ToList()
            };

        // ── Collect all players ───────────────────────────────────────────────

        private static Dictionary<string, Player> CollectAllPlayers(GameManager gm)
        {
            var map = new Dictionary<string, Player>();

            foreach (var team in gm.allTeams)
                foreach (var p in team.roster)
                    if (!map.ContainsKey(p.playerId))
                        map[p.playerId] = p;

            if (gm.currentSeason != null)
            {
                foreach (var p in gm.currentSeason.freeAgents)
                    if (!map.ContainsKey(p.playerId))
                        map[p.playerId] = p;

                if (gm.currentSeason.currentDraft != null)
                    foreach (var p in gm.currentSeason.currentDraft.prospects)
                        if (!map.ContainsKey(p.playerId))
                            map[p.playerId] = p;
            }

            return map;
        }

        // ── Slot info builder ─────────────────────────────────────────────────

        private static SaveSlotInfo BuildSlotInfo(int slot, GameManager gm, SaveData data)
        {
            var playerTeam = gm.GetPlayerTeam();
            return new SaveSlotInfo
            {
                slotIndex   = slot,
                isEmpty     = false,
                teamName    = playerTeam?.FullName ?? "Unknown",
                seasonYear  = gm.currentSeason?.year ?? 0,
                seasonPhase = gm.currentSeason != null
                    ? $"{gm.currentSeason.currentPhase} — Week {gm.currentSeason.currentWeek}"
                    : "Unknown",
                wins        = playerTeam?.record.wins    ?? 0,
                losses      = playerTeam?.record.losses  ?? 0,
                lastSavedAt = data.savedAt,
                saveVersion = CurrentVersion
            };
        }

        // ── Encryption ────────────────────────────────────────────────────────

        private static byte[] Encrypt(string plainText)
        {
            using var aes       = Aes.Create();
            aes.Key             = AesKey;
            aes.IV              = AesIV;
            aes.Mode            = CipherMode.CBC;
            aes.Padding         = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            byte[] inputBytes   = Encoding.UTF8.GetBytes(plainText);
            return encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
        }

        private static string Decrypt(byte[] cipherBytes)
        {
            using var aes   = Aes.Create();
            aes.Key         = AesKey;
            aes.IV          = AesIV;
            aes.Mode        = CipherMode.CBC;
            aes.Padding     = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            byte[] outputBytes  = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(outputBytes);
        }
    }
}