using FitForge.Models;
using Microsoft.Extensions.Logging;
namespace FitForge.DL
{
    public class MeasurementDL(ILogger<MeasurementDL> log)
    {
        public bool Log(int uid, LogMeasurementReq req)
        {
            try
            {
                DB.NonQuery(@"INSERT INTO body_measurements
                    (user_id,chest_cm,waist_cm,hips_cm,left_arm_cm,right_arm_cm,
                     left_thigh_cm,right_thigh_cm,neck_cm,shoulders_cm,body_fat_pct,notes)
                    VALUES(@u,@ch,@wa,@hi,@la,@ra,@lt,@rt,@ne,@sh,@bf,@no)",
                    DB.P("@u",  uid),
                    DB.P("@ch", req.ChestCm),     DB.P("@wa", req.WaistCm),
                    DB.P("@hi", req.HipsCm),       DB.P("@la", req.LeftArmCm),
                    DB.P("@ra", req.RightArmCm),   DB.P("@lt", req.LeftThighCm),
                    DB.P("@rt", req.RightThighCm), DB.P("@ne", req.NeckCm),
                    DB.P("@sh", req.ShouldersCm),  DB.P("@bf", req.BodyFatPct),
                    DB.P("@no", req.Notes));
                return true;
            }
            catch (Exception ex) { log.LogError(ex, "Log measurement uid={U}", uid); return false; }
        }

        public List<BodyMeasurementModel> GetHistory(int uid, int limit = 20)
        {
            try
            {
                return DB.Select(@"SELECT * FROM body_measurements WHERE user_id=@u
                    ORDER BY recorded_at DESC LIMIT @l", DB.P("@u", uid), DB.P("@l", limit))
                    .Rows().Select(Map).ToList();
            }
            catch (Exception ex) { log.LogError(ex, "GetHistory uid={U}", uid); return new(); }
        }

        public BodyMeasurementModel? GetLatest(int uid)
        {
            try
            {
                var dt = DB.Select(@"SELECT * FROM body_measurements WHERE user_id=@u
                    ORDER BY recorded_at DESC LIMIT 1", DB.P("@u", uid));
                return dt.Rows.Count > 0 ? Map(dt.Rows[0]) : null;
            }
            catch { return null; }
        }

        public int GetTotalCount(int uid)
        {
            try { return Convert.ToInt32(DB.Scalar("SELECT COUNT(*) FROM body_measurements WHERE user_id=@u", DB.P("@u", uid))); }
            catch { return 0; }
        }

        private static BodyMeasurementModel Map(System.Data.DataRow r) => new()
        {
            MeasurementId = Convert.ToInt32(r["measurement_id"]),
            ChestCm       = r["chest_cm"]      == DBNull.Value ? null : (double?)Convert.ToDouble(r["chest_cm"]),
            WaistCm       = r["waist_cm"]      == DBNull.Value ? null : (double?)Convert.ToDouble(r["waist_cm"]),
            HipsCm        = r["hips_cm"]       == DBNull.Value ? null : (double?)Convert.ToDouble(r["hips_cm"]),
            LeftArmCm     = r["left_arm_cm"]   == DBNull.Value ? null : (double?)Convert.ToDouble(r["left_arm_cm"]),
            RightArmCm    = r["right_arm_cm"]  == DBNull.Value ? null : (double?)Convert.ToDouble(r["right_arm_cm"]),
            LeftThighCm   = r["left_thigh_cm"] == DBNull.Value ? null : (double?)Convert.ToDouble(r["left_thigh_cm"]),
            RightThighCm  = r["right_thigh_cm"]== DBNull.Value ? null : (double?)Convert.ToDouble(r["right_thigh_cm"]),
            NeckCm        = r["neck_cm"]        == DBNull.Value ? null : (double?)Convert.ToDouble(r["neck_cm"]),
            ShouldersCm   = r["shoulders_cm"]  == DBNull.Value ? null : (double?)Convert.ToDouble(r["shoulders_cm"]),
            BodyFatPct    = r["body_fat_pct"]  == DBNull.Value ? null : (double?)Convert.ToDouble(r["body_fat_pct"]),
            Notes         = r["notes"]          == DBNull.Value ? "" : r["notes"].ToString()!,
            RecordedAt    = Convert.ToDateTime(r["recorded_at"])
        };
    }
}
