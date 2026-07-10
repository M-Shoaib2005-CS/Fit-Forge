using Microsoft.Extensions.Logging;
using System.Data;
using System.Linq;
// ============================================================
// DL/InjuryDL.cs
// Smart injury system:
//   - User picks body part (Shoulder Joint, Hamstring, etc.)
//   - User picks category (Muscle Pull | Joint Pain)
//   - System looks up injury_exercise_flags to find affected exercises
//   - Returns flagged exercises + their alternatives
// ============================================================
using FitForge.Models;
namespace FitForge.DL
{
    public class InjuryDL(ILogger<InjuryDL> log)
    {
        public List<BodyPartModel> GetBodyParts()=>
            DB.Select("SELECT * FROM body_parts ORDER BY region,part_type,name").Rows()
              .Select(r=>new BodyPartModel{PartId=Convert.ToInt32(r["part_id"]),Name=r["name"].ToString()!,
                  PartType=r["part_type"].ToString()!,Region=r["region"].ToString()!}).ToList();

        public List<InjuryCategoryModel> GetCategories()=>
            DB.Select("SELECT * FROM injury_categories").Rows()
              .Select(r=>new InjuryCategoryModel{CategoryId=Convert.ToInt32(r["category_id"]),
                  Name=r["name"].ToString()!,Description=r["description"]?.ToString()??"",
                  SeverityTip=r["severity_tip"]?.ToString()??""}).ToList();

        public List<UserInjuryModel> GetActiveForUser(int uid)=>
            DB.Select(@"SELECT ui.*,bp.name AS part_name,bp.part_type,ic.name AS cat_name
                FROM user_injuries ui JOIN body_parts bp ON ui.part_id=bp.part_id
                JOIN injury_categories ic ON ui.category_id=ic.category_id
                WHERE ui.user_id=@u AND ui.status='Active' ORDER BY ui.occurred_date DESC",DB.P("@u",uid))
              .Rows().Select(r=>new UserInjuryModel{
                  UiId=Convert.ToInt32(r["ui_id"]),PartId=Convert.ToInt32(r["part_id"]),
                  CategoryId=Convert.ToInt32(r["category_id"]),BodyPart=r["part_name"].ToString()!,
                  PartType=r["part_type"].ToString()!,Category=r["cat_name"].ToString()!,
                  OccurredDate=Convert.ToDateTime(r["occurred_date"]),Status=r["status"].ToString()!,
                  Notes=r["notes"]?.ToString()??""}).ToList();

        // Returns exercise IDs flagged by user's active injuries + the best alternative
        public List<ExerciseFlagModel> GetFlaggedExercises(int uid){
            var injuries=DB.Select(@"SELECT DISTINCT ui.part_id,ui.category_id FROM user_injuries ui
                WHERE ui.user_id=@u AND ui.status='Active'",DB.P("@u",uid));
            if(injuries.Rows.Count==0)return new();
            // Build IN clause for each (part_id, category_id) combo
            var pairs=injuries.Rows().Select(r=>
                $"(part_id={r["part_id"]} AND category_id={r["category_id"]})").ToList();
            var where=string.Join(" OR ",pairs);
            return DB.Select($@"SELECT ief.exercise_id,e.name AS ex_name,ief.reason,
                ea.alternative_id,alt.name AS alt_name
                FROM injury_exercise_flags ief
                JOIN exercises e ON ief.exercise_id=e.exercise_id
                LEFT JOIN exercise_alternatives ea ON ea.exercise_id=ief.exercise_id
                LEFT JOIN exercises alt ON ea.alternative_id=alt.exercise_id
                WHERE {where}")
              .Rows().GroupBy(r=>Convert.ToInt32(r["exercise_id"]))
              .Select(g=>{var r=g.First();return new ExerciseFlagModel{
                  ExerciseId=Convert.ToInt32(r["exercise_id"]),ExerciseName=r["ex_name"].ToString()!,
                  Reason=r["reason"]?.ToString()??"",
                  AlternativeId=r["alternative_id"]!=DBNull.Value?Convert.ToInt32(r["alternative_id"]):null,
                  AlternativeName=r["alt_name"]?.ToString()??""};}).ToList();
        }

        public bool LogInjury(int uid, int partId, int categoryId, string notes){
            log.LogInformation("Injury logged uid:{U} part:{P} cat:{C}",uid,partId,categoryId);
            return DB.NonQuery(@"INSERT INTO user_injuries(user_id,part_id,category_id,notes)
                VALUES(@u,@p,@c,@n) ON DUPLICATE KEY UPDATE status='Active',occurred_date=CURDATE(),notes=@n",
                DB.P("@u",uid),DB.P("@p",partId),DB.P("@c",categoryId),DB.P("@n",notes))>0;
        }

        public bool Resolve(int uid, int uiId)=>
            DB.NonQuery("UPDATE user_injuries SET status='Resolved' WHERE ui_id=@id AND user_id=@u",
                DB.P("@id",uiId),DB.P("@u",uid))>0;
    }
}
