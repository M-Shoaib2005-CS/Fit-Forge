const CACHE = 'fitforge-v2';
const STATIC = [
  '/',
  '/css/app.css',
  '/js/app.js',
  '/manifest.json',
  '/offline.html'
];

self.addEventListener('install', e => {
  e.waitUntil(caches.open(CACHE).then(c => c.addAll(STATIC)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', e => {
  e.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k)))
    ).then(() => self.clients.claim())
  );
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
