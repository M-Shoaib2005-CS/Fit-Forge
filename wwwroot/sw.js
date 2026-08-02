const CACHE = 'fitforge-v17';
const STATIC = [
    '/',
    '/css/app.css',
    '/js/app.js',
    '/manifest.json',
    '/offline.html'
];

self.addEventListener('install', e => {
    // skipWaiting so a redeploy takes effect on next load instead of sitting "waiting"
    // until someone happens to tap the in-app update banner. Combined with clients.claim()
    // below, this means: redeploy -> next page load anywhere already gets the new
    // logo/icons/badge, no manual step required.
    self.skipWaiting();
    e.waitUntil(caches.open(CACHE).then(c => c.addAll(STATIC)));
});

self.addEventListener('activate', e => {
    e.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k)))
        ).then(() => self.clients.claim())
    );
});

// Triggered by the "Update" button in the frontend (see pwa.js) — lets a waiting new
// worker take over immediately instead of only on next full close-and-reopen.
self.addEventListener('message', e => {
    if (e.data && e.data.action === 'skipWaiting') self.skipWaiting();
});

self.addEventListener('fetch', e => {
    // Navigation: network-first. fetch() only rejects on a real connectivity
    // failure (offline, DNS down, etc) — legitimate 404/500 responses from the
    // server still resolve normally and are NOT caught here, so this only
    // shows the offline page when there's genuinely no internet.
    if (e.request.mode === 'navigate') {
        e.respondWith(
            fetch(e.request).catch(() => caches.match('/offline.html'))
        );
        return;
    }
    // Static assets: cache-first
    if (e.request.destination === 'style' || e.request.destination === 'script' || e.request.destination === 'image') {
        e.respondWith(
            caches.match(e.request).then(cached => cached || fetch(e.request).then(res => {
                const clone = res.clone();
                caches.open(CACHE).then(c => c.put(e.request, clone));
                return res;
            }).catch(() => cached))
        );
        return;
    }
    // API/form calls: network only
    e.respondWith(fetch(e.request));
});

// ── PUSH NOTIFICATIONS ──────────────────────────────────────
// The payload is whatever PushNotificationService.SendToUserAsync serialized:
// { title, body, url, type }. If parsing ever fails (some browsers/edge cases send
// plain text), fall back to a generic FitForge notification rather than
// silently dropping it.
self.addEventListener('push', e => {
    let data = {};
    try { data = e.data ? e.data.json() : {}; }
    catch (err) { data = { title: 'FitForge', body: e.data ? e.data.text() : '' }; }

    const title = data.title || 'FitForge';
    const options = {
        body: data.body || '',
        // Android/Chrome renders TWO icons if both are set: `icon` (large, right side)
        // and `badge` (small, left side/status bar). We only want the one, left-aligned
        // icon like native apps use — so we set `badge` (a proper transparent, white-only
        // silhouette; Android tints this itself, which is why it must be alpha-only, not
        // full color) and leave `icon` unset entirely so nothing renders on the right.
        badge: '/icons/badge-96.png',
        data: { url: data.url || '/Dashboard/Index' }
    };
    // Workout-day reminders get a visible call-to-action button. Per how this is meant to
    // work, tapping it lands on the Dashboard exactly like tapping the notification body
    // does — it's a visual nudge, not a shortcut that skips a step.
    if (data.type === 'workout') {
        options.actions = [{ action: 'open', title: 'Start workout' }];
    }
    e.waitUntil(self.registration.showNotification(title, options));
});

// Tapping the notification (body OR the action button): focus an already-open FitForge
// tab if one exists (rather than piling up duplicate tabs), otherwise open a new one —
// either way landing on the Dashboard, per the notification's intended destination.
self.addEventListener('notificationclick', e => {
    e.notification.close();
    const url = (e.notification.data && e.notification.data.url) || '/Dashboard/Index';
    e.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(clientList => {
            for (const client of clientList) {
                if ('focus' in client) {
                    client.navigate(url);
                    return client.focus();
                }
            }
            if (clients.openWindow) return clients.openWindow(url);
        })
    );
});