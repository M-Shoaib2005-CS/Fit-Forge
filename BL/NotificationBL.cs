using Microsoft.Extensions.Logging;
using FitForge.DL;
using FitForge.Services;

namespace FitForge.BL
{
    public class NotificationBL(NotificationDL dl, IPushNotificationService push, ILogger<NotificationBL> log)
    {
        private static readonly Random _rng = new();

        private static readonly string[] EncouragementLines = {
            "Haven't seen you on the mat in a couple days — a short session still counts.",
            "Two days off the radar. Your streak wants you back, no pressure.",
            "Consistency beats intensity. Even 20 minutes today keeps the thread going.",
            "Missed you these last couple days — how are you feeling about getting back in?",
        };

        private static readonly string[] InjuryCheckinLines = {
            "How's that {0} feeling today compared to a few days ago?",
            "Checking in — any change in your {0} since you last logged it?",
            "Just making sure your {0} is healing okay. Let me know if anything's changed.",
        };

        private static readonly string[] RestDayLines = {
            "Today's a rest day — recovery is part of the plan, not a break from it.",
            "No training scheduled today. Stretch, hydrate, or just take the win of a planned rest day.",
            "Rest day. Your muscles rebuild on days like this — enjoy it.",
        };

        public async Task<int> SendWorkoutRemindersAsync()
        {
            int weekDay = ((int)DateTime.Today.DayOfWeek + 6) % 7; // Mon=0, matches ScheduleDL.GetTodaySlot
            // NOTE: this uses the SERVER's current weekday to decide which schedule slot to
            // check. For the vast majority of users this is fine, but right near midnight a
            // user in a very different timezone from the server could technically be a day
            // off from what the server considers "today". A known, minor edge case — not
            // worth the complexity of a full per-user-weekday rewrite for how rarely it'd
            // actually cause a wrong day's reminder.
            var candidates = dl.GetWorkoutReminderCandidates(weekDay);
            int sent = 0;

            foreach (var c in candidates)
            {
                TimeZoneInfo tz;
                try { tz = TimeZoneInfo.FindSystemTimeZoneById(c.Timezone); }
                catch { tz = TimeZoneInfo.Utc; } // unknown/bad id — fall back rather than skip the user entirely

                var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                if (localNow.TimeOfDay < c.ReminderTime) continue; // not due yet in THEIR time

                if (c.IsRest)
                {
                    var line = RestDayLines[_rng.Next(RestDayLines.Length)];
                    await push.SendToUserAsync(c.UserId, "Rest day", line, "/Dashboard/Index");
                }
                else
                {
                    await push.SendToUserAsync(c.UserId, "Time to train — " + c.DayName,
                        "Your " + c.DayName + " day is on deck for today.", "/Dashboard/Index");
                }
                dl.MarkSent(c.UserId, "WorkoutReminder");
                sent++;
            }
            if (sent > 0) log.LogInformation("Sent {N} daily reminders", sent);
            return sent;
        }

        public async Task<int> SendInjuryCheckinsAsync()
        {
            var candidates = dl.GetInjuryCheckinCandidates();
            foreach (var (uid, _, bodyPart) in candidates)
            {
                var line = string.Format(InjuryCheckinLines[_rng.Next(InjuryCheckinLines.Length)], bodyPart.ToLower());
                await push.SendToUserAsync(uid, "Checking in from your coach", line, "/Dashboard/Index");
                dl.MarkSent(uid, "InjuryCheckin");
            }
            if (candidates.Count > 0) log.LogInformation("Sent {N} injury check-ins", candidates.Count);
            return candidates.Count;
        }

        public async Task<int> SendEncouragementAsync()
        {
            var candidates = dl.GetEncouragementCandidates();
            foreach (var (uid, _) in candidates)
            {
                var line = EncouragementLines[_rng.Next(EncouragementLines.Length)];
                await push.SendToUserAsync(uid, "Your coach checking in", line, "/Dashboard/Index");
                dl.MarkSent(uid, "CoachEncouragement");
            }
            if (candidates.Count > 0) log.LogInformation("Sent {N} encouragement nudges", candidates.Count);
            return candidates.Count;
        }
    }
}
