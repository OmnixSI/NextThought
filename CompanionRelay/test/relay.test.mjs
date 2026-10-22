import {test} from 'node:test';
import assert from 'node:assert/strict';
import {once} from 'node:events';
import {WebSocket} from 'ws';
import {createCipheriv,hkdfSync,randomBytes} from 'node:crypto';
import {createRelay} from '../dist/server.js';
import {seal,open} from '../src/crypto.js';
const wait=ms=>new Promise(r=>setTimeout(r,ms));
async function fixture(t,options={}){
 const relay=createRelay(options);relay.server.listen(0,'127.0.0.1');await once(relay.server,'listening');
 const base='http://127.0.0.1:'+relay.server.address().port;
 assert.match((await fetch(base)).headers.get('content-security-policy'),/img-src 'self' data:/);
 const room=await(await fetch(base+'/rooms',{method:'POST'})).json();
 const pair={...room,key:randomBytes(32).toString('base64')};
 t.after(()=>new Promise(resolve=>{for(const r of relay.rooms.values())for(const ws of r.sockets.keys())ws.terminate();relay.server.close(resolve)}));
 async function join(role='subscriber',token=room[role]){const ws=new WebSocket(base.replace('http','ws')+'/socket');const messages=[];ws.on('message',d=>messages.push(JSON.parse(d)));await once(ws,'open');ws.send(JSON.stringify({room:room.room,role,token}));await wait(25);return{ws,messages};}
 return{...relay,base,room,pair,join};
}
test('AES-GCM/HKDF bytes match Node crypto, reject tamper/direction/room',()=>{
 const pair={room:'room-one',key:Buffer.alloc(32,7).toString('base64')},nonce=new Uint8Array(12).fill(4),value={question:'secret 可靠 -3.7 dBm'};
 const packet=seal(pair,'state',value,nonce),key=hkdfSync('sha256',Buffer.alloc(32,7),'room-one','ih-companion-v1/state',32);
 const cipher=createCipheriv('aes-256-gcm',key,nonce);cipher.setAAD(Buffer.from('room-one/state'));
 const expected=Buffer.concat([cipher.update(JSON.stringify(value)),cipher.final(),cipher.getAuthTag()]);
 assert.equal(packet.body,expected.toString('base64'));assert.deepEqual(open(pair,packet),value);
 assert.throws(()=>open(pair,{...packet,kind:'command'}));assert.throws(()=>open({...pair,room:'other'},packet));
 const bad=Buffer.from(packet.body,'base64');bad[0]^=1;assert.throws(()=>open(pair,{...packet,body:bad.toString('base64')}));
});
test('real sockets: private state, reconnect full snapshot, encrypted command/ack',async t=>{
 const f=await fixture(t),pub=await f.join('publisher'),sub=await f.join();
 const envelope={version:1,serverID:'mac-one',sequence:3,updatedAt:Date.now(),snapshot:{question:'private-question',answer:'private-answer'}};
 pub.ws.send(JSON.stringify(seal(f.pair,'state',envelope)));await wait(30);
 assert.deepEqual(open(f.pair,sub.messages.at(-1)),envelope);
 const room=f.rooms.get(f.room.room);assert(!room.latest.includes('private-question'));assert(!room.latest.includes('private-answer'));
 sub.ws.close();await once(sub.ws,'close');const recovered=await f.join();assert.deepEqual(open(f.pair,recovered.messages.at(-1)),envelope);
 const cmd={id:'05b46748-0ab9-46e6-bdfc-6da156e97b5f',action:'next',createdAt:Date.now()};
 recovered.ws.send(JSON.stringify(seal(f.pair,'command',cmd)));await wait(25);assert.deepEqual(open(f.pair,pub.messages.at(-1)),cmd);
 pub.ws.send(JSON.stringify(seal(f.pair,'ack',{id:cmd.id})));await wait(25);assert.equal(open(f.pair,recovered.messages.at(-1)).id,cmd.id);
 assert.equal((await fetch(f.base+'/rooms/'+f.room.room+'/close',{method:'POST',headers:{Authorization:'Bearer '+f.room.publisher}})).status,204);
 assert.equal(f.rooms.size,0);
});
test('auth, role, plaintext rejection, expiration and capacity',async t=>{
 const f=await fixture(t,{ttl:300,capacity:1});assert.equal((await fetch(f.base+'/rooms',{method:'POST'})).status,429);
 const bad=await f.join('subscriber','wrong');assert.equal(bad.ws.readyState,WebSocket.CLOSED);
 const sub=await f.join();sub.ws.send(JSON.stringify(seal(f.pair,'state',{text:'not allowed'})));await wait(25);assert.equal(sub.ws.readyState,WebSocket.CLOSED);
 const pub=await f.join('publisher');pub.ws.send(JSON.stringify({question:'plaintext'}));await wait(25);assert.equal(pub.ws.readyState,WebSocket.CLOSED);
 await wait(650);assert.equal(f.rooms.size,0);
});
