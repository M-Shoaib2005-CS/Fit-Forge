using Microsoft.Extensions.Logging;
using System.Linq;
using FitForge.Models; using System.Data;
namespace FitForge.DL
{
    public class ExerciseDL
    {
        public List<ExerciseModel> GetAll()=>
            DB.Select(@"SELECT e.*,mg.name AS mg_name FROM exercises e
                JOIN muscle_groups mg ON e.muscle_group_id=mg.group_id
                WHERE e.is_active=1 ORDER BY e.exercise_type,mg.name,e.name")
              .Rows().Select(MapExercise).ToList();

        public List<MuscleGroupModel> GetMuscleGroups()=>
            DB.Select("SELECT * FROM muscle_groups ORDER BY name").Rows()
              .Select(r=>new MuscleGroupModel{GroupId=Convert.ToInt32(r["group_id"]),
                  Name=r["name"].ToString()!,Icon=r["icon"]?.ToString()??"💪"}).ToList();

        public Dictionary<int,string> GetNameMap()=>
            DB.Select("SELECT exercise_id, name FROM exercises WHERE is_active=1").Rows()
              .ToDictionary(r=>Convert.ToInt32(r["exercise_id"]), r=>r["name"].ToString()!);

        private static ExerciseModel MapExercise(DataRow r)=>new(){
            ExerciseId=Convert.ToInt32(r["exercise_id"]),Name=r["name"].ToString()!,
            MuscleGroupId=Convert.ToInt32(r["muscle_group_id"]),
            MuscleGroup=r.Table.Columns.Contains("mg_name")?r["mg_name"].ToString()!:"",
            ExerciseType=r["exercise_type"].ToString()!,TrackingMode=r["tracking_mode"].ToString()!,
            Difficulty=r["difficulty"].ToString()!,Description=r["description"]?.ToString()??""};
    }

    public class WorkoutDL(ILogger<WorkoutDL> log)
    {
        public int StartSession(int uid, int dayId){
            DB.NonQuery("INSERT INTO workout_sessions(user_id,day_id) VALUES(@u,@d)",
                DB.P("@u",uid),DB.P("@d",dayId));
            int sid=Convert.ToInt32(DB.Scalar("SELECT LAST_INSERT_ID()"));
            log.LogInformation("Session started uid:{U} day:{D} sid:{S}",uid,dayId,sid);
            return sid;
        }

        public void FinishSession(int sid, int uid, string? notes){
            DB.NonQuery(@"UPDATE workout_sessions SET finished_at=NOW(),
                duration_secs=TIMESTAMPDIFF(SECOND,started_at,NOW()),notes=@n
                WHERE session_id=@sid AND user_id=@u",
                DB.P("@n",notes),DB.P("@sid",sid),DB.P("@u",uid));
        }

        public void LogSet(int sid, int eid, int pdeId, int setNum, int targetReps, int actualReps, double? weightKg, int? rpe, bool skipped){
            DB.NonQuery(@"INSERT INTO workout_sets(session_id,exercise_id,pde_id,set_number,target_reps,actual_reps,weight_kg,rpe,was_skipped)
                VALUES(@sid,@eid,@pde,@sn,@tr,@ar,@w,@rpe,@sk)",
                DB.P("@sid",sid),DB.P("@eid",eid),DB.P("@pde",pdeId),DB.P("@sn",setNum),
                DB.P("@tr",targetReps),DB.P("@ar",actualReps),DB.P("@w",weightKg),
                DB.P("@rpe",rpe),DB.P("@sk",skipped?1:0));
        }

        public List<SessionModel> GetHistory(int uid, int limit=50)=>
            DB.Select(@"SELECT ws.session_id,ws.started_at,ws.finished_at,ws.duration_secs,ws.notes,
                pd.name AS day_name,p.name AS prog_name,
                COUNT(DISTINCT wset.set_id) AS total_sets,
                COUNT(DISTINCT wset.exercise_id) AS total_exs
                FROM workout_sessions ws
                JOIN program_days pd ON ws.day_id=pd.day_id
                JOIN programs p ON pd.program_id=p.program_id
                LEFT JOIN workout_sets wset ON ws.session_id=wset.session_id
                WHERE ws.user_id=@u
                GROUP BY ws.session_id,ws.started_at,ws.finished_at,ws.duration_secs,ws.notes,pd.name,p.name
                ORDER BY ws.started_at DESC LIMIT @lim",DB.P("@u",uid),DB.P("@lim",limit))
              .Rows().Select(r=>new SessionModel{
                  SessionId=Convert.ToInt32(r["session_id"]),DayName=r["day_name"].ToString()!,
                  ProgramName=r["prog_name"].ToString()!,StartedAt=Convert.ToDateTime(r["started_at"]),
                  FinishedAt=r["finished_at"]!=DBNull.Value?Convert.ToDateTime(r["finished_at"]):null,
                  DurationSecs=r["duration_secs"]!=DBNull.Value?Convert.ToInt32(r["duration_secs"]):null,
                  Notes=r["notes"]?.ToString()??"",TotalSets=Convert.ToInt32(r["total_sets"]),
                  TotalExercises=Convert.ToInt32(r["total_exs"])}).ToList();

        public int GetTotalWorkouts(int uid)=>
            Convert.ToInt32(DB.Scalar("SELECT COUNT(*) FROM workout_sessions WHERE user_id=@u AND finished_at IS NOT NULL",DB.P("@u",uid)));

        public double GetAvgSessionMins(int uid){
            var v=DB.Scalar("SELECT AVG(duration_secs)/60 FROM workout_sessions WHERE user_id=@u AND duration_secs IS NOT NULL",DB.P("@u",uid));
            return v==null||v==DBNull.Value?0:Math.Round(Convert.ToDouble(v),1);
        }

        public SessionModel? GetOpenSession(int uid){
            var dt=DB.Select(@"SELECT ws.*,pd.name AS day_name,p.name AS prog_name
                FROM workout_sessions ws JOIN program_days pd ON ws.day_id=pd.day_id
                JOIN programs p ON pd.program_id=p.program_id
                WHERE ws.user_id=@u AND ws.finished_at IS NULL ORDER BY ws.started_at DESC LIMIT 1",DB.P("@u",uid));
            if(dt.Rows.Count==0)return null;
            var r=dt.Rows[0];
            return new SessionModel{SessionId=Convert.ToInt32(r["session_id"]),
                DayId=Convert.ToInt32(r["day_id"]),DayName=r["day_name"].ToString()!,
                ProgramName=r["prog_name"].ToString()!,StartedAt=Convert.ToDateTime(r["started_at"])};
        }
    }

    // ── Adaptive Engine DL ────────────────────────────────────
    public class AdaptiveDL(ILogger<AdaptiveDL> log)
    {
        // Gets current adaptive target for a pde, or initialises from base target
        public ExerciseTargetModel GetTarget(int uid, int pdeId){
            var dt=DB.Select(@"SELECT uet.*,pde.target_sets,pde.rest_seconds,pde.target_reps AS base_reps,
                e.name AS ex_name,e.tracking_mode
                FROM user_exercise_targets uet
                JOIN program_day_exercises pde ON uet.pde_id=pde.pde_id
                JOIN exercises e ON pde.exercise_id=e.exercise_id
                WHERE uet.user_id=@u AND uet.pde_id=@p",DB.P("@u",uid),DB.P("@p",pdeId));
            if(dt.Rows.Count>0){
                var r=dt.Rows[0];
                return new ExerciseTargetModel{PdeId=pdeId,
                    ExerciseId=Convert.ToInt32(r.Table.Columns.Contains("exercise_id")?r["exercise_id"]:0),
                    ExerciseName=r["ex_name"].ToString()!,TrackingMode=r["tracking_mode"].ToString()!,
                    CurrentTargetReps=Convert.ToInt32(r["current_target_reps"]),
                    CurrentTargetWeight=r["current_target_weight"]!=DBNull.Value?Convert.ToDouble(r["current_target_weight"]):null,
                    ConsecutiveHits=Convert.ToInt32(r["consecutive_hits"]),
                    TargetSets=Convert.ToInt32(r["target_sets"]),RestSeconds=Convert.ToInt32(r["rest_seconds"])};
            }
            // No target yet — initialise from program base
            var init=DB.Select(@"SELECT pde.*,e.name AS ex_name,e.tracking_mode
                FROM program_day_exercises pde JOIN exercises e ON pde.exercise_id=e.exercise_id
                WHERE pde.pde_id=@p",DB.P("@p",pdeId));
            if(init.Rows.Count==0)return new ExerciseTargetModel{PdeId=pdeId};
            var ir=init.Rows[0];
            int baseReps=Convert.ToInt32(ir["target_reps"]);
            double? baseW=ir["target_weight_kg"]!=DBNull.Value?Convert.ToDouble(ir["target_weight_kg"]):null;
            DB.NonQuery(@"INSERT INTO user_exercise_targets(user_id,pde_id,current_target_reps,current_target_weight)
                VALUES(@u,@p,@r,@w) ON DUPLICATE KEY UPDATE user_id=user_id",
                DB.P("@u",uid),DB.P("@p",pdeId),DB.P("@r",baseReps),DB.P("@w",baseW));
            return new ExerciseTargetModel{PdeId=pdeId,ExerciseId=Convert.ToInt32(ir["exercise_id"]),
                ExerciseName=ir["ex_name"].ToString()!,TrackingMode=ir["tracking_mode"].ToString()!,
                CurrentTargetReps=baseReps,CurrentTargetWeight=baseW,
                TargetSets=Convert.ToInt32(ir["target_sets"]),RestSeconds=Convert.ToInt32(ir["rest_seconds"])};
        }

        public List<ExerciseTargetModel> GetTargetsForDay(int uid, int dayId){
            var pdes=DB.Select("SELECT pde_id FROM program_day_exercises WHERE day_id=@d",DB.P("@d",dayId));
            return pdes.Rows().Select(r=>GetTarget(uid,Convert.ToInt32(r["pde_id"]))).ToList();
        }

        // Core adaptive logic — called after every session
        // Conservative: miss→+1, hit→+1, exceed→+ceil(excess*0.4)
        // Moderate:     miss→same, hit→+2, exceed→+ceil(excess*0.6)
        // Aggressive:   miss→+1, hit→+2, exceed→+ceil(excess*0.8)
        // Adaptive:     dynamic based on consecutive hits + miss ratio
        public void UpdateTargetsAfterSession(int uid, int sessionId, int dayId, string progressionStyle, List<LoggedSet> sets){
            var grouped=sets.Where(s=>!s.Skipped).GroupBy(s=>s.PdeId);
            foreach(var g in grouped){
                var t=GetTarget(uid,g.Key);
                if(t.CurrentTargetReps==0)continue;
                // average actual reps across non-skipped sets
                double avgActual=g.Average(s=>s.ActualReps);
                double avgTarget=t.CurrentTargetReps;
                double ratio=avgActual/avgTarget; // <1=missed, =1=hit, >1=exceeded
                int newTarget=CalcNewTarget(t,ratio,progressionStyle);
                if(newTarget==t.CurrentTargetReps){
                    DB.NonQuery("UPDATE user_exercise_targets SET consecutive_hits=CASE WHEN @r>=1 THEN consecutive_hits+1 ELSE 0 END WHERE user_id=@u AND pde_id=@p",
                        DB.P("@r",ratio),DB.P("@u",uid),DB.P("@p",g.Key));
                    continue;
                }
                string reason=ratio<0.85?"missed_target":ratio<=1.05?"hit_target":"exceeded_target";
                DB.NonQuery(@"INSERT INTO target_history(user_id,pde_id,session_id,old_target,new_target,reason)
                    VALUES(@u,@p,@sid,@ot,@nt,@r)",
                    DB.P("@u",uid),DB.P("@p",g.Key),DB.P("@sid",sessionId),
                    DB.P("@ot",t.CurrentTargetReps),DB.P("@nt",newTarget),DB.P("@r",reason));
                DB.NonQuery(@"UPDATE user_exercise_targets SET current_target_reps=@nt,
                    consecutive_hits=CASE WHEN @ratio>=1 THEN consecutive_hits+1 ELSE 0 END
                    WHERE user_id=@u AND pde_id=@p",
                    DB.P("@nt",newTarget),DB.P("@ratio",ratio),DB.P("@u",uid),DB.P("@p",g.Key));
                log.LogInformation("Target updated uid:{U} pde:{P} {Old}->{New} ({R})",uid,g.Key,t.CurrentTargetReps,newTarget,reason);
                // Weight progression for gym exercises
                if(t.TrackWeight&&g.Any(s=>s.WeightKg.HasValue)&&ratio>=1.0){
                    double avgW=g.Where(s=>s.WeightKg.HasValue).Average(s=>s.WeightKg!.Value);
                    double weightIncrement=progressionStyle=="Aggressive"?2.5:progressionStyle=="Conservative"?1.25:2.5;
                    if(t.ConsecutiveHits>=2)
                        DB.NonQuery("UPDATE user_exercise_targets SET current_target_weight=@w WHERE user_id=@u AND pde_id=@p",
                            DB.P("@w",Math.Round(avgW+weightIncrement,2)),DB.P("@u",uid),DB.P("@p",g.Key));
                }
            }
        }

        private static int CalcNewTarget(ExerciseTargetModel t, double ratio, string style){
            int cur=t.CurrentTargetReps;
            int excess=(int)Math.Ceiling((ratio-1.0)*cur); // how many reps above target on avg
            int deficit=(int)Math.Ceiling((1.0-ratio)*cur);
            return style switch{
                "Conservative"=>ratio<0.85?Math.Max(1,cur-1):ratio>1.05?cur+Math.Max(1,(int)Math.Ceiling(excess*0.4)):cur+1,
                "Moderate"=>ratio<0.75?cur:ratio>1.1?cur+Math.Max(1,(int)Math.Ceiling(excess*0.6)):cur+(ratio>=1?2:0),
                "Aggressive"=>ratio<0.85?cur+1:ratio>1.05?cur+Math.Max(2,(int)Math.Ceiling(excess*0.8)):cur+2,
                // Adaptive: conservative when missing, accelerate when consistently hitting
                _=>ratio<0.85?Math.Max(1,cur-1):ratio>1.15&&t.ConsecutiveHits>=2?cur+Math.Max(1,(int)Math.Ceiling(excess*0.6)):
                   ratio>=1&&t.ConsecutiveHits>=3?cur+2:ratio>=1?cur+1:cur
            };
        }
    }

    // ── Personal Records DL ───────────────────────────────────
    public class PersonalRecordDL
    {
        public List<PersonalRecordModel> GetForUser(int uid, int limit=20)=>
            DB.Select(@"SELECT pr.*,e.name AS ex_name FROM personal_records pr
                JOIN exercises e ON pr.exercise_id=e.exercise_id
                WHERE pr.user_id=@u ORDER BY pr.achieved_at DESC LIMIT @lim",
                DB.P("@u",uid),DB.P("@lim",limit))
              .Rows().Select(MapPR).ToList();

        public List<PersonalRecordModel> GetForExercise(int uid, int eid)=>
            DB.Select(@"SELECT pr.*,e.name AS ex_name FROM personal_records pr
                JOIN exercises e ON pr.exercise_id=e.exercise_id
                WHERE pr.user_id=@u AND pr.exercise_id=@e ORDER BY pr.achieved_at DESC",
                DB.P("@u",uid),DB.P("@e",eid))
              .Rows().Select(MapPR).ToList();

        // Checks if a logged set beats any existing PR and saves if so
        public bool CheckAndSave(int uid, int eid, int sessionId, int reps, double? weightKg){
            bool newPr=false;
            // max_reps PR
            var curReps=DB.Scalar("SELECT MAX(value) FROM personal_records WHERE user_id=@u AND exercise_id=@e AND record_type='max_reps'",DB.P("@u",uid),DB.P("@e",eid));
            if(curReps==null||curReps==DBNull.Value||reps>Convert.ToDouble(curReps)){
                DB.NonQuery(@"INSERT INTO personal_records(user_id,exercise_id,record_type,value,weight_kg,achieved_at,session_id)
                    VALUES(@u,@e,'max_reps',@v,@w,CURDATE(),@s) ON DUPLICATE KEY UPDATE value=@v,achieved_at=CURDATE()",
                    DB.P("@u",uid),DB.P("@e",eid),DB.P("@v",reps),DB.P("@w",weightKg),DB.P("@s",sessionId));
                newPr=true;
            }
            // max_weight PR
            if(weightKg.HasValue){
                var curW=DB.Scalar("SELECT MAX(value) FROM personal_records WHERE user_id=@u AND exercise_id=@e AND record_type='max_weight'",DB.P("@u",uid),DB.P("@e",eid));
                if(curW==null||curW==DBNull.Value||weightKg.Value>Convert.ToDouble(curW)){
                    DB.NonQuery(@"INSERT INTO personal_records(user_id,exercise_id,record_type,value,weight_kg,achieved_at,session_id)
                        VALUES(@u,@e,'max_weight',@v,@w,CURDATE(),@s) ON DUPLICATE KEY UPDATE value=@v,achieved_at=CURDATE()",
                        DB.P("@u",uid),DB.P("@e",eid),DB.P("@v",weightKg.Value),DB.P("@w",weightKg),DB.P("@s",sessionId));
                    newPr=true;
                }
                // max_volume (weight × reps)
                double vol=weightKg.Value*reps;
                var curVol=DB.Scalar("SELECT MAX(value) FROM personal_records WHERE user_id=@u AND exercise_id=@e AND record_type='max_volume'",DB.P("@u",uid),DB.P("@e",eid));
                if(curVol==null||curVol==DBNull.Value||vol>Convert.ToDouble(curVol)){
                    DB.NonQuery(@"INSERT INTO personal_records(user_id,exercise_id,record_type,value,weight_kg,achieved_at,session_id)
                        VALUES(@u,@e,'max_volume',@v,@w,CURDATE(),@s) ON DUPLICATE KEY UPDATE value=@v,achieved_at=CURDATE()",
                        DB.P("@u",uid),DB.P("@e",eid),DB.P("@v",vol),DB.P("@w",weightKg),DB.P("@s",sessionId));
                }
            }
            return newPr;
        }

        private static PersonalRecordModel MapPR(DataRow r)=>new(){
            PrId=Convert.ToInt32(r["pr_id"]),ExerciseId=Convert.ToInt32(r["exercise_id"]),
            ExerciseName=r["ex_name"].ToString()!,RecordType=r["record_type"].ToString()!,
            Value=Convert.ToDouble(r["value"]),
            WeightKg=r["weight_kg"]!=DBNull.Value?Convert.ToDouble(r["weight_kg"]):null,
            AchievedAt=Convert.ToDateTime(r["achieved_at"])};
    }

    // ── Streak DL ─────────────────────────────────────────────
    public class StreakDL
    {
        public void UpdateStreak(int uid){
            var dt=DB.Select("SELECT * FROM user_streaks WHERE user_id=@u",DB.P("@u",uid));
            if(dt.Rows.Count==0){DB.NonQuery("INSERT INTO user_streaks(user_id,current_streak,longest_streak,last_workout_date) VALUES(@u,1,1,CURDATE())",DB.P("@u",uid));return;}
            var r=dt.Rows[0];
            var last=r["last_workout_date"]==DBNull.Value?(DateTime?)null:Convert.ToDateTime(r["last_workout_date"]);
            int cur=Convert.ToInt32(r["current_streak"]);
            int longest=Convert.ToInt32(r["longest_streak"]);
            if(last.HasValue&&last.Value.Date==DateTime.Today)return; // already logged today
            bool consecutive=last.HasValue&&(DateTime.Today-last.Value.Date).TotalDays<=1;
            cur=consecutive?cur+1:1;
            longest=Math.Max(cur,longest);
            DB.NonQuery("UPDATE user_streaks SET current_streak=@c,longest_streak=@l,last_workout_date=CURDATE() WHERE user_id=@u",
                DB.P("@c",cur),DB.P("@l",longest),DB.P("@u",uid));
        }
    }
}
