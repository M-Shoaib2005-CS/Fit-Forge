using FitForge.Models;
using Microsoft.Extensions.Logging;
namespace FitForge.DL
{
    public class AchievementDL(ILogger<AchievementDL> log)
    {
        // ── Achievements ─────────────────────────────────────────────
        public List<AchievementModel> GetAll(int uid)
        {
            try
            {
                var dt = DB.Select(@"
                    SELECT a.*, ua.unlocked_at, ua.seen
                    FROM achievements a
                    LEFT JOIN user_achievements ua ON ua.achievement_id=a.achievement_id AND ua.user_id=@u
                    ORDER BY a.category, a.rarity", DB.P("@u", uid));
                return dt.Rows().Select(r => new AchievementModel
                {
                    AchievementId = Convert.ToInt32(r["achievement_id"]),
                    Code          = r["code"].ToString()!,
                    Name          = r["name"].ToString()!,
                    Description   = r["description"].ToString()!,
                    Icon          = r["icon"].ToString()!,
                    Category      = r["category"].ToString()!,
                    Rarity        = r["rarity"].ToString()!,
                    IsUnlocked    = r["unlocked_at"] != DBNull.Value,
                    UnlockedAt    = r["unlocked_at"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["unlocked_at"]),
                    Seen          = r["seen"] != DBNull.Value && Convert.ToInt32(r["seen"]) == 1
                }).ToList();
            }
            catch (Exception ex) { log.LogError(ex, "GetAll achievements uid={U}", uid); return new(); }
        }

        public List<AchievementModel> GetUnseen(int uid)
        {
            try
            {
                var dt = DB.Select(@"
                    SELECT a.*, ua.unlocked_at FROM achievements a
                    JOIN user_achievements ua ON ua.achievement_id=a.achievement_id
                    WHERE ua.user_id=@u AND ua.seen=0", DB.P("@u", uid));
                return dt.Rows().Select(r => new AchievementModel
                {
                    AchievementId = Convert.ToInt32(r["achievement_id"]),
                    Code          = r["code"].ToString()!,
                    Name          = r["name"].ToString()!,
                    Description   = r["description"].ToString()!,
                    Icon          = r["icon"].ToString()!,
                    Category      = r["category"].ToString()!,
                    Rarity        = r["rarity"].ToString()!,
                    IsUnlocked    = true,
                    UnlockedAt    = Convert.ToDateTime(r["unlocked_at"])
                }).ToList();
            }
            catch { return new(); }
        }

        public void MarkSeen(int uid)
        {
            try { DB.NonQuery("UPDATE user_achievements SET seen=1 WHERE user_id=@u", DB.P("@u", uid)); }
            catch (Exception ex) { log.LogError(ex, "MarkSeen uid={U}", uid); }
        }

        /// <summary>Tries to award an achievement. Returns true if newly unlocked.</summary>
        public bool TryAward(int uid, string code)
        {
            try
            {
                var aidObj = DB.Scalar("SELECT achievement_id FROM achievements WHERE code=@c", DB.P("@c", code));
                if (aidObj == null || aidObj == DBNull.Value) return false;
                int aid = Convert.ToInt32(aidObj);
                var existing = DB.Scalar("SELECT ua_id FROM user_achievements WHERE user_id=@u AND achievement_id=@a",
                    DB.P("@u", uid), DB.P("@a", aid));
                if (existing != null && existing != DBNull.Value) return false;
                DB.NonQuery("INSERT INTO user_achievements(user_id, achievement_id) VALUES(@u, @a)",
                    DB.P("@u", uid), DB.P("@a", aid));
                return true;
            }
            catch (Exception ex) { log.LogError(ex, "TryAward code={C} uid={U}", code, uid); return false; }
        }

        public int GetUnlockedCount(int uid)
        {
            try { return Convert.ToInt32(DB.Scalar("SELECT COUNT(*) FROM user_achievements WHERE user_id=@u", DB.P("@u", uid))); }
            catch { return 0; }
        }

        // ── Exercise Badges ───────────────────────────────────────────
        public List<ExerciseBadgeModel> GetBadges(int uid)
        {
            try
            {
                var dt = DB.Select(@"
                    SELECT eb.exercise_id, e.name AS ex_name, mg.name AS muscle,
                           eb.session_count, eb.tier
                    FROM exercise_badges eb
                    JOIN exercises e ON eb.exercise_id=e.exercise_id
                    LEFT JOIN muscle_groups mg ON e.muscle_group_id=mg.group_id
                    WHERE eb.user_id=@u AND eb.tier != 'none'
                    ORDER BY FIELD(eb.tier,'legend','diamond','gold','silver','bronze'), eb.session_count DESC",
                    DB.P("@u", uid));
                return dt.Rows().Select(r =>
                {
                    int cnt = Convert.ToInt32(r["session_count"]);
                    return new ExerciseBadgeModel
                    {
                        ExerciseId   = Convert.ToInt32(r["exercise_id"]),
                        ExerciseName = r["ex_name"].ToString()!,
                        MuscleGroup  = r["muscle"] == DBNull.Value ? "" : r["muscle"].ToString()!,
                        Tier         = r["tier"].ToString()!,
                        SessionCount = cnt,
                        NextTierAt   = ExerciseBadgeModel.NextThreshold(cnt)
                    };
                }).ToList();
            }
            catch (Exception ex) { log.LogError(ex, "GetBadges uid={U}", uid); return new(); }
        }

        /// <summary>
        /// Increments session count for the exercise and upgrades tier if threshold crossed.
        /// Returns the new tier if it changed (for achievement checking), otherwise null.
        /// </summary>
        public string? IncrementBadge(int uid, int exerciseId)
        {
            try
            {
                // Upsert session_count
                DB.NonQuery(@"
                    INSERT INTO exercise_badges(user_id, exercise_id, session_count, tier)
                    VALUES(@u, @e, 1, 'bronze')
                    ON DUPLICATE KEY UPDATE session_count=session_count+1,
                        tier=CASE
                            WHEN session_count+1 >= 100 THEN 'legend'
                            WHEN session_count+1 >= 50  THEN 'diamond'
                            WHEN session_count+1 >= 25  THEN 'gold'
                            WHEN session_count+1 >= 10  THEN 'silver'
                            ELSE 'bronze' END,
                        awarded_at=CASE
                            WHEN tier != CASE
                                WHEN session_count+1 >= 100 THEN 'legend'
                                WHEN session_count+1 >= 50  THEN 'diamond'
                                WHEN session_count+1 >= 25  THEN 'gold'
                                WHEN session_count+1 >= 10  THEN 'silver'
                                ELSE 'bronze' END
                            THEN NOW() ELSE awarded_at END",
                    DB.P("@u", uid), DB.P("@e", exerciseId));

                var dt = DB.Select("SELECT session_count, tier FROM exercise_badges WHERE user_id=@u AND exercise_id=@e",
                    DB.P("@u", uid), DB.P("@e", exerciseId));
                if (dt.Rows.Count == 0) return null;
                string newTier = dt.Rows[0]["tier"].ToString()!;
                int count = Convert.ToInt32(dt.Rows[0]["session_count"]);
                // Return new tier only on exact threshold hits
                if (count == 1 || count == 10 || count == 25 || count == 50 || count == 100)
                    return newTier;
                return null;
            }
            catch (Exception ex) { log.LogError(ex, "IncrementBadge uid={U} ex={E}", uid, exerciseId); return null; }
        }
    }
}
