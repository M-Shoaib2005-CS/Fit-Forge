using FitForge.Models;
using Microsoft.Extensions.Logging;
using MySqlConnector;
namespace FitForge.DL
{
    public class RecoveryCoachingDL(ILogger<RecoveryCoachingDL> log)
    {
        // ── Volume log (append-only) ────────────────────────────────
        public void LogVolume(int uid, int sessionId, int dayId, int setsCompleted, int setsScheduled, double ratio)
        {
            DB.NonQuery(@"INSERT INTO day_volume_log(user_id,session_id,day_id,sets_completed,sets_scheduled,ratio)
                VALUES(@u,@s,@d,@sc,@ss,@r)
                ON DUPLICATE KEY UPDATE sets_completed=@sc,sets_scheduled=@ss,ratio=@r",
                DB.P("@u", uid), DB.P("@s", sessionId), DB.P("@d", dayId),
                DB.P("@sc", setsCompleted), DB.P("@ss", setsScheduled), DB.P("@r", ratio));
        }

        // Real training volume PER EXERCISE (reps × weight), not just set count.
        // Bodyweight sets (no target weight) count reps at a weight of 1, so
        // they still contribute proportionally.
        public List<(int PdeId, int TargetSets, int TargetReps, double? TargetWeightKg)> GetDayExerciseTargetsForVolume(int dayId)
        {
            var dt = DB.Select(@"SELECT pde_id, target_sets, target_reps, target_weight_kg
                FROM program_day_exercises WHERE day_id=@d", DB.P("@d", dayId));
            var list = new List<(int, int, int, double?)>();
            foreach (System.Data.DataRow r in dt.Rows)
            {
                list.Add((
                    Convert.ToInt32(r["pde_id"]),
                    Convert.ToInt32(r["target_sets"]),
                    Convert.ToInt32(r["target_reps"]),
                    r["target_weight_kg"] != DBNull.Value ? Convert.ToDouble(r["target_weight_kg"]) : (double?)null
                ));
            }
            return list;
        }



        // Last N sessions overall for this user, regardless of which day type,
        // most recent first — used for the "3 in a row = whole program" check.
        public List<DayVolumeLogModel> GetRecentOverall(int uid, int count)
        {
            var dt = DB.Select(@"SELECT l.log_id,l.user_id,l.session_id,l.day_id,pd.name AS day_name,
                    l.sets_completed,l.sets_scheduled,l.ratio,l.logged_at
                FROM day_volume_log l
                JOIN program_days pd ON pd.day_id=l.day_id
                WHERE l.user_id=@u ORDER BY l.logged_at DESC LIMIT @n",
                DB.P("@u", uid), DB.P("@n", count));
            return MapVolumeLogs(dt);
        }

        // Last N occurrences of one specific day, most recent first, only
        // counting history after that day's cooldown (if any) — the cooldown
        // timestamp doubles as "ignore everything before the last reset".
        public List<DayVolumeLogModel> GetRecentForDay(int uid, int dayId, int count)
        {
            var cooldown = GetCooldownUntil(uid, dayId);
            var dt = cooldown.HasValue
                ? DB.Select(@"SELECT l.log_id,l.user_id,l.session_id,l.day_id,pd.name AS day_name,
                        l.sets_completed,l.sets_scheduled,l.ratio,l.logged_at
                    FROM day_volume_log l JOIN program_days pd ON pd.day_id=l.day_id
                    WHERE l.user_id=@u AND l.day_id=@d AND l.logged_at > @cd
                    ORDER BY l.logged_at DESC LIMIT @n",
                    DB.P("@u", uid), DB.P("@d", dayId), DB.P("@cd", cooldown.Value), DB.P("@n", count))
                : DB.Select(@"SELECT l.log_id,l.user_id,l.session_id,l.day_id,pd.name AS day_name,
                        l.sets_completed,l.sets_scheduled,l.ratio,l.logged_at
                    FROM day_volume_log l JOIN program_days pd ON pd.day_id=l.day_id
                    WHERE l.user_id=@u AND l.day_id=@d
                    ORDER BY l.logged_at DESC LIMIT @n",
                    DB.P("@u", uid), DB.P("@d", dayId), DB.P("@n", count));
            return MapVolumeLogs(dt);
        }

        private List<DayVolumeLogModel> MapVolumeLogs(System.Data.DataTable dt)
        {
            var list = new List<DayVolumeLogModel>();
            foreach (System.Data.DataRow r in dt.Rows)
            {
                list.Add(new DayVolumeLogModel
                {
                    LogId = Convert.ToInt32(r["log_id"]),
                    UserId = Convert.ToInt32(r["user_id"]),
                    SessionId = Convert.ToInt32(r["session_id"]),
                    DayId = Convert.ToInt32(r["day_id"]),
                    DayName = r["day_name"].ToString() ?? "",
                    SetsCompleted = Convert.ToInt32(r["sets_completed"]),
                    SetsScheduled = Convert.ToInt32(r["sets_scheduled"]),
                    Ratio = Convert.ToDouble(r["ratio"]),
                    LoggedAt = Convert.ToDateTime(r["logged_at"])
                });
            }
            return list;
        }

        // ── Cooldown (also functions as "reset the streak") ────────
        public DateTime? GetCooldownUntil(int uid, int dayId)
        {
            var v = DB.Scalar("SELECT cooldown_until FROM day_suggestion_cooldown WHERE user_id=@u AND day_id=@d",
                DB.P("@u", uid), DB.P("@d", dayId));
            return v != null && v != DBNull.Value ? Convert.ToDateTime(v) : (DateTime?)null;
        }

        public void SetCooldown(int uid, int dayId, DateTime until)
        {
            DB.NonQuery(@"INSERT INTO day_suggestion_cooldown(user_id,day_id,cooldown_until) VALUES(@u,@d,@c)
                ON DUPLICATE KEY UPDATE cooldown_until=@c",
                DB.P("@u", uid), DB.P("@d", dayId), DB.P("@c", until));
        }

        // ── Suggestions ──────────────────────────────────────────
        public int CreateSuggestion(int uid, string type, string payloadJson)
        {
            DB.NonQuery("INSERT INTO coach_suggestions(user_id,type,payload,status) VALUES(@u,@t,@p,'pending')",
                DB.P("@u", uid), DB.P("@t", type), DB.P("@p", payloadJson));
            var id = DB.Scalar("SELECT LAST_INSERT_ID()");
            return Convert.ToInt32(id);
        }

        public CoachSuggestionModel? GetPendingSuggestion(int uid)
        {
            var dt = DB.Select(@"SELECT suggestion_id,user_id,type,payload,status,created_at,resolved_at
                FROM coach_suggestions WHERE user_id=@u AND status='pending'
                ORDER BY created_at DESC LIMIT 1", DB.P("@u", uid));
            if (dt.Rows.Count == 0) return null;
            var r = dt.Rows[0];
            return new CoachSuggestionModel
            {
                SuggestionId = Convert.ToInt32(r["suggestion_id"]),
                UserId = Convert.ToInt32(r["user_id"]),
                Type = r["type"].ToString() ?? "",
                Payload = r["payload"].ToString() ?? "{}",
                Status = r["status"].ToString() ?? "pending",
                CreatedAt = Convert.ToDateTime(r["created_at"]),
                ResolvedAt = r["resolved_at"] != DBNull.Value ? Convert.ToDateTime(r["resolved_at"]) : (DateTime?)null
            };
        }

        public void ResolveSuggestion(int suggestionId, string status)
        {
            DB.NonQuery("UPDATE coach_suggestions SET status=@s,resolved_at=NOW() WHERE suggestion_id=@id",
                DB.P("@s", status), DB.P("@id", suggestionId));
        }

        // ── Data needed to build a suggestion's payload ─────────────
        public int GetScheduledSetsForDay(int dayId)
        {
            var v = DB.Scalar("SELECT COALESCE(SUM(target_sets),0) FROM program_day_exercises WHERE day_id=@d", DB.P("@d", dayId));
            return Convert.ToInt32(v);
        }

        public string GetDayName(int dayId)
        {
            var v = DB.Scalar("SELECT name FROM program_days WHERE day_id=@d", DB.P("@d", dayId));
            return v?.ToString() ?? "";
        }

        // Exercises currently scheduled on a day, with the classification
        // data needed to decide what's safe to cut (isolation before compound).
        public List<(int PdeId, int ExerciseId, string Name, string MuscleGroup, int TargetSets, bool? IsCompound)> GetDayExercisesForCoaching(int dayId)
        {
            var dt = DB.Select(@"SELECT pde.pde_id,pde.exercise_id,e.name,e.muscle_group_id,mg.name AS muscle_group,
                    pde.target_sets,e.is_compound
                FROM program_day_exercises pde
                JOIN exercises e ON e.exercise_id=pde.exercise_id
                JOIN muscle_groups mg ON mg.group_id=e.muscle_group_id
                WHERE pde.day_id=@d ORDER BY pde.exercise_order", DB.P("@d", dayId));
            var list = new List<(int, int, string, string, int, bool?)>();
            foreach (System.Data.DataRow r in dt.Rows)
            {
                list.Add((
                    Convert.ToInt32(r["pde_id"]),
                    Convert.ToInt32(r["exercise_id"]),
                    r["name"].ToString() ?? "",
                    r["muscle_group"].ToString() ?? "",
                    Convert.ToInt32(r["target_sets"]),
                    r["is_compound"] != DBNull.Value ? Convert.ToBoolean(r["is_compound"]) : (bool?)null
                ));
            }
            return list;
        }

        // Candidate exercises to add: same muscle group(s) as what's already on
        // the day, not already scheduled on it, active exercises only.
        public List<CandidateExercise> GetAddCandidates(int dayId, List<string> muscleGroups, int limit)
        {
            if (muscleGroups.Count == 0) return new();
            var placeholders = string.Join(",", muscleGroups.Select((_, i) => $"@mg{i}"));
            var pars = new List<MySqlParameter>();
            for (int i = 0; i < muscleGroups.Count; i++) pars.Add(DB.P($"@mg{i}", muscleGroups[i]));
            pars.Add(DB.P("@d", dayId));
            pars.Add(DB.P("@lim", limit));
            var dt = DB.Select($@"SELECT e.exercise_id,e.name,mg.name AS muscle_group
                FROM exercises e
                JOIN muscle_groups mg ON mg.group_id=e.muscle_group_id
                WHERE mg.name IN ({placeholders}) AND e.is_active=1
                  AND e.exercise_id NOT IN (SELECT exercise_id FROM program_day_exercises WHERE day_id=@d)
                ORDER BY e.is_compound DESC, e.name LIMIT @lim", pars.ToArray());
            var list = new List<CandidateExercise>();
            foreach (System.Data.DataRow r in dt.Rows)
                list.Add(new CandidateExercise { ExerciseId = Convert.ToInt32(r["exercise_id"]), Name = r["name"].ToString() ?? "", MuscleGroup = r["muscle_group"].ToString() ?? "" });
            return list;
        }

        // All day_ids currently active for this user (their active schedule's
        // distinct scheduled workout days) — used to check "every day flagged".
        public List<int> GetActiveScheduleDayIds(int uid)
        {
            var dt = DB.Select(@"SELECT DISTINCT ss.day_id FROM schedule_slots ss
                JOIN user_schedules us ON us.schedule_id=ss.schedule_id
                WHERE us.user_id=@u AND us.is_active=1 AND ss.day_id IS NOT NULL", DB.P("@u", uid));
            var list = new List<int>();
            foreach (System.Data.DataRow r in dt.Rows) list.Add(Convert.ToInt32(r["day_id"]));
            return list;
        }

        public void ApplyChange(SuggestedChange c)
        {
            if (c.Action == "remove_exercise")
                DB.NonQuery("DELETE FROM program_day_exercises WHERE pde_id=@p", DB.P("@p", c.PdeId));
            else if (c.Action == "cut_set" && c.NewTargetSets.HasValue)
                DB.NonQuery("UPDATE program_day_exercises SET target_sets=@t WHERE pde_id=@p",
                    DB.P("@t", c.NewTargetSets.Value), DB.P("@p", c.PdeId));
            else if (c.Action == "add_set" && c.NewTargetSets.HasValue)
                DB.NonQuery("UPDATE program_day_exercises SET target_sets=@t WHERE pde_id=@p",
                    DB.P("@t", c.NewTargetSets.Value), DB.P("@p", c.PdeId));
        }

        public void AddExerciseToDay(int dayId, int exerciseId, int targetSets, int targetReps)
        {
            var maxOrder = DB.Scalar("SELECT COALESCE(MAX(exercise_order),0) FROM program_day_exercises WHERE day_id=@d", DB.P("@d", dayId));
            int order = Convert.ToInt32(maxOrder) + 1;
            DB.NonQuery(@"INSERT INTO program_day_exercises(day_id,exercise_id,exercise_order,target_sets,target_reps)
                VALUES(@d,@e,@o,@ts,@tr)",
                DB.P("@d", dayId), DB.P("@e", exerciseId), DB.P("@o", order), DB.P("@ts", targetSets), DB.P("@tr", targetReps));
        }
    }
}
