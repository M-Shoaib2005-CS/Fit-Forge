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
        private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(30);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Give the app a minute to finish starting before the first tick.
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var bl = scope.ServiceProvider.GetRequiredService<NotificationBL>();
                    var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

                    if (!push.IsConfigured)
                    {
                        log.LogDebug("Push notifications not configured (missing Vapid keys) — skipping tick");
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
