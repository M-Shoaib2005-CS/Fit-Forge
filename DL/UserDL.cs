using Microsoft.Extensions.Logging;
using System.Linq;
using FitForge.Models; using System.Data;
namespace FitForge.DL
{
    public class UserDL(ILogger<UserDL> log)
    {
        private const int MaxAttempts = 5;
        private const int LockoutMinutes = 15;

        public int Register(string name, string username, string email, string hash){
            log.LogInformation("Registering {Username}",username);
            return Convert.ToInt32(DB.Scalar(
                "INSERT INTO users(name,username,email,password) VALUES(@n,@u,@e,@p); SELECT LAST_INSERT_ID();",
                DB.P("@n",name),DB.P("@u",username.ToLower()),DB.P("@e",email.ToLower()),DB.P("@p",hash)));
        }
        public void CreateProfile(int uid, DateTime dob, string gender, decimal h, decimal w, string level){
            int age=DateTime.Now.Year-dob.Year; if(DateTime.Now<dob.AddYears(age))age--;
            DB.NonQuery("INSERT INTO user_profile(user_id,dob,gender,height_cm,weight_kg,fitness_level) VALUES(@u,@d,@g,@h,@w,@l)",
                DB.P("@u",uid),DB.P("@d",dob.ToString("yyyy-MM-dd")),DB.P("@g",gender),
                DB.P("@h",h),DB.P("@w",w),DB.P("@l",level));
            DB.NonQuery("INSERT INTO user_streaks(user_id) VALUES(@u) ON DUPLICATE KEY UPDATE user_id=user_id",DB.P("@u",uid));
        }

        public (UserModel? user, string msg) Login(string username, string password){
            try {
                var dt = DB.Select(
                    "SELECT u.*, p.dob, p.gender, p.height_cm, p.weight_kg, p.fitness_level, s.current_streak, s.longest_streak " +
                    "FROM users u LEFT JOIN user_profile p ON u.user_id=p.user_id LEFT JOIN user_streaks s ON u.user_id=s.user_id " +
                    "WHERE u.username=@u", DB.P("@u", username.ToLower()));
                if(dt.Rows.Count==0){log.LogWarning("Login: account not found {U}",username);return(null,"Account not found");}
                var row=dt.Rows[0];

                // Check lockout (only if column exists after migration)
                if(row.Table.Columns.Contains("lockout_until") && row["lockout_until"] != DBNull.Value){
                    var lockout = Convert.ToDateTime(row["lockout_until"]);
                    if(DateTime.UtcNow < lockout){
                        int mins = (int)Math.Ceiling((lockout - DateTime.UtcNow).TotalMinutes);
                        return (null, $"Account locked. Try again in {mins} minute{(mins==1?"":"s")}.");
                    }
                }

                if(!BCrypt.Net.BCrypt.Verify(password, row["password"].ToString()!)){
                    // Increment attempt counter
                    if(row.Table.Columns.Contains("login_attempts")){
                        int attempts = row["login_attempts"] == DBNull.Value ? 0 : Convert.ToInt32(row["login_attempts"]);
                        attempts++;
                        if(attempts >= MaxAttempts){
                            DB.NonQuery("UPDATE users SET login_attempts=@a, lockout_until=@l WHERE user_id=@id",
                                DB.P("@a", attempts),
                                DB.P("@l", DateTime.UtcNow.AddMinutes(LockoutMinutes).ToString("yyyy-MM-dd HH:mm:ss")),
                                DB.P("@id", row["user_id"]));
                            log.LogWarning("Account locked after {N} attempts: {U}", attempts, username);
                            return (null, $"Too many failed attempts. Account locked for {LockoutMinutes} minutes.");
                        }
                        DB.NonQuery("UPDATE users SET login_attempts=@a WHERE user_id=@id",
                            DB.P("@a", attempts), DB.P("@id", row["user_id"]));
                        int remaining = MaxAttempts - attempts;
                        return (null, $"Incorrect password. {remaining} attempt{(remaining==1?"":"s")} remaining.");
                    }
                    return (null,"Incorrect password");
                }

                // Success — reset attempts
                if(row.Table.Columns.Contains("login_attempts"))
                    DB.NonQuery("UPDATE users SET login_attempts=0, lockout_until=NULL WHERE user_id=@id", DB.P("@id", row["user_id"]));

                log.LogInformation("Login ok: {U}", username);
                return (MapUser(row), "ok");
            } catch(Exception ex){ log.LogError(ex,"Login error {U}",username); return(null,"Login failed"); }
        }

        public UserModel? GetById(int uid){
            var dt=DB.Select(@"SELECT u.*,p.dob,p.gender,p.height_cm,p.weight_kg,p.fitness_level,
                s.current_streak,s.longest_streak FROM users u
                LEFT JOIN user_profile p ON u.user_id=p.user_id
                LEFT JOIN user_streaks s ON u.user_id=s.user_id
                WHERE u.user_id=@id",DB.P("@id",uid));
            return dt.Rows.Count>0?MapUser(dt.Rows[0]):null;
        }
        public void UpdateProfile(int uid, decimal h, decimal w){
            int ex=Convert.ToInt32(DB.Scalar("SELECT COUNT(*) FROM user_profile WHERE user_id=@id",DB.P("@id",uid)));
            if(ex>0) DB.NonQuery("UPDATE user_profile SET height_cm=@h,weight_kg=@w WHERE user_id=@id",DB.P("@h",h),DB.P("@w",w),DB.P("@id",uid));
            else DB.NonQuery("INSERT INTO user_profile(user_id,height_cm,weight_kg) VALUES(@id,@h,@w)",DB.P("@id",uid),DB.P("@h",h),DB.P("@w",w));
        }
        public string UpdatePassword(int uid, string cur, string newPw){
            var h=DB.Scalar("SELECT password FROM users WHERE user_id=@id",DB.P("@id",uid));
            if(h==null||!BCrypt.Net.BCrypt.Verify(cur,h.ToString()!))return "Current password incorrect";
            DB.NonQuery("UPDATE users SET password=@p WHERE user_id=@id",DB.P("@p",BCrypt.Net.BCrypt.HashPassword(newPw)),DB.P("@id",uid));
            return "Password updated";
        }
        public void UpdateTheme(int uid, string theme){
            if(theme!="dark"&&theme!="light")theme="dark";
            DB.NonQuery("UPDATE users SET theme=@t WHERE user_id=@id",DB.P("@t",theme),DB.P("@id",uid));
        }
        public void DeleteAccount(int uid){ log.LogWarning("Deleting account {Uid}",uid); DB.NonQuery("DELETE FROM users WHERE user_id=@id",DB.P("@id",uid)); }
        public void LogWeight(int uid, decimal w, string notes){
            DB.NonQuery("INSERT INTO weight_history(user_id,weight_kg,notes) VALUES(@u,@w,@n)",DB.P("@u",uid),DB.P("@w",w),DB.P("@n",notes));
            DB.NonQuery("UPDATE user_profile SET weight_kg=@w WHERE user_id=@u",DB.P("@w",w),DB.P("@u",uid));
        }
        public List<WeightEntryModel> GetWeightHistory(int uid){
            return DB.Select("SELECT * FROM weight_history WHERE user_id=@u ORDER BY recorded_at DESC LIMIT 30",DB.P("@u",uid))
                .Rows().Select(r=>new WeightEntryModel{
                    WeightId=Convert.ToInt32(r["weight_id"]),WeightKg=Convert.ToDouble(r["weight_kg"]),
                    RecordedAt=Convert.ToDateTime(r["recorded_at"]),Notes=r["notes"]?.ToString()??""}).ToList();
        }
        public int GetWeightLogCount(int uid){
            return Convert.ToInt32(DB.Scalar("SELECT COUNT(*) FROM weight_history WHERE user_id=@u", DB.P("@u",uid)));
        }
        private static UserModel MapUser(DataRow r){
            var u=new UserModel{UserId=Convert.ToInt32(r["user_id"]),Name=r["name"].ToString()!,
                Username=r["username"].ToString()!,Email=r["email"]?.ToString()??"",
                Theme=r.Table.Columns.Contains("theme")&&r["theme"]!=DBNull.Value?r["theme"].ToString()!:"dark"};
            if(r.Table.Columns.Contains("fitness_level")&&r["fitness_level"]!=DBNull.Value) u.FitnessLevel=r["fitness_level"].ToString()!;
            if(r.Table.Columns.Contains("height_cm")&&r["height_cm"]!=DBNull.Value)  u.Height=Convert.ToDouble(r["height_cm"]);
            if(r.Table.Columns.Contains("weight_kg")&&r["weight_kg"]!=DBNull.Value)  u.Weight=Convert.ToDouble(r["weight_kg"]);
            if(r.Table.Columns.Contains("gender")&&r["gender"]!=DBNull.Value)        u.Gender=r["gender"].ToString()!;
            if(r.Table.Columns.Contains("dob")&&r["dob"]!=DBNull.Value){var d=Convert.ToDateTime(r["dob"]);int a=DateTime.Now.Year-d.Year;if(DateTime.Now<d.AddYears(a))a--;u.Age=a;}
            if(r.Table.Columns.Contains("current_streak")&&r["current_streak"]!=DBNull.Value) u.CurrentStreak=Convert.ToInt32(r["current_streak"]);
            if(r.Table.Columns.Contains("longest_streak")&&r["longest_streak"]!=DBNull.Value) u.LongestStreak=Convert.ToInt32(r["longest_streak"]);
            if(r.Table.Columns.Contains("email_verified")&&r["email_verified"]!=DBNull.Value) u.EmailVerified=Convert.ToInt32(r["email_verified"])==1;
            if(r.Table.Columns.Contains("water_goal_ml")&&r["water_goal_ml"]!=DBNull.Value)   u.WaterGoalMl=Convert.ToInt32(r["water_goal_ml"]);
            return u;
        }
    }
}
