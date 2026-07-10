using FitForge.DL; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Filters;
namespace FitForge.Controllers
{
    public abstract class BaseController(UserDL userDL) : Controller
    {
        protected int? Uid=>HttpContext.Session.GetInt32("uid");
        public override void OnActionExecuting(ActionExecutingContext ctx){
            if(Uid.HasValue){var u=userDL.GetById(Uid.Value);if(u!=null)ViewData["UserTheme"]=u.Theme;}
            base.OnActionExecuting(ctx);
        }
    }
}
