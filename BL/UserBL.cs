using Microsoft.Extensions.Logging;
using FitForge.DL; using FitForge.Models; using Microsoft.AspNetCore.Http;
namespace FitForge.BL
{
    public class UserBL(UserDL dl, IHttpContextAccessor http, ILogger<UserBL> log)
    {
        public string? ValidateRegistration(string name,string username,string email,string pw,string confirm,double h,double w){
            if(string.IsNullOrWhiteSpace(name))      return "Name is required";
            if(string.IsNullOrWhiteSpace(username))   return "Username is required";
            if(string.IsNullOrWhiteSpace(email)||!email.Contains('@')) return "Valid email required";
            if(pw!=confirm)                           return "Passwords do not match";
            if(pw.Length<8)                           return "Password must be at least 8 characters";
            if(!pw.Any(char.IsUpper))                 return "Password needs an uppercase letter";
            if(!pw.Any(char.IsDigit))                 return "Password needs a number";
            if(h<100||h>250)                          return "Height must be 100–250 cm";
            if(w<30||w>300)                           return "Weight must be 30–300 kg";
            return null;
        }
        public (bool ok, string msg) Register(string name,string username,string email,string pw,string confirm,string dob,string gender,double h,double w,string level){
            var err=ValidateRegistration(name,username,email,pw,confirm,h,w);
            if(err!=null)return(false,err);
            try{
                string hash=BCrypt.Net.BCrypt.HashPassword(pw);
                int uid=dl.Register(name.Trim(),username.Trim(),email.Trim(),hash);
                var d=DateTime.TryParse(dob,out var dt)?dt:DateTime.Now.AddYears(-20);
                dl.CreateProfile(uid,d,gender,(decimal)h,(decimal)w,level);
                SetSession(uid,name.Trim());
                return(true,"");
            }catch(Exception ex){
                string msg=ex.Message.Contains("Duplicate")?"Username or email already taken":"Registration failed";
                log.LogError(ex,"Register failed for {U}",username);
                return(false,msg);
            }
        }
        public (bool ok, string msg) Login(string username, string password){
            var(user,msg)=dl.Login(username?.Trim()??"",password??"");
            if(user==null)return(false,msg);
            SetSession(user.UserId,user.Name);
            return(true,"");
        }
        public void UpdateTheme(int uid,string theme)=>dl.UpdateTheme(uid,theme);
        public (bool ok,string msg) UpdatePassword(int uid,string cur,string newPw,string confirm){
            if(newPw!=confirm)return(false,"Passwords do not match");
            if(newPw.Length<8)return(false,"Minimum 8 characters");
            if(!newPw.Any(char.IsUpper))return(false,"Needs an uppercase letter");
            if(!newPw.Any(char.IsDigit))return(false,"Needs a number");
            string r=dl.UpdatePassword(uid,cur,newPw);
            return(r=="Password updated",r);
        }
        public void DeleteAccount(int uid){dl.DeleteAccount(uid);http.HttpContext?.Session.Clear();}
        private void SetSession(int uid,string name){http.HttpContext?.Session.SetInt32("uid",uid);http.HttpContext?.Session.SetString("uname",name);}
    }
}
