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
