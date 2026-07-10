using FitForge.Models;
using Microsoft.Extensions.Logging;
namespace FitForge.DL
{
    public class CalendarDL(ILogger<CalendarDL> log)
    {
        public List<CalendarDayModel> GetMonth(int uid, int year, int month)
        {
            try
            {
                var first = new DateTime(year, month, 1);
                var last  = first.AddMonths(1).AddDays(-1);
                var dt = DB.Select(@"
                    SELECT DATE(ws.started_at) AS day,
                           ws.session_id,
                           ws.finished_at,
                           COUNT(wset.set_id) AS set_count
                    FROM workout_sessions ws
                    LEFT JOIN workout_sets wset ON ws.session_id = wset.session_id
                    WHERE ws.user_id=@u AND DATE(ws.started_at) BETWEEN @f AND @l
                    GROUP BY DATE(ws.started_at), ws.session_id, ws.finished_at
                    ORDER BY day",
                    DB.P("@u", uid), DB.P("@f", first.ToString("yyyy-MM-dd")), DB.P("@l", last.ToString("yyyy-MM-dd")));

                var sessionByDay = dt.Rows()
                    .GroupBy(r => Convert.ToDateTime(r["day"]).Date)
                    .ToDictionary(g => g.Key, g =>
                    {
                        var best = g.OrderByDescending(r => r["finished_at"] != DBNull.Value)
                                    .ThenByDescending(r => Convert.ToInt32(r["set_count"]))
                                    .First();
                        return new { sid = Convert.ToInt32(best["session_id"]), finished = best["finished_at"] != DBNull.Value, sets = Convert.ToInt32(best["set_count"]) };
                    });

                return Enumerable.Range(0, last.Day).Select(i =>
                {
                    var date = first.AddDays(i);
                    sessionByDay.TryGetValue(date, out var s);
                    return new CalendarDayModel
                    {
                        Date        = date,
                        HasWorkout  = s != null,
                        IsCompleted = s?.finished ?? false,
                        SessionId   = s?.sid,
                        SetCount    = s?.sets ?? 0
                    };
                }).ToList();
            }
            catch (Exception ex) { log.LogError(ex, "GetMonth uid={U}", uid); return new(); }
        }
    }
}
