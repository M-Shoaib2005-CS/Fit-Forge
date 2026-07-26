const CACHE = 'fitforge-v5';
const STATIC = [
  '/',
  '/css/app.css',
  '/js/app.js',
  '/manifest.json',
  '/offline.html'
];

self.addEventListener('install', e => {
  // NOTE: no self.skipWaiting() here anymore. A first-ever install still activates
  // immediately regardless (nothing to conflict with) — but when this is an UPDATE to an
  // already-running app, holding off here is what lets the new worker sit in "waiting"
  // state so the frontend can detect it, show an "Update available" banner, and only
  // activate it once the user actually taps the button (see the 'message' listener below).
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
    // icon-192.png is full color — fine here. It was ALSO being used as `badge`, which
    // Android requires to be a transparent-background monochrome silhouette (it derives
    // the small status-bar icon from the alpha channel); an opaque RGB PNG can't be used
    // that way, which is why a generic fallback icon was showing instead. Dropping `badge`
    // entirely rather than shipping a half-working asset — Android/Chrome supplies its own
    // default small icon in that slot instead.
    icon: '/icons/icon-192.png',
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
