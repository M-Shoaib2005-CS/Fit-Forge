using FitForge.BL; using FitForge.DL; using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
namespace FitForge.Controllers
{
    public class AccountController(UserBL bl, UserDL dl) : BaseController(dl)
    {
        public IActionResult Login(){
            if(Uid.HasValue) return RedirectToAction("Index","Dashboard");
            return View();
        }

        [HttpPost,ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password){
            var(ok,msg)=await bl.Login(username,password);
            if(!ok){
                TempData["LoginError"]=msg;
                TempData["LoginUsername"]=username; // preserve username on error
                return View();
            }
            return RedirectToAction("Index","Dashboard");
        }

        [HttpPost,ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string name, string username, string email, string password, string confirm,
            string dob, string gender, double weight, double height, string fitnessLevel){
            var(ok,msg)=await bl.Register(name,username,email,password,confirm,dob,gender,height,weight,fitnessLevel);
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

        public async Task<IActionResult> Logout(){
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}
