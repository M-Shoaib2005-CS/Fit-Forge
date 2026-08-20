using Microsoft.Extensions.Logging;
using FitForge.DL;
using FitForge.Models;

namespace FitForge.BL
{
    public class WorkoutBL(
        WorkoutDL wDL, AdaptiveDL aDL, PersonalRecordDL prDL, SkillDL skillDL,
        StreakDL streakDL, InjuryDL injuryDL, ProgramDL progDL,
        ScheduleDL schedDL, AchievementDL achDL, AchievementBL achBL,
        WaterDL waterDL, CalendarDL calDL, ExerciseDL exDL, RecoveryCoachingBL rcBL, ILogger<WorkoutBL> log)
    {
        public DashboardVM BuildDashboard(int uid, UserModel user)
        {
            var todaySlot   = schedDL.GetTodaySlot(uid);
            var openSession = wDL.GetOpenSession(uid);
            SessionModel? completedToday = null;
            ProgramDayModel? todayDay = null;
            var todayExercises = new List<ActiveSessionExerciseModel>();
            bool isRest = false;
            string restMsg = "";

            if (todaySlot != null)
            {
                if (todaySlot.IsRest)
                {
                    isRest = true;
                    restMsg = "Rest day — recovery is part of training.";
                }
                else if (todaySlot.DayId.HasValue)
                {
                    todayDay = LoadDayWithTargets(uid, todaySlot.DayId.Value);
                    todayExercises = BuildActiveExercises(uid, todayDay);
                    // Only relevant once there's no session currently open — an open session
                    // already takes priority in the view (shows "Continue" instead).
                    if (openSession == null)
                        completedToday = wDL.GetCompletedToday(uid, todaySlot.DayId.Value);
                }
            }

            // Pull unseen achievements to surface on dashboard
            var newAchievements = achDL.GetUnseen(uid);
            if (newAchievements.Any()) achDL.MarkSeen(uid);

            return new DashboardVM
            {
                User            = user,
                TodaySession    = openSession,
                TodayCompletedSession = completedToday,
                TodaySlot       = todaySlot,
                TodayDay        = todayDay,
                TodayExercises  = todayExercises,
                IsRestDay       = isRest,
                RestMessage     = restMsg,
                HasSchedule     = todaySlot != null,
                RecentSessions  = wDL.GetHistory(uid, 5),
                RecentPRs       = prDL.GetForUser(uid, 5),
                TotalWorkouts   = wDL.GetTotalWorkouts(uid),
                AvgSessionMins  = wDL.GetAvgSessionMins(uid),
                ActiveInjuries  = injuryDL.GetActiveForUser(uid),
                ActiveSkills    = skillDL.GetActiveForUser(uid),
                Water           = waterDL.GetTodaySummary(uid),
                NewAchievements = newAchievements,
                TopBadges       = achDL.GetBadges(uid).Take(6).ToList(),
                ActiveSchedule  = schedDL.GetActiveForUser(uid),
                BmiWarning      = user.BMI >= 30 ? $"BMI {user.BMI} — consider speaking to a doctor before intense training." :
                                  user.BMI < 18.5 && user.BMI > 0 ? $"BMI {user.BMI} — ensure adequate nutrition." : ""
            };
        }

        public ProgramDayModel LoadDayWithTargets(int uid, int dayId)
        {
            var exercises = progDL.GetExercisesForDay(dayId);
            var targets   = aDL.GetTargetsForDay(uid, dayId);
            foreach (var ex in exercises)
            {
                var t = targets.FirstOrDefault(x => x.PdeId == ex.PdeId);
                if (t != null) { ex.TargetReps = t.CurrentTargetReps; ex.TargetWeightKg = t.CurrentTargetWeight; }
            }
            return new ProgramDayModel { DayId = dayId, Exercises = exercises };
        }

        public List<ActiveSessionExerciseModel> BuildActiveExercises(int uid, ProgramDayModel day)
        {
            var flags   = injuryDL.GetFlaggedExercises(uid).ToDictionary(f => f.ExerciseId);
            var targets = aDL.GetTargetsForDay(uid, day.DayId).ToDictionary(t => t.PdeId);
            return day.Exercises.Select(ex =>
            {
                targets.TryGetValue(ex.PdeId, out var t);
                flags.TryGetValue(ex.ExerciseId, out var flag);
                var last = wDL.GetLastSet(uid, ex.ExerciseId);
                return new ActiveSessionExerciseModel
                {
                    PdeId           = ex.PdeId,
                    ExerciseId      = ex.ExerciseId,
                    ExerciseName    = ex.ExerciseName,
                    MuscleGroup     = ex.MuscleGroup,
                    TrackingMode    = ex.TrackingMode,
                    TargetSets      = ex.TargetSets,
                    TargetReps      = t?.CurrentTargetReps ?? ex.TargetReps,
                    TargetWeight    = t?.CurrentTargetWeight ?? ex.TargetWeightKg,
                    RestSeconds     = ex.RestSeconds,
                    Notes           = ex.Notes,
                    IsFlagged       = flag != null,
                    FlagReason      = flag?.Reason ?? "",
                    AlternativeId   = flag?.AlternativeId,
                    AlternativeName = flag?.AlternativeName ?? "",
                    EquipmentType   = ex.EquipmentType,
                    LastReps        = last.reps,
                    LastWeightKg    = last.weightKg,
                    LastDate        = last.date
                };
            }).ToList();
        }

        public int StartSession(int uid, int dayId)
        {
            var existing = wDL.GetOpenSession(uid);
            if (existing != null && existing.DayId == dayId) return existing.SessionId;
            return wDL.StartSession(uid, dayId);
        }

        public bool DiscardSession(int uid, int sessionId) => wDL.DiscardSession(sessionId, uid);

        public (bool ok, List<string> newPRs, List<AchievementModel> newAchievements) FinishSession(
            int uid, FinishSessionReq req, string progressionStyle)
        {
            // IDOR guard — must happen before anything else touches the DB.
            // LogSet's INSERT has no per-row ownership check of its own, so a
            // tampered sessionId in the request body would otherwise let sets
            // get written into a session that isn't this user's.
            if (!wDL.SessionBelongsToUser(req.SessionId, uid))
                return (false, new List<string>(), new List<AchievementModel>());

            wDL.FinishSession(req.SessionId, uid, req.Notes);
            var newPRs = new List<string>();
            double totalVolume = 0;
            var exerciseIds = new HashSet<int>();

            var exNames = exDL.GetNameMap();

            foreach (var s in req.Sets)
            {
                int targetReps = s.TargetReps > 0 ? s.TargetReps : 0;
                string setType = string.IsNullOrWhiteSpace(s.SetType) ? "Working" : s.SetType;
                wDL.LogSet(req.SessionId, s.ExerciseId, s.PdeId,
                    s.SetNumber, targetReps, s.ActualReps, s.WeightKg, s.Rpe, s.Skipped, setType, s.DropIndex, s.Tempo);

                // Warm-up sets are deliberately sub-maximal, so they must never
                // count toward a PR or toward session volume — otherwise a light
                // warm-up rep could register as beating a real working-set best.
                bool countsForPR = !s.Skipped && s.ActualReps > 0 && setType != "Warmup";

                if (countsForPR)
                {
                    bool pr = prDL.CheckAndSave(uid, s.ExerciseId, req.SessionId, s.ActualReps, s.WeightKg);
                    if (pr)
                    {
                        exNames.TryGetValue(s.ExerciseId, out var exName);
                        newPRs.Add(exName ?? $"Exercise #{s.ExerciseId}");
                    }
                    if (s.WeightKg.HasValue) totalVolume += s.ActualReps * s.WeightKg.Value;
                    exerciseIds.Add(s.ExerciseId);
                }
            }

            // Badge updates per unique exercise in this session
            var allNewAchievements = new List<AchievementModel>();
            foreach (var exId in exerciseIds)
            {
                string? newTier = achDL.IncrementBadge(uid, exId);
                if (newTier != null)
                    allNewAchievements.AddRange(achBL.CheckBadgeAchievements(uid, newTier));
            }

            aDL.UpdateTargetsAfterSession(uid, req.SessionId, req.DayId, progressionStyle, req.Sets);
            streakDL.UpdateStreak(uid);
            rcBL.RecordSessionAndDetect(uid, req.SessionId, req.DayId, req.Sets);

            // Achievement checks
            int totalWorkouts = wDL.GetTotalWorkouts(uid);
            int streak = 0;
            try { streak = Convert.ToInt32(DB.Scalar("SELECT current_streak FROM user_streaks WHERE user_id=@u", DB.P("@u", uid))); } catch { }
            int totalPRCount = prDL.GetForUser(uid).Count;
            int hour = DateTime.Now.Hour;

            allNewAchievements.AddRange(achBL.CheckAfterWorkout(uid, totalWorkouts, streak,
                totalPRCount, exerciseIds.ToList(), hour, totalVolume));

            // Anything awarded here gets shown to the client immediately (the
            // Active screen pops it right after Finish) — mark exactly these
            // seen now, not just on the next Dashboard load. Otherwise these
            // rows sit at seen=0 (the schema default on insert) until
            // BuildDashboard's own GetUnseen() catches them, which shows the
            // exact same badge a second time the moment the user lands back
            // on the Dashboard. Scoped (not blanket) so an unrelated real
            // notification earned elsewhere doesn't get silently marked seen
            // without ever having been shown.
            if (allNewAchievements.Count > 0) achDL.MarkSeen(uid, allNewAchievements.Select(a => a.AchievementId));

            log.LogInformation("Session {S} finished — {N} sets, {V:F0}kg volume", req.SessionId, req.Sets.Count, totalVolume);
            return (true, newPRs, allNewAchievements);
        }

        // Returns null when the URL's sessionId doesn't match the user's actual
        // open session (e.g. a stale link after the workout was already
        // finished, or a browser-back navigation) — previously this silently
        // displayed whatever GetOpenSession(uid) returned regardless of
        // whether it matched the sessionId in the URL at all.
        public ActiveWorkoutVM? BuildActiveWorkoutVM(int uid, int sessionId, int dayId)
        {
            var session = wDL.GetOpenSession(uid);
            if (session == null || session.SessionId != sessionId) return null;

            var day = LoadDayWithTargets(uid, dayId);
            return new ActiveWorkoutVM
            {
                SessionId   = sessionId,
                DayId       = dayId,
                DayName     = session.DayName,
                ProgramName = session.ProgramName,
                Exercises   = BuildActiveExercises(uid, day),
                StartedAt   = session.StartedAt,
                ServerNow   = DateTime.Now
            };
        }

        public WorkoutsVM BuildWorkoutsVM(int uid)
        {
            var now = DateTime.Now;
            return new WorkoutsVM
            {
                History      = wDL.GetHistory(uid, 50),
                PRs          = prDL.GetForUser(uid),
                CalendarDays = calDL.GetMonth(uid, now.Year, now.Month),
                Badges       = achDL.GetBadges(uid)
            };
        }

        public List<SessionModel>        GetHistory(int uid) => wDL.GetHistory(uid, 50);
        public List<PersonalRecordModel> GetPRs(int uid)     => prDL.GetForUser(uid);
    }
}
