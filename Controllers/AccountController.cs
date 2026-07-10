using FitForge.BL; using FitForge.DL; using Microsoft.AspNetCore.Mvc;
namespace FitForge.Controllers
{
    public class AccountController(UserBL bl, UserDL dl) : BaseController(dl)
    {
        public IActionResult Login(){
            if(Uid.HasValue) return RedirectToAction("Index","Dashboard");
            return View();
        }

        [HttpPost,ValidateAntiForgeryToken]
        public IActionResult Login(string username, string password){
            var(ok,msg)=bl.Login(username,password);
            if(!ok){
                TempData["LoginError"]=msg;
                TempData["LoginUsername"]=username; // preserve username on error
                return View();
            }
            return RedirectToAction("Index","Dashboard");
        }

        [HttpPost,ValidateAntiForgeryToken]
        public IActionResult Register(string name, string username, string email, string password, string confirm,
            string dob, string gender, double weight, double height, string fitnessLevel){
            var(ok,msg)=bl.Register(name,username,email,password,confirm,dob,gender,height,weight,fitnessLevel);
            if(!ok){
                TempData["RegError"]=msg;
                TempData["ShowReg"]=1;
                // Preserve form data
                TempData["RegName"]=name; TempData["RegUsername"]=username;
                TempData["RegEmail"]=email; TempData["RegGender"]=gender;
                TempData["RegWeight"]=weight; TempData["RegHeight"]=height;
                TempData["RegLevel"]=fitnessLevel; TempData["RegDob"]=dob;
                return RedirectToAction("Login");
            }
            return RedirectToAction("Index","Dashboard");
        }

        public IActionResult Logout(){ HttpContext.Session.Clear(); return RedirectToAction("Login"); }
    }
}
