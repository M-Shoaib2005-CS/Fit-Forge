using FitForge.DL; using FitForge.Models; using FitForge.Services;
using Microsoft.AspNetCore.Mvc;
namespace FitForge.Controllers
{
    public class NotificationsController(NotificationDL dl, UserDL userDL, IPushNotificationService push)
        : BaseController(userDL)
    {
        // The frontend needs this to call PushManager.subscribe() with the matching key —
        // it must be the public half of whatever key pair Vapid:PrivateKey belongs to.
        [HttpGet]
        public IActionResult VapidPublicKey(){
            if(!push.IsConfigured) return Json(new{configured=false});
            return Json(new{configured=true,publicKey=push.PublicKey});
        }

        [HttpPost,IgnoreAntiforgeryToken]
        public IActionResult Subscribe([FromBody]PushSubscriptionReq req){
            if(Uid==null)return Json(new{success=false,msg="Not logged in"});
            if(string.IsNullOrWhiteSpace(req.Endpoint)||string.IsNullOrWhiteSpace(req.P256dh)||string.IsNullOrWhiteSpace(req.Auth))
                return Json(new{success=false,msg="Incomplete subscription"});
            dl.SaveSubscription(Uid.Value,req);
            return Json(new{success=true});
        }

        [HttpPost,IgnoreAntiforgeryToken]
        public IActionResult Unsubscribe([FromBody]string endpoint){
            if(Uid==null)return Json(new{success=false});
            dl.RemoveSubscription(endpoint);
            return Json(new{success=true});
        }

        [HttpPost,IgnoreAntiforgeryToken]
        public IActionResult SavePref([FromBody]NotificationPrefReq req){
            if(Uid==null)return Json(new{success=false,msg="Not logged in"});
            userDL.UpdateNotificationPref(Uid.Value,req.Pref);
            return Json(new{success=true});
        }

        [HttpPost,IgnoreAntiforgeryToken]
        public IActionResult SaveReminderTime([FromBody]ReminderTimeReq req){
            if(Uid==null)return Json(new{success=false,msg="Not logged in"});
            if(!System.TimeSpan.TryParse(req.Time,out _))return Json(new{success=false,msg="Invalid time"});
            userDL.UpdateReminderTime(Uid.Value,req.Time);
            return Json(new{success=true});
        }
    }
}
