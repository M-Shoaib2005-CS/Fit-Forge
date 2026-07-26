using Microsoft.Extensions.Logging;
using System.Linq;
using FitForge.Models; using System.Data;
namespace FitForge.DL
{
    public class PushSubscriptionModel
    {
        public int SubId { get; set; }
        public int UserId { get; set; }
        public string Endpoint { get; set; } = "";
        public string P256dh { get; set; } = "";
        public string Auth { get; set; } = "";
    }

    // A candidate for the daily reminder: which user, which day (if any — null day_id
    // means an unassigned rest day), whether it's a rest day, and the day's name (for
    // workout days) so the notification can name it specifically.
    public class ReminderCandidate
    {
        public int UserId { get; set; }
        public int? DayId { get; set; }
        public string DayName { get; set; } = "";
        public bool IsRest { get; set; }
        public string UserName { get; set; } = "";
    }

    public class NotificationDL(ILogger<NotificationDL> log)
    {
        // ── Push subscriptions ──────────────────────────────────
        public void SaveSubscription(int uid, PushSubscriptionReq req){
            // Same browser/device can re-subscribe (e.g. after clearing storage) with a new
            // endpoint, or re-send the same endpoint — upsert on the endpoint's uniqueness.
            DB.NonQuery(@"INSERT INTO push_subscriptions(user_id,endpoint,p256dh,auth)
                VALUES(@u,@e,@p,@a)
                ON DUPLICATE KEY UPDATE user_id=@u,p256dh=@p,auth=@a",
                DB.P("@u",uid),DB.P("@e",req.Endpoint),DB.P("@p",req.P256dh),DB.P("@a",req.Auth));
        }

        public void RemoveSubscription(string endpoint){
            DB.NonQuery("DELETE FROM push_subscriptions WHERE endpoint=@e",DB.P("@e",endpoint));
        }

        // Called when a push send comes back 404/410 — the browser's push service has
        // permanently invalidated that endpoint, so keeping it around just wastes sends.
        public void RemoveSubscriptionById(int subId){
            DB.NonQuery("DELETE FROM push_subscriptions WHERE sub_id=@id",DB.P("@id",subId));
        }

        public List<PushSubscriptionModel> GetSubscriptionsForUser(int uid)=>
            DB.Select("SELECT * FROM push_subscriptions WHERE user_id=@u",DB.P("@u",uid))
              .Rows().Select(MapSub).ToList();

        private static PushSubscriptionModel MapSub(DataRow r)=>new(){
            SubId=Convert.ToInt32(r["sub_id"]),UserId=Convert.ToInt32(r["user_id"]),
            Endpoint=r["endpoint"].ToString()!,P256dh=r["p256dh"].ToString()!,Auth=r["auth"].ToString()!};

        // ── Send log (dedupe) ────────────────────────────────────
        public bool WasSentToday(int uid, string type)=>
            Convert.ToInt32(DB.Scalar(@"SELECT COUNT(*) FROM notification_log
                WHERE user_id=@u AND type=@t AND DATE(sent_at)=CURDATE()",
                DB.P("@u",uid),DB.P("@t",type)))>0;

        public bool WasSentWithinDays(int uid, string type, int days)=>
            Convert.ToInt32(DB.Scalar(@"SELECT COUNT(*) FROM notification_log
                WHERE user_id=@u AND type=@t AND sent_at >= DATE_SUB(NOW(), INTERVAL @d DAY)",
                DB.P("@u",uid),DB.P("@t",type),DB.P("@d",days)))>0;

        public void MarkSent(int uid, string type){
            DB.NonQuery("INSERT INTO notification_log(user_id,type) VALUES(@u,@t)",
                DB.P("@u",uid),DB.P("@t",type));
        }

        // ── Candidate queries for the background service ────────

        // Users whose reminder_time has passed for today's server-clock (each user picks
        // their own time in Settings — this compares against theirs, not one global hour),
        // haven't already been reminded today, and either:
        //   - have a Workout day scheduled today that isn't finished yet, or
        //   - have nothing scheduled / a Rest day today (still get a gentle rest-day note,
        //     rather than silence, per how you wanted it).
        // "Passed" rather than "equals" is deliberate: the background service ticks every
        // 30 min and isn't guaranteed to land exactly on a user's chosen minute, so this
        // fires on the first tick at-or-after their time, then the same-day dedupe (via
        // notification_log) stops it firing again later that day.
        public List<ReminderCandidate> GetWorkoutReminderCandidates(int weekDay)=>
            DB.Select(@"SELECT DISTINCT u.user_id,ss.day_id,pd.name AS day_name,
                    (ss.day_id IS NULL OR pd.day_type='Rest') AS is_rest,u.name AS user_name
                FROM users u
                JOIN user_schedules us ON us.user_id=u.user_id AND us.is_active=1
                JOIN schedule_slots ss ON ss.schedule_id=us.schedule_id AND ss.week_day=@wd
                LEFT JOIN program_days pd ON pd.day_id=ss.day_id
                JOIN push_subscriptions psub ON psub.user_id=u.user_id
                WHERE u.notification_pref IN ('All','WorkoutOnly')
                  AND TIME(NOW()) >= u.reminder_time
                  AND NOT EXISTS (
                    SELECT 1 FROM notification_log nl
                    WHERE nl.user_id=u.user_id AND nl.type='WorkoutReminder' AND DATE(nl.sent_at)=CURDATE()
                  )
                  AND (
                    ss.day_id IS NULL OR pd.day_type='Rest'
                    OR NOT EXISTS (
                        SELECT 1 FROM workout_sessions ws
                        WHERE ws.user_id=u.user_id AND ws.day_id=pd.day_id
                          AND ws.finished_at IS NOT NULL AND DATE(ws.finished_at)=CURDATE()
                    )
                  )",DB.P("@wd",weekDay))
              .Rows().Select(r=>new ReminderCandidate{
                  UserId=Convert.ToInt32(r["user_id"]),
                  DayId=r["day_id"]!=DBNull.Value?Convert.ToInt32(r["day_id"]):null,
                  DayName=r["day_name"]?.ToString()??"",
                  IsRest=Convert.ToBoolean(r["is_rest"]),
                  UserName=r["user_name"].ToString()!}).ToList();

        // Users with an Active injury, opted into 'All' notifications, with a live
        // subscription, who haven't had an injury check-in sent in the last 3 days.
        public List<(int UserId, string UserName, string BodyPart)> GetInjuryCheckinCandidates()=>
            DB.Select(@"SELECT DISTINCT u.user_id,u.name AS user_name,bp.name AS part_name
                FROM users u
                JOIN user_injuries ui ON ui.user_id=u.user_id AND ui.status='Active'
                JOIN body_parts bp ON bp.part_id=ui.part_id
                JOIN push_subscriptions psub ON psub.user_id=u.user_id
                WHERE u.notification_pref='All'
                  AND NOT EXISTS (
                    SELECT 1 FROM notification_log nl
                    WHERE nl.user_id=u.user_id AND nl.type='InjuryCheckin'
                      AND nl.sent_at >= DATE_SUB(NOW(), INTERVAL 3 DAY)
                  )",new MySqlConnector.MySqlParameter[0])
              .Rows().Select(r=>(Convert.ToInt32(r["user_id"]),r["user_name"].ToString()!,r["part_name"].ToString()!)).ToList();

        // Users opted into 'All' notifications, with a live subscription, who haven't
        // logged a finished session in 2+ days and haven't had an encouragement nudge in
        // the last 2 days either (so this can't stack daily and get naggy).
        public List<(int UserId, string UserName)> GetEncouragementCandidates()=>
            DB.Select(@"SELECT u.user_id,u.name AS user_name
                FROM users u
                JOIN push_subscriptions psub ON psub.user_id=u.user_id
                WHERE u.notification_pref='All'
                  AND NOT EXISTS (
                    SELECT 1 FROM workout_sessions ws
                    WHERE ws.user_id=u.user_id AND ws.finished_at IS NOT NULL
                      AND ws.finished_at >= DATE_SUB(NOW(), INTERVAL 2 DAY)
                  )
                  AND NOT EXISTS (
                    SELECT 1 FROM notification_log nl
                    WHERE nl.user_id=u.user_id AND nl.type='CoachEncouragement'
                      AND nl.sent_at >= DATE_SUB(NOW(), INTERVAL 2 DAY)
                  )
                GROUP BY u.user_id,u.name",new MySqlConnector.MySqlParameter[0])
              .Rows().Select(r=>(Convert.ToInt32(r["user_id"]),r["user_name"].ToString()!)).ToList();
    }
}
