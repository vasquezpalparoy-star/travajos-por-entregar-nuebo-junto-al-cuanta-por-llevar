
window.mobileTab=function(tab){if(tab!=='work')window.setScannerActive?.(false);for(const [name,id] of Object.entries({work:'work-panel',new:'new-panel',tools:'tools-panel'})){document.getElementById(id)?.classList.toggle('mobile-off',tab!==name)}document.querySelectorAll('[data-tab]').forEach(b=>b.classList.toggle('active',b.dataset.tab===tab));window.scrollTo({top:0,behavior:'smooth'});if(tab==='work')window.autoCamera?.()};
window.addEventListener('DOMContentLoaded',()=>{mobileTab('work');const section=document.createElement('div');section.id='connection-actions';section.innerHTML='<p>Los trabajos se sincronizan con tu cuenta de Grafiplot. Necesitas Internet para consultar y guardar cambios.</p><button id="configure-cloud">Cambiar cuenta</button><button id="signout-cloud">Cerrar sesión</button>';document.getElementById('tools-panel').append(section);document.getElementById('configure-cloud').onclick=()=>document.getElementById('cloud-gate').classList.remove('hidden');document.getElementById('signout-cloud').onclick=()=>window.dispatchEvent(new Event('grafiplot-signout'));});
window.androidBack=function(){const visible=[...document.querySelectorAll('.fixed.inset-0')].find(m=>m.id!=='cloud-gate'&&!m.classList.contains('hidden'));if(visible){visible.classList.add('hidden');return true}if(!document.getElementById('work-panel').classList.contains('mobile-off'))return false;mobileTab('work');return true};

window.grafiplotForeground=true;
window.grafiplotReady=false;
window.autoCamera=function(){
    if(!window.grafiplotForeground || !window.grafiplotReady) return;
    if(!document.getElementById('cloud-gate')?.classList.contains('hidden')) return;
    if(document.getElementById('work-panel')?.classList.contains('mobile-off')) return;
    const modal=[...document.querySelectorAll('.fixed.inset-0')].some(m=>m.id!=='cloud-gate'&&!m.classList.contains('hidden'));
    if(!modal)window.setScannerActive?.(true);
};
window.addEventListener('grafiplot-ready',()=>{window.grafiplotReady=true;window.autoCamera()});
window.addEventListener('grafiplot-pause',()=>{window.grafiplotForeground=false;window.setScannerActive?.(false)});
window.addEventListener('grafiplot-resume',()=>{window.grafiplotForeground=true;window.mobileTab('work')});
window.addEventListener('DOMContentLoaded',()=>{
    new MutationObserver(()=>{if(!document.getElementById('cloud-gate').classList.contains('hidden'))window.setScannerActive?.(false)}).observe(document.getElementById('cloud-gate'),{attributes:true,attributeFilter:['class']});
});
