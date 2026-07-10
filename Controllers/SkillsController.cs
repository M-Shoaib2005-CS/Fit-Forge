using FitForge.BL; using FitForge.DL; using FitForge.Models; using Microsoft.AspNetCore.Mvc;
namespace FitForge.Controllers
{
    public class SkillsController(SkillBL bl, PersonalRecordDL prDL, UserDL uDL) : BaseController(uDL)
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
    }
}
