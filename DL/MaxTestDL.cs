using Microsoft.Extensions.Logging;
using System.Data;
using System.Linq;
// ============================================================
// DL/MaxTestDL.cs
// "Test Your Max" — logging a one-off max-effort attempt for any exercise
// without starting a full workout session. Every attempt is kept here (full
// history, for the "past attempts" list); personal_records only ever holds
// the single current best per exercise, so this table is what makes the
// trend visible.
// ============================================================
using FitForge.Models;
namespace FitForge.DL
{
    public class MaxTestDL(ILogger<MaxTestDL> log)
    {
        // Curated by name, not hardcoded id — safer against seed data changing
        // between environments. Matches what's actually in the exercise catalog.
        private static readonly string[] GymLiftNames = {
            "Deadlift", "Bench Press", "Barbell Squat", "Lat Pulldown", "Dumbbell Curl", "Leg Press"
        };

        public void LogAttempt(int uid, int exerciseId, int reps, double? weightKg){
            DB.NonQuery(@"INSERT INTO max_test_attempts(user_id,exercise_id,reps,weight_kg)
                VALUES(@u,@e,@r,@w)",
                DB.P("@u",uid), DB.P("@e",exerciseId), DB.P("@r",reps), DB.P("@w",weightKg));
        }

        public List<MaxTestAttemptModel> GetRecentAttempts(int uid, int exerciseId, int limit = 3){
            return DB.Select(@"SELECT * FROM max_test_attempts
                WHERE user_id=@u AND exercise_id=@e ORDER BY attempted_at DESC LIMIT @lim",
                DB.P("@u",uid), DB.P("@e",exerciseId), DB.P("@lim",limit))
              .Rows().Select(r => new MaxTestAttemptModel{
                  AttemptId = Convert.ToInt32(r["attempt_id"]),
                  Reps = Convert.ToInt32(r["reps"]),
                  WeightKg = r["weight_kg"] == DBNull.Value ? null : Convert.ToDouble(r["weight_kg"]),
                  AttemptedAt = Convert.ToDateTime(r["attempted_at"])
              }).ToList();
        }

        public List<MaxTestHubItemModel> GetSkillExerciseItems(int uid){
            var rows = DB.Select(@"
                SELECT sr.exercise_id, e.name AS ex_name, e.tracking_mode, sr.required_reps, sk.name AS skill_name,
                    COALESCE((SELECT MAX(value) FROM personal_records WHERE user_id=@u AND exercise_id=sr.exercise_id AND record_type='max_reps'),0) AS best_reps,
                    (SELECT MAX(value) FROM personal_records WHERE user_id=@u AND exercise_id=sr.exercise_id AND record_type='max_weight') AS best_weight
                FROM skill_requirements sr
                JOIN exercises e ON sr.exercise_id = e.exercise_id
                JOIN skills sk ON sr.skill_id = sk.skill_id",
                DB.P("@u", uid));

            // Same exercise can be required by more than one skill — group in C#
            // rather than SQL so we can combine the skill names cleanly.
            return rows.Rows()
              .GroupBy(r => Convert.ToInt32(r["exercise_id"]))
              .Select(g => {
                  var first = g.First();
                  var skillNames = g.Select(r => r["skill_name"].ToString()).Distinct();
                  return new MaxTestHubItemModel{
                      ExerciseId = g.Key,
                      ExerciseName = first["ex_name"].ToString()!,
                      TrackingMode = first["tracking_mode"].ToString()!,
                      BestReps = Convert.ToInt32(first["best_reps"]),
                      BestWeightKg = first["best_weight"] == DBNull.Value ? null : Convert.ToDouble(first["best_weight"]),
                      IsSkillRequirement = true,
                      SkillName = string.Join(", ", skillNames),
                      RequiredReps = Convert.ToInt32(first["required_reps"])
                  };
              }).OrderBy(x => x.ExerciseName).ToList();
        }

        public List<MaxTestHubItemModel> GetGymLiftItems(int uid){
            var placeholders = string.Join(",", GymLiftNames.Select((_, i) => $"@n{i}"));
            var pars = GymLiftNames.Select((n, i) => DB.P($"@n{i}", n)).Append(DB.P("@u", uid)).ToArray();
            var rows = DB.Select($@"
                SELECT e.exercise_id, e.name AS ex_name, e.tracking_mode,
                    COALESCE((SELECT MAX(value) FROM personal_records WHERE user_id=@u AND exercise_id=e.exercise_id AND record_type='max_reps'),0) AS best_reps,
                    (SELECT MAX(value) FROM personal_records WHERE user_id=@u AND exercise_id=e.exercise_id AND record_type='max_weight') AS best_weight
                FROM exercises e WHERE e.name IN ({placeholders})", pars);

            return rows.Rows().Select(r => new MaxTestHubItemModel{
                ExerciseId = Convert.ToInt32(r["exercise_id"]),
                ExerciseName = r["ex_name"].ToString()!,
                TrackingMode = r["tracking_mode"].ToString()!,
                BestReps = Convert.ToInt32(r["best_reps"]),
                BestWeightKg = r["best_weight"] == DBNull.Value ? null : Convert.ToDouble(r["best_weight"]),
                IsSkillRequirement = false
            }).OrderBy(x => Array.IndexOf(GymLiftNames, x.ExerciseName)).ToList();
        }
    }
}
