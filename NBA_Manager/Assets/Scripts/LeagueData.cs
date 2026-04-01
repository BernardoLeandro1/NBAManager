using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAManager
{
    // ── Team template ─────────────────────────────────────────────────────────
    // Defines the fixed identity of a franchise — city, name, colors, conference.
    // Ratings and rosters are generated fresh each save.

    [Serializable]
    public class TeamTemplate
    {
        public string     teamId;
        public string     city;
        public string     teamName;
        public string     abbreviation;
        public Conference conference;
        public Division   division;
        public Color      primaryColor;
        public Color      secondaryColor;
        public bool       isCustom;       // true for extra expansion slots
    }

    // ── League data ───────────────────────────────────────────────────────────

    public static class LeagueData
    {
        // ── Name pools ────────────────────────────────────────────────────────

        private static readonly string[] FirstNames =
        {
            // --- NBA-INSPIRED (100) ---
            "LeBron","Stephen","Kevin","Kobe","Michael","Shaquille","Tim","Kawhi","Giannis","Nikola",
            "Luka","Joel","Jayson","Jimmy","Damian","Kyrie","Anthony","Paul","Russell","Chris",
            "James","Zion","Ja","Devin","Donovan","Jaylen","Brandon","Jaren","Tyrese","LaMelo",
            "Lonzo","Jrue","Klay","Draymond","Blake","DeMar","Pascal","Fred","OG","Jakob",
            "Kristaps","Rudy","Karl","Bam","Julius","Derrick","Zach","Aaron","Gordon","Tobias",
            "Mikal","Brook","Clint","Steven","Andrew","Wiggins","Buddy","Harrison","Reggie","Dennis",
            "Bogdan","Danilo","Immanuel","Coby","Patrick","Keldon","Dejounte","Tre","Jabari","Tari",
            "Keegan","Bennedict","Scoot","Amen","Ausar","Jaden","Shaedon","Anfernee","Cam","Rui",
            "Dillon","Naz","Markelle","Collin","Darius","Evan","Cade","Paolo","Victor","Scottie",

            // --- NON-NBA (100) ---
            "Oliver","Noah","Liam","Elijah","Lucas","Henry","Alexander","Benjamin","Theodore","Samuel",
            "Daniel","Matthew","Joseph","David","Leo","Julian","Nathan","Caleb","Isaac","Eli",
            "Adrian","Roman","Axel","Leon","Felix","Oscar","Hugo","Max","Sebastian","Dominic",
            "Vincent","Damien","Tristan","Rowan","Jasper","Kai","Ezra","Silas","Archer","Emmett",
            "Finn","Callum","Nolan","Reid","Spencer","Tucker","Wesley","Grant","Peter","Louis",
            "Raphael","Enzo","Diego","Santiago","Andres","Luis","Tomás","Miguel","João","Tiago",
            "Rafael","Gonçalo","Bruno","Ricardo","Filipe","André","Eduardo","Diogo","Martim","Afonso",
            "Arjun","Rohan","Aditya","Kabir","Ishaan","Omar","Ali","Hassan","Amir","Zayd",
            "Youssef","Tariq","Bilal","Wei","Jian","Hao","Jun","Min","Tao","Kenji",
            "Haruto","Yuki","Ren","Kaito","Riku","Takumi","Daichi","Sota","Hiro","Chen"
        };

        private static readonly string[] LastNames =
        {
            // --- US (100) ---
            "Smith","Johnson","Williams","Brown","Jones","Garcia","Miller","Davis","Rodriguez","Martinez",
            "Hernandez","Lopez","Gonzalez","Wilson","Anderson","Thomas","Taylor","Moore","Jackson","Martin",
            "Lee","Perez","Thompson","White","Harris","Sanchez","Clark","Ramirez","Lewis","Robinson",
            "Walker","Young","Allen","King","Wright","Scott","Torres","Nguyen","Hill","Flores",
            "Green","Adams","Nelson","Baker","Hall","Rivera","Campbell","Mitchell","Carter","Roberts",
            "Gomez","Phillips","Evans","Turner","Diaz","Parker","Cruz","Edwards","Collins","Reyes",
            "Stewart","Morris","Morales","Murphy","Cook","Rogers","Gutierrez","Ortiz","Morgan","Cooper",
            "Peterson","Bailey","Reed","Kelly","Howard","Ramos","Kim","Cox","Ward","Richardson",
            "Watson","Brooks","Chavez","Wood","James","Bennett","Gray","Mendoza","Ruiz","Hughes",

            // --- Europe (60, diversified) ---
            // Portugal (10)
            "Silva","Santos","Ferreira","Pereira","Oliveira","Costa","Rodrigues","Martins","Sousa","Gomes",

            // Spain (10)
            "Garcia","Fernandez","Gonzalez","Rodriguez","Lopez","Martinez","Sanchez","Perez","Gomez","Ruiz",

            // France (10)
            "Dubois","Moreau","Laurent","Simon","Michel","Lefebvre","Girard","Bonnet","Roux","Fontaine",

            // Italy (10)
            "Rossi","Russo","Ferrari","Esposito","Bianchi","Romano","Colombo","Ricci","Marino","Greco",

            // Germany (10)
            "Muller","Schmidt","Schneider","Fischer","Weber","Meyer","Wagner","Becker","Schulz","Hoffmann",

            // UK & Ireland (10)
            "Smith","Jones","Taylor","Brown","Williams","Wilson","Davies","Evans","Thomas","Roberts",

            // --- Rest of World (40) ---
            // South Asia (10)
            "Singh","Patel","Sharma","Khan","Gupta","Verma","Yadav","Malhotra","Chopra","Reddy",

            // Middle East (10)
            "Ali","Hassan","Hussein","Rahman","Aziz","Qureshi","Farooq","Khalid","Zaman","Iqbal",

            // China (10)
            "Wang","Li","Zhang","Liu","Chen","Yang","Huang","Zhao","Wu","Zhou",

            // Japan (10)
            "Tanaka","Suzuki","Takahashi","Sato","Ito","Kobayashi","Yamamoto","Nakamura","Kimura","Shimizu"
        };

        // ── 30 Default team templates ─────────────────────────────────────────
        // Fictional names, real cities, real conference/division structure.
        // Colors loosely inspired by real NBA teams for visual recognition.

        public static readonly List<TeamTemplate> DefaultTeams = new List<TeamTemplate>
        {
            // ── Eastern Conference — Atlantic ─────────────────────────────────
            new TeamTemplate { teamId="BOS", city="Boston",       teamName="Wildcats",   abbreviation="BOW", conference=Conference.East, division=Division.Atlantic, primaryColor=HexColor("007A33"), secondaryColor=HexColor("FFFFFF") },
            new TeamTemplate { teamId="BKN", city="Brooklyn",     teamName="Specters",   abbreviation="BKS", conference=Conference.East, division=Division.Atlantic, primaryColor=HexColor("000000"), secondaryColor=HexColor("FFFFFF") },
            new TeamTemplate { teamId="NYK", city="New York",     teamName="Titans",     abbreviation="NYT", conference=Conference.East, division=Division.Atlantic, primaryColor=HexColor("006BB6"), secondaryColor=HexColor("F58426") },
            new TeamTemplate { teamId="PHI", city="Philadelphia", teamName="Liberty",    abbreviation="PHL", conference=Conference.East, division=Division.Atlantic, primaryColor=HexColor("006BB6"), secondaryColor=HexColor("ED174C") },
            new TeamTemplate { teamId="TOR", city="Toronto",      teamName="Northmen",   abbreviation="TOR", conference=Conference.East, division=Division.Atlantic, primaryColor=HexColor("CE1141"), secondaryColor=HexColor("000000") },

            // ── Eastern Conference — Central ──────────────────────────────────
            new TeamTemplate { teamId="CHI", city="Chicago",      teamName="Wolves",     abbreviation="CHW", conference=Conference.East, division=Division.Central,  primaryColor=HexColor("CE1141"), secondaryColor=HexColor("000000") },
            new TeamTemplate { teamId="CLE", city="Cleveland",    teamName="Forge",      abbreviation="CLF", conference=Conference.East, division=Division.Central,  primaryColor=HexColor("860038"), secondaryColor=HexColor("FDBB30") },
            new TeamTemplate { teamId="DET", city="Detroit",      teamName="Engines",    abbreviation="DTE", conference=Conference.East, division=Division.Central,  primaryColor=HexColor("C8102E"), secondaryColor=HexColor("1D42BA") },
            new TeamTemplate { teamId="IND", city="Indiana",      teamName="Racers",     abbreviation="INR", conference=Conference.East, division=Division.Central,  primaryColor=HexColor("002D62"), secondaryColor=HexColor("FDBB30") },
            new TeamTemplate { teamId="MIL", city="Milwaukee",    teamName="Stags",      abbreviation="MLS", conference=Conference.East, division=Division.Central,  primaryColor=HexColor("00471B"), secondaryColor=HexColor("EEE1C6") },

            // ── Eastern Conference — Southeast ────────────────────────────────
            new TeamTemplate { teamId="ATL", city="Atlanta",      teamName="Hawks",      abbreviation="ATH", conference=Conference.East, division=Division.Southeast, primaryColor=HexColor("E03A3E"), secondaryColor=HexColor("C1D32F") },
            new TeamTemplate { teamId="CHA", city="Charlotte",    teamName="Storm",      abbreviation="CHS", conference=Conference.East, division=Division.Southeast, primaryColor=HexColor("1D1160"), secondaryColor=HexColor("00788C") },
            new TeamTemplate { teamId="MIA", city="Miami",        teamName="Waves",      abbreviation="MIW", conference=Conference.East, division=Division.Southeast, primaryColor=HexColor("98002E"), secondaryColor=HexColor("F9A01B") },
            new TeamTemplate { teamId="ORL", city="Orlando",      teamName="Comets",     abbreviation="ORC", conference=Conference.East, division=Division.Southeast, primaryColor=HexColor("0077C0"), secondaryColor=HexColor("C4CED4") },
            new TeamTemplate { teamId="WAS", city="Washington",   teamName="Eagles",     abbreviation="WAE", conference=Conference.East, division=Division.Southeast, primaryColor=HexColor("002B5C"), secondaryColor=HexColor("E31837") },

            // ── Western Conference — Northwest ────────────────────────────────
            new TeamTemplate { teamId="DEN", city="Denver",       teamName="Peaks",      abbreviation="DNP", conference=Conference.West, division=Division.Northwest, primaryColor=HexColor("0E2240"), secondaryColor=HexColor("FEC524") },
            new TeamTemplate { teamId="MIN", city="Minnesota",    teamName="Frost",      abbreviation="MNF", conference=Conference.West, division=Division.Northwest, primaryColor=HexColor("0C2340"), secondaryColor=HexColor("236192") },
            new TeamTemplate { teamId="OKC", city="Oklahoma City",teamName="Thunder",    abbreviation="OKT", conference=Conference.West, division=Division.Northwest, primaryColor=HexColor("007AC1"), secondaryColor=HexColor("EF3B24") },
            new TeamTemplate { teamId="POR", city="Portland",     teamName="Timbers",    abbreviation="POT", conference=Conference.West, division=Division.Northwest, primaryColor=HexColor("E03A3E"), secondaryColor=HexColor("000000") },
            new TeamTemplate { teamId="UTA", city="Utah",         teamName="Canyons",    abbreviation="UTC", conference=Conference.West, division=Division.Northwest, primaryColor=HexColor("002B5C"), secondaryColor=HexColor("00471B") },

            // ── Western Conference — Pacific ───────────────────────────────────
            new TeamTemplate { teamId="GSW", city="Golden State", teamName="Bay FC",     abbreviation="GSB", conference=Conference.West, division=Division.Pacific,   primaryColor=HexColor("1D428A"), secondaryColor=HexColor("FFC72C") },
            new TeamTemplate { teamId="LAC", city="Los Angeles",  teamName="Condors",    abbreviation="LAC", conference=Conference.West, division=Division.Pacific,   primaryColor=HexColor("C8102E"), secondaryColor=HexColor("1D428A") },
            new TeamTemplate { teamId="LAL", city="Los Angeles",  teamName="Royals",     abbreviation="LAR", conference=Conference.West, division=Division.Pacific,   primaryColor=HexColor("552583"), secondaryColor=HexColor("FDB927") },
            new TeamTemplate { teamId="PHX", city="Phoenix",      teamName="Suns",       abbreviation="PHX", conference=Conference.West, division=Division.Pacific,   primaryColor=HexColor("1D1160"), secondaryColor=HexColor("E56020") },
            new TeamTemplate { teamId="SAC", city="Sacramento",   teamName="Kings",      abbreviation="SAK", conference=Conference.West, division=Division.Pacific,   primaryColor=HexColor("5A2D81"), secondaryColor=HexColor("63727A") },

            // ── Western Conference — Southwest ─────────────────────────────────
            new TeamTemplate { teamId="DAL", city="Dallas",       teamName="Mavericks",  abbreviation="DAM", conference=Conference.West, division=Division.Southwest, primaryColor=HexColor("00538C"), secondaryColor=HexColor("002F5F") },
            new TeamTemplate { teamId="HOU", city="Houston",      teamName="Rockets",    abbreviation="HOR", conference=Conference.West, division=Division.Southwest, primaryColor=HexColor("CE1141"), secondaryColor=HexColor("C4CED4") },
            new TeamTemplate { teamId="MEM", city="Memphis",      teamName="Grizzlies",  abbreviation="MEG", conference=Conference.West, division=Division.Southwest, primaryColor=HexColor("5D76A9"), secondaryColor=HexColor("12173F") },
            new TeamTemplate { teamId="NOP", city="New Orleans",  teamName="Pelicans",   abbreviation="NOP", conference=Conference.West, division=Division.Southwest, primaryColor=HexColor("0C2340"), secondaryColor=HexColor("C8102E") },
            new TeamTemplate { teamId="SAS", city="San Antonio",  teamName="Silver",     abbreviation="SAS", conference=Conference.West, division=Division.Southwest, primaryColor=HexColor("C4CED4"), secondaryColor=HexColor("000000") },
        };

        // ── Expansion slots (custom teams) ────────────────────────────────────
        // Players can swap any of the 30 default teams for one of these,
        // or add them to a custom league configuration.

        public static readonly List<TeamTemplate> ExpansionTeams = new List<TeamTemplate>
        {
            new TeamTemplate { teamId="SEA", city="Seattle",      teamName="Storm",      abbreviation="SEA", conference=Conference.West, division=Division.Northwest, primaryColor=HexColor("00653A"), secondaryColor=HexColor("FFC200"), isCustom=true },
            new TeamTemplate { teamId="LVG", city="Las Vegas",    teamName="Aces",       abbreviation="LVA", conference=Conference.West, division=Division.Pacific,   primaryColor=HexColor("A7A8AA"), secondaryColor=HexColor("000000"), isCustom=true },
            new TeamTemplate { teamId="PIT", city="Pittsburgh",   teamName="Steel",      abbreviation="PIT", conference=Conference.East, division=Division.Central,  primaryColor=HexColor("FFB612"), secondaryColor=HexColor("000000"), isCustom=true },
            new TeamTemplate { teamId="VAN", city="Vancouver",    teamName="Rain",       abbreviation="VAN", conference=Conference.West, division=Division.Northwest, primaryColor=HexColor("00B2A9"), secondaryColor=HexColor("000000"), isCustom=true },
        };

        // ── Team factory ──────────────────────────────────────────────────────

        // Creates a Team ScriptableObject from a template with randomised tactics
        public static Team CreateTeamFromTemplate(TeamTemplate template)
        {
            var team = ScriptableObject.CreateInstance<Team>();

            team.teamId        = template.teamId;
            team.teamName      = template.teamName;
            team.city          = template.city;
            team.abbreviation  = template.abbreviation;
            team.conference    = template.conference;
            team.division      = template.division;
            team.primaryColor  = template.primaryColor;
            team.secondaryColor = template.secondaryColor;
            team.isPlayerTeam  = false;

            // Randomise AI personality
            team.aiPersonality = (AIPersonality)UnityEngine.Random.Range(0, 4);

            // Randomise offensive play priorities (a valid 1-5 ranking, no repeats)
            RandomiseOffensiveTactics(team.offensiveTactics);

            // Randomise defensive scheme
            team.defensiveTactics.activeScheme =
                (DefensiveScheme)UnityEngine.Random.Range(0, 5);
            team.defensiveTactics.foulAggression     = UnityEngine.Random.Range(20, 70);
            team.defensiveTactics.doubleTeamTendency = UnityEngine.Random.Range(10, 60);

            return team;
        }

        private static void RandomiseOffensiveTactics(OffensiveTactics tactics)
        {
            // Shuffle 1-5 and assign to each play
            var ranks = new List<int> { 1, 2, 3, 4, 5 }
                .OrderBy(_ => UnityEngine.Random.value).ToList();

            tactics.pickAndRollPriority    = ranks[0];
            tactics.isolationPriority      = ranks[1];
            tactics.spacingAndCutsPriority = ranks[2];
            tactics.postUpPriority         = ranks[3];
            tactics.fastBreakPriority      = ranks[4];
        }

        // ── Player generator ──────────────────────────────────────────────────

        // Generates a full roster for a team.
        // teamStrength: 0.0 (weak) to 1.0 (strong) — controls overall rating range.
        // Called once per team at the start of a new save.
        public static List<Player> GenerateRoster(Team team, float teamStrength)
        {
            var roster = new List<Player>();

            // Position slots: 2 PG, 2 SG, 2 SF, 2 PF, 2 C + 3 flex bench spots
            var positionSlots = new List<(Position pos, bool isStarter)>
            {
                (Position.PointGuard,    true),
                (Position.ShootingGuard, true),
                (Position.SmallForward,  true),
                (Position.PowerForward,  true),
                (Position.Center,        true),
                (Position.PointGuard,    false),
                (Position.ShootingGuard, false),
                (Position.SmallForward,  false),
                (Position.PowerForward,  false),
                (Position.Center,        false),
                // 3 flex spots — random position
                (RandomPosition(),       false),
                (RandomPosition(),       false),
                (RandomPosition(),       false),
            };

            foreach (var (pos, isStarter) in positionSlots)
            {
                var player = GeneratePlayer(pos, isStarter, teamStrength);
                roster.Add(player);
            }

            return roster;
        }

        // Generates a single player with semi-realistic ratings and age curve
        public static Player GeneratePlayer(
            Position position,
            bool isStarter,
            float teamStrength,
            bool isProspect = false,
            float prospectTier = 0.5f)
        {
            var player = ScriptableObject.CreateInstance<Player>();

            player.playerId  = Guid.NewGuid().ToString();
            player.firstName = FirstNames[UnityEngine.Random.Range(0, FirstNames.Length)];
            player.lastName  = LastNames[UnityEngine.Random.Range(0, LastNames.Length)];
            player.position  = position;
            player.nationality = RandomNationality();
            player.isFreeAgent = false;
            player.isRookie   = isProspect;
            player.mood       = PlayerMood.Neutral;

            // ── Age ──────────────────────────────────────────────────────────
            // Starters skew toward prime years (24-30), bench toward young or old
            player.age = isStarter
                ? UnityEngine.Random.Range(22, 32)
                : (UnityEngine.Random.value > 0.5f
                    ? UnityEngine.Random.Range(19, 24)   // young bench
                    : UnityEngine.Random.Range(30, 38)); // veteran bench

            if (isProspect) player.age = UnityEngine.Random.Range(18, 22);

            // ── Physical ─────────────────────────────────────────────────────
            player.heightCm = position switch
            {
                Position.PointGuard    => UnityEngine.Random.Range(178, 193),
                Position.ShootingGuard => UnityEngine.Random.Range(188, 200),
                Position.SmallForward  => UnityEngine.Random.Range(198, 208),
                Position.PowerForward  => UnityEngine.Random.Range(203, 213),
                Position.Center        => UnityEngine.Random.Range(208, 221),
                _                      => 200
            };
            player.weightKg = UnityEngine.Random.Range(82f, 122f);

            // ── Overall range ─────────────────────────────────────────────────
            // Starters: 72-88 based on teamStrength
            // Bench:    60-76 based on teamStrength
            // Prospects: 45-75 based on prospectTier
            float strengthBase = isProspect
                ? Mathf.Lerp(45f, 75f, prospectTier)
                : isStarter
                    ? Mathf.Lerp(72f, 88f, teamStrength)
                    : Mathf.Lerp(60f, 76f, teamStrength);

            int targetOverall = Mathf.RoundToInt(
                strengthBase + UnityEngine.Random.Range(-4f, 4f));

            // ── Player type ───────────────────────────────────────────────────
            player.playerType = RandomPlayerTypeForPosition(position);

            // ── Ratings ───────────────────────────────────────────────────────
            player.ratings = GenerateRatings(position, player.playerType, targetOverall);

            // ── Progression ───────────────────────────────────────────────────
            int peakStart = UnityEngine.Random.Range(24, 27);
            int peakEnd   = UnityEngine.Random.Range(29, 33);

            // Potential: young players can still grow, veterans are set
            int potentialCeiling = isProspect
                ? Mathf.RoundToInt(Mathf.Lerp(70f, 99f, prospectTier))
                : player.age < 24
                    ? Mathf.Min(99, targetOverall + UnityEngine.Random.Range(5, 20))
                    : player.age < 28
                        ? Mathf.Min(99, targetOverall + UnityEngine.Random.Range(0, 8))
                        : targetOverall; // veterans are at their ceiling

            player.progression = new PlayerProgression
            {
                potential       = potentialCeiling,
                peakAgeStart    = peakStart,
                peakAgeEnd      = peakEnd,
                developmentRate = player.age < 23
                    ? UnityEngine.Random.Range(1.2f, 2.0f)
                    : UnityEngine.Random.Range(0.6f, 1.2f),
                injuryRisk      = UnityEngine.Random.Range(5, 45),
                isInjuryProne   = UnityEngine.Random.value < 0.15f
            };

            // ── Contract ──────────────────────────────────────────────────────
            player.contract = GenerateContract(player, isProspect);

            return player;
        }

        // ── Rating generation ─────────────────────────────────────────────────

        // Generates sub-ratings that reflect the player's type and position,
        // then derives the overall from them using RecalculateOverall(playerType).
        private static PlayerRatings GenerateRatings(
            Position position, PlayerType type, int targetOverall)
        {
            var r = new PlayerRatings();

            // Start with a base of 50 for all sub-ratings
            int b = 50;

            // Position tendencies — each position has natural strengths
            switch (position)
            {
                case Position.PointGuard:
                    r.ballHandling = Rand(b + 10, b + 30);
                    r.passing      = Rand(b + 10, b + 30);
                    r.offIQ        = Rand(b + 5,  b + 25);
                    r.speed        = Rand(b + 10, b + 25);
                    r.agility      = Rand(b + 10, b + 20);
                    r.threePoint   = Rand(b - 5,  b + 20);
                    r.midRange     = Rand(b - 5,  b + 15);
                    r.finishing    = Rand(b - 10, b + 10);
                    r.interior     = Rand(b - 20, b);
                    r.blocking     = Rand(b - 25, b - 5);
                    r.strength     = Rand(b - 15, b + 5);
                    break;

                case Position.ShootingGuard:
                    r.threePoint   = Rand(b + 5,  b + 25);
                    r.midRange     = Rand(b + 5,  b + 20);
                    r.finishing    = Rand(b,       b + 20);
                    r.perimeter    = Rand(b + 5,  b + 20);
                    r.ballHandling = Rand(b - 5,  b + 15);
                    r.passing      = Rand(b - 10, b + 10);
                    r.speed        = Rand(b,       b + 15);
                    r.agility      = Rand(b + 5,  b + 20);
                    r.interior     = Rand(b - 20, b - 5);
                    r.blocking     = Rand(b - 20, b - 5);
                    break;

                case Position.SmallForward:
                    r.finishing    = Rand(b + 5,  b + 20);
                    r.threePoint   = Rand(b,       b + 20);
                    r.perimeter    = Rand(b,       b + 20);
                    r.strength     = Rand(b,       b + 15);
                    r.verticalLeap = Rand(b,       b + 20);
                    r.ballHandling = Rand(b - 10,  b + 10);
                    r.passing      = Rand(b - 10,  b + 10);
                    r.interior     = Rand(b - 15,  b + 5);
                    r.blocking     = Rand(b - 15,  b + 5);
                    break;

                case Position.PowerForward:
                    r.strength     = Rand(b + 5,  b + 25);
                    r.finishing    = Rand(b + 5,  b + 20);
                    r.interior     = Rand(b + 5,  b + 20);
                    r.defIQ        = Rand(b,       b + 20);
                    r.verticalLeap = Rand(b,       b + 15);
                    r.threePoint   = Rand(b - 15,  b + 15);
                    r.ballHandling = Rand(b - 20,  b + 5);
                    r.passing      = Rand(b - 15,  b + 10);
                    r.speed        = Rand(b - 10,  b + 10);
                    break;

                case Position.Center:
                    r.strength     = Rand(b + 10, b + 30);
                    r.interior     = Rand(b + 10, b + 30);
                    r.blocking     = Rand(b + 5,  b + 30);
                    r.finishing    = Rand(b + 5,  b + 20);
                    r.defIQ        = Rand(b,       b + 20);
                    r.verticalLeap = Rand(b,       b + 20);
                    r.threePoint   = Rand(b - 25,  b + 5);
                    r.ballHandling = Rand(b - 25,  b);
                    r.passing      = Rand(b - 20,  b + 10);
                    r.speed        = Rand(b - 20,  b + 5);
                    break;
            }

            // Fill any unset ratings with a random base value
            if (r.perimeter    == 0) r.perimeter    = Rand(b - 10, b + 15);
            if (r.stealing     == 0) r.stealing     = Rand(b - 10, b + 15);
            if (r.freeThrow    == 0) r.freeThrow    = Rand(b - 10, b + 20);
            if (r.stamina      == 0) r.stamina      = Rand(b,       b + 20);
            if (r.clutch       == 0) r.clutch       = Rand(b - 10, b + 20);
            if (r.consistency  == 0) r.consistency  = Rand(b - 10, b + 20);
            if (r.leadership   == 0) r.leadership   = Rand(b - 10, b + 20);
            if (r.midRange     == 0) r.midRange     = Rand(b - 10, b + 15);
            if (r.agility      == 0) r.agility      = Rand(b - 10, b + 15);
            if (r.verticalLeap == 0) r.verticalLeap = Rand(b - 10, b + 15);
            if (r.offIQ        == 0) r.offIQ        = Rand(b - 10, b + 15);

            // Apply type boost — inflate the 2-3 most relevant sub-ratings
            ApplyTypeBoost(r, type);

            // Clamp everything to 0-99
            ClampRatings(r);

            // Derive overall from type-weighted formula
            r.RecalculateOverall(type);

            // Nudge sub-ratings up or down to land near targetOverall
            int diff = targetOverall - r.overall;
            AdjustRatingsToTarget(r, type, diff);

            // Final clamp + recalculate
            ClampRatings(r);
            r.RecalculateOverall(type);

            return r;
        }

        // Inflates the 2-3 sub-ratings most relevant to the player's archetype
        private static void ApplyTypeBoost(PlayerRatings r, PlayerType type)
        {
            int boost = UnityEngine.Random.Range(8, 18);

            switch (type)
            {
                case PlayerType.Scorer:
                    r.midRange   += boost; r.finishing += boost; r.freeThrow += boost / 2;
                    break;
                case PlayerType.ShotCreator:
                    r.ballHandling += boost; r.midRange += boost; r.threePoint += boost / 2;
                    break;
                case PlayerType.Slasher:
                    r.finishing += boost + 5; r.speed += boost; r.agility += boost / 2;
                    break;
                case PlayerType.StretchBig:
                    r.threePoint += boost + 5; r.freeThrow += boost;
                    break;
                case PlayerType.LockdownDefender:
                    r.perimeter += boost + 5; r.defIQ += boost; r.stealing += boost / 2;
                    break;
                case PlayerType.RimProtector:
                    r.interior += boost + 5; r.blocking += boost + 5; r.strength += boost / 2;
                    break;
                case PlayerType.ThreeAndD:
                    r.threePoint += boost; r.perimeter += boost;
                    break;
                case PlayerType.Playmaker:
                    r.passing += boost + 5; r.ballHandling += boost; r.offIQ += boost;
                    break;
                case PlayerType.FloorGeneral:
                    r.passing += boost; r.offIQ += boost + 5; r.consistency += boost;
                    break;
                case PlayerType.TwoWayPlayer:
                    r.perimeter += boost / 2; r.finishing += boost / 2;
                    r.defIQ     += boost / 2; r.threePoint += boost / 2;
                    break;
                case PlayerType.PointCenter:
                    r.passing += boost; r.interior += boost / 2; r.offIQ += boost;
                    break;
                case PlayerType.GlueGuy:
                    r.leadership += boost + 5; r.consistency += boost; r.defIQ += boost / 2;
                    break;
            }
        }

        // Nudges all sub-ratings slightly toward hitting the target overall
        private static void AdjustRatingsToTarget(PlayerRatings r, PlayerType type, int diff)
        {
            if (diff == 0) return;

            int perRating = Mathf.CeilToInt(Mathf.Abs(diff) / 5f);
            int sign      = diff > 0 ? 1 : -1;

            r.finishing    += sign * perRating;
            r.threePoint   += sign * perRating;
            r.passing      += sign * perRating;
            r.defIQ        += sign * perRating;
            r.perimeter    += sign * perRating;
        }

        private static void ClampRatings(PlayerRatings r)
        {
            r.threePoint   = Mathf.Clamp(r.threePoint,   0, 99);
            r.midRange     = Mathf.Clamp(r.midRange,     0, 99);
            r.finishing    = Mathf.Clamp(r.finishing,    0, 99);
            r.freeThrow    = Mathf.Clamp(r.freeThrow,    0, 99);
            r.ballHandling = Mathf.Clamp(r.ballHandling, 0, 99);
            r.passing      = Mathf.Clamp(r.passing,      0, 99);
            r.offIQ        = Mathf.Clamp(r.offIQ,        0, 99);
            r.perimeter    = Mathf.Clamp(r.perimeter,    0, 99);
            r.interior     = Mathf.Clamp(r.interior,     0, 99);
            r.stealing     = Mathf.Clamp(r.stealing,     0, 99);
            r.blocking     = Mathf.Clamp(r.blocking,     0, 99);
            r.defIQ        = Mathf.Clamp(r.defIQ,        0, 99);
            r.speed        = Mathf.Clamp(r.speed,        0, 99);
            r.agility      = Mathf.Clamp(r.agility,      0, 99);
            r.strength     = Mathf.Clamp(r.strength,     0, 99);
            r.verticalLeap = Mathf.Clamp(r.verticalLeap, 0, 99);
            r.stamina      = Mathf.Clamp(r.stamina,      0, 99);
            r.clutch       = Mathf.Clamp(r.clutch,       0, 99);
            r.consistency  = Mathf.Clamp(r.consistency,  0, 99);
            r.leadership   = Mathf.Clamp(r.leadership,   0, 99);
        }

        // ── Contract generation ───────────────────────────────────────────────

        private static Contract GenerateContract(Player player, bool isProspect)
        {
            if (isProspect)
                return new Contract
                {
                    annualSalary   = UnityEngine.Random.Range(1.1f, 5.0f),
                    yearsRemaining = 4,
                    isRookieDeal   = true
                };

            // Salary loosely based on overall
            float baseSalary = Mathf.Lerp(1.1f, 45.0f,
                (player.ratings.overall - 50f) / 49f);
            baseSalary += UnityEngine.Random.Range(-3f, 3f);
            baseSalary  = Mathf.Max(1.1f, baseSalary);

            // Veteran players have shorter contracts remaining
            int yearsLeft = player.age > 32
                ? UnityEngine.Random.Range(1, 3)
                : UnityEngine.Random.Range(1, 5);

            return new Contract
            {
                annualSalary      = baseSalary,
                yearsRemaining    = yearsLeft,
                isMaxContract     = player.ratings.overall >= 88,
                hasPlayerOption   = UnityEngine.Random.value < 0.2f,
                hasTeamOption     = UnityEngine.Random.value < 0.15f,
                isRookieDeal      = false
            };
        }

        // ── Full league initialisation ────────────────────────────────────────

        // Creates all 30 teams with full rosters, coaches, and initial picks.
        // Call this once when starting a new save file.
        public static List<Team> InitialiseLeague(List<TeamTemplate> templates = null)
        {
            templates ??= DefaultTeams;
            var teams = new List<Team>();

            // Spread team strengths across the league (0.2 = weak, 1.0 = strong)
            // Shuffle so strength isn't tied to conference order
            var strengths = GenerateLeagueStrengths(templates.Count);

            for (int i = 0; i < templates.Count; i++)
            {
                var team = CreateTeamFromTemplate(templates[i]);

                // Generate 13-player roster
                var roster = GenerateRoster(team, strengths[i]);
                foreach (var p in roster)
                    team.SignPlayer(p);

                // Auto-set opening rotation
                team.AutoSetRotation();

                // Generate a coach
                team.headCoach = GenerateCoach(strengths[i]);

                // Assign initial draft picks (each team owns their own next 3 years)
                AssignInitialPicks(team);

                teams.Add(team);
            }

            return teams;
        }

        // Generates a spread of team strengths across the league
        private static List<float> GenerateLeagueStrengths(int count)
        {
            var strengths = new List<float>();

            // Distribute: ~5 elite, ~10 good, ~10 average, ~5 weak
            for (int i = 0; i < count; i++)
                strengths.Add((float)i / (count - 1)); // 0.0 to 1.0

            // Shuffle
            return strengths.OrderBy(_ => UnityEngine.Random.value).ToList();
        }

        // ── Coach generation ──────────────────────────────────────────────────

        public static Coach GenerateCoach(float teamStrength)
        {
            var coach = ScriptableObject.CreateInstance<Coach>();

            coach.coachId   = Guid.NewGuid().ToString();
            coach.firstName = FirstNames[UnityEngine.Random.Range(0, FirstNames.Length)];
            coach.lastName  = LastNames[UnityEngine.Random.Range(0, LastNames.Length)];
            coach.age       = UnityEngine.Random.Range(38, 65);
            coach.isFreeAgent = false;

            int baseRating = Mathf.RoundToInt(Mathf.Lerp(50f, 85f, teamStrength)
                + UnityEngine.Random.Range(-8f, 8f));

            coach.ratings = new CoachRatings
            {
                offensiveSchemeIQ      = Rand(baseRating - 10, baseRating + 10),
                defensiveSchemeIQ      = Rand(baseRating - 10, baseRating + 10),
                playerDevelopment      = Rand(baseRating - 15, baseRating + 15),
                inGameAdjustments      = Rand(baseRating - 10, baseRating + 10),
                lockerRoomManagement   = Rand(baseRating - 10, baseRating + 10),
                rotationManagement     = Rand(baseRating - 10, baseRating + 10)
            };
            coach.ratings.RecalculateOverall();

            coach.potential          = Mathf.Min(99, coach.ratings.overall + UnityEngine.Random.Range(0, 10));
            coach.preferredOffense   = (OffensivePlay)UnityEngine.Random.Range(0, 5);
            coach.preferredDefense   = (DefensiveScheme)UnityEngine.Random.Range(0, 5);

            return coach;
        }

        // ── Draft pick initialisation ─────────────────────────────────────────

        private static void AssignInitialPicks(Team team)
        {
            int currentYear = DateTime.Now.Year;

            for (int year = currentYear; year <= currentYear + 2; year++)
            {
                for (int round = 1; round <= 2; round++)
                {
                    team.ownedPicks.Add(new DraftPick
                    {
                        year          = year,
                        round         = round,
                        originalOwner = team,
                        isProtected   = false
                    });
                }
            }
        }

        // ── Utility helpers ───────────────────────────────────────────────────

        private static int Rand(int min, int max) =>
            UnityEngine.Random.Range(Mathf.Max(0, min), Mathf.Min(99, max + 1));

        private static Position RandomPosition() =>
            (Position)UnityEngine.Random.Range(0, 5);

        private static PlayerType RandomPlayerTypeForPosition(Position pos) =>
            pos switch
            {
                Position.PointGuard => RandomFrom(new[]
                {
                    PlayerType.Playmaker, PlayerType.FloorGeneral,
                    PlayerType.ShotCreator, PlayerType.Scorer
                }),
                Position.ShootingGuard => RandomFrom(new[]
                {
                    PlayerType.ThreeAndD, PlayerType.Scorer,
                    PlayerType.ShotCreator, PlayerType.Slasher
                }),
                Position.SmallForward => RandomFrom(new[]
                {
                    PlayerType.ThreeAndD, PlayerType.TwoWayPlayer,
                    PlayerType.Slasher, PlayerType.Scorer, PlayerType.GlueGuy
                }),
                Position.PowerForward => RandomFrom(new[]
                {
                    PlayerType.StretchBig, PlayerType.TwoWayPlayer,
                    PlayerType.GlueGuy, PlayerType.LockdownDefender
                }),
                Position.Center => RandomFrom(new[]
                {
                    PlayerType.RimProtector, PlayerType.StretchBig,
                    PlayerType.PointCenter, PlayerType.TwoWayPlayer
                }),
                _ => PlayerType.GlueGuy
            };

        private static T RandomFrom<T>(T[] arr) =>
            arr[UnityEngine.Random.Range(0, arr.Length)];

        private static string RandomNationality()
        {
            string[] nationalities =
            {
                "American", "American", "American", "American", "American",
                "American", "American", "American", "American", "American",
                "French", "Spanish", "Serbian", "Slovenian", "Greek",
                "Canadian", "Australian", "Nigerian", "Cameroonian", "German",
                "Latvian", "Argentinian", "Brazilian", "Croatian", "Turkish"
            };
            return nationalities[UnityEngine.Random.Range(0, nationalities.Length)];
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }
    }
}