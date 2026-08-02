using FitForge.BL; using FitForge.DL; using FitForge.Models; using Microsoft.AspNetCore.Mvc;
namespace FitForge.Controllers
{
    public class SkillsController(SkillBL bl, PersonalRecordDL prDL, UserDL uDL, MaxTestBL maxTestBL) : BaseController(uDL)
    {
        public IActionResult Index(){
            if(Uid==null)return RedirectToAction("Login","Account");
            ViewData["Page"]="Skills";
            return View(new SkillsVM{Skills=bl.GetAll(Uid.Value),PRs=prDL.GetForUser(Uid.Value)});
        }
        [HttpPost,IgnoreAntiforgeryToken]
        public IActionResult Toggle([FromBody]SkillActionReq req){
            if(Uid==null)return Json(new{success=false});
            var(ok,msg,isReq,achievements)=bl.Toggle(Uid.Value,req.SkillId);
            return Json(new{success=ok,msg,isRequirement=isReq,
                newAchievements=achievements.Select(a=>new{a.Icon,a.Name,a.Rarity})});
        }
        [HttpPost,IgnoreAntiforgeryToken]
        public IActionResult Advance([FromBody]SkillActionReq req){
            if(Uid==null)return Json(new{success=false});
            var(ok,msg,achievements)=bl.Advance(Uid.Value,req.SkillId);
            return Json(new{success=ok,msg,newAchievements=achievements.Select(a=>new{a.Icon,a.Name,a.Rarity})});
        }

        // ── Test Your Max ────────────────────────────────────────────
        public IActionResult MaxTest(int? exerciseId){
            if(Uid==null)return RedirectToAction("Login","Account");
            ViewData["Page"]="Skills";
            ViewData["HighlightExerciseId"]=exerciseId;
            return View(maxTestBL.GetHub(Uid.Value));
        }
        [HttpPost,IgnoreAntiforgeryToken]
        public IActionResult LogMaxTest([FromBody]LogMaxTestReq req){
            if(Uid==null)return Json(new{success=false});
            if(req.Reps<=0)return Json(new{success=false,msg="Enter at least 1 rep"});
            var item=maxTestBL.LogAttempt(Uid.Value,req);
            return Json(new{success=true,item=new{
                item.ExerciseId,item.ExerciseName,item.BestReps,item.BestWeightKg,
                item.IsSkillRequirement,item.SkillName,item.RequiredReps,
                requirementMet = item.IsSkillRequirement && item.RequiredReps.HasValue && item.BestReps>=item.RequiredReps.Value
            }});
        }
        public IActionResult GetAttempts(int exerciseId){
            if(Uid==null)return Json(new{success=false});
            var attempts=maxTestBL.GetRecentAttempts(Uid.Value,exerciseId);
            return Json(new{success=true,attempts=attempts.Select(a=>new{a.Reps,a.WeightKg,attemptedAt=a.AttemptedAt.ToString("MMM d")})});
        }
    }
}
