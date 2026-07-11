using FitForge.DL; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
namespace FitForge.Controllers
{
    public abstract class BaseController(UserDL userDL) : Controller
    {
        protected int? Uid {
            get {
                var fromSession = HttpContext.Session.GetInt32("uid");
                if (fromSession.HasValue) return fromSession;

                // Session was wiped (e.g. server restart) but the persistent 60-day
                // auth cookie is still valid — recover the uid from it and restore
                // the session so the rest of the app keeps working as before.
                var claim = HttpContext.User?.FindFirst(ClaimTypes.NameIdentifier);
                if (claim != null && int.TryParse(claim.Value, out int uidFromCookie)) {
                    HttpContext.Session.SetInt32("uid", uidFromCookie);
                    var nameClaim = HttpContext.User?.FindFirst(ClaimTypes.Name);
                    if (nameClaim != null) HttpContext.Session.SetString("uname", nameClaim.Value);
                    return uidFromCookie;
                }
                return null;
            }
        }
        public override void OnActionExecuting(ActionExecutingContext ctx){
            if(Uid.HasValue){var u=userDL.GetById(Uid.Value);if(u!=null)ViewData["UserTheme"]=u.Theme;}
            base.OnActionExecuting(ctx);
        }
    }
}
