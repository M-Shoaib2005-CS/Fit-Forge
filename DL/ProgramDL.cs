using Microsoft.Extensions.Logging;
using System.Linq;
using FitForge.Models; using System.Data;
namespace FitForge.DL
{
    public class ProgramDL(ILogger<ProgramDL> log)
    {
        // ── Programs ──────────────────────────────────────────
        public List<ProgramModel> GetPresetsShallow()=>GetByUser(1,shallow:true);
        public List<ProgramModel> GetByUserShallow(int uid)=>GetByUser(uid,shallow:true);

        private List<ProgramModel> GetByUser(int uid, bool shallow){
            var dt=DB.Select("SELECT * FROM programs WHERE user_id=@u ORDER BY created_at DESC",DB.P("@u",uid));
            var list=dt.Rows().Select(r=>new ProgramModel{
                ProgramId=Convert.ToInt32(r["program_id"]),UserId=Convert.ToInt32(r["user_id"]),
                Name=r["name"].ToString()!,Description=r["description"]?.ToString()??"",
                GoalType=r["goal_type"].ToString()!,ProgressionStyle=r["progression_style"].ToString()!,
                CreatedAt=Convert.ToDateTime(r["created_at"])}).ToList();
            if(!shallow)foreach(var p in list)p.Days=GetDaysForProgram(p.ProgramId,withExercises:true);
            return list;
        }

        public ProgramModel? GetFull(int pid){
            var dt=DB.Select("SELECT * FROM programs WHERE program_id=@id",DB.P("@id",pid));
            if(dt.Rows.Count==0)return null;
            var r=dt.Rows[0];
            return new ProgramModel{ProgramId=pid,UserId=Convert.ToInt32(r["user_id"]),
                Name=r["name"].ToString()!,Description=r["description"]?.ToString()??"",
                GoalType=r["goal_type"].ToString()!,ProgressionStyle=r["progression_style"].ToString()!,
                CreatedAt=Convert.ToDateTime(r["created_at"]),
                Days=GetDaysForProgram(pid,withExercises:true)};
        }

        public List<ProgramDayModel> GetDaysForProgram(int pid, bool withExercises=false){
            var days=DB.Select("SELECT * FROM program_days WHERE program_id=@p ORDER BY day_order",DB.P("@p",pid))
                .Rows().Select(r=>new ProgramDayModel{
                    DayId=Convert.ToInt32(r["day_id"]),ProgramId=pid,DayOrder=Convert.ToInt32(r["day_order"]),
                    Name=r["name"].ToString()!,DayType=r["day_type"].ToString()!,Notes=r["notes"]?.ToString()??""}).ToList();
            if(withExercises)foreach(var d in days)d.Exercises=GetExercisesForDay(d.DayId);
            return days;
        }

        public List<ProgramDayExerciseModel> GetExercisesForDay(int dayId)=>
            DB.Select(@"SELECT pde.*,e.name AS ex_name,mg.name AS mg_name,e.tracking_mode,e.exercise_type
                FROM program_day_exercises pde JOIN exercises e ON pde.exercise_id=e.exercise_id
                JOIN muscle_groups mg ON e.muscle_group_id=mg.group_id
                WHERE pde.day_id=@d ORDER BY pde.exercise_order",DB.P("@d",dayId))
              .Rows().Select(r=>new ProgramDayExerciseModel{
                  PdeId=Convert.ToInt32(r["pde_id"]),DayId=dayId,ExerciseId=Convert.ToInt32(r["exercise_id"]),
                  ExerciseName=r["ex_name"].ToString()!,MuscleGroup=r["mg_name"].ToString()!,
                  TrackingMode=r["tracking_mode"].ToString()!,ExerciseType=r["exercise_type"].ToString()!,
                  ExerciseOrder=Convert.ToInt32(r["exercise_order"]),TargetSets=Convert.ToInt32(r["target_sets"]),
                  TargetReps=Convert.ToInt32(r["target_reps"]),
                  TargetWeightKg=r["target_weight_kg"]!=DBNull.Value?Convert.ToDouble(r["target_weight_kg"]):null,
                  RestSeconds=Convert.ToInt32(r["rest_seconds"]),Notes=r["notes"]?.ToString()??""}).ToList();

        public int CreateProgram(int uid, string name, string desc, string goalType, string progStyle){
            DB.NonQuery("INSERT INTO programs(user_id,name,description,goal_type,progression_style) VALUES(@u,@n,@d,@g,@p)",
                DB.P("@u",uid),DB.P("@n",name),DB.P("@d",desc),DB.P("@g",goalType),DB.P("@p",progStyle));
            return Convert.ToInt32(DB.Scalar("SELECT LAST_INSERT_ID()"));
        }

        public int AddDay(int programId, int order, string name, string dayType){
            DB.NonQuery("INSERT INTO program_days(program_id,day_order,name,day_type) VALUES(@p,@o,@n,@t)",
                DB.P("@p",programId),DB.P("@o",order),DB.P("@n",name),DB.P("@t",dayType));
            return Convert.ToInt32(DB.Scalar("SELECT LAST_INSERT_ID()"));
        }

        public void AddExercise(int dayId, int exerciseId, int order, int sets, int reps, double? weightKg, int rest){
            DB.NonQuery(@"INSERT INTO program_day_exercises(day_id,exercise_id,exercise_order,target_sets,target_reps,target_weight_kg,rest_seconds)
                VALUES(@d,@e,@o,@s,@r,@w,@rs)",
                DB.P("@d",dayId),DB.P("@e",exerciseId),DB.P("@o",order),DB.P("@s",sets),
                DB.P("@r",reps),DB.P("@w",weightKg),DB.P("@rs",rest));
        }

        public string GetProgressionStyleForDay(int dayId){
            var style=DB.Scalar(@"SELECT p.progression_style FROM programs p
                JOIN program_days pd ON pd.program_id=p.program_id WHERE pd.day_id=@d",DB.P("@d",dayId));
            return style!=null&&style!=DBNull.Value?style.ToString()!:"Adaptive";
        }

        public bool DeleteProgram(int programId, int uid){
            log.LogInformation("Deleting program {PId} for user {UId}",programId,uid);
            // Verify ownership first (never delete presets owned by user 1)
            var dt=DB.Select("SELECT program_id FROM programs WHERE program_id=@p AND user_id=@u AND user_id!=1",
                DB.P("@p",programId),DB.P("@u",uid));
            if(dt.Rows.Count==0)return false;
            // Get all day IDs for this program
            var dayDt=DB.Select("SELECT day_id FROM program_days WHERE program_id=@p",DB.P("@p",programId));
            foreach(System.Data.DataRow row in dayDt.Rows){
                var did=Convert.ToInt32(row["day_id"]);
                // Remove any schedule slots referencing these days
                DB.NonQuery("UPDATE schedule_slots SET day_id=NULL WHERE day_id=@d",DB.P("@d",did));
                // Remove any logged workout sessions for this day. workout_sets cascades
                // automatically, and personal_records.session_id is set NULL by the DB,
                // so PR history is preserved even though the session row is gone.
                DB.NonQuery("DELETE FROM workout_sessions WHERE day_id=@d",DB.P("@d",did));
                // Remove exercises under this day
                DB.NonQuery("DELETE FROM program_day_exercises WHERE day_id=@d",DB.P("@d",did));
            }
            // Remove the days
            DB.NonQuery("DELETE FROM program_days WHERE program_id=@p",DB.P("@p",programId));
            // Now delete the program itself
            return DB.NonQuery("DELETE FROM programs WHERE program_id=@p AND user_id=@u",
                DB.P("@p",programId),DB.P("@u",uid))>0;
        }
    }
}
