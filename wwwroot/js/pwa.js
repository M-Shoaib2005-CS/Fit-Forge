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

  // ── Service worker ──────────────────────────────────────────
  function registerSW(){
    if(!('serviceWorker' in navigator)) return;
    navigator.serviceWorker.register('/sw.js').catch(function(){});
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
