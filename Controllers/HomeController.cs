using Microsoft.Extensions.Logging;
using FitForge.DL; using Microsoft.AspNetCore.Diagnostics; using Microsoft.AspNetCore.Mvc;
namespace FitForge.Controllers
{
    public class HomeController(ILogger<HomeController> log, UserDL dl) : BaseController(dl)
    {
        [Route("/Home/Error")]
        public IActionResult Error(){
            var f=HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if(f?.Error!=null)log.LogError(f.Error,"Unhandled exception at {P}",f.Path);
            return View();
        }
    }
}
