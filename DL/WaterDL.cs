using FitForge.Models;
using Microsoft.Extensions.Logging;
namespace FitForge.DL
{
    public class WaterDL(ILogger<WaterDL> log)
    {
        public WaterSummaryModel GetTodaySummary(int uid)
        {
            try
            {
                var goal = DB.Scalar("SELECT water_goal_ml FROM users WHERE user_id=@u", DB.P("@u", uid));
                int goalMl = goal != null && goal != DBNull.Value ? Convert.ToInt32(goal) : 2500;
                var dt = DB.Select(
                    "SELECT intake_id, amount_ml, logged_at FROM water_intake WHERE user_id=@u AND DATE(logged_at)=CURDATE() ORDER BY logged_at DESC",
                    DB.P("@u", uid));
                var entries = dt.Rows().Select(r => new WaterIntakeModel
                {
                    IntakeId = Convert.ToInt32(r["intake_id"]),
                    AmountMl = Convert.ToInt32(r["amount_ml"]),
                    LoggedAt = Convert.ToDateTime(r["logged_at"])
                }).ToList();
                return new WaterSummaryModel
                {
                    TotalMl = entries.Sum(e => e.AmountMl),
                    GoalMl  = goalMl,
                    Entries = entries
                };
            }
            catch (Exception ex) { log.LogError(ex, "GetTodaySummary uid={U}", uid); return new WaterSummaryModel(); }
        }

        public bool LogWater(int uid, int amountMl)
        {
            try
            {
                DB.NonQuery("INSERT INTO water_intake(user_id, amount_ml) VALUES(@u, @a)",
                    DB.P("@u", uid), DB.P("@a", amountMl));
                return true;
            }
            catch (Exception ex) { log.LogError(ex, "LogWater uid={U}", uid); return false; }
        }

        public bool DeleteEntry(int uid, int intakeId)
        {
            try
            {
                DB.NonQuery("DELETE FROM water_intake WHERE intake_id=@i AND user_id=@u",
                    DB.P("@i", intakeId), DB.P("@u", uid));
                return true;
            }
            catch (Exception ex) { log.LogError(ex, "DeleteWater uid={U}", uid); return false; }
        }

        public bool UpdateGoal(int uid, int goalMl)
        {
            try
            {
                DB.NonQuery("UPDATE users SET water_goal_ml=@g WHERE user_id=@u",
                    DB.P("@g", goalMl), DB.P("@u", uid));
                return true;
            }
            catch (Exception ex) { log.LogError(ex, "UpdateGoal uid={U}", uid); return false; }
        }

        /// <summary>Returns how many consecutive days the user hit their water goal (for achievement check).</summary>
        public int GetConsecutiveGoalDays(int uid)
        {
            try
            {
                var dt = DB.Select(@"
                    SELECT DATE(logged_at) AS day, SUM(amount_ml) AS total
                    FROM water_intake WHERE user_id=@u AND logged_at >= DATE_SUB(CURDATE(), INTERVAL 30 DAY)
                    GROUP BY DATE(logged_at) ORDER BY day DESC", DB.P("@u", uid));
                var goal = DB.Scalar("SELECT water_goal_ml FROM users WHERE user_id=@u", DB.P("@u", uid));
                int goalMl = goal != null && goal != DBNull.Value ? Convert.ToInt32(goal) : 2500;
                int streak = 0;
                var today = DateTime.Today;
                foreach (var r in dt.Rows())
                {
                    var day   = Convert.ToDateTime(r["day"]).Date;
                    var total = Convert.ToInt32(r["total"]);
                    if (day == today.AddDays(-streak) && total >= goalMl) streak++;
                    else break;
                }
                return streak;
            }
            catch { return 0; }
        }
    }
}
