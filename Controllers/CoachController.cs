using FitForge.BL;
using FitForge.DL;
using FitForge.Models;
using FitForge.Services;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Text.Json;

namespace FitForge.Controllers
{
    public class ChatTurnDto { public string Role { get; set; } = "user"; public string Text { get; set; } = ""; }
    public class ChatReq { public List<ChatTurnDto> History { get; set; } = new(); }

    public class CoachController(GeminiService gemini, ProgramBL programBL, ProfileBL profileBL, InjuryDL injuryDL, UserDL uDL) : BaseController(uDL)
    {
        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatReq req)
        {
            if (Uid == null) return Json(new { kind = "chat", message = "Please log in first." });
            if (req.History == null || req.History.Count == 0)
                return Json(new { kind = "chat", message = "Say something and I'll help out." });

            var turns = req.History.Select(h => new CoachTurn { Role = h.Role, Text = h.Text }).ToList();
            var reply = await gemini.SendAsync(turns);

            // Safety net: if the model claimed a proposal but the program object didn't actually
            // come back with real day/exercise content (schema drift, truncation, etc.), don't let
            // a confusing "here's your program!" message reach the user with nothing to tap.
            if (reply.Kind == "proposal" && !HasRealDays(reply.Program))
            {
                return Json(new
                {
                    kind = "chat",
                    message = "Sorry — I put that program together but it didn't come through properly. Mind trying again, maybe with a bit less detail in one message?",
                    quickReplies = Array.Empty<string>(),
                    program = (object?)null,
                    injury = (object?)null
                });
            }

            // The model's own chat message might claim an injury has been "logged" the moment it
            // has enough detail — so rather than requiring a separate, easy-to-miss button tap to
            // actually make that true, we log it for real right here, server-side, and let our own
            // deterministic message (not the model's) reflect what actually happened in the DB.
            if (reply.Kind == "injury_report")
            {
                var (logged, resultMessage, resolvedInjury) = TryLogInjury(Uid.Value, reply.Injury);
                return Json(new
                {
                    kind = logged ? "injury_logged" : "question",
                    message = resultMessage,
                    quickReplies = Array.Empty<string>(),
                    program = (object?)null,
                    injury = resolvedInjury
                });
            }

            return Json(new
            {
                kind = reply.Kind,
                message = reply.Message,
                quickReplies = reply.QuickReplies,
                program = reply.Program,
                injury = reply.Injury
            });
        }

        // Resolves the model's plain-text bodyPart/category (from the fixed catalogs baked into
        // the system prompt) into real DB ids and logs it immediately — same effect as the manual
        // Profile > Injuries flow. Returns a message grounded in what actually happened, never in
        // what the model merely said.
        private (bool logged, string message, object? injury) TryLogInjury(int uid, JsonElement? injuryEl)
        {
            if (injuryEl == null || injuryEl.Value.ValueKind != JsonValueKind.Object)
                return (false, "Which body part is it, and is it more of a muscle pull/strain or joint pain/stiffness?", null);

            string bodyPart = Get(injuryEl.Value, "bodyPart");
            string category = Get(injuryEl.Value, "category");
            string notes    = Get(injuryEl.Value, "notes");

            var part = injuryDL.GetBodyParts()
                .FirstOrDefault(p => string.Equals(p.Name, bodyPart, StringComparison.OrdinalIgnoreCase));
            var cat = injuryDL.GetCategories()
                .FirstOrDefault(c => string.Equals(c.Name, category, StringComparison.OrdinalIgnoreCase));

            if (part == null || cat == null)
                return (false, "I want to make sure I flag the right thing — which body part is it exactly, and is it more of a muscle pull/strain or joint pain/stiffness?", null);

            var (ok, _) = profileBL.LogInjury(uid, new LogInjuryReq { PartId = part.PartId, CategoryId = cat.CategoryId, Notes = notes });
            if (!ok)
                return (false, "Sorry, something went wrong logging that — mind trying again?", null);

            string msg = $"Logged — {part.Name} ({cat.Name}). I'll flag any exercises that could aggravate it in your workouts.";
            return (true, msg, new { bodyPart = part.Name, category = cat.Name, notes });
        }

        private static bool HasRealDays(JsonElement? program)
        {
            if (program == null || program.Value.ValueKind != JsonValueKind.Object) return false;
            if (!program.Value.TryGetProperty("days", out var days) || days.ValueKind != JsonValueKind.Array) return false;
            if (days.GetArrayLength() == 0) return false;
            // At least one day must have a non-empty exercises array (a program that's all Rest days
            // with zero exercises anywhere is not a usable proposal).
            foreach (var d in days.EnumerateArray())
                if (d.TryGetProperty("exercises", out var ex) && ex.ValueKind == JsonValueKind.Array && ex.GetArrayLength() > 0)
                    return true;
            return false;
        }

        [HttpPost]
        public IActionResult ApplyProgram([FromBody] JsonElement programJson)
        {
            if (Uid == null) return Json(new { success = false, msg = "Please log in first." });
            try
            {
                var req = new CreateProgramReq
                {
                    Name = Get(programJson, "name"),
                    Description = Get(programJson, "description"),
                    GoalType = OrDefault(Get(programJson, "goalType"), "General"),
                    ProgressionStyle = OrDefault(Get(programJson, "progressionStyle"), "Adaptive"),
                    Days = new List<DayReq>()
                };

                if (programJson.TryGetProperty("days", out var daysEl) && daysEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var d in daysEl.EnumerateArray())
                    {
                        var day = new DayReq
                        {
                            Name = Get(d, "name"),
                            DayType = OrDefault(Get(d, "dayType"), "Workout"),
                            Exercises = new List<ExerciseReq>()
                        };
                        if (d.TryGetProperty("exercises", out var exEl) && exEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var e in exEl.EnumerateArray())
                            {
                                day.Exercises.Add(new ExerciseReq
                                {
                                    ExerciseId  = e.TryGetProperty("exerciseId", out var eid) ? eid.GetInt32() : 0,
                                    Sets        = e.TryGetProperty("sets", out var s) ? s.GetInt32() : 3,
                                    Reps        = e.TryGetProperty("reps", out var r) ? r.GetInt32() : 10,
                                    WeightKg    = e.TryGetProperty("weightKg", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetDouble() : (double?)null,
                                    RestSeconds = e.TryGetProperty("restSeconds", out var rs) ? rs.GetInt32() : 90
                                });
                            }
                        }
                        req.Days.Add(day);
                    }
                }

                // autoSchedule:true — a program the coach hands over should show up on the
                // week immediately, not just sit unassigned in the Programs list.
                var (ok, msg, programId) = programBL.CreateProgram(Uid.Value, req, autoSchedule: true);
                return Json(new { success = ok, msg, programId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "Couldn't create that program: " + ex.Message });
            }
        }

        // Kept for manual/API use — the coach's own chat flow now logs injuries automatically via
        // TryLogInjury above, but this endpoint still works standalone if ever needed.
        [HttpPost]
        public IActionResult LogInjury([FromBody] JsonElement injuryJson)
        {
            if (Uid == null) return Json(new { success = false, msg = "Please log in first." });
            try
            {
                string bodyPart = Get(injuryJson, "bodyPart");
                string category = Get(injuryJson, "category");
                string notes    = Get(injuryJson, "notes");

                var part = injuryDL.GetBodyParts()
                    .FirstOrDefault(p => string.Equals(p.Name, bodyPart, StringComparison.OrdinalIgnoreCase));
                var cat = injuryDL.GetCategories()
                    .FirstOrDefault(c => string.Equals(c.Name, category, StringComparison.OrdinalIgnoreCase));

                if (part == null || cat == null)
                    return Json(new { success = false, msg = "Couldn't match that to a known body part / injury type." });

                var (ok, msg) = profileBL.LogInjury(Uid.Value, new LogInjuryReq
                {
                    PartId = part.PartId,
                    CategoryId = cat.CategoryId,
                    Notes = notes
                });
                return Json(new { success = ok, msg });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "Couldn't log that injury: " + ex.Message });
            }
        }

        private static string Get(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        private static string OrDefault(string v, string fallback) => string.IsNullOrWhiteSpace(v) ? fallback : v;
    }
}
