using Microsoft.Extensions.Logging;
using FitForge.DL;
using FitForge.Models;
using System.Text.Json;

namespace FitForge.BL
{
    // Recovery-aware volume coaching: detects when a user is consistently doing
    // far less (or far more) than their programmed volume and produces a
    // concrete, deterministic suggestion. Every number and exercise choice here
    // is computed in C# — Gemini's only job (elsewhere, in GeminiService/
    // CoachController) is to phrase the resulting payload into a message; it
    // never generates the detection or the specific changes itself.
    public class RecoveryCoachingBL(RecoveryCoachingDL rcDL, ILogger<RecoveryCoachingBL> log)
    {
        private const double LowThreshold = 0.5;   // < 50% of scheduled volume
        private const double HighThreshold = 2.0;  // > 200% of scheduled volume
        private const int RecencyDays = 20;         // ignore occurrences older than this when checking a day's last-2
        private const int CooldownDays = 14;

        public CoachSuggestionModel? GetPending(int uid) => rcDL.GetPendingSuggestion(uid);

        // Call once per finished session, after sets are persisted. Logs the
        // volume ratio, then runs detection. Returns true if a new suggestion
        // was created (caller can use this to badge the coach icon).
        public bool RecordSessionAndDetect(int uid, int sessionId, int dayId, List<LoggedSet> sets)
        {
            // Ground-truth dump of exactly what the client submitted, before any
            // filtering/math touches it — if actualReps/weight/skipped/setType
            // aren't what's expected here, the bug is upstream of this method
            // (client submission), not in the ratio math below.
            foreach (var s in sets)
                log.LogInformation("RecoveryCoaching: raw set — pde {PdeId} setNum {SetNumber} reps {ActualReps} weight {WeightKg} skipped {Skipped} type {SetType}",
                    s.PdeId, s.SetNumber, s.ActualReps, s.WeightKg?.ToString("F1") ?? "null", s.Skipped, s.SetType);

            var countedSets = sets.Where(s => !s.Skipped && s.SetType != "Warmup").ToList();
            int setsCompleted = countedSets.Select(s => (s.PdeId, s.SetNumber)).Distinct().Count();
            int setsScheduled = rcDL.GetScheduledSetsForDay(dayId);

            // Real volume: reps × weight actually logged vs reps × weight
            // scheduled — not just "was a set touched at all". Computed PER
            // EXERCISE and then averaged, rather than summed across the whole
            // day first: summing raw work-units would let one heavy weighted
            // exercise's huge numbers (weight×reps in the hundreds/thousands)
            // completely drown out a bodyweight exercise's tiny numbers
            // (weight=1) on a mixed day. Averaging per-exercise ratios means
            // every exercise pulls the day's overall ratio by an equal share,
            // regardless of how much it weighs.
            var targets = rcDL.GetDayExerciseTargetsForVolume(dayId);
            if (targets.Count == 0)
            {
                log.LogWarning("RecoveryCoaching: day {DayId} has no scheduled exercises — skipping volume log/detection entirely for session {SessionId}", dayId, sessionId);
                return false;
            }
            foreach (var t in targets)
                log.LogInformation("RecoveryCoaching: raw target — pde {PdeId} sets {TargetSets} reps {TargetReps} weight {TargetWeightKg}",
                    t.PdeId, t.TargetSets, t.TargetReps, t.TargetWeightKg?.ToString("F1") ?? "null");

            var perExerciseRatios = new List<double>();
            foreach (var t in targets)
            {
                double weight = t.TargetWeightKg.HasValue && t.TargetWeightKg.Value > 0 ? t.TargetWeightKg.Value : 1.0;
                double scheduledWork = t.TargetReps * t.TargetSets * weight;
                if (scheduledWork <= 0) continue;

                double actualWork = countedSets
                    .Where(s => s.PdeId == t.PdeId)
                    .Sum(s => s.ActualReps * (s.WeightKg.HasValue && s.WeightKg.Value > 0 ? s.WeightKg.Value : 1.0));

                double exRatio = actualWork / scheduledWork;
                perExerciseRatios.Add(exRatio);
                log.LogInformation("RecoveryCoaching:   pde {PdeId} — work {ActualWork:F0}/{ScheduledWork:F0} = {Ratio:P0}", t.PdeId, actualWork, scheduledWork, exRatio);
            }

            if (perExerciseRatios.Count == 0)
            {
                log.LogWarning("RecoveryCoaching: day {DayId} — no exercise had a valid scheduled target, skipping", dayId);
                return false;
            }

            double ratio = perExerciseRatios.Average();
            log.LogInformation("RecoveryCoaching: uid {Uid} day {DayId} session {SessionId} — {SetsCompleted}/{SetsScheduled} sets, day ratio (avg of {Count} exercise ratio(s)) = {Ratio:P0} ({Direction})",
                uid, dayId, sessionId, setsCompleted, setsScheduled, perExerciseRatios.Count, ratio, ratio < LowThreshold ? "LOW" : ratio > HighThreshold ? "HIGH" : "normal");

            rcDL.LogVolume(uid, sessionId, dayId, setsCompleted, setsScheduled, ratio);

            // Only one suggestion is ever pending at a time — don't pile another
            // on top while the user hasn't responded to the last one.
            var existingPending = rcDL.GetPendingSuggestion(uid);
            if (existingPending != null)
            {
                log.LogInformation("RecoveryCoaching: uid {Uid} already has a pending suggestion ({SuggestionId}, type {Type}) — skipping detection", uid, existingPending.SuggestionId, existingPending.Type);
                return false;
            }

            bool created = Detect(uid, dayId);
            log.LogInformation("RecoveryCoaching: Detect() for uid {Uid} day {DayId} returned {Created}", uid, dayId, created);
            return created;
        }

        private bool Detect(int uid, int dayId)
        {
            // ── Whole-program check: last 3 sessions overall, any days, all
            //    the same direction (all low or all high), within recency. ──
            // Must be 3 DIFFERENT day-types, not just 3 sessions — otherwise
            // redoing the same day 3x in a row (or a 2-day program's natural
            // A-B-A rotation) would falsely read as "the whole program is
            // struggling" when it's really just one day. With only 2 distinct
            // day-types possible on a 2-day program, 3 different-in-a-row is
            // structurally impossible there — which is intentional: a 2-day
            // program is meant to resolve through the anchor+bundle path below,
            // never through this whole-program path.
            var recentOverall = rcDL.GetRecentOverall(uid, 3);
            if (recentOverall.Count == 3)
            {
                var cutoff = DateTime.Now.AddDays(-RecencyDays);
                bool allRecent = recentOverall.All(l => l.LoggedAt >= cutoff);
                bool allDistinctDays = recentOverall.Select(l => l.DayId).Distinct().Count() == 3;
                bool allLow = recentOverall.All(l => l.IsLow);
                bool allHigh = recentOverall.All(l => l.IsHigh);

                // A day still under its own 14-day cooldown (just resolved) must
                // not count toward this either — otherwise a day that was
                // supposed to go fully quiet could still get swept into a
                // whole-program suggestion through this separate path.
                bool anyOnCooldown = recentOverall.Any(l =>
                {
                    var cd = rcDL.GetCooldownUntil(uid, l.DayId);
                    return cd.HasValue && l.LoggedAt <= cd.Value;
                });

                if (allRecent && allDistinctDays && !anyOnCooldown && (allLow || allHigh))
                {
                    CreateWholeProgramSuggestion(uid, allLow ? "low" : "high");
                    return true;
                }
                if (anyOnCooldown)
                    log.LogInformation("RecoveryCoaching: whole-program check skipped — one of the last 3 distinct days is still under its own cooldown");
            }

            // ── Per-day anchor check: does THIS day's last 2 occurrences (within
            //    recency, since its own cooldown reset) both go the same way? ──
            var recentForDay = rcDL.GetRecentForDay(uid, dayId, 2);
            log.LogInformation("RecoveryCoaching: day {DayId} has {Count} recent occurrence(s) after cooldown filtering (need 2)", dayId, recentForDay.Count);
            if (recentForDay.Count < 2) return false;
            var cutoff2 = DateTime.Now.AddDays(-RecencyDays);
            if (!recentForDay.All(l => l.LoggedAt >= cutoff2))
            {
                log.LogInformation("RecoveryCoaching: day {DayId} — one of the last 2 occurrences is older than the {Days}-day recency window, not anchoring", dayId, RecencyDays);
                return false;
            }

            bool anchorLow = recentForDay.All(l => l.IsLow);
            bool anchorHigh = recentForDay.All(l => l.IsHigh);
            if (!anchorLow && !anchorHigh)
            {
                log.LogInformation("RecoveryCoaching: day {DayId} — last 2 occurrences don't agree (ratios: {Ratios}), not anchoring", dayId, string.Join(", ", recentForDay.Select(l => l.Ratio.ToString("P0"))));
                return false;
            }

            string reason = anchorLow ? "low" : "high";
            var anchorOldest = recentForDay.Min(l => l.LoggedAt);
            var anchorNewest = recentForDay.Max(l => l.LoggedAt);

            // Bundle in any OTHER day-type that also had a hit in the same
            // direction somewhere between the anchor's two occurrences.
            var windowLogs = rcDL.GetRecentOverall(uid, 20)
                .Where(l => l.LoggedAt > anchorOldest && l.LoggedAt < anchorNewest && l.DayId != dayId)
                .Where(l => reason == "low" ? l.IsLow : l.IsHigh)
                .GroupBy(l => l.DayId)
                .Select(g => g.First())
                .ToList();

            var bundledDayIds = windowLogs.Select(l => l.DayId).ToList();

            // If the anchor plus everything bundled with it already covers every
            // currently scheduled day, this IS a whole-program situation even
            // though it was found via the anchor path rather than the "3
            // distinct days in a row" path — e.g. a 2-day program's A-B-A
            // naturally flags both A and B this way. Present it as such rather
            // than "Fix Day A (+ Day B)" phrasing when it's really everything.
            var allActiveDays = rcDL.GetActiveScheduleDayIds(uid);
            var covered = new HashSet<int>(bundledDayIds) { dayId };
            if (allActiveDays.Count > 0 && covered.SetEquals(allActiveDays))
            {
                CreateWholeProgramSuggestion(uid, reason);
                return true;
            }

            CreateDaySuggestion(uid, dayId, reason, bundledDayIds);
            return true;
        }

        private void CreateDaySuggestion(int uid, int anchorDayId, string reason, List<int> bundledDayIds)
        {
            var payload = new SuggestionPayload();
            payload.Days.Add(BuildSuggestionDay(anchorDayId, reason, isAnchor: true));
            foreach (var bId in bundledDayIds)
                payload.Days.Add(BuildSuggestionDay(bId, reason, isAnchor: false));

            var json = JsonSerializer.Serialize(payload);
            rcDL.CreateSuggestion(uid, reason == "low" ? "day_fix" : "add_exercise", json);
        }

        private void CreateWholeProgramSuggestion(int uid, string reason)
        {
            var dayIds = rcDL.GetActiveScheduleDayIds(uid);
            var payload = new SuggestionPayload();
            foreach (var dId in dayIds)
                payload.Days.Add(BuildSuggestionDay(dId, reason, isAnchor: true));

            var json = JsonSerializer.Serialize(payload);
            rcDL.CreateSuggestion(uid, "whole_program_fix", json);
        }

        private SuggestionDay BuildSuggestionDay(int dayId, string reason, bool isAnchor)
        {
            var day = new SuggestionDay
            {
                DayId = dayId,
                DayName = rcDL.GetDayName(dayId),
                IsAnchor = isAnchor,
                Reason = reason
            };

            var exercises = rcDL.GetDayExercisesForCoaching(dayId);

            if (reason == "low")
            {
                day.Changes = BuildLowVolumeChanges(exercises);
            }
            else // high — suggest adding an exercise, candidates from muscle groups already on the day
            {
                var muscleGroups = exercises.Select(e => e.MuscleGroup).Distinct().ToList();
                day.Candidates = rcDL.GetAddCandidates(dayId, muscleGroups, 4);
            }

            return day;
        }

        // Cuts volume proportionally to how far under the day actually was, instead
        // of always trimming a single set regardless of severity:
        //  - Any exercise at 3+ sets loses exactly 1 set, up to 3 exercises at once
        //    (so a max of 3 sets come off in one suggestion).
        //  - If NO exercise has 3+ sets (everything's already down to ~2), the day
        //    has more than 2 exercises, and there's an isolation exercise to drop,
        //    it gets removed entirely instead of shaving a set nobody will notice.
        //  - If the day only has 1-2 exercises left, removing one entirely would
        //    gut the day — cut a single set from one instead, so it never goes
        //    from "under-programmed" to "barely a workout".
        // Isolation exercises are always preferred over compound ones, using the
        // real is_compound column. A final safety cap keeps the total sets
        // removed under half the day's scheduled total, so a day that's already
        // light (e.g. 2 exercises, 2 sets each) never gets over-cut relative to
        // its own size.
        private List<SuggestedChange> BuildLowVolumeChanges(
            List<(int PdeId, int ExerciseId, string Name, string MuscleGroup, int TargetSets, bool? IsCompound)> exercises)
        {
            var changes = new List<SuggestedChange>();
            if (exercises.Count == 0) return changes;

            int totalScheduledSets = exercises.Sum(e => e.TargetSets);
            var ordered = exercises.OrderBy(e => e.IsCompound == true ? 1 : 0).ThenByDescending(e => e.TargetSets).ToList();

            var cuttable = ordered.Where(e => e.TargetSets >= 3).Take(3).ToList();

            if (cuttable.Count > 0)
            {
                foreach (var e in cuttable)
                    changes.Add(new SuggestedChange { PdeId = e.PdeId, ExerciseName = e.Name, Action = "cut_set", NewTargetSets = e.TargetSets - 1 });
            }
            else if (exercises.Count > 2)
            {
                // Nothing left carrying 3+ sets — the day only has real room to
                // trim by dropping a whole (isolation-first) exercise.
                var target = ordered[0];
                changes.Add(new SuggestedChange { PdeId = target.PdeId, ExerciseName = target.Name, Action = "remove_exercise" });
            }
            else
            {
                // Only 1-2 exercises total and none has 3+ sets — e.g. exactly the
                // "2 exercises, 2 sets each" case. Removing one entirely would
                // leave almost nothing, so just take one set off instead.
                var withRoom = ordered.Where(e => e.TargetSets > 1).ToList();
                var target = withRoom.Count > 0 ? withRoom[0] : ordered[0];
                if (target.TargetSets > 1)
                    changes.Add(new SuggestedChange { PdeId = target.PdeId, ExerciseName = target.Name, Action = "cut_set", NewTargetSets = target.TargetSets - 1 });
            }

            // Safety cap: never let one suggestion remove more than half the day's
            // total scheduled sets. Drop the least-impactful change(s) first until
            // back under the cap, rather than silently gutting a light day.
            int Removed() => changes.Sum(c => c.Action == "remove_exercise"
                ? (exercises.FirstOrDefault(e => e.PdeId == c.PdeId).TargetSets)
                : 1);
            while (changes.Count > 1 && Removed() > totalScheduledSets * 0.5)
                changes.RemoveAt(changes.Count - 1);

            return changes;
        }

        // ── Resolving a suggestion ──────────────────────────────────
        // acceptedDays: dayId -> picked exerciseId (only present/used for
        // "high" reason days, where the user chose one of the candidate pills).
        public void Resolve(int uid, int suggestionId, Dictionary<int, int?> acceptedDays)
        {
            var suggestion = rcDL.GetPendingSuggestion(uid);
            if (suggestion == null || suggestion.SuggestionId != suggestionId) return;

            var payload = JsonSerializer.Deserialize<SuggestionPayload>(suggestion.Payload) ?? new SuggestionPayload();

            foreach (var day in payload.Days)
            {
                bool accepted = acceptedDays.ContainsKey(day.DayId);
                if (accepted)
                {
                    if (day.Reason == "low")
                    {
                        foreach (var change in day.Changes) rcDL.ApplyChange(change);
                    }
                    else // high — user picked one of the candidate exercises for this day
                    {
                        var pickedId = acceptedDays[day.DayId];
                        var candidate = pickedId.HasValue ? day.Candidates.FirstOrDefault(c => c.ExerciseId == pickedId.Value) : null;
                        if (candidate != null)
                            rcDL.AddExerciseToDay(day.DayId, candidate.ExerciseId, targetSets: 3, targetReps: 10);
                    }
                }
                // Every involved day resets + gets a cooldown either way — accepting
                // only some of a bundled group still resolves the WHOLE group, per spec.
                rcDL.SetCooldown(uid, day.DayId, DateTime.Now.AddDays(CooldownDays));
            }

            rcDL.ResolveSuggestion(suggestionId, acceptedDays.Count > 0 ? "accepted" : "declined");
        }

        // ── Turning a suggestion into a natural message + choices ───
        // Deterministic templating, not a Gemini call — keeps viewing a
        // suggestion free (0 model calls), which matters for free-tier users.
        // Gemini only gets involved if the person actually types a follow-up
        // question about it in chat.
        public object BuildSuggestionView(CoachSuggestionModel s)
        {
            var payload = JsonSerializer.Deserialize<SuggestionPayload>(s.Payload) ?? new SuggestionPayload();
            string message;
            var options = new List<object>();

            if (s.Type == "whole_program_fix")
            {
                bool high = payload.Days.Count > 0 && payload.Days[0].Reason == "high";
                var dayNames = string.Join(", ", payload.Days.Select(d => d.DayName));
                message = high
                    ? $"Your last 3 sessions have all landed well over your planned volume, across {dayNames} — looks like the whole program might be light for where you're at. Want me to add an exercise to each day?"
                    : $"Your last 3 sessions have all landed under half your planned volume, across {dayNames} — feels like the current program might be more than fits right now. Want me to trim sets and exercises across the board?";
                options.Add(new { label = high ? "Yes, bulk it up" : "Yes, trim it down", dayIds = payload.Days.Select(d => d.DayId).ToList() });
                options.Add(new { label = "No, leave it", dayIds = new List<int>() });
            }
            else
            {
                var anchor = payload.Days.FirstOrDefault(d => d.IsAnchor) ?? payload.Days.First();
                var bundled = payload.Days.Where(d => !d.IsAnchor).ToList();
                bool high = anchor.Reason == "high";
                string verb = high ? "well over" : "under half of";
                message = bundled.Count > 0
                    ? $"{anchor.DayName} has been running {verb} its planned volume the last couple times" +
                      $" — same story on {string.Join(", ", bundled.Select(b => b.DayName))}. Want me to {(high ? "add something to" : "trim")} {anchor.DayName}, or both?"
                    : $"{anchor.DayName} has been running {verb} its planned volume the last couple times. Want me to {(high ? "add an exercise" : "trim it down")}?";

                options.Add(new { label = $"Fix {anchor.DayName}", dayIds = new List<int> { anchor.DayId } });
                if (bundled.Count > 0)
                    options.Add(new { label = $"Fix {anchor.DayName} + {string.Join(" + ", bundled.Select(b => b.DayName))}", dayIds = payload.Days.Select(d => d.DayId).ToList() });
                options.Add(new { label = "Leave it", dayIds = new List<int>() });
            }

            return new { suggestionId = s.SuggestionId, type = s.Type, message, options, days = payload.Days };
        }

        public void ApplyAddExercise(int dayId, int exerciseId, int targetSets, int targetReps)
            => rcDL.AddExerciseToDay(dayId, exerciseId, targetSets, targetReps);
    }
}
