using FitForge.BL;
using FitForge.DL;
using FitForge.Models;
using Microsoft.AspNetCore.Mvc;

namespace FitForge.Controllers
{
    public class WorkoutsController(WorkoutBL wBL, UserDL uDL, ExerciseDL eDL) : BaseController(uDL)
    {
        public IActionResult Index()
        {
            if (Uid == null) return RedirectToAction("Login", "Account");
            ViewData["Page"] = "Workouts";
            return View(wBL.BuildWorkoutsVM(Uid.Value));
        }

        public IActionResult Active(int sessionId, int dayId)
        {
            if (Uid == null) return RedirectToAction("Login", "Account");
            ViewData["Page"] = "Workouts";
            return View(wBL.BuildActiveWorkoutVM(Uid.Value, sessionId, dayId));
        }

        public IActionResult SessionDetail(int sessionId)
        {
            if (Uid == null) return Json(new { success = false });
            var dt = DB.Select(
                @"SELECT ws.session_id, ws.started_at, pd.name AS day_name, p.name AS prog_name,
                    wset.exercise_id, e.name AS ex_name, wset.set_number, wset.actual_reps,
                    wset.target_reps, wset.weight_kg
                  FROM workout_sessions ws
                  JOIN program_days pd ON ws.day_id = pd.day_id
                  JOIN programs p ON pd.program_id = p.program_id
                  LEFT JOIN workout_sets wset ON ws.session_id = wset.session_id
                  LEFT JOIN exercises e ON wset.exercise_id = e.exercise_id
                  WHERE ws.session_id = @sid AND ws.user_id = @uid",
                DB.P("@sid", sessionId), DB.P("@uid", Uid.Value));

            if (dt.Rows.Count == 0) return Json(new { success = false });
            var first = dt.Rows[0];
            var detail = new
            {
                sessionDate = Convert.ToDateTime(first["started_at"]),
                dayLabel    = first["day_name"].ToString(),
                goalType    = first["prog_name"].ToString(),
                exercises   = dt.Rows()
                    .Where(r => r["exercise_id"] != System.DBNull.Value)
                    .GroupBy(r => Convert.ToInt32(r["exercise_id"]))
                    .Select(g =>
                    {
                        var fr = g.First();
                        int target = fr["target_reps"] != System.DBNull.Value ? Convert.ToInt32(fr["target_reps"]) : 0;
                        return new
                        {
                            exerciseId   = Convert.ToInt32(fr["exercise_id"]),
                            exerciseName = fr["ex_name"].ToString(),
                            expectedReps = target,
                            loggedSets   = g.Select(r => new
                            {
                                setNumber  = Convert.ToInt32(r["set_number"]),
                                actualReps = Convert.ToInt32(r["actual_reps"]),
                                weightKg   = r["weight_kg"] != System.DBNull.Value ? (double?)Convert.ToDouble(r["weight_kg"]) : null
                            }).ToList()
                        };
                    }).ToList()
            };
            return Json(new { success = true, detail });
        }

        public IActionResult CalendarMonth(int year, int month)
        {
            if (Uid == null) return Json(new { success = false });
            var calDL = HttpContext.RequestServices.GetRequiredService<CalendarDL>();
            var days  = calDL.GetMonth(Uid.Value, year, month);
            return Json(new { success = true, days = days.Select(d => new {
                date        = d.Date.ToString("yyyy-MM-dd"),
                hasWorkout  = d.HasWorkout,
                isCompleted = d.IsCompleted,
                isToday     = d.IsToday,
                isFuture    = d.IsFuture,
                sessionId   = d.SessionId,
                setCount    = d.SetCount
            })});
        }
    }
}
