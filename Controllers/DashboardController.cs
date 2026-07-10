using FitForge.BL;
using FitForge.DL;
using FitForge.Models;
using Microsoft.AspNetCore.Mvc;

namespace FitForge.Controllers
{
    public class DashboardController(WorkoutBL wBL, UserDL uDL, WaterDL waterDL, ProgramDL progDL) : BaseController(uDL)
    {
        public IActionResult Index()
        {
            if (Uid == null) return RedirectToAction("Login", "Account");
            var user = uDL.GetById(Uid.Value) ?? new UserModel { Name = HttpContext.Session.GetString("uname") ?? "User" };
            ViewData["UserTheme"] = user.Theme;
            ViewData["Page"]      = "Dashboard";
            return View(wBL.BuildDashboard(Uid.Value, user));
        }

        [HttpPost, IgnoreAntiforgeryToken]
        public IActionResult StartSession([FromBody] StartSessionReq req)
        {
            if (Uid == null) return Json(new { success = false });
            int sid = wBL.StartSession(Uid.Value, req.DayId);
            return Json(new { success = true, sessionId = sid });
        }

        [HttpPost, IgnoreAntiforgeryToken]
        public IActionResult FinishSession([FromBody] FinishSessionReq req)
        {
            if (Uid == null) return Json(new { success = false });
            var progression = progDL.GetProgressionStyleForDay(req.DayId);
            var (ok, prs, achievements) = wBL.FinishSession(Uid.Value, req, progression);
            return Json(new { success = ok, newPRs = prs,
                newAchievements = achievements.Select(a => new { a.Icon, a.Name, a.Rarity }) });
        }

        // ── Water API ─────────────────────────────────────────────────
        [HttpPost, IgnoreAntiforgeryToken]
        public IActionResult LogWater([FromBody] WaterLogReq req)
        {
            if (Uid == null) return Json(new { success = false });
            bool ok = waterDL.LogWater(Uid.Value, Math.Max(50, Math.Min(2000, req.AmountMl)));
            var summary = waterDL.GetTodaySummary(Uid.Value);
            return Json(new { success = ok, totalMl = summary.TotalMl, goalMl = summary.GoalMl, percent = summary.Percent });
        }

        [HttpPost, IgnoreAntiforgeryToken]
        public IActionResult DeleteWater([FromBody] int intakeId)
        {
            if (Uid == null) return Json(new { success = false });
            bool ok = waterDL.DeleteEntry(Uid.Value, intakeId);
            var summary = waterDL.GetTodaySummary(Uid.Value);
            return Json(new { success = ok, totalMl = summary.TotalMl, goalMl = summary.GoalMl, percent = summary.Percent });
        }

        [HttpPost, IgnoreAntiforgeryToken]
        public IActionResult SetWaterGoal([FromBody] WaterLogReq req)
        {
            if (Uid == null) return Json(new { success = false });
            bool ok = waterDL.UpdateGoal(Uid.Value, Math.Max(500, Math.Min(6000, req.AmountMl)));
            var summary = waterDL.GetTodaySummary(Uid.Value);
            return Json(new { success = ok, goalMl = summary.GoalMl, totalMl = summary.TotalMl, percent = summary.Percent });
        }
    }
}
