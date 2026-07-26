using FitForge.BL; using FitForge.DL; using FitForge.Models; using FitForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
namespace FitForge.Controllers
{
    public class NotificationsController(NotificationDL dl, UserDL userDL, IPushNotificationService push,
        NotificationBL bl, IConfiguration config)
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
            if(!string.IsNullOrWhiteSpace(req.Timezone)) userDL.UpdateTimezone(Uid.Value,req.Timezone);
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

        // ── Cron-triggered tick (replaces the old in-process BackgroundService) ──
        // Free-tier hosts spin the app down after ~15 min idle, silently killing any
        // in-process timer along with it — so instead an external scheduler (e.g.
        // cron-job.org) hits this URL every few minutes. Each hit both wakes the app
        // (if asleep) and runs the actual check, working identically regardless of
        // hosting tier. Protected by a shared-secret query param since it's otherwise
        // a fully public, unauthenticated endpoint — set Notifications:CronSecret and
        // put the same value in the scheduler's URL as ?key=...
        [HttpGet]
        public async Task<IActionResult> Tick(string key){
            var expected=config["Notifications:CronSecret"];
            if(string.IsNullOrEmpty(expected)||key!=expected) return Unauthorized();
            if(!push.IsConfigured) return Json(new{ran=false,reason="Push not configured"});

            int reminders=await bl.SendWorkoutRemindersAsync();
            int injuries=await bl.SendInjuryCheckinsAsync();
            int encouragement=await bl.SendEncouragementAsync();
            return Json(new{ran=true,reminders,injuries,encouragement,at=DateTime.UtcNow});
        }
    }
}
