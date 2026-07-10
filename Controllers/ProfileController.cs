using FitForge.BL; using FitForge.DL; using FitForge.Models; using Microsoft.AspNetCore.Mvc;
namespace FitForge.Controllers
{
    public class ProfileController(ProfileBL bl, UserBL uBL, UserDL uDL) : BaseController(uDL)
    {
        public IActionResult Index(){
            if(Uid==null)return RedirectToAction("Login","Account");
            ViewData["Page"]="Profile";
            var vm = bl.BuildVM(Uid.Value);
            ViewData["UserTheme"] = vm.User.Theme;
            return View(vm);
        }
        [HttpPost,ValidateAntiForgeryToken]
        public IActionResult UpdateStats(double height, double weight){
            if(Uid==null)return RedirectToAction("Login","Account");
            var(ok,msg)=bl.UpdateStats(Uid.Value,height,weight);
            TempData[ok?"Success":"Error"]=msg; return RedirectToAction("Index");
        }
        [HttpPost,ValidateAntiForgeryToken]
        public IActionResult LogWeight(decimal weight, string notes){
            if(Uid==null)return RedirectToAction("Login","Account");
            bl.LogWeight(Uid.Value,weight,notes??"");
            TempData["Success"]="Weight logged!"; return RedirectToAction("Index");
        }
        [HttpPost,IgnoreAntiforgeryToken]
        public IActionResult LogMeasurement([FromBody]LogMeasurementReq req){
            if(Uid==null)return Json(new{success=false});
            var(ok,msg)=bl.LogMeasurement(Uid.Value,req);
            return Json(new{success=ok,msg});
        }
        [HttpPost,ValidateAntiForgeryToken]
        public IActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword){
            if(Uid==null)return RedirectToAction("Login","Account");
            var(ok,msg)=uBL.UpdatePassword(Uid.Value,currentPassword,newPassword,confirmPassword);
            TempData[ok?"Success":"Error"]=msg; return RedirectToAction("Index");
        }
        [HttpPost,IgnoreAntiforgeryToken]
        public IActionResult SetTheme([FromBody]ThemeReq req){
            if(Uid==null)return Json(new{success=false});
            uBL.UpdateTheme(Uid.Value,req.Theme); return Json(new{success=true});
        }
        [HttpPost,IgnoreAntiforgeryToken]
        public IActionResult LogInjury([FromBody]LogInjuryReq req){
            if(Uid==null)return Json(new{success=false});
            var(ok,msg)=bl.LogInjury(Uid.Value,req);
            return Json(new{success=ok,msg});
        }
        [HttpPost,IgnoreAntiforgeryToken]
        public IActionResult ResolveInjury([FromBody]int uiId){
            if(Uid==null)return Json(new{success=false});
            bool ok=bl.ResolveInjury(Uid.Value,uiId); return Json(new{success=ok});
        }
        public IActionResult GetInjuryVM(){
            if(Uid==null)return Json(new{success=false});
            return Json(new{success=true,data=bl.GetInjuryReportVM(Uid.Value)});
        }
        [HttpPost,ValidateAntiForgeryToken]
        public IActionResult DeleteAccount(string confirmText){
            if(Uid==null)return RedirectToAction("Login","Account");
            if(confirmText?.ToUpper()!="DELETE"){TempData["Error"]="Type DELETE to confirm";return RedirectToAction("Index");}
            uBL.DeleteAccount(Uid.Value); return RedirectToAction("Login","Account");
        }
    }
}
