using FitForge.BL; using FitForge.DL; using FitForge.Models; using Microsoft.AspNetCore.Mvc;
namespace FitForge.Controllers
{
    public class ProgramsController(ProgramBL bl, UserDL userDL) : BaseController(userDL)
    {
        public IActionResult Index(){
            if(Uid==null)return RedirectToAction("Login","Account");
            ViewData["Page"]="Programs";
            return View(bl.BuildVM(Uid.Value));
        }

        [HttpPost,IgnoreAntiforgeryToken]
        public IActionResult Create([FromBody]CreateProgramReq req){
            if(Uid==null)return Json(new{success=false,msg="Not logged in"});
            var(ok,msg,pid)=bl.CreateProgram(Uid.Value,req);
            return Json(new{success=ok,msg,programId=pid});
        }

        [HttpPost,IgnoreAntiforgeryToken]
        public IActionResult Delete([FromBody]int programId){
            if(Uid==null)return Json(new{success=false});
            var(ok,msg)=bl.DeleteProgram(Uid.Value,programId);
            return Json(new{success=ok,msg});
        }

        public IActionResult GetFull(int programId){
            if(Uid==null)return Json(new{success=false});
            return Json(new{success=true,program=bl.GetFull(programId)});
        }

        [HttpPost,IgnoreAntiforgeryToken]
        public IActionResult SaveSchedule([FromBody]SaveScheduleReq req){
            if(Uid==null)return Json(new{success=false});
            var(ok,msg)=bl.SaveSchedule(Uid.Value,req);
            return Json(new{success=ok,msg});
        }
    }
}
