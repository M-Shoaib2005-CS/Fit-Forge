using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebPush;
using FitForge.DL;

namespace FitForge.Services
{
    public interface IPushNotificationService
    {
        // Sends to every device the user has subscribed on. Any endpoint the push service
        // reports as gone (404/410 — user uninstalled, cleared storage, etc.) gets cleaned
        // out of push_subscriptions so future ticks stop wasting a send on it.
        // `type` is optional and only used client-side (sw.js) to decide whether to show an
        // action button — "workout" gets a "Start workout" button, everything else doesn't.
        Task SendToUserAsync(int uid, string title, string body, string url, string? type = null);
        bool IsConfigured { get; }
        string PublicKey { get; }
    }

    /// <summary>
    /// Stub-safe: if VAPID keys aren't configured, logs what would have been sent instead of
    /// throwing — same pattern as EmailService, so a missing config never crashes a request.
    /// </summary>
    public class PushNotificationService : IPushNotificationService
    {
        private readonly NotificationDL _dl;
        private readonly ILogger<PushNotificationService> _log;
        private readonly string? _publicKey;
        private readonly string? _privateKey;
        private readonly string _subject;
        private readonly WebPushClient _client = new();

        public PushNotificationService(NotificationDL dl, IConfiguration config, ILogger<PushNotificationService> log)
        {
            _dl = dl;
            _log = log;
            _publicKey = config["Vapid:PublicKey"];
            _privateKey = config["Vapid:PrivateKey"];
            _subject = config["Vapid:Subject"] ?? "mailto:support@fitforge.app";
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_publicKey) && !string.IsNullOrWhiteSpace(_privateKey);
        public string PublicKey => _publicKey ?? "";

        public async Task SendToUserAsync(int uid, string title, string body, string url, string? type = null)
        {
            if (!IsConfigured)
            {
                _log.LogInformation("Push not configured — would send to user {Uid}: {Title} — {Body}", uid, title, body);
                return;
            }

            var vapid = new VapidDetails(_subject, _publicKey, _privateKey);
            var payload = System.Text.Json.JsonSerializer.Serialize(new { title, body, url, type });

            foreach (var sub in _dl.GetSubscriptionsForUser(uid))
            {
                var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                try
                {
                    await _client.SendNotificationAsync(pushSub, payload, vapid);
                }
                catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound
                                                 || ex.StatusCode == System.Net.HttpStatusCode.Gone)
                {
                    _log.LogInformation("Push subscription {SubId} for user {Uid} is dead — removing", sub.SubId, uid);
                    _dl.RemoveSubscriptionById(sub.SubId);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Push send failed for user {Uid}, subscription {SubId}", uid, sub.SubId);
                }
            }
        }
    }
}
