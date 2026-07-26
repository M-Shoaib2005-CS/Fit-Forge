// FitForge PWA — standalone app shell, install prompt, splash
(function(){
  'use strict';

  var _deferredInstallPrompt = null;
  var SPLASH_KEY = 'ff_splash_seen';
  var INSTALL_DISMISS_KEY = 'ff_install_dismissed';

  // ── Standalone detection ────────────────────────────────────
  function isStandalone(){
    return window.matchMedia('(display-mode: standalone)').matches
        || window.matchMedia('(display-mode: fullscreen)').matches
        || window.navigator.standalone === true;
  }

  function applyStandaloneClass(){
    if(isStandalone()){
      document.documentElement.classList.add('standalone');
      document.body.classList.add('standalone');
    } else {
      document.documentElement.classList.add('browser');
      document.body.classList.add('browser');
    }
  }

  // ── Splash screen ───────────────────────────────────────────
  function hideSplash(){
    var splash = document.getElementById('app-splash');
    if(!splash || splash.classList.contains('splash-out')) return;
    splash.classList.add('splash-out');
    setTimeout(function(){
      splash.style.display = 'none';
      splash.remove();
    }, 450);
    try { sessionStorage.setItem(SPLASH_KEY, '1'); } catch(e){}
  }

  function initSplash(){
    var splash = document.getElementById('app-splash');
    if(!splash) return;
    // Skip splash if already seen this session or in standalone (feels native)
    var seen = false;
    try { seen = sessionStorage.getItem(SPLASH_KEY) === '1'; } catch(e){}
    if(seen && !isStandalone()){
      splash.style.display = 'none';
      return;
    }
    // Minimum splash time for branding
    var minMs = isStandalone() ? 600 : 900;
    setTimeout(hideSplash, minMs);
    window.addEventListener('load', function(){ setTimeout(hideSplash, minMs); });
  }

  // ── Service worker + update detection ────────────────────────
  var _pendingWorker = null; // the new, waiting worker once an update is detected

  function showUpdateBanner(){
    var banner = document.getElementById('ff-update-banner');
    if(banner) banner.classList.add('show');
  }

  window.applyPendingUpdate = function(){
    if(!_pendingWorker) return;
    _pendingWorker.postMessage({ action: 'skipWaiting' });
    // Once the new worker actually takes control, reload to pick up the fresh JS/CSS/HTML —
    // reloading before it's actually activated would just re-run the old code again.
    navigator.serviceWorker.addEventListener('controllerchange', function(){
      window.location.reload();
    });
  };

  function registerSW(){
    if(!('serviceWorker' in navigator)) return;
    navigator.serviceWorker.register('/sw.js').then(function(reg){
      // Case 1: an update was already waiting before this page even loaded (e.g. user
      // closed the app mid-update last time).
      if(reg.waiting && navigator.serviceWorker.controller){
        _pendingWorker = reg.waiting;
        showUpdateBanner();
      }
      // Case 2: a new version starts installing while the app is open right now.
      reg.addEventListener('updatefound', function(){
        var newWorker = reg.installing;
        if(!newWorker) return;
        newWorker.addEventListener('statechange', function(){
          // "installed" + an existing controller = this is a genuine update, not the
          // very first install (which has no controller yet and activates on its own).
          if(newWorker.state === 'installed' && navigator.serviceWorker.controller){
            _pendingWorker = newWorker;
            showUpdateBanner();
          }
        });
      });
    }).catch(function(){});
  }

  // ── Install prompt ────────────────────────────────────────────
  window.addEventListener('beforeinstallprompt', function(e){
    e.preventDefault();
    _deferredInstallPrompt = e;
    showInstallUI();
  });

  function showInstallUI(){
    if(isStandalone()) return;
    var dismissed = false;
    try { dismissed = localStorage.getItem(INSTALL_DISMISS_KEY) === '1'; } catch(e){}
    if(dismissed) return;
    var banner = document.getElementById('install-banner');
    if(banner) banner.style.display = '';
  }

  window.installPwa = function(){
    if(_deferredInstallPrompt){
      _deferredInstallPrompt.prompt();
      _deferredInstallPrompt.userChoice.finally(function(){
        _deferredInstallPrompt = null;
        dismissInstallBanner();
      });
    } else if(/iphone|ipad|ipod/i.test(navigator.userAgent)){
      showStackToast ? showStackToast('Tap Share → Add to Home Screen', 'success', 5000)
                     : alert('To install: tap Share, then "Add to Home Screen".');
    } else {
      showStackToast ? showStackToast('Install from browser menu → Install app', 'success', 4000) : null;
    }
  };

  window.dismissInstallBanner = function(){
    var banner = document.getElementById('install-banner');
    if(banner){
      banner.style.display = 'none';
      try { localStorage.setItem(INSTALL_DISMISS_KEY, '1'); } catch(e){}
    }
  };

  window.addEventListener('appinstalled', function(){
    _deferredInstallPrompt = null;
    dismissInstallBanner();
    if(typeof showToast === 'function') showToast('FitForge installed! 🎉', 'success');
  });

  // ── Prevent pull-to-refresh in standalone (optional) ──────────
  function preventOverscroll(){
    if(!isStandalone()) return;
    var startY = 0;
    document.addEventListener('touchstart', function(e){
      if(e.touches.length === 1) startY = e.touches[0].clientY;
    }, { passive: true });
    document.addEventListener('touchmove', function(e){
      var el = e.target.closest('.modal-body, .main, .ex-card-body, .page-content');
      if(!el) return;
      if(el.scrollTop <= 0 && e.touches[0].clientY > startY + 10){
        // at top, pulling down — allow only on main scroll areas at top
        if(el === document.querySelector('.main') && window.scrollY <= 0){
          e.preventDefault();
        }
      }
    }, { passive: false });
  }

  // ── Page enter animation ────────────────────────────────────
  function initPageTransition(){
    var content = document.querySelector('.page-content');
    if(content) content.classList.add('page-enter');
  }

  // ── Push notifications ──────────────────────────────────────
  // VAPID public keys arrive from the server as URL-safe base64; PushManager
  // wants a raw Uint8Array, so this converts between the two.
  function urlBase64ToUint8Array(base64String){
    var padding = '='.repeat((4 - base64String.length % 4) % 4);
    var base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    var raw = window.atob(base64);
    var out = new Uint8Array(raw.length);
    for (var i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
    return out;
  }

  window.getPushSubscriptionStatus = function(){
    if(!('serviceWorker' in navigator) || !('PushManager' in window)) return Promise.resolve('unsupported');
    return navigator.serviceWorker.ready
      .then(function(reg){ return reg.pushManager.getSubscription(); })
      .then(function(sub){ return sub ? 'enabled' : 'disabled'; })
      .catch(function(){ return 'disabled'; });
  };

  window.enablePushNotifications = function(){
    if(!('serviceWorker' in navigator) || !('PushManager' in window)){
      if(typeof showToast === 'function') showToast('Notifications are not supported on this browser', 'danger');
      return Promise.resolve(false);
    }
    console.log('[push] starting subscribe flow');
    return fetch('/Notifications/VapidPublicKey').then(function(r){ return r.json(); }).then(function(d){
      console.log('[push] vapid key fetched, configured =', d.configured);
      if(!d.configured){
        if(typeof showToast === 'function') showToast('Notifications are not set up on the server yet', 'danger');
        return false;
      }
      return Notification.requestPermission().then(function(perm){
        console.log('[push] permission result:', perm);
        if(perm !== 'granted'){
          if(typeof showToast === 'function') showToast('Notification permission denied', 'danger');
          return false;
        }
        return navigator.serviceWorker.ready.then(function(reg){
          console.log('[push] service worker ready, subscribing…');
          return reg.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: urlBase64ToUint8Array(d.publicKey)
          }).catch(function(subErr){
            // This is the step most likely to fail on Android (Play Services issues, a
            // stale/broken service worker registration, etc) — surface the REAL browser
            // error instead of a generic message, since that's what actually tells us
            // what's wrong instead of guessing blind.
            console.error('[push] pushManager.subscribe failed:', subErr);
            if(typeof showToast === 'function')
              showToast('Could not enable notifications: ' + (subErr && subErr.message ? subErr.message : subErr), 'danger');
            subErr._handled = true; // outer catch shouldn't toast again for this
            throw subErr;
          });
        }).then(function(sub){
          console.log('[push] subscribed successfully, saving to server…');
          var json = sub.toJSON();
          var tz = null;
          try { tz = Intl.DateTimeFormat().resolvedOptions().timeZone; } catch(e){}
          return fetch('/Notifications/Subscribe', {
            method: 'POST', headers: {'Content-Type':'application/json'},
            body: JSON.stringify({ endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth, timezone: tz })
          }).then(function(r){ return r.json(); }).then(function(rd){
            console.log('[push] server save result:', rd);
            if(rd.success){ if(typeof showToast === 'function') showToast('Notifications enabled', 'success'); return true; }
            if(typeof showToast === 'function') showToast(rd.msg || 'Could not enable notifications', 'danger');
            return false;
          });
        });
      });
    }).catch(function(err){
      console.error('[push] enable flow failed:', err);
      if(!(err && err._handled) && typeof showToast === 'function')
        showToast('Could not enable notifications', 'danger');
      return false;
    });
  };

  window.disablePushNotifications = function(){
    if(!('serviceWorker' in navigator)) return Promise.resolve(false);
    return navigator.serviceWorker.ready
      .then(function(reg){ return reg.pushManager.getSubscription(); })
      .then(function(sub){
        if(!sub) return true;
        var endpoint = sub.endpoint;
        return sub.unsubscribe().then(function(){
          return fetch('/Notifications/Unsubscribe', {
            method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(endpoint)
          });
        }).then(function(){
          if(typeof showToast === 'function') showToast('Notifications turned off', 'success');
          return true;
        });
      }).catch(function(){ return false; });
  };

  // ── Init ────────────────────────────────────────────────────
  applyStandaloneClass();
  initSplash();
  registerSW();
  initPageTransition();
  preventOverscroll();

  // Show install banner after splash on first visit
  if(!isStandalone()){
    setTimeout(showInstallUI, 2500);
  }
})();
