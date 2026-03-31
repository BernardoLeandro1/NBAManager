using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAManager
{
    // ── Trade party ───────────────────────────────────────────────────────────

    [Serializable]
    public class TradeParty
    {
        public Team            team;
        public List<Player>    playersOut  = new List<Player>();
        public List<Player>    playersIn   = new List<Player>();
        public List<DraftPick> picksOut    = new List<DraftPick>();
        public List<DraftPick> picksIn     = new List<DraftPick>();

        public float SalaryOut   => playersOut.Sum(p => p.contract.annualSalary);
        public float SalaryIn    => playersIn.Sum(p => p.contract.annualSalary);
        public float SalaryDelta => SalaryIn - SalaryOut;

        // Incoming salary <= outgoing * 1.25 + 0.1M (simplified NBA trade rule)
        public bool SalaryIsLegal =>
            SalaryIn <= (SalaryOut * 1.25f + 0.1f) || SalaryOut == 0;

        public string SalaryImpactLabel()
        {
            string dir = SalaryDelta >= 0 ? "+" : "";
            return $"{team.abbreviation} salary: {dir}{SalaryDelta:F1}M";
        }
    }

    // ── Multi-team trade offer ────────────────────────────────────────────────

    [Serializable]
    public class MultiTeamTradeOffer
    {
        // Min 3, max 4 parties (mirrors real NBA rules)
        public List<TradeParty> parties = new List<TradeParty>();

        public TradeStatus status     = TradeStatus.Pending;
        public int         dayOffered;
        public int         expiryDay;
        public Team        initiatedBy;

        // ── Helpers ───────────────────────────────────────────────────────────

        public bool IsValid            => parties.Count >= 3 && parties.Count <= 4;
        public bool IsPending          => status == TradeStatus.Pending;
        public bool IsResolved         => status != TradeStatus.Pending;
        public bool InvolvesPlayerTeam => parties.Any(p => p.team.isPlayerTeam);

        public TradeParty GetParty(Team team) =>
            parties.FirstOrDefault(p => p.team == team);

        // ── Build helpers ─────────────────────────────────────────────────────

        public TradeParty AddParty(Team team)
        {
            if (parties.Count >= 4)
            {
                Debug.LogWarning("MultiTeamTrade: maximum 4 teams allowed.");
                return null;
            }
            if (parties.Any(p => p.team == team))
            {
                Debug.LogWarning($"{team.FullName} is already in this trade.");
                return null;
            }

            var party = new TradeParty { team = team };
            parties.Add(party);
            return party;
        }

        // Routes outgoing assets to their destination teams.
        // Call this after all parties have declared their outgoing assets.
        public void ResolveIncoming(
            Dictionary<Player,    Team> playerDestinations,
            Dictionary<DraftPick, Team> pickDestinations)
        {
            foreach (var party in parties)
            {
                party.playersIn.Clear();
                party.picksIn.Clear();
            }

            foreach (var kv in playerDestinations)
            {
                var dest = GetParty(kv.Value);
                if (dest != null) dest.playersIn.Add(kv.Key);
                else Debug.LogWarning($"No party found for destination {kv.Value?.FullName}");
            }

            foreach (var kv in pickDestinations)
            {
                var dest = GetParty(kv.Value);
                if (dest != null) dest.picksIn.Add(kv.Key);
                else Debug.LogWarning($"No party found for pick destination {kv.Value?.FullName}");
            }
        }

        // ── Validation ────────────────────────────────────────────────────────

        public MultiTradeValidationResult Validate()
        {
            var result = new MultiTradeValidationResult();

            if (parties.Count < 3)
                result.errors.Add("Multi-team trade requires at least 3 teams.");

            if (parties.Count > 4)
                result.errors.Add("Multi-team trade cannot involve more than 4 teams.");

            foreach (var party in parties)
            {
                foreach (var p in party.playersOut)
                    if (!party.team.roster.Contains(p))
                        result.errors.Add(
                            $"{p.FullName} is no longer on {party.team.FullName}'s roster.");

                foreach (var pick in party.picksOut)
                    if (!party.team.ownedPicks.Contains(pick))
                        result.errors.Add(
                            $"{party.team.FullName} no longer owns the {pick.year} R{pick.round} pick.");

                if (party.playersOut.Count == 0 && party.picksOut.Count == 0)
                    result.errors.Add(
                        $"{party.team.FullName} must send at least one player or pick.");

                if (!party.SalaryIsLegal)
                    result.errors.Add(
                        $"{party.team.FullName}: incoming salary (${party.SalaryIn:F1}M) " +
                        $"exceeds allowable limit based on outgoing (${party.SalaryOut:F1}M).");
            }

            // Every outgoing asset must arrive somewhere and vice versa
            var allPlayersOut = parties.SelectMany(p => p.playersOut).ToList();
            var allPlayersIn  = parties.SelectMany(p => p.playersIn).ToList();

            foreach (var p in allPlayersOut)
                if (!allPlayersIn.Contains(p))
                    result.errors.Add($"{p.FullName} is being sent out but has no destination.");

            foreach (var p in allPlayersIn)
                if (!allPlayersOut.Contains(p))
                    result.errors.Add($"{p.FullName} is arriving somewhere but no team is sending them.");

            var allPicksOut = parties.SelectMany(p => p.picksOut).ToList();
            var allPicksIn  = parties.SelectMany(p => p.picksIn).ToList();

            foreach (var pick in allPicksOut)
                if (!allPicksIn.Contains(pick))
                    result.errors.Add($"{pick.year} R{pick.round} pick has no destination.");

            result.isValid = result.errors.Count == 0;
            return result;
        }

        // ── Execution ─────────────────────────────────────────────────────────

        public bool Execute()
        {
            var validation = Validate();
            if (!validation.isValid)
            {
                Debug.LogWarning("Multi-team trade failed:\n" +
                    string.Join("\n", validation.errors));
                return false;
            }

            foreach (var party in parties)
            {
                foreach (var player in party.playersOut)
                {
                    party.team.roster.Remove(player);
                    party.team.rotation.RemoveAll(s => s.player == player);
                }

                foreach (var pick in party.picksOut)
                    party.team.ownedPicks.Remove(pick);

                foreach (var player in party.playersIn)
                {
                    party.team.roster.Add(player);
                    party.team.rotation.Add(new PlayerLineupSlot(player, 0, false));
                    player.isFreeAgent = false;
                }

                foreach (var pick in party.picksIn)
                    party.team.ownedPicks.Add(pick);

                party.team.finances.RecalculateCapUsed(party.team.roster);
            }

            status = TradeStatus.Accepted;

            var summary = parties.Select(p =>
                $"{p.team.abbreviation} gets: " +
                string.Join(", ", p.playersIn.Select(pl => pl.FullName)
                    .Concat(p.picksIn.Select(pk => $"{pk.year} R{pk.round}"))));
            Debug.Log("Multi-team trade executed:\n" + string.Join("\n", summary));

            return true;
        }

        // ── AI evaluation ─────────────────────────────────────────────────────

        // Returns true only if every AI party in the trade is satisfied with their end
        public bool AllAIPartiesAccept()
        {
            foreach (var party in parties.Where(p => !p.team.isPlayerTeam))
            {
                float valueIn  = party.playersIn.Sum(p => party.team.EvaluatePlayerValue(p))
                               + party.picksIn.Sum(p => EvaluatePickValue(p, party.team));

                float valueOut = party.playersOut.Sum(p => party.team.EvaluatePlayerValue(p))
                               + party.picksOut.Sum(p => EvaluatePickValue(p, party.team));

                // AI won't sign off if they're getting less than 90% of what they give
                if (valueIn < valueOut * 0.90f) return false;
            }
            return true;
        }

        private float EvaluatePickValue(DraftPick pick, Team evaluator)
        {
            float roundValue   = pick.round == 1 ? 20f : 8f;
            int   yearsAway    = Mathf.Max(0, pick.year - DateTime.Now.Year);
            float timeDiscount = Mathf.Pow(0.85f, yearsAway);

            float personalityMod = evaluator.aiPersonality switch
            {
                AIPersonality.Rebuilding => 1.4f,
                AIPersonality.Tanking    => 1.6f,
                AIPersonality.Contending => 0.7f,
                _                        => 1.0f
            };

            return roundValue * timeDiscount * personalityMod;
        }

        // ── Display helpers ───────────────────────────────────────────────────

        public List<string> GetTradeSummaryLines() =>
            parties.Select(p =>
            {
                var sending   = p.playersOut.Select(pl => pl.FullName)
                    .Concat(p.picksOut.Select(pk => $"{pk.year} R{pk.round}"));
                var receiving = p.playersIn.Select(pl => pl.FullName)
                    .Concat(p.picksIn.Select(pk => $"{pk.year} R{pk.round}"));

                return $"{p.team.FullName}:  " +
                       $"OUT [{string.Join(", ", sending)}]  →  " +
                       $"IN  [{string.Join(", ", receiving)}]  " +
                       $"({p.SalaryImpactLabel()})";
            }).ToList();

        public string ShortSummary() =>
            $"{parties.Count}-team trade involving " +
            string.Join(", ", parties.Select(p => p.team.abbreviation));
    }

    // ── Validation result ─────────────────────────────────────────────────────

    public class MultiTradeValidationResult
    {
        public bool         isValid = true;
        public List<string> errors  = new List<string>();

        public static implicit operator bool(MultiTradeValidationResult r) => r.isValid;
    }
}