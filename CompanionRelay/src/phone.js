import {seal,open,unb64} from './crypto.js';
const locale=/^zh/i.test(navigator.language||'')?'zh':'en';
const ui={
 connecting:['连接中…','Connecting…'],decreaseAnswerFont:['减小回答字号','Decrease answer text size'],increaseAnswerFont:['增大回答字号','Increase answer text size'],
 answerHistory:['回答记录','Answer history'],previous:['上一条','Previous'],next:['下一条','Next'],answerPosition:['当前回答位置','Current answer position'],
 suggestedAnswer:['建议回答','Suggested answer'],connectMacPlaceholder:['连接 Mac 后，当前问题和回答会显示在这里。','Connect to your Mac to see the current question and answer here.'],
 showVerificationUpdate:['显示核查更新','Show verification update'],answerActions:['回答操作','Answer actions'],retryAction:['重试操作','Retry action'],answer:['回答','Answer'],
 stopGenerating:['停止生成','Stop generating'],shorter:['更简短','Shorter'],example:['举例','Example'],englishVersion:['英文版','English version'],
 otherCommandsPending:['等待其他操作确认…','Waiting for other actions to be confirmed…'],commandConfirmed:['Mac 已确认操作','Mac confirmed the action'],commandUnconfirmed:['操作未确认，请重试','Action not confirmed. Try again.'],
 liveSync:['已连接 · 实时同步','Connected · Live sync'],snapshotSync:['备用连接 · 快照同步','Backup connection · Snapshot sync'],offlineRetry:['离线，正在恢复…','Offline · Reconnecting…'],
 relayValidationFailed:['中转校验失败，请重新配对','Relay validation failed. Pair again.'],relayAddressInvalid:['中转地址或配对信息无效','Invalid relay address or pairing information'],
 relayTLSRequired:['中转连接需要 TLS','The relay connection requires TLS'],relaySnapshotSync:['备用连接 · 加密中转','Backup connection · Encrypted relay'],
 reconnecting:['正在恢复连接…','Reconnecting…'],scanPairingCode:['请扫描 Mac 上的配对二维码','Scan the pairing QR code on your Mac'],
 commandExpired:['操作已过期，请先核对 Mac 当前状态','This action expired. Check the current state on your Mac first.'],
 retrying:['正在重试，等待 Mac 确认…','Retrying · Waiting for Mac confirmation…'],sending:['正在发送操作，等待 Mac 确认…','Sending action · Waiting for Mac confirmation…'],
 checkConnection:['操作未确认，请检查连接后重试','Action not confirmed. Check the connection and try again.'],generating:['正在生成…','Generating…'],waitingForAnswer:['等待回答…','Waiting for an answer…']
};
const t=key=>ui[key]?.[locale==='zh'?0:1]||key;
document.documentElement.lang=locale==='zh'?'zh-CN':'en';
for(const node of document.querySelectorAll('[data-i18n]'))node.textContent=t(node.dataset.i18n);
for(const node of document.querySelectorAll('[data-i18n-aria]'))node.setAttribute('aria-label',t(node.dataset.i18nAria));
const el=id=>document.getElementById(id);
let pair={}; try{pair=JSON.parse(new TextDecoder().decode(unb64(new URLSearchParams(location.hash.slice(1)).get('p')||'')))}catch{}
const token=pair.token||new URLSearchParams(location.search).get('t');
let size=+(localStorage.getItem('ih-size')||20);const A=el('a');A.style.fontSize=size+'px';
window.zoom=d=>{size=Math.max(14,Math.min(40,size+d));A.style.fontSize=size+'px';localStorage.setItem('ih-size',size)};
function esc(s){return s.replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]))}
function md(s){return esc(s).replace(/\*\*(.+?)\*\*/g,'<strong>$1</strong>').replace(/`([^`]+)`/g,'<code>$1</code>')}
let es=null,retry=null,attempt=0,lastEvent=0,lastGood=0,lastLAN=0,server='',sequence=-1,pendingPoll=false;
let ws=null,relayTimer=null,relayAttempt=0,relayFrame=null,relayGood=0;const commands=new Map(),retired=new Set(),seenNonces=new Set();
let failedCommand=null;
function operation(label){const output=el('operation');if(output)output.textContent=label;
 for(const button of document.querySelectorAll('[data-action]'))button.disabled=button.dataset.action!=='cancel'&&[...commands.values()].some(c=>c.action===button.dataset.action);
 for(const button of document.querySelectorAll('[data-retry]'))button.hidden=!failedCommand;
}
function confirmed(id){if(!commands.has(id)&&failedCommand?.id!==id)return;commands.delete(id);if(failedCommand?.id===id)failedCommand=null;if(!failedCommand)operation(commands.size?t('otherCommandsPending'):t('commandConfirmed'));else operation(el('operation')?.textContent||t('commandUnconfirmed'))}
function connection(label,live=false){el('s').textContent=label;el('h').className=live?'live':''}
export function apply(e,transport){
 if(!e||e.version!==1||!e.snapshot||typeof e.sequence!=='number'||typeof e.serverID!=='string')return;
 if(retired.has(e.serverID)||(server===e.serverID&&e.sequence<sequence))return;
 if(server&&server!==e.serverID)retired.add(server);
 server=e.serverID;sequence=e.sequence;lastGood=Date.now();connection(transport,true);
 const d=e.snapshot;el('mode').textContent=d.status+(d.answering?' · 生成中':'');el('p').textContent=d.position;
 el('q').textContent=d.question;A.innerHTML=d.answer?md(d.answer):(d.answering?t('generating'):t('waitingForAnswer'));
 el('cand').textContent=d.candidate?((locale==='zh'?'候选：':'Candidate: ')+d.candidate):'';el('sources').textContent=[d.slide,d.sources].filter(Boolean).join(' · ');el('update').hidden=!d.updateAvailable;
}
// HTTPS relay pages never probe LAN HTTP (mixed content and private network restrictions).
const lan=location.protocol==='http:'&&!!token;
async function request(path,options={}){
 const controller=new AbortController(),timeout=setTimeout(()=>controller.abort(),2500);
 try{const r=await fetch(path,{...options,cache:'no-store',signal:controller.signal});if(!r.ok)throw Error('HTTP '+r.status);return r}finally{clearTimeout(timeout)}
}
async function snapshot(){if(!lan||pendingPoll||document.hidden)return;pendingPoll=true;
 try{const frame=await(await request('/snapshot?t='+encodeURIComponent(token))).json();lastLAN=Date.now();apply(frame,t('snapshotSync'))}
 catch{if(Date.now()-lastGood>8000)connection(t('offlineRetry'))}finally{pendingPoll=false}}
function retrySSE(){if(!lan||retry)return;retry=setTimeout(()=>{retry=null;connect()},Math.min(8000,500*2**attempt++)*(.8+Math.random()*.4))}
function connect(){if(!lan)return;if(es)es.close();es=new EventSource('/events?t='+encodeURIComponent(token));
 const received=e=>{try{lastEvent=lastLAN=Date.now();attempt=0;apply(JSON.parse(e.data),t('liveSync'))}catch{}};
 es.onmessage=received;es.addEventListener('heartbeat',received);es.onerror=()=>{es.close();connection(t('reconnecting'));snapshot();retrySSE()};}
function relay(){
 if(!pair.relay||!pair.room||!pair.key)return;
 try{const url=new URL('/socket',pair.relay);url.protocol=url.protocol==='https:'?'wss:':'ws:';
 if(location.protocol!=='http:'&&url.protocol!=='wss:')throw Error(t('relayTLSRequired'));
 if(ws)ws.close();const socket=new WebSocket(url);ws=socket;
 socket.onopen=()=>{relayAttempt=0;socket.send(JSON.stringify({room:pair.room,role:'subscriber',token:pair.subscriber}));};
 socket.onmessage=e=>{try{const p=JSON.parse(e.data);if(p.joined)return;const value=open(pair,p);
 if(p.kind==='state'){if(seenNonces.has(p.n))return;seenNonces.add(p.n);if(seenNonces.size>16384)seenNonces.delete(seenNonces.values().next().value);relayFrame=value;relayGood=Date.now();if(Date.now()-lastLAN>4000)apply(value,t('relaySnapshotSync'))}
 if(p.kind==='ack'&&typeof value.id==='string')confirmed(value.id);
 }catch{connection(t('relayValidationFailed'))}};
 socket.onclose=()=>{if(ws!==socket)return;clearTimeout(relayTimer);relayTimer=setTimeout(relay,Math.min(8000,500*2**relayAttempt++)*(.8+Math.random()*.4))};
 socket.onerror=()=>socket.close();
 }catch{connection(t('relayAddressInvalid'))}
}
function uuid(){return crypto.randomUUID?crypto.randomUUID():'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g,c=>{const r=crypto.getRandomValues(new Uint8Array(1))[0]&15;return(c==='x'?r:(r&3)|8).toString(16)})}
async function sendCommand(cmd){
 if(lan){try{await request('/action?t='+encodeURIComponent(token)+'&a='+cmd.action+'&id='+cmd.id,{method:'POST'});confirmed(cmd.id);snapshot();return}catch{}}
 if(ws?.readyState===1)ws.send(JSON.stringify(seal(pair,'command',cmd)));
}
window.retryAction=()=>{if(!failedCommand)return;if(Date.now()-failedCommand.firstCreatedAt>=240000){failedCommand=null;operation(t('commandExpired'));return}
 const cmd=failedCommand;failedCommand=null;cmd.createdAt=Date.now();commands.set(cmd.id,cmd);operation(t('retrying'));sendCommand(cmd)};
window.act=action=>{if([...commands.values()].some(c=>c.action===action))return;if(failedCommand?.action===action){window.retryAction();return}
 const cmd={id:uuid(),action,createdAt:Date.now(),firstCreatedAt:Date.now()};commands.set(cmd.id,cmd);operation(t('sending'));sendCommand(cmd)};
setInterval(()=>{
 if(document.hidden)return;
 if(lan&&Date.now()-lastEvent>6000){snapshot();if(es)es.close();retrySSE()}
 if(relayFrame&&Date.now()-lastLAN>4000&&Date.now()-relayGood<8000)apply(relayFrame,t('relaySnapshotSync'));
 if(Date.now()-lastGood>8000)connection(t('offlineRetry'));
 for(const [id,cmd] of commands){if(Date.now()-cmd.createdAt>15000){commands.delete(id);failedCommand=cmd;operation(t('checkConnection'))}else sendCommand(cmd)}
},1000);
function resume(){if(document.hidden)return;snapshot();connect();if(!ws||ws.readyState>1)relay()}
document.addEventListener('visibilitychange',resume);window.addEventListener('online',resume);window.addEventListener('pageshow',resume);
if(!lan&&!pair.relay)connection(t('scanPairingCode'));
snapshot();connect();relay();

for(const button of document.querySelectorAll("[data-action]"))button.addEventListener("click",()=>window.act(button.dataset.action));
for(const button of document.querySelectorAll("[data-zoom]"))button.addEventListener("click",()=>window.zoom(Number(button.dataset.zoom)));

for(const button of document.querySelectorAll('[data-retry]'))button.addEventListener('click',()=>window.retryAction());
