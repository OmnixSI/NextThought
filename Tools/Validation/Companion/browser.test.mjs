import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import {webcrypto} from 'node:crypto';
import assert from 'node:assert/strict';
const swift=readFileSync(new URL('../../../Sources/InterviewHacker/CompanionServer.swift',import.meta.url),'utf8');
const script=swift.match(/<script>([\s\S]*?)<\/script>/)[1];
const elements=new Map(),intervals=[],events={},requests=[];
const document={hidden:false,getElementById(id){if(!elements.has(id))elements.set(id,{style:{},textContent:'',innerHTML:'',hidden:false});return elements.get(id)},addEventListener(k,f){events[k]=f}};
let active;
class EventSource{constructor(){this.handlers={};active=this}addEventListener(k,f){this.handlers[k]=f}close(){this.closed=true}}
let now=100000;
const frame=(n,q,a,extra={})=>({version:1,serverID:'test',sequence:n,snapshot:{question:q,answer:a,answering:!a,status:'正在聆听',position:'',...extra}});
const context={document,EventSource,Date:{now:()=>now},URLSearchParams,location:{search:'?t=token'},crypto:webcrypto,
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
await context.act('next');
const command=requests.find(r=>r.url.startsWith('/action'));
assert.match(command.url,/id=[0-9a-f-]{36}/);
assert.equal(command.options.method,'POST');
console.log('Companion browser regression: initial snapshot, heartbeat, stale ordering, empty new answer, stall polling, SSE failure and command IDs passed.');
