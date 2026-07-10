using FitForge.DL; using FitForge.Models;
namespace FitForge.BL
{
    public class SkillBL(SkillDL dl, AchievementBL achBL)
    {
        public List<SkillModel> GetAll(int uid)=>dl.GetAllForUser(uid);

        public (bool ok, string msg, bool isReq, List<AchievementModel> achievements) Toggle(int uid, int skillId){
            if(dl.IsUnlocked(uid,skillId)){dl.Lock(uid,skillId);return(true,"Skill removed",false,new());}
            var(met,unmet)=dl.CheckRequirements(uid,skillId);
            if(!met){
                var lines=unmet.Select(r=>$"{r.ExerciseName}: need {r.RequiredReps} reps (best: {(r.UserBest>0?r.UserBest.ToString():"none")})");
                return(false,"Requirements not met: "+string.Join(" | ",lines),true,new());
            }
            dl.Unlock(uid,skillId);
            var achievements = achBL.CheckSkillAchievements(uid, true, false, false);
            return(true,"Skill unlocked! 🔓",false,achievements);
        }

        public (bool ok, string msg, List<AchievementModel> achievements) Advance(int uid, int skillId){
            var (ok, msg) = dl.AdvanceStep(uid, skillId);
            var achievements = new List<AchievementModel>();
            if (ok && msg.Contains("mastered"))
            {
                var allSkills = dl.GetAllForUser(uid);
                bool allMastered = allSkills.Any() && allSkills.All(s => s.MasteredAt != null);
                achievements = achBL.CheckSkillAchievements(uid, false, true, allMastered);
            }
            return (ok, msg, achievements);
        }
    }
}
