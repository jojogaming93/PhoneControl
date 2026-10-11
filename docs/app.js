// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

'use strict';
const guide = JSON.parse(document.getElementById('guide-data').textContent);
const hud = guide.hud;
const $ = (id) => document.getElementById(id);
const escapeHTML = (s) => String(s).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let selected = 'action-2';
let profile = 'saved';
const routeLabels = {atlas:'HUD explorer',evidence:'Evidence notes',...Object.fromEntries(guide.chapters.map(c=>[c.slug,c.title]))};
function navigate() {
  let route = location.hash.slice(1) || 'atlas';
  if (!Object.hasOwn(routeLabels,route)) route='atlas';
  document.querySelectorAll('.route-page,.chapter').forEach(e=>e.hidden=true);
  const target = $(route) || $('chapter-'+route);
  target.hidden=false;
  document.querySelectorAll('nav a').forEach(a=>{const active=a.dataset.route===route;a.classList.toggle('active',active);if(active)a.setAttribute('aria-current','page');else a.removeAttribute('aria-current');});
  $('breadcrumb').textContent=routeLabels[route];
  document.title=routeLabels[route]+' · PhoneControl field guide';
  $('search-results').hidden=true;
  window.scrollTo(0,0);
}
function renderAtlas() {
  const keyField=profile==='saved'?'savedKey':'starterKey', modeField=profile==='saved'?'savedMode':'starterMode';
  $('markers').replaceChildren();$('action-list').replaceChildren();
  hud.entries.forEach(e=>{
    const marker=document.createElement('button');
    marker.className='marker'+(e.id===selected?' selected':'')+(e.status==='Contextual'?' contextual':'');
    marker.textContent=e.number;marker.style.left=(e.x*100)+'%';marker.style.top=(e.y*100)+'%';
    marker.title=e.name+' · '+e[keyField];marker.setAttribute('aria-label',e.name+' at '+e.x+', '+e.y);marker.setAttribute('aria-pressed',e.id===selected);
    marker.addEventListener('click',ev=>{ev.stopPropagation();selected=e.id;renderAtlas();});
    $('markers').append(marker);
    const item=document.createElement('button');item.className=e.id===selected?'selected':'';item.innerHTML='<span>'+e.number+'</span> '+escapeHTML(e.name);
    item.setAttribute('aria-pressed',e.id===selected);item.addEventListener('click',()=>{selected=e.id;renderAtlas();});$('action-list').append(item);
  });
  const e=hud.entries.find(e=>e.id===selected), px=Math.round(e.x*(hud.width-1)),py=Math.round(e.y*(hud.height-1));
  $('detail').innerHTML='<span class="target-number">TARGET '+String(e.number).padStart(2,'0')+'</span><h3>'+escapeHTML(e.name)+'</h3><span class="tag">'+escapeHTML(e.status)+'</span><span class="binding">'+escapeHTML(e[keyField])+' / '+escapeHTML(e[modeField])+'</span><div class="coordinates"><div><span>X / NORMALIZED</span><strong>'+e.x.toFixed(3)+'</strong></div><div><span>Y / NORMALIZED</span><strong>'+e.y.toFixed(3)+'</strong></div></div><p class="physical">'+px+', '+py+' px · logical finger '+e.logicalId+'</p><p>'+escapeHTML(e.note)+'</p><a href="profiles/the tested game-'+(profile==='saved'?'example':'starter')+'.json" download>Download this profile ↗</a>';
}
$('profile').addEventListener('change',ev=>{profile=ev.target.value;renderAtlas();});
$('hud-stage').addEventListener('click',ev=>{
  const r=$('hud-stage').getBoundingClientRect(),x=Math.max(0,Math.min(1,(ev.clientX-r.left)/r.width)),y=Math.max(0,Math.min(1,(ev.clientY-r.top)/r.height));
  $('crosshair').hidden=false;$('crosshair').style.left=(x*100)+'%';$('crosshair').style.top=(y*100)+'%';
  $('pick-output').textContent='x '+x.toFixed(3)+' / y '+y.toFixed(3)+' · '+Math.round(x*(hud.width-1))+', '+Math.round(y*(hud.height-1))+' px';
});
$('search').addEventListener('input',ev=>{
  const terms=ev.target.value.trim().toLowerCase().split(/\s+/).filter(Boolean),panel=$('search-results');panel.replaceChildren();
  if(!terms.length){panel.hidden=true;return;}
  const hits=guide.chapters.filter(c=>terms.every(t=>(c.title+' '+c.text).toLowerCase().includes(t)));
  hits.forEach(c=>{const a=document.createElement('a');a.href='#'+c.slug;const plain=c.text.replace(/[#*`|]/g,' ').replace(/\s+/g,' ');const at=plain.toLowerCase().indexOf(terms[0]);const start=Math.max(0,at-45);const title=document.createElement('strong');title.textContent=c.title;const excerpt=document.createElement('small');excerpt.textContent=(start?'…':'')+plain.slice(start,start+170)+'…';a.append(title,excerpt);a.addEventListener('click',()=>panel.hidden=true);panel.append(a);});
  if(!hits.length){const p=document.createElement('p');p.textContent='No chapter matches. Try a shorter term.';panel.append(p);}panel.hidden=false;
});
document.addEventListener('keydown',ev=>{if(ev.key==='Escape')$('search-results').hidden=true;});
document.addEventListener('click',ev=>{if(!ev.target.closest('.search-wrap'))$('search-results').hidden=true;});
window.addEventListener('hashchange',navigate);
renderAtlas();navigate();
