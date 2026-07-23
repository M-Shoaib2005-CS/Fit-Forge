// ── ACCENT COLOR (live, persisted client-side) ─────────────────
var FF_ACCENTS = {
  orange: { accent:'#ff5a2b', dim:'#ff5a2b33' },
  lime:   { accent:'#d7f24c', dim:'#d7f24c33' },
  pink:   { accent:'#ff3d7a', dim:'#ff3d7a33' },
  blue:   { accent:'#4ea6ff', dim:'#4ea6ff33' }
};
function applyAccent(key){
  var a = FF_ACCENTS[key] || FF_ACCENTS.orange;
  document.documentElement.style.setProperty('--accent', a.accent);
  document.documentElement.style.setProperty('--accent-dim', a.dim);
  document.querySelectorAll('.accent-swatch').forEach(function(s){
    s.classList.toggle('active', s.dataset.accent === key);
  });
  try{ localStorage.setItem('ff-accent', key); }catch(e){}
}
(function(){
  var saved = 'orange';
  try{ saved = localStorage.getItem('ff-accent') || 'orange'; }catch(e){}
  applyAccent(saved);
})();

// ── CONFETTI BURST ───────────────────────────────────────────
function ffConfetti(originEl){
  var layer = document.getElementById('ff-confetti-layer');
  if(!layer){
    layer = document.createElement('div');
    layer.id = 'ff-confetti-layer';
    layer.className = 'ff-confetti-layer';
    document.body.appendChild(layer);
  }
  var rect = originEl ? originEl.getBoundingClientRect() : { left: innerWidth/2, top: innerHeight/2, width:0, height:0 };
  var ox = rect.left + rect.width/2, oy = rect.top + rect.height/2;
  var colors = ['#ff5a2b','#d7f24c','#ff3d7a','#f2efe6'];
  for(var i=0;i<24;i++){
    var c = document.createElement('div');
    c.className = 'ff-confetto';
    var ang = Math.random()*Math.PI*2, dist = 50+Math.random()*130;
    c.style.left = ox+'px'; c.style.top = oy+'px';
    c.style.setProperty('--dx', Math.cos(ang)*dist+'px');
    c.style.setProperty('--dy', (Math.sin(ang)*dist-30)+'px');
    c.style.setProperty('--rot', (Math.random()*520-260)+'deg');
    c.style.background = colors[i % colors.length];
    c.style.borderRadius = Math.random()>.5 ? '2px' : '50%';
    layer.appendChild(c);
    (function(el){ setTimeout(function(){ el.remove(); }, 950); })(c);
  }
}

// ── SVG RING ANIMATOR (skills, badges — anything with .ring-fg) ─
function ffAnimateRings(scope){
  (scope || document).querySelectorAll('.ring-fg[data-pct]').forEach(function(r){
    var pct = +r.dataset.pct;
    var radius = +(r.getAttribute('r') || 31.5);
    var circumference = 2 * Math.PI * radius;
    r.style.strokeDasharray = circumference;
    r.style.strokeDashoffset = circumference;
    requestAnimationFrame(function(){
      requestAnimationFrame(function(){
        r.style.transition = 'stroke-dashoffset 1s cubic-bezier(.2,.8,.2,1)';
        r.style.strokeDashoffset = circumference * (1 - pct/100);
      });
    });
  });
}
document.addEventListener('DOMContentLoaded', function(){ ffAnimateRings(); });

// ── ONBOARDING TOUR (skippable spotlight walkthrough) ─────────
var FFTour = (function(){
  var steps = [], idx = 0, uid = null;
  var els = {};

  function build(){
    var overlay = document.createElement('div');
    overlay.className = 'ff-tour-overlay';
    overlay.innerHTML =
      '<div class="ff-tour-spot" id="ff-tour-spot"></div>' +
      '<div class="ff-tour-card" id="ff-tour-card">' +
        '<div class="ff-tour-dots" id="ff-tour-dots"></div>' +
        '<div class="ff-tour-title" id="ff-tour-title"></div>' +
        '<div class="ff-tour-text" id="ff-tour-text"></div>' +
        '<div class="ff-tour-actions">' +
          '<button class="ff-tour-skip" id="ff-tour-skip">Skip tour</button>' +
          '<button class="btn btn-solid btn-sm" id="ff-tour-next">Next →</button>' +
        '</div>' +
      '</div>';
    document.body.appendChild(overlay);
    els.overlay = overlay;
    els.spot   = overlay.querySelector('#ff-tour-spot');
    els.card   = overlay.querySelector('#ff-tour-card');
    els.dots   = overlay.querySelector('#ff-tour-dots');
    els.title  = overlay.querySelector('#ff-tour-title');
    els.text   = overlay.querySelector('#ff-tour-text');
    els.next   = overlay.querySelector('#ff-tour-next');
    els.skip   = overlay.querySelector('#ff-tour-skip');
    els.next.onclick = next;
    els.skip.onclick = function(){ finish(true); };
    window.addEventListener('resize', position);

    // The guide orb — same visual as the persistent coach orb — escorts the tour
    if(!els.guideOrb){
      var g = document.createElement('div');
      g.className = 'ff-tour-guide-orb';
      g.innerHTML = '<svg viewBox="0 0 24 24" fill="none"><path d="M12 2a5 5 0 0 0-5 5v2.05A6 6 0 0 0 3 15v1a1 1 0 0 0 1 1h1.17A3 3 0 0 0 8 19.83V21a1 1 0 0 0 1 1h6a1 1 0 0 0 1-1v-1.17A3 3 0 0 0 18.83 17H20a1 1 0 0 0 1-1v-1a6 6 0 0 0-4-5.66V7a5 5 0 0 0-5-5Zm-2 9.5a1.25 1.25 0 1 1 0 2.5 1.25 1.25 0 0 1 0-2.5Zm4 0a1.25 1.25 0 1 1 0 2.5 1.25 1.25 0 0 1 0-2.5Z" fill="var(--accent-ink)"/></svg>';
      document.body.appendChild(g);
      els.guideOrb = g;
    }
    var realOrb = document.getElementById('coach-orb');
    if(realOrb) realOrb.classList.add('coach-hidden');
  }

  function renderDots(){
    els.dots.innerHTML = '';
    steps.forEach(function(s, i){
      var d = document.createElement('span');
      d.className = 'ff-tour-dot' + (i === idx ? ' active' : '');
      els.dots.appendChild(d);
    });
  }

  function position(){
    var step = steps[idx];
    if(!step.selector){
      // final full-screen step — no spotlight
      els.spot.style.display = 'none';
      els.card.classList.add('ff-tour-card--center');
      els.guideOrb.classList.remove('show');
      setTimeout(function(){
        var cr = els.card.getBoundingClientRect();
        els.guideOrb.style.left = (cr.left + cr.width/2 - 22) + 'px';
        els.guideOrb.style.top  = (cr.top - 54) + 'px';
        els.guideOrb.classList.add('show');
      }, 260);
      return;
    }
    var target = document.querySelector(step.selector);
    if(!target){ next(); return; }
    target.scrollIntoView({ block:'center', behavior:'smooth' });
    setTimeout(function(){
      var r = target.getBoundingClientRect();
      var pad = 10;
      els.spot.style.display = 'block';
      els.card.classList.remove('ff-tour-card--center');
      els.spot.style.left   = (r.left - pad) + 'px';
      els.spot.style.top    = (r.top - pad) + 'px';
      els.spot.style.width  = (r.width + pad*2) + 'px';
      els.spot.style.height = (r.height + pad*2) + 'px';

      var cardTop = r.bottom + 18;
      if(cardTop + 180 > window.innerHeight){ cardTop = Math.max(16, r.top - 190); }
      els.card.style.top = cardTop + 'px';
      els.card.style.left = '50%';

      // Guide orb perches at the top-right corner of the spotlighted element,
      // like it's the one pointing this out to you.
      els.guideOrb.style.left = (r.left + r.width - 22) + 'px';
      els.guideOrb.style.top  = (r.top - 22) + 'px';
      els.guideOrb.classList.add('show');
    }, 220);
  }

  function show(){
    var step = steps[idx];
    els.title.textContent = step.title;
    els.text.textContent  = step.text;
    els.next.textContent  = idx === steps.length - 1 ? "Let's go 💪" : 'Next →';
    renderDots();
    position();
  }

  function next(){
    if(idx >= steps.length - 1){ finish(false); return; }
    idx++; show();
  }

  function finish(skipped){
    var realOrb = document.getElementById('coach-orb');
    if(els.guideOrb && realOrb){
      var target = realOrb.getBoundingClientRect();
      els.guideOrb.style.left = target.left + 'px';
      els.guideOrb.style.top  = target.top + 'px';
      setTimeout(function(){
        els.guideOrb.classList.remove('show');
        realOrb.classList.remove('coach-hidden');
        setTimeout(function(){ if(els.guideOrb) els.guideOrb.remove(); els.guideOrb = null; }, 400);
      }, 500);
    } else if(realOrb){
      realOrb.classList.remove('coach-hidden');
    }
    if(els.overlay) els.overlay.remove();
    try{ localStorage.setItem('ff-tour-done-' + uid, '1'); }catch(e){}
  }

  return {
    start: function(stepList, userId){
      steps = stepList; idx = 0; uid = userId || 'anon';
      build(); show();
    },
    hasSeen: function(userId){
      try{ return localStorage.getItem('ff-tour-done-' + (userId||'anon')) === '1'; }catch(e){ return false; }
    },
    reset: function(userId){
      try{ localStorage.removeItem('ff-tour-done-' + (userId||'anon')); }catch(e){}
    }
  };
})();

// ── BEEP (rest timer alerts — no audio file needed) ───────────
var _ffAudioCtx = null;
function ffBeep(freq, durationMs){
  try{
    if(!_ffAudioCtx) _ffAudioCtx = new (window.AudioContext || window.webkitAudioContext)();
    var ctx = _ffAudioCtx;
    var osc = ctx.createOscillator(), gain = ctx.createGain();
    osc.type = 'sine'; osc.frequency.value = freq || 440;
    gain.gain.setValueAtTime(0.15, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + (durationMs||150)/1000);
    osc.connect(gain); gain.connect(ctx.destination);
    osc.start(); osc.stop(ctx.currentTime + (durationMs||150)/1000);
  }catch(e){ /* audio not available — silently skip */ }
}

// ── AI COACH ─────────────────────────────────────────────────
var Coach = (function(){
  var history = []; // [{role:'user'|'model', text}]
  var greeted = false;
  var els = {};

  function el(){
    els.backdrop = document.getElementById('coach-backdrop');
    els.orb      = document.getElementById('coach-orb');
    els.body     = document.getElementById('coach-body');
    els.input    = document.getElementById('coach-input');
  }

  function addMsg(text, who){
    var d = document.createElement('div');
    d.className = 'coach-msg ' + who;
    d.textContent = text;
    els.body.appendChild(d);
    els.body.scrollTop = els.body.scrollHeight;
    return d;
  }

  function addChips(options){
    var wrap = document.createElement('div');
    wrap.className = 'coach-chips';
    options.forEach(function(opt){
      var c = document.createElement('div');
      c.className = 'coach-chip';
      c.textContent = opt;
      c.onclick = function(){ wrap.remove(); sendText(opt); };
      wrap.appendChild(c);
    });
    els.body.appendChild(wrap);
    els.body.scrollTop = els.body.scrollHeight;
  }

  function addTyping(){
    var d = document.createElement('div');
    d.className = 'coach-msg bot typing';
    d.textContent = '···';
    els.body.appendChild(d);
    els.body.scrollTop = els.body.scrollHeight;
    return d;
  }

  function esc(s){
    return String(s == null ? '' : s).replace(/[&<>"']/g, function(c){
      return {'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c];
    });
  }

  function addProposal(program){
    var card = document.createElement('div');
    card.className = 'coach-propose';
    var daysHtml = (program.days||[]).map(function(d, idx){
      var exHtml = (d.exercises||[]).map(function(e){
        var label = e.name || ('Exercise #' + e.exerciseId);
        var scheme = e.sets + '×' + e.reps + (e.weightKg ? ' @ ' + e.weightKg + 'kg' : '');
        var rest = (e.restSeconds != null) ? (Math.round(e.restSeconds/60*10)/10 + 'm rest') : '';
        return '<div class="coach-propose-ex"><span>' + esc(label) + '</span><span>' + esc(scheme) +
          (rest ? ' · ' + esc(rest) : '') + '</span></div>';
      }).join('') || '<div class="coach-propose-ex"><span style="color:var(--muted)">No exercises — recovery day</span></div>';
      return '<div class="coach-propose-day">Day ' + (idx+1) + ' — ' + esc(d.name) + ' (' + esc(d.dayType) + ')</div>' + exHtml;
    }).join('');
    card.innerHTML =
      '<div class="coach-propose-head"><div class="coach-propose-title">' + esc(program.name||'New Program') + '</div>' +
      '<div class="coach-propose-days">' + (program.days||[]).length + ' days</div></div>' +
      (program.description ? '<div style="font-size:12px;color:var(--muted);margin-bottom:8px">' + esc(program.description) + '</div>' : '') +
      daysHtml +
      '<div class="coach-propose-actions">' +
        '<button class="btn btn-outline btn-sm" style="flex:1" onclick="Coach.tweak()">Tweak it</button>' +
        '<button class="btn btn-solid btn-sm" style="flex:1" onclick="Coach.apply(this)">✓ Create Program</button>' +
      '</div>';
    card._program = program;
    els.body.appendChild(card);
    els.body.scrollTop = els.body.scrollHeight;
  }

  function addInjuryLogged(injury){
    var card = document.createElement('div');
    card.className = 'coach-propose';
    card.innerHTML =
      '<div class="coach-propose-head"><div class="coach-propose-title">' + esc(injury.bodyPart) + '</div>' +
      '<div class="coach-propose-days">' + esc(injury.category) + '</div></div>' +
      (injury.notes ? '<div style="font-size:12px;color:var(--muted);margin-bottom:8px">' + esc(injury.notes) + '</div>' : '') +
      '<div class="coach-applied">✓ Logged — find it in Profile, exercises will be flagged automatically</div>';
    els.body.appendChild(card);
    els.body.scrollTop = els.body.scrollHeight;
  }

  function open(){
    el();
    els.backdrop.classList.add('open');
    els.orb.classList.add('coach-hidden');
    if(!greeted){
      greeted = true;
      var typing = addTyping();
      setTimeout(function(){
        typing.remove();
        addMsg("Hey — I'm your FitForge coach. Ask me how something works, or tell me to build you a program.", 'bot');
        addChips(["How do I build a program?", "Build me a program"]);
      }, 500);
    }
  }
  function close(){
    els.backdrop.classList.remove('open');
    els.orb.classList.remove('coach-hidden');
  }

  function sendText(text){
    history.push({ role:'user', text: text });
    var typing = addTyping();
    fetch('/Coach/Chat', {
      method:'POST', headers:{'Content-Type':'application/json'},
      body: JSON.stringify({ history: history })
    }).then(function(r){ return r.json(); }).then(function(d){
      typing.remove();
      history.push({ role:'model', text: JSON.stringify(d) });
      addMsg(d.message, 'bot');
      if(d.kind === 'question' && d.quickReplies && d.quickReplies.length){
        addChips(d.quickReplies);
      } else if(d.kind === 'proposal' && d.program && d.program.days && d.program.days.length){
        addProposal(d.program);
      } else if(d.kind === 'proposal'){
        addMsg("Hmm, that program didn't come through right — mind asking again?", 'bot');
      } else if(d.kind === 'injury_logged' && d.injury){
        addInjuryLogged(d.injury);
        showStackToast('Injury logged 🩹', 'success', 2500);
      }
    }).catch(function(){
      typing.remove();
      addMsg("Network hiccup — try that again?", 'bot');
    });
  }

  function send(){
    var text = els.input.value.trim();
    if(!text) return;
    addMsg(text, 'user');
    els.input.value = '';
    sendText(text);
  }

  function tweak(){
    addMsg("What would you like to change — the goal, days per week, or equipment?", 'bot');
  }

  function apply(btn){
    var card = btn.closest('.coach-propose');
    var program = card._program;
    btn.disabled = true; btn.textContent = 'Creating…';
    fetch('/Coach/ApplyProgram', {
      method:'POST', headers:{'Content-Type':'application/json'},
      body: JSON.stringify(program)
    }).then(function(r){ return r.json(); }).then(function(d){
      if(d.success){
        card.querySelector('.coach-propose-actions').outerHTML = '<div class="coach-applied">✓ ' + esc(d.msg || 'Created — find it in Programs and on your weekly schedule') + '</div>';
        showStackToast('Program created! 🎉', 'success', 2500);
      } else {
        btn.disabled = false; btn.textContent = '✓ Create Program';
        showStackToast(d.msg || 'Could not create program', 'danger');
      }
    }).catch(function(){
      btn.disabled = false; btn.textContent = '✓ Create Program';
      showStackToast('Network error', 'danger');
    });
  }

  return { open: open, close: close, send: send, tweak: tweak, apply: apply };
})();

// ── TOAST ────────────────────────────────────────────────────
function showToast(msg, type){
  var t=document.getElementById('toast');
  if(!t)return;
  t.textContent=msg;
  t.className='toast '+(type||'');
  t.classList.add('show');
  clearTimeout(t._timer);
  t._timer=setTimeout(function(){t.classList.remove('show');},3200);
}

// Auto-toasts injected by server (TempData)
document.querySelectorAll('.auto-toast').forEach(function(el){
  showToast(el.dataset.msg, el.dataset.type);
  el.remove();
});

// ── SWIPE-TO-CLOSE MODALS (mobile) ──────────────────────────
(function(){
  var startY=0, startScrollTop=0, isDragging=false;
  document.addEventListener('touchstart',function(e){
    var modal=e.target.closest('.modal');
    if(!modal)return;
    startY=e.touches[0].clientY;
    startScrollTop=modal.querySelector('.modal-body')?.scrollTop||0;
    isDragging=false;
  },{passive:true});

  document.addEventListener('touchmove',function(e){
    var modal=e.target.closest('.modal');
    if(!modal)return;
    var dy=e.touches[0].clientY-startY;
    var body=modal.querySelector('.modal-body');
    // Only allow drag-down when scrolled to top
    if(dy>0 && (!body||body.scrollTop<=0)){
      isDragging=true;
      modal.style.transform='translateY('+Math.min(dy*.6,160)+'px)';
      modal.style.transition='none';
    }
  },{passive:true});

  document.addEventListener('touchend',function(e){
    var modal=document.querySelector('.modal[style*="translateY"]');
    if(!modal)return;
    var dy=parseFloat(modal.style.transform.replace(/[^0-9.]/g,''))||0;
    modal.style.transition='';
    if(dy>60){
      modal.style.transform='translateY(100%)';
      modal.style.opacity='0';
      setTimeout(function(){
        var backdrop=modal.closest('.modal-backdrop');
        if(backdrop)backdrop.style.display='none';
        modal.style.transform='';modal.style.opacity='';
      },280);
    } else {
      modal.style.transform='';
    }
    isDragging=false;
  },{passive:true});
})();

// Prevent double-tap zoom on interactive elements (CSS touch-action handles click delay)
document.querySelectorAll('button,.btn,.nav-item,.tab-pill,.water-btn,.rpe-pill').forEach(function(el){
  el.style.touchAction='manipulation';
});
