using FitForge.DL; using FitForge.Models;
namespace FitForge.BL
{
    public class ProfileBL(UserDL userDL, InjuryDL injuryDL, PersonalRecordDL prDL,
        MeasurementDL measDL, AchievementDL achDL, AchievementBL achBL)
    {
        public ProfileVM BuildVM(int uid) => new ProfileVM {
            User              = userDL.GetById(uid) ?? new UserModel(),
            Injuries          = injuryDL.GetActiveForUser(uid),
            BodyParts         = injuryDL.GetBodyParts(),
            Categories        = injuryDL.GetCategories(),
            WeightHistory     = userDL.GetWeightHistory(uid),
            TopPRs            = prDL.GetForUser(uid, limit:10),
            Measurements      = measDL.GetHistory(uid, 10),
            LatestMeasurement = measDL.GetLatest(uid),
            Achievements      = achDL.GetAll(uid),
            Badges            = achDL.GetBadges(uid)
        };

        public (bool ok, string msg) UpdateStats(int uid, double h, double w) {
            if(h<100||h>250) return(false,"Height must be 100–250 cm");
            if(w<30||w>300)  return(false,"Weight must be 30–300 kg");
            userDL.UpdateProfile(uid,(decimal)h,(decimal)w);
            return(true,"Stats updated!");
        }

        public void LogWeight(int uid, decimal w, string notes) {
            userDL.LogWeight(uid, w, notes);
            int count = userDL.GetWeightLogCount(uid);
            achBL.CheckHealthAchievements(uid, count);
        }

        public (bool ok, string msg) LogMeasurement(int uid, LogMeasurementReq req) {
            bool ok = measDL.Log(uid, req);
            if(ok) achBL.CheckHealthAchievements(uid, userDL.GetWeightLogCount(uid));
            return ok ? (true,"Measurements logged!") : (false,"Failed to save measurements.");
        }

        public (bool ok, string msg) LogInjury(int uid, LogInjuryReq req) {
            if(req.PartId==0||req.CategoryId==0) return(false,"Select body part and injury type");
            injuryDL.LogInjury(uid,req.PartId,req.CategoryId,req.Notes);
            return(true,"Injury logged. Affected exercises will be flagged in your workout.");
        }

        public bool ResolveInjury(int uid, int uiId) => injuryDL.Resolve(uid, uiId);

        public InjuryReportVM GetInjuryReportVM(int uid) {
            var parts = injuryDL.GetBodyParts();
            return new InjuryReportVM {
                JointParts  = parts.Where(p=>p.PartType=="Joint").ToList(),
                MuscleParts = parts.Where(p=>p.PartType=="Muscle").ToList(),
                Categories  = injuryDL.GetCategories()
            };
        }
    }
}
