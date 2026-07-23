using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FitForge.Services
{
    public class CoachTurn
    {
        public string Role { get; set; } = "user"; // "user" or "model"
        public string Text { get; set; } = "";
    }

    // Mirrors the JSON envelope Gemini is instructed (and schema-constrained) to return.
    // "program" is only populated when Kind == "proposal", and its shape matches
    // FitForge.Models.CreateProgramReq exactly so it can be handed straight to
    // ProgramBL.CreateProgram with no translation step.
    // "injury" is only populated when Kind == "injury_report".
    public class CoachReply
    {
        public string Kind { get; set; } = "chat"; // "chat" | "question" | "proposal" | "injury_report"
        public string Message { get; set; } = "";
        public List<string> QuickReplies { get; set; } = new();
        public JsonElement? Program { get; set; }
        public JsonElement? Injury { get; set; }
    }

    public class GeminiService
    {
        private readonly HttpClient _http;
        private readonly ILogger<GeminiService> _log;
        private readonly string _apiKey;
        private readonly string _model;

        public GeminiService(HttpClient http, IConfiguration config, ILogger<GeminiService> log)
        {
            _http = http;
            _log = log;
            _apiKey = config["Gemini:ApiKey"] ?? "";
            _model = config["Gemini:Model"] ?? "gemini-3.5-flash-lite";
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey) && _apiKey != "PASTE_YOUR_GEMINI_API_KEY_HERE";

        // The exercise catalog embedded in every system prompt so the model can only
        // ever reference IDs that actually exist — no fuzzy name matching needed later.
        public const string ExerciseCatalog =
            "1=Push-Up|Chest|reps_only;2=Wide Push-Up|Chest|reps_only;3=Diamond Push-Up|Triceps|reps_only;" +
            "4=Decline Push-Up|Chest|reps_only;5=Archer Push-Up|Chest|reps_only;6=Pike Push-Up|Shoulders|reps_only;" +
            "7=Pseudo Planche Push-Up|Chest|reps_only;8=Pull-Up|Back|reps_only;9=Chin-Up|Biceps|reps_only;" +
            "10=Australian Pull-Up|Back|reps_only;11=Archer Pull-Up|Back|reps_only;12=Commando Pull-Up|Back|reps_only;" +
            "13=Plank|Core|duration;14=Hollow Body Hold|Core|duration;15=L-Sit|Core|duration;" +
            "16=Dragon Flag|Core|reps_only;17=Hanging Leg Raise|Core|reps_only;18=Ab Wheel Rollout|Core|reps_only;" +
            "19=Squat|Legs|reps_only;20=Bulgarian Split Squat|Legs|reps_only;21=Pistol Squat|Legs|reps_only;" +
            "22=Nordic Curl|Legs|reps_only;23=Jump Squat|Legs|reps_only;24=Calf Raise|Calves|reps_only;" +
            "25=Handstand Hold|Shoulders|duration;26=Wall Handstand Push-Up|Shoulders|reps_only;27=Burpee|Full Body|reps_only;" +
            "28=Mountain Climber|Full Body|reps_only;29=Bear Crawl|Full Body|duration;30=Bench Press|Chest|reps_weight;" +
            "31=Incline Bench Press|Chest|reps_weight;32=Overhead Press|Shoulders|reps_weight;33=Deadlift|Back|reps_weight;" +
            "34=Barbell Row|Back|reps_weight;35=Barbell Squat|Legs|reps_weight;36=Dumbbell Curl|Biceps|reps_weight;" +
            "37=Tricep Pushdown|Triceps|reps_weight;38=Cable Row|Back|reps_weight;39=Lat Pulldown|Back|reps_weight;" +
            "40=Leg Press|Legs|reps_weight;41=Leg Curl|Legs|reps_weight;42=Dumbbell Lateral Raise|Shoulders|reps_weight;" +
            "43=Jump Rope|Cardio|duration;44=Box Jump|Legs|reps_only;45=Sprint|Cardio|duration";

        // Body parts the injury system knows about (must match body_parts table exactly, by name).
        public const string BodyPartCatalog =
            "Shoulder Joint|Joint;Elbow Joint|Joint;Wrist Joint|Joint;Hip Joint|Joint;Knee Joint|Joint;Ankle Joint|Joint;" +
            "Chest (Pec)|Muscle;Upper Back|Muscle;Lower Back|Muscle;Bicep|Muscle;Tricep|Muscle;Shoulder (Delt)|Muscle;" +
            "Hamstring|Muscle;Quadricep|Muscle;Calf|Muscle;Glute|Muscle;Core / Abs|Muscle";

        // The only two injury categories the app supports (must match injury_categories table exactly, by name).
        public const string InjuryCategoryCatalog = "Muscle Pull / Strain;Joint Pain / Stiffness";

        public const string SystemPrompt = @"You are the in-app coach for FitForge, a calisthenics + gym fitness tracking app.
Speak like a knowledgeable, encouraging coach — concise, warm, no fluff.

APP KNOWLEDGE (use this to answer 'how do I...' questions accurately):
- Dashboard: shows today's scheduled session, streak, hydration, quick stats. 'Start Session' begins logging.
- Programs tab: browse/build training programs (a program has named days, each day has exercises with
  sets/reps/rest), and set a weekly schedule assigning a program day to each day of the week.
- Workouts tab: session history, a calendar of trained days, and personal records per exercise.
- Skills tab: calisthenics skill tree (planche, front lever, muscle-up, etc.) with step-by-step progressions.
- Profile: body stats, injuries, measurements, achievements, and app settings (theme/accent color).
- To build a program manually: Programs tab -> '+ Build Program' -> name it, add days, add exercises per day.
- Progression is adaptive: hitting more reps in a set can raise the weight/reps target next time automatically.

EXERCISE CATALOG (id=Name|MuscleGroup|TrackingMode — tracking_mode is reps_only, reps_weight, or duration):
" + ExerciseCatalog + @"

BODY PART CATALOG (name|type) — used only for the injury protocol below:
" + BodyPartCatalog + @"

INJURY CATEGORY CATALOG — used only for the injury protocol below:
" + InjuryCategoryCatalog + @"

CONVERSATION PROTOCOL (minimize API calls — this matters, each turn costs money):
- If the user asks a question about how the app works, answer directly using the app knowledge above. kind=""chat"".
- If the user asks you to build/create a program and you're missing info, ask for EVERYTHING you need in ONE
  single message — goal, days-per-week, AND equipment/experience together, not one at a time. kind=""question"".
  Offer 2-3 quickReplies that are complete combined presets (e.g. ""Build muscle · 4 days · Dumbbells"",
  ""Get stronger · 3 days · Full gym"", ""Lose fat · 5 days · Bodyweight only"") so a single tap can answer
  everything at once — but the user may also just type a free-text answer covering all three, which you
  should parse yourself rather than asking follow-ups if they gave enough detail.
- The MOMENT you have goal + days/week + equipment (from one message, tapped or typed), immediately propose
  the full program in the same turn — don't ask a second confirmation question first. kind=""proposal"", with
  a short friendly message and a fully populated 'program' object. The 'program' object is NEVER just a name —
  it MUST include every single day (matching days-per-week, e.g. 4 days/week = 4 Workout day entries, plus
  Rest days if you want a full 7-day week) and EVERY exercise within each day fully specified: exerciseId,
  name (the catalog name, for display), sets, reps, weightKg (only for reps_weight exercises, else null),
  and restSeconds. Never return a program with an empty or partial 'days' array — that is a broken response.
  The app shows this as a card with an Apply button the user taps directly — do not wait for the user to
  say ""ok"" or ""confirm"" in chat; that would waste an extra call on something a button already handles for free.
- Every exercise MUST use a valid id from the catalog above, matched to the stated equipment (don't suggest
  barbell exercises to a bodyweight-only user). dayType must be one of Workout, Rest, Active Recovery.
  goalType must be one of Strength, Hypertrophy, Endurance, Fat Loss, Skill, General. progressionStyle must
  be one of Conservative, Moderate, Aggressive, Adaptive.
- Never invent exercise ids that aren't in the catalog.

INJURY PROTOCOL (takes priority over the program-building flow if the user mentions pain/injury at any point):
- If the user mentions any pain, ache, soreness beyond normal, a tweak, a strain, or says something is
  ""hurt""/""injured""/""bothering"" them, respond with a short warm, human line acknowledging it first (e.g.
  sound genuinely concerned, not clinical) — THEN, in the same message, ask which body part it is and
  whether it feels more like a muscle pull/strain or joint pain/stiffness, so you can flag any exercises
  that could aggravate it. kind=""question"". Offer 2-3 quickReplies combining the body part they hinted at
  with both categories (e.g. user says ""my shoulder hurts"" -> quickReplies: [""Shoulder Joint · Joint Pain"",
  ""Shoulder (Delt) · Muscle Strain""]), but also accept a free-text answer.
- The MOMENT you can identify both a specific body part (matching the BODY PART CATALOG exactly) AND a
  category (matching the INJURY CATEGORY CATALOG exactly) from what the user said (tapped or typed),
  respond immediately with kind=""injury_report"" — a fully populated 'injury' object: { bodyPart, category,
  notes }. bodyPart and category MUST be copied verbatim from the catalogs above (exact spelling). notes is
  a brief (<12 words) restatement of what the user described, or empty string if nothing extra was said.
  The app logs it immediately when it receives this — your 'message' text for this kind is never shown to
  the user (the app replaces it with its own confirmation), so keep it minimal. Do not populate 'program'
  in this response.
- If the body part or category the user describes doesn't clearly map to the catalogs, ask a clarifying
  question (kind=""question"") instead of guessing.
- Always respond with ONLY the JSON envelope — no markdown, no prose outside the JSON.";

        private static readonly object ResponseSchema = new
        {
            type = "OBJECT",
            properties = new
            {
                kind = new { type = "STRING", @enum = new[] { "chat", "question", "proposal", "injury_report" } },
                message = new { type = "STRING" },
                quickReplies = new { type = "ARRAY", items = new { type = "STRING" } },
                program = new
                {
                    type = "OBJECT",
                    nullable = true,
                    properties = new
                    {
                        name = new { type = "STRING" },
                        description = new { type = "STRING" },
                        goalType = new { type = "STRING" },
                        progressionStyle = new { type = "STRING" },
                        days = new
                        {
                            type = "ARRAY",
                            items = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    name = new { type = "STRING" },
                                    dayType = new { type = "STRING" },
                                    exercises = new
                                    {
                                        type = "ARRAY",
                                        items = new
                                        {
                                            type = "OBJECT",
                                            properties = new
                                            {
                                                exerciseId = new { type = "INTEGER" },
                                                name = new { type = "STRING" },
                                                sets = new { type = "INTEGER" },
                                                reps = new { type = "INTEGER" },
                                                weightKg = new { type = "NUMBER", nullable = true },
                                                restSeconds = new { type = "INTEGER" }
                                            },
                                            required = new[] { "exerciseId", "name", "sets", "reps", "restSeconds" }
                                        }
                                    }
                                },
                                required = new[] { "name", "dayType", "exercises" }
                            }
                        }
                    },
                    required = new[] { "name", "description", "goalType", "progressionStyle", "days" }
                },
                injury = new
                {
                    type = "OBJECT",
                    nullable = true,
                    properties = new
                    {
                        bodyPart = new { type = "STRING" },
                        category = new { type = "STRING" },
                        notes = new { type = "STRING" }
                    },
                    required = new[] { "bodyPart", "category", "notes" }
                }
            },
            required = new[] { "kind", "message" }
        };

        public async Task<CoachReply> SendAsync(List<CoachTurn> history)
        {
            if (!IsConfigured)
                return new CoachReply { Kind = "chat", Message = "The coach isn't configured yet — an admin needs to add a Gemini API key in appsettings.json." };

            var contents = history.Select(h => new
            {
                role = h.Role == "model" ? "model" : "user",
                parts = new[] { new { text = h.Text } }
            });

            var body = new
            {
                systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
                contents,
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    responseSchema = ResponseSchema
                    // NOTE: temperature/top_p/top_k are deliberately omitted — Gemini's
                    // 3.x model family deprecated these and returns an error if present.
                }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
            var json = JsonSerializer.Serialize(body);

            try
            {
                var resp = await _http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
                var respBody = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    _log.LogError("Gemini API error {Status}: {Body}", resp.StatusCode, respBody);
                    return new CoachReply { Kind = "chat", Message = "Sorry, I couldn't reach the coach service just now — try again in a moment." };
                }

                using var doc = JsonDocument.Parse(respBody);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "{}";

                var reply = JsonSerializer.Deserialize<CoachReply>(text, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                return reply ?? new CoachReply { Kind = "chat", Message = "Hmm, I didn't quite catch that — could you rephrase?" };
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Gemini call failed");
                return new CoachReply { Kind = "chat", Message = "Something went wrong reaching the coach — try again shortly." };
            }
        }
    }
}
