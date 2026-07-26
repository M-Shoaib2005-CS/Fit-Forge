using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FitForge.BL;

namespace FitForge.Services
{
    /// <summary>
    /// Ticks every 30 minutes. Each user picks their own daily reminder time in Settings
    /// (users.reminder_time) — NotificationDL.GetWorkoutReminderCandidates compares against
    /// that per-user, so this loop doesn't gate on any single global hour anymore. It just
    /// asks "who's due right now" every tick; same-day dedupe (notification_log) stops a
    /// user getting reminded twice even though the check runs every 30 minutes. Injury
    /// check-ins and encouragement nudges work the same way, gated by their own longer
    /// dedupe windows (3 days / 2 days, in NotificationDL) rather than a time-of-day check.
    /// </summary>
    public class NotificationBackgroundService(
        IServiceScopeFactory scopeFactory, ILogger<NotificationBackgroundService> log)
        : BackgroundService
    {
        // TEMP FOR TESTING — was 30 min. Set back to TimeSpan.FromMinutes(30) once you've
        // confirmed a real notification arrives; 1-minute ticks are just for fast feedback
        // while debugging, not meant to run in production long-term.
        private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // TEMP FOR TESTING — was 1 minute.
            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); } catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var bl = scope.ServiceProvider.GetRequiredService<NotificationBL>();
                    var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

                    if (!push.IsConfigured)
                    {
                        // Was LogDebug — invisible in Render's default log level, meaning a
                        // Vapid key misconfiguration would fail completely silently, forever,
                        // with zero signal in the logs. Bumped to Warning so this is actually
                        // visible if it's the cause.
                        log.LogWarning("Push notifications not configured (missing or invalid Vapid keys) — skipping tick");
                    }
                    else
                    {
                        await bl.SendWorkoutRemindersAsync();
                        await bl.SendInjuryCheckinsAsync();
                        await bl.SendEncouragementAsync();
                    }
                }
                catch (Exception ex)
                {
                    // A bad tick should never kill the whole background loop — log and try
                    // again next interval rather than taking notifications down entirely.
                    log.LogError(ex, "Notification background tick failed");
                }

                try { await Task.Delay(TickInterval, stoppingToken); } catch (TaskCanceledException) { break; }
            }
        }
    }
}
