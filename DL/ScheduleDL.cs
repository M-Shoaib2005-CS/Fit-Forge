using Microsoft.Extensions.Logging;
using System.Linq;
using FitForge.Models; using System.Data;
namespace FitForge.DL
{
    public class ScheduleDL(ILogger<ScheduleDL> log)
    {
        public UserScheduleModel? GetActiveForUser(int uid){
            var dt=DB.Select("SELECT * FROM user_schedules WHERE user_id=@u AND is_active=1 LIMIT 1",DB.P("@u",uid));
            if(dt.Rows.Count==0)return null;
            var r=dt.Rows[0];
            var sched=new UserScheduleModel{ScheduleId=Convert.ToInt32(r["schedule_id"]),
                UserId=uid,Name=r["name"].ToString()!,IsActive=true};
            sched.Slots=GetSlots(sched.ScheduleId);
            return sched;
        }

        public List<ScheduleSlotModel> GetSlots(int scheduleId)=>
            DB.Select(@"SELECT ss.*,pd.name AS day_name,pd.day_type,p.name AS prog_name
                FROM schedule_slots ss
                LEFT JOIN program_days pd ON ss.day_id=pd.day_id
                LEFT JOIN programs p ON pd.program_id=p.program_id
                WHERE ss.schedule_id=@sid ORDER BY ss.week_day",DB.P("@sid",scheduleId))
              .Rows().Select(r=>new ScheduleSlotModel{
                  SlotId=Convert.ToInt32(r["slot_id"]),ScheduleId=scheduleId,
                  WeekDay=Convert.ToInt32(r["week_day"]),
                  DayId=r["day_id"]!=DBNull.Value?Convert.ToInt32(r["day_id"]):null,
                  DayName=r["day_name"]?.ToString()??"Rest",
                  ProgramName=r["prog_name"]?.ToString()??"",
                  DayType=r["day_type"]?.ToString()??"Rest"}).ToList();

        public int EnsureSchedule(int uid){
            var ex=DB.Scalar("SELECT schedule_id FROM user_schedules WHERE user_id=@u AND is_active=1 LIMIT 1",DB.P("@u",uid));
            if(ex!=null&&ex!=DBNull.Value)return Convert.ToInt32(ex);
            return (int)DB.InsertGetId("INSERT INTO user_schedules(user_id,name) VALUES(@u,'My Schedule')",DB.P("@u",uid));
        }

        public void SaveSlots(int scheduleId, List<SlotReq> slots){
            DB.NonQuery("DELETE FROM schedule_slots WHERE schedule_id=@sid",DB.P("@sid",scheduleId));
            foreach(var s in slots)
                DB.NonQuery("INSERT INTO schedule_slots(schedule_id,week_day,day_id) VALUES(@sid,@wd,@did)",
                    DB.P("@sid",scheduleId),DB.P("@wd",s.WeekDay),DB.P("@did",s.DayId));
            log.LogInformation("Saved schedule {SId} with {N} slots",scheduleId,slots.Count);
        }

        // Gets today's slot (0=Mon…6=Sun based on current day)
        public ScheduleSlotModel? GetTodaySlot(int uid){
            int wd=((int)DateTime.Today.DayOfWeek+6)%7; // convert Sun=0 to Mon=0
            var sched=GetActiveForUser(uid);
            return sched?.Slots.FirstOrDefault(s=>s.WeekDay==wd);
        }
    }
}
