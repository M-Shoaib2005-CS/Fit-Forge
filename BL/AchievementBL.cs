using FitForge.DL;
using FitForge.Models;
using Microsoft.Extensions.Logging;

namespace FitForge.BL
{
    public class AchievementBL(AchievementDL aDL, WaterDL wDL, MeasurementDL mDL, ILogger<AchievementBL> log)
    {
        /// <summary>
        /// Full achievement check after a workout session finishes.
        /// Returns list of newly unlocked achievements to show as toasts.
        /// </summary>
        public List<AchievementModel> CheckAfterWorkout(int uid, int totalWorkouts, int currentStreak,
            int totalPRs, List<int> exerciseIds, int sessionHour, double totalVolumeKg)
        {
            var newOnes = new List<string>();

            // Workout count milestones
            if (totalWorkouts >= 1)   newOnes.Add("first_workout");
            if (totalWorkouts >= 10)  newOnes.Add("workouts_10");
            if (totalWorkouts >= 50)  newOnes.Add("workouts_50");
            if (totalWorkouts >= 100) newOnes.Add("workouts_100");
            if (totalWorkouts >= 250) newOnes.Add("workouts_250");

            // Streak milestones
            if (currentStreak >= 3)   newOnes.Add("streak_3");
            if (currentStreak >= 7)   newOnes.Add("streak_7");
            if (currentStreak >= 30)  newOnes.Add("streak_30");
            if (currentStreak >= 100) newOnes.Add("streak_100");

            // PR milestones
            if (totalPRs >= 1)  newOnes.Add("first_pr");
            if (totalPRs >= 5)  newOnes.Add("pr_5");
            if (totalPRs >= 25) newOnes.Add("pr_25");

            // Time-based
            if (sessionHour < 7)  newOnes.Add("early_bird");
            if (sessionHour >= 22) newOnes.Add("night_owl");

            // Volume king
            if (totalVolumeKg >= 10000) newOnes.Add("volume_10000");

            return AwardList(uid, newOnes);
        }

        /// <summary>Check and award badge-related achievements after a badge tier change.</summary>
        public List<AchievementModel> CheckBadgeAchievements(int uid, string newTier)
        {
            var codes = new List<string>();
            if (newTier == "bronze"  || newTier == "silver" || newTier == "gold" || newTier == "diamond" || newTier == "legend")
                codes.Add("bronze_collector");
            if (newTier == "silver"  || newTier == "gold"  || newTier == "diamond" || newTier == "legend")
                codes.Add("silver_collector");
            if (newTier == "gold"    || newTier == "diamond" || newTier == "legend")
                codes.Add("gold_collector");
            if (newTier == "diamond" || newTier == "legend")
                codes.Add("diamond_miner");
            if (newTier == "legend")
                codes.Add("legend_born");
            return AwardList(uid, codes);
        }

        public List<AchievementModel> CheckSkillAchievements(int uid, bool isFirstUnlock, bool isMastered, bool allMastered)
        {
            var codes = new List<string>();
            if (isFirstUnlock) codes.Add("first_skill");
            if (isMastered)    codes.Add("skill_mastered");
            if (allMastered)   codes.Add("all_skills");
            return AwardList(uid, codes);
        }

        public List<AchievementModel> CheckHealthAchievements(int uid, int weightLogCount)
        {
            var codes = new List<string>();
            if (weightLogCount >= 10) codes.Add("weight_logged_10");
            int waterStreak = wDL.GetConsecutiveGoalDays(uid);
            if (waterStreak >= 7) codes.Add("hydrated_7");
            int mCount = mDL.GetTotalCount(uid);
            if (mCount >= 1) codes.Add("measurements_1");
            return AwardList(uid, codes);
        }

        private List<AchievementModel> AwardList(int uid, List<string> codes)
        {
            var result = new List<AchievementModel>();
            var all = aDL.GetAll(uid);
            foreach (var code in codes.Distinct())
            {
                bool awarded = aDL.TryAward(uid, code);
                if (awarded)
                {
                    var meta = all.FirstOrDefault(a => a.Code == code);
                    if (meta != null)
                    {
                        meta.IsUnlocked = true;
                        result.Add(meta);
                        log.LogInformation("Achievement unlocked: {Code} for uid={U}", code, uid);
                    }
                }
            }
            return result;
        }
    }
}
