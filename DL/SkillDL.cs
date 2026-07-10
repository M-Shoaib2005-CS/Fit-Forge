using Microsoft.Extensions.Logging;
using System.Linq;
using FitForge.Models; using System.Data;
namespace FitForge.DL
{
    public class SkillDL(ILogger<SkillDL> log)
    {
        // All 5 queries run once for ALL skills — no N+1
        public List<SkillModel> GetAllForUser(int uid){
            var skills=DB.Select("SELECT * FROM skills ORDER BY category,difficulty,skill_id");
            var userSkills=DB.Select("SELECT * FROM user_skills WHERE user_id=@u",DB.P("@u",uid));
            var steps=DB.Select("SELECT * FROM skill_steps ORDER BY skill_id,step_order");
            var tips=DB.Select("SELECT * FROM skill_tips ORDER BY skill_id,tip_id");
            var reqs=DB.Select(@"SELECT sr.*,e.name AS ex_name,
                COALESCE((SELECT MAX(value) FROM personal_records WHERE user_id=@u AND exercise_id=sr.exercise_id AND record_type='max_reps'),0) AS user_best
                FROM skill_requirements sr JOIN exercises e ON sr.exercise_id=e.exercise_id",DB.P("@u",uid));

            var usMap=userSkills.Rows().ToDictionary(r=>Convert.ToInt32(r["skill_id"]),r=>(
                Unlocked:r["is_unlocked"]!=DBNull.Value&&Convert.ToInt32(r["is_unlocked"])==1,
                StepId:r["current_step_id"]==DBNull.Value?0:Convert.ToInt32(r["current_step_id"]),
                UnlockedAt:r["unlocked_at"]==DBNull.Value?(DateTime?)null:Convert.ToDateTime(r["unlocked_at"]),
                MasteredAt:r["mastered_at"]==DBNull.Value?(DateTime?)null:Convert.ToDateTime(r["mastered_at"])));
            var stepsMap=steps.Rows().GroupBy(r=>Convert.ToInt32(r["skill_id"])).ToDictionary(g=>g.Key,
                g=>g.Select(r=>new SkillStepModel{StepId=Convert.ToInt32(r["step_id"]),StepOrder=Convert.ToInt32(r["step_order"]),Title=r["title"].ToString()!,Instructions=r["instructions"]?.ToString()??""}).ToList());
            var tipsMap=tips.Rows().GroupBy(r=>Convert.ToInt32(r["skill_id"])).ToDictionary(g=>g.Key,g=>g.Select(r=>r["tip_text"].ToString()!).ToList());
            var reqsMap=reqs.Rows().GroupBy(r=>Convert.ToInt32(r["skill_id"])).ToDictionary(g=>g.Key,
                g=>g.Select(r=>new SkillRequirementModel{ExerciseId=Convert.ToInt32(r["exercise_id"]),ExerciseName=r["ex_name"].ToString()!,RequiredReps=Convert.ToInt32(r["required_reps"]),UserBest=Convert.ToInt32(r["user_best"])}).ToList());

            return skills.Rows().Select(r=>{
                int sid=Convert.ToInt32(r["skill_id"]);
                var us=usMap.TryGetValue(sid,out var u)?u:(Unlocked:false,StepId:0,UnlockedAt:(DateTime?)null,MasteredAt:(DateTime?)null);
                var sk=stepsMap.TryGetValue(sid,out var st)?st:new();
                var tp=tipsMap.TryGetValue(sid,out var t)?t:new();
                var rq=reqsMap.TryGetValue(sid,out var rv)?rv:new();
                var curStep=sk.FirstOrDefault(s=>s.StepId==us.StepId)??(us.Unlocked&&sk.Count>0?sk[0]:null);
                int tipIdx=tp.Count>0&&curStep!=null?Math.Max(0,Math.Min(curStep.StepOrder-1,tp.Count-1)):0;
                return new SkillModel{SkillId=sid,Name=r["name"].ToString()!,Description=r["description"]?.ToString()??"",
                    Category=r["category"].ToString()!,Difficulty=r["difficulty"].ToString()!,
                    IsUnlocked=us.Unlocked,CurrentStepId=us.StepId,CurrentStep=curStep?.StepOrder??0,
                    TotalSteps=sk.Count,RequirementsMet=rq.Count==0||rq.All(x=>x.Met),
                    CurrentStepTitle=curStep?.Title??"",CurrentStepInstructions=curStep?.Instructions??"",
                    CurrentTip=us.Unlocked&&tp.Count>0?tp[tipIdx]:"",
                    UnlockedAt=us.UnlockedAt,MasteredAt=us.MasteredAt,Steps=sk,Tips=tp,Requirements=rq};
            }).ToList();
        }

        public List<SkillModel> GetActiveForUser(int uid)=>
            GetAllForUser(uid).Where(s=>s.IsUnlocked&&s.MasteredAt==null).Take(5).ToList();

        public (bool met, List<SkillRequirementModel> unmet) CheckRequirements(int uid, int skillId){
            var reqs=DB.Select(@"SELECT sr.*,e.name AS ex_name,
                COALESCE((SELECT MAX(value) FROM personal_records WHERE user_id=@u AND exercise_id=sr.exercise_id AND record_type='max_reps'),0) AS user_best
                FROM skill_requirements sr JOIN exercises e ON sr.exercise_id=e.exercise_id WHERE sr.skill_id=@s",
                DB.P("@u",uid),DB.P("@s",skillId))
              .Rows().Select(r=>new SkillRequirementModel{ExerciseId=Convert.ToInt32(r["exercise_id"]),
                  ExerciseName=r["ex_name"].ToString()!,RequiredReps=Convert.ToInt32(r["required_reps"]),UserBest=Convert.ToInt32(r["user_best"])}).ToList();
            var unmet=reqs.Where(r=>!r.Met).ToList();
            return(unmet.Count==0,unmet);
        }

        public bool IsUnlocked(int uid, int skillId)=>
            Convert.ToInt32(DB.Scalar("SELECT is_unlocked FROM user_skills WHERE user_id=@u AND skill_id=@s",DB.P("@u",uid),DB.P("@s",skillId)))==1;

        public void Unlock(int uid, int skillId){
            var firstStep=DB.Scalar("SELECT step_id FROM skill_steps WHERE skill_id=@s ORDER BY step_order LIMIT 1",DB.P("@s",skillId));
            int? stepId=firstStep!=null&&firstStep!=DBNull.Value?Convert.ToInt32(firstStep):null;
            DB.NonQuery(@"INSERT INTO user_skills(user_id,skill_id,is_unlocked,current_step_id,unlocked_at)
                VALUES(@u,@s,1,@step,NOW()) ON DUPLICATE KEY UPDATE is_unlocked=1,current_step_id=@step,unlocked_at=NOW()",
                DB.P("@u",uid),DB.P("@s",skillId),DB.P("@step",stepId));
            log.LogInformation("Skill {S} unlocked for user {U}",skillId,uid);
        }

        public void Lock(int uid, int skillId)=>
            DB.NonQuery("UPDATE user_skills SET is_unlocked=0,current_step_id=NULL WHERE user_id=@u AND skill_id=@s",DB.P("@u",uid),DB.P("@s",skillId));

        public (bool success, string msg) AdvanceStep(int uid, int skillId){
            var curStepId=DB.Scalar("SELECT current_step_id FROM user_skills WHERE user_id=@u AND skill_id=@s AND is_unlocked=1",DB.P("@u",uid),DB.P("@s",skillId));
            if(curStepId==null||curStepId==DBNull.Value)return(false,"Skill not active");
            var next=DB.Scalar(@"SELECT step_id FROM skill_steps WHERE skill_id=@s
                AND step_order>(SELECT step_order FROM skill_steps WHERE step_id=@cur AND skill_id=@s)
                ORDER BY step_order LIMIT 1",DB.P("@s",skillId),DB.P("@cur",curStepId));
            if(next==null||next==DBNull.Value){
                DB.NonQuery("UPDATE user_skills SET mastered_at=NOW() WHERE user_id=@u AND skill_id=@s",DB.P("@u",uid),DB.P("@s",skillId));
                return(true,"Skill mastered! 🎉");
            }
            DB.NonQuery("UPDATE user_skills SET current_step_id=@n WHERE user_id=@u AND skill_id=@s",DB.P("@n",next),DB.P("@u",uid),DB.P("@s",skillId));
            return(true,"Step complete! Keep going 💪");
        }

        /// <summary>
        /// Auto-advance is disabled during workouts — skills advance manually on the Skills page.
        /// Unlock requirements are separate from per-step progression.
        /// </summary>
        public List<string> CheckAutoAdvance(int uid, int exerciseId, int repsAchieved)
        {
            return new List<string>();
        }
    }
}
