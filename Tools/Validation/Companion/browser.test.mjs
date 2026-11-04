import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import {webcrypto} from 'node:crypto';
import assert from 'node:assert/strict';
const script=readFileSync(new URL('../../../CompanionRelay/src/phone.js',import.meta.url),'utf8').replace(/^import .*$/m,'').replace('export function apply','function apply');
const elements=new Map(),intervals=[],events={},requests=[];
const document={documentElement:{},hidden:false,querySelectorAll(){return []},getElementById(id){if(!elements.has(id))elements.set(id,{style:{},textContent:'',innerHTML:'',hidden:false});return elements.get(id)},addEventListener(k,f){events[k]=f}};
let active;
class EventSource{constructor(){this.handlers={};active=this}addEventListener(k,f){this.handlers[k]=f}close(){this.closed=true}}
let now=100000;
const frame=(n,q,a,extra={})=>({version:1,serverID:'test',sequence:n,snapshot:{question:q,answer:a,answering:!a,status:'正在聆听',position:'',...extra}});
const context={navigator:{language:'zh-CN'},document,EventSource,Date:{now:()=>now},TextDecoder,URLSearchParams,location:{search:'?t=token',hash:'',protocol:'http:'},crypto:webcrypto,
 localStorage:{getItem:()=>null,setItem(){}},window:{addEventListener(k,f){events[k]=f}},
 setInterval(f,n){intervals.push({f,n})},setTimeout(){return 1},clearTimeout(){},Math,AbortController,
 async fetch(url,options){requests.push({url,options});return {ok:true,status:200,json:async()=>frame(1,'初始','答案')}}};
vm.runInNewContext(script,context);
await new Promise(r=>setImmediate(r));
assert.equal(elements.get('q').textContent,'初始');
active.onmessage({data:JSON.stringify(frame(3,'新问题',''))});
assert.equal(elements.get('a').innerHTML,'正在生成…');
active.onmessage({data:JSON.stringify(frame(2,'旧问题','旧答案'))});
assert.equal(elements.get('q').textContent,'新问题');
active.handlers.heartbeat({data:JSON.stringify(frame(4,'新问题','完整回答'))});
assert.equal(elements.get('a').innerHTML,'完整回答');
now+=9000;intervals.find(x=>x.n===1000).f();
await new Promise(r=>setImmediate(r));
assert(requests.filter(r=>r.url.startsWith('/snapshot')).length>=2);
active.onerror();
await new Promise(r=>setImmediate(r));
assert(active.closed);
context.window.act('next');
await new Promise(r=>setImmediate(r));
const command=requests.find(r=>r.url.startsWith('/action'));
assert.match(command.url,/id=[0-9a-f-]{36}/);
assert.equal(command.options.method,'POST');
console.log('Companion browser regression: initial snapshot, heartbeat, stale ordering, empty new answer, stall polling, SSE failure and command IDs passed.');
// HTTPS bookmarks use WSS alone; a repeated ciphertext cannot keep health alive.
const relayElements=new Map(),relayIntervals=[],relayRequests=[],relaySends=[];
let relaySocket;
const pair={v:1,relay:'https://relay.example',room:'room',subscriber:'subscriber',key:'test-key'};
const fragment=Buffer.from(JSON.stringify(pair)).toString('base64url');
const relayContext={...context,
 location:{protocol:'https:',hash:'#p='+fragment,search:''},URL,
 unb64:value=>Buffer.from(value,'base64url'),
 open:(_pair,packet)=>packet.value,seal:(_pair,kind,value)=>({kind,value}),
 WebSocket:class{constructor(url){this.url=url;this.readyState=1;relaySocket=this}send(value){relaySends.push(JSON.parse(value))}close(){this.readyState=3}},
 document:{documentElement:{},hidden:false,querySelectorAll:()=>[],getElementById(id){if(!relayElements.has(id))relayElements.set(id,{style:{},textContent:'',innerHTML:'',hidden:false});return relayElements.get(id)},addEventListener(){}},
 window:{addEventListener(){}},setInterval(f,n){relayIntervals.push({f,n})},async fetch(url){relayRequests.push(url);throw Error('HTTPS page must never fetch LAN')}
};
vm.runInNewContext(script,relayContext);relaySocket.onopen();
assert.equal(relaySocket.url.protocol,'wss:');assert.equal(relayRequests.length,0);
relaySocket.onmessage({data:JSON.stringify({v:1,kind:'state',n:'once',value:frame(8,'中转问题','中转答案')})});
assert.equal(relayElements.get('q').textContent,'中转问题');
relayContext.window.act('next');await new Promise(r=>setImmediate(r));
const sent=relaySends.find(p=>p.kind==='command');assert.match(sent.value.id,/^[0-9a-f-]{36}$/);
relaySocket.onmessage({data:JSON.stringify({v:1,kind:'ack',n:'ack-once',value:{id:sent.value.id}})});
const count=relaySends.length;now+=9000;
relaySocket.onmessage({data:JSON.stringify({v:1,kind:'state',n:'once',value:frame(99,'重放旧密文','伪更新')})});
relayIntervals.find(x=>x.n===1000).f();await new Promise(r=>setImmediate(r));
assert.equal(relayElements.get('q').textContent,'中转问题');assert.match(relayElements.get('s').textContent,/离线/);
assert.equal(relaySends.length,count);assert.equal(relayRequests.length,0);
console.log('HTTPS relay browser regression: WSS-only, state decrypt routing, encrypted command ack and repeated ciphertext health rejection passed.');

// A normal state/heartbeat cannot erase an operation timeout. Retry reuses its UUID.
relayContext.window.act('shorter'); await new Promise(r=>setImmediate(r));
const pending=relaySends.at(-1);assert.equal(pending.kind,'command');
assert.match(relayElements.get('operation').textContent,/正在发送/);
relayContext.window.act('shorter');assert.equal(relaySends.at(-1),pending);
now+=16000;relayIntervals.find(x=>x.n===1000).f();await new Promise(r=>setImmediate(r));
assert.match(relayElements.get('operation').textContent,/未确认/);
relaySocket.onmessage({data:JSON.stringify({v:1,kind:'state',n:'fresh-health',value:frame(10,'在线','答案')})});
assert.match(relayElements.get('s').textContent,/中转/);assert.match(relayElements.get('operation').textContent,/未确认/);
relayContext.window.retryAction();await new Promise(r=>setImmediate(r));
assert.equal(relaySends.at(-1).value.id,pending.value.id);
relaySocket.onmessage({data:JSON.stringify({v:1,kind:'ack',n:'retry-ack',value:{id:pending.value.id}})});
assert.match(relayElements.get('operation').textContent,/Mac 已确认/);
console.log('Operation feedback: pending suppression, sticky timeout, UUID-preserving retry and authenticated confirmation passed.');
