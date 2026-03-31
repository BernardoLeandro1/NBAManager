using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAManager
{
    // ── Trade status ─────────────────────────────────────────────────────────

    public enum TradeStatus
    {
        Pending,      // Waiting for the player to respond
        Accepted,     // Trade went through
        Declined,     // Player rejected it
        Countered,    // Player sent a counter offer
        Expired       // Offer window passed without response
    }

    // ── Trade offer ───────────────────────────────────────────────────────────

    [Serializable]
    public class TradeOffer
    {
        // ── Parties ───────────────────────────────────────────────────────────

        public Team offeredBy;          // team initiating the trade
        public Team targetTeam;         // team receiving the offer

        // ── Assets exchanged ─────────────────────────────────────────────────

        // What the offering team sends to the target team
        public List<Player>    playersOffered   = new List<Player>();
        public List<DraftPick> picksOffered     = new List<DraftPick>();

        // What the offering team wants from the target team
        public List<Player>    playersRequested = new List<Player>();
        public List<DraftPick> picksRequested   = new List<DraftPick>();

        // ── Metadata ─────────────────────────────────────────────────────────

        public TradeStatus status      = TradeStatus.Pending;
        public int         dayOffered;  // season day this was created
        public int         expiryDay;   // offer expires after this day (typically +3)

        // AI's internal valuation — positive = AI thinks it's getting a good deal
        public float aiEvalScore;

        // ── Computed helpers ──────────────────────────────────────────────────

        public bool IsPending  => status == TradeStatus.Pending;
        public bool IsExpired  => status == TradeStatus.Expired;
        public bool IsResolved => status != TradeStatus.Pending;

        public float IncomingSalary =>
            playersOffered.Sum(p => p.contract.annualSalary);

        public float OutgoingSalary =>
            playersRequested.Sum(p => p.contract.annualSalary);

        // Positive = target team takes on more salary
        public float SalaryDelta => IncomingSalary - OutgoingSalary;

        // ── Validation ────────────────────────────────────────────────────────

        public TradeValidationResult Validate()
        {
            var result = new TradeValidationResult();

            foreach (var p in playersOffered)
                if (!offeredBy.roster.Contains(p))
                    result.errors.Add($"{p.FullName} is no longer on {offeredBy.FullName}'s roster.");

            foreach (var p in playersRequested)
                if (!targetTeam.roster.Contains(p))
                    result.errors.Add($"{p.FullName} is no longer on {targetTeam.FullName}'s roster.");

            foreach (var pick in picksOffered)
                if (!offeredBy.ownedPicks.Contains(pick))
                    result.errors.Add($"{offeredBy.FullName} no longer owns the {pick.year} R{pick.round} pick.");

            foreach (var pick in picksRequested)
                if (!targetTeam.ownedPicks.Contains(pick))
                    result.errors.Add($"{targetTeam.FullName} no longer owns the {pick.year} R{pick.round} pick.");

            if (playersOffered.Count == 0 && picksOffered.Count == 0)
                result.errors.Add("Offering team must include at least one player or pick.");

            if (playersRequested.Count == 0 && picksRequested.Count == 0)
                result.errors.Add("Target team must give up at least one player or pick.");

            // ── Salary cap legality ───────────────────────────────────────────
            // Simplified NBA trade rule: incoming salary cannot exceed
            // outgoing salary * 1.25 + $0.1M (the "125% + $100k" rule).
            // Exception: if the sending team is under the cap, they can absorb
            // any salary up to the cap limit.

            // Check from the target team's perspective (they receive playersOffered)
            float targetIncoming = IncomingSalary;
            float targetOutgoing = OutgoingSalary;

            bool targetUnderCap = !targetTeam.finances.IsOverCap;

            if (!targetUnderCap && targetIncoming > targetOutgoing * 1.25f + 0.1f)
                result.errors.Add(
                    $"Salary mismatch: {targetTeam.FullName} would receive ${targetIncoming:F1}M " +
                    $"but can only take in ${(targetOutgoing * 1.25f + 0.1f):F1}M " +
                    $"based on outgoing salary of ${targetOutgoing:F1}M.");

            // Check from the offering team's perspective (they receive playersRequested)
            float offerIncoming = OutgoingSalary;   // what offeredBy receives
            float offerOutgoing = IncomingSalary;   // what offeredBy sends out

            bool offerUnderCap = !offeredBy.finances.IsOverCap;

            if (!offerUnderCap && offerIncoming > offerOutgoing * 1.25f + 0.1f)
                result.errors.Add(
                    $"Salary mismatch: {offeredBy.FullName} would receive ${offerIncoming:F1}M " +
                    $"but can only take in ${(offerOutgoing * 1.25f + 0.1f):F1}M " +
                    $"based on outgoing salary of ${offerOutgoing:F1}M.");

            result.isValid = result.errors.Count == 0;
            return result;
        }

        // ── Execution ─────────────────────────────────────────────────────────

        public bool Execute()
        {
            var validation = Validate();
            if (!validation.isValid)
            {
                Debug.LogWarning("Trade validation failed:\n" +
                    string.Join("\n", validation.errors));
                return false;
            }

            foreach (var player in playersOffered)
            {
                offeredBy.roster.Remove(player);
                offeredBy.rotation.RemoveAll(s => s.player == player);
                targetTeam.roster.Add(player);
                targetTeam.rotation.Add(new PlayerLineupSlot(player, 0, false));
                player.isFreeAgent = false;
            }

            foreach (var player in playersRequested)
            {
                targetTeam.roster.Remove(player);
                targetTeam.rotation.RemoveAll(s => s.player == player);
                offeredBy.roster.Add(player);
                offeredBy.rotation.Add(new PlayerLineupSlot(player, 0, false));
                player.isFreeAgent = false;
            }

            foreach (var pick in picksOffered)
            {
                offeredBy.ownedPicks.Remove(pick);
                targetTeam.ownedPicks.Add(pick);
            }

            foreach (var pick in picksRequested)
            {
                targetTeam.ownedPicks.Remove(pick);
                offeredBy.ownedPicks.Add(pick);
            }

            offeredBy.finances.RecalculateCapUsed(offeredBy.roster);
            targetTeam.finances.RecalculateCapUsed(targetTeam.roster);

            status = TradeStatus.Accepted;

            Debug.Log($"Trade executed: " +
                $"{offeredBy.FullName} receives {string.Join(", ", playersRequested.Select(p => p.FullName))} | " +
                $"{targetTeam.FullName} receives {string.Join(", ", playersOffered.Select(p => p.FullName))}");

            return true;
        }

        // ── AI evaluation ─────────────────────────────────────────────────────

        public void RecalculateAIEval()
        {
            float valueIn  = playersOffered.Sum(p => offeredBy.EvaluatePlayerValue(p))
                           + picksOffered.Sum(p => EvaluatePickValue(p, offeredBy));

            float valueOut = playersRequested.Sum(p => offeredBy.EvaluatePlayerValue(p))
                           + picksRequested.Sum(p => EvaluatePickValue(p, offeredBy));

            aiEvalScore = valueOut - valueIn;
        }

        public bool AIWouldAccept()
        {
            RecalculateAIEval();
            return aiEvalScore >= 0f;
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

        // ── Counter offer ─────────────────────────────────────────────────────

        public TradeOffer BuildCounterOffer(
            List<Player>    counterPlayersOffered,
            List<DraftPick> counterPicksOffered,
            List<Player>    counterPlayersRequested,
            List<DraftPick> counterPicksRequested)
        {
            return new TradeOffer
            {
                offeredBy        = targetTeam,
                targetTeam       = offeredBy,
                playersOffered   = counterPlayersOffered,
                picksOffered     = counterPicksOffered,
                playersRequested = counterPlayersRequested,
                picksRequested   = counterPicksRequested,
                status           = TradeStatus.Pending,
                dayOffered       = dayOffered,
                expiryDay        = expiryDay
            };
        }

        // ── Display helpers ───────────────────────────────────────────────────

        public string ShortSummary()
        {
            var give = playersRequested.Select(p => p.FullName)
                .Concat(picksRequested.Select(p => $"{p.year} R{p.round}"));
            var get  = playersOffered.Select(p => p.FullName)
                .Concat(picksOffered.Select(p => $"{p.year} R{p.round}"));

            return $"Give: {string.Join(", ", give)}  |  Get: {string.Join(", ", get)}";
        }

        public string SalaryImpactLabel()
        {
            string direction = SalaryDelta >= 0 ? "+" : "";
            return $"Salary impact: {direction}{SalaryDelta:F1}M";
        }
    }

    // ── Validation result ─────────────────────────────────────────────────────

    public class TradeValidationResult
    {
        public bool         isValid = true;
        public List<string> errors  = new List<string>();

        public static implicit operator bool(TradeValidationResult r) => r.isValid;
    }
}