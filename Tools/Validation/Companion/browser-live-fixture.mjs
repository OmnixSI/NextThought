// Local, authored browser QA only. Never reads production app data.
import {WebSocket} from '../../../CompanionRelay/node_modules/ws/wrapper.mjs';
import {seal,open} from '../../../CompanionRelay/src/crypto.js';
import {randomBytes} from 'node:crypto';
const base='http://127.0.0.1:8787', room=await(await fetch(base+'/rooms',{method:'POST'})).json();
const pair={v:1,relay:base,room:room.room,subscriber:room.subscriber,key:randomBytes(32).toString('base64')};
const ws=new WebSocket(base.replace('http','ws')+'/socket');
let sequence=1,answer='密文链路验证：可靠覆盖率 ≥ 95%，误差 −3.7 dBm。';
ws.on('open',()=>{ws.send(JSON.stringify({room:room.room,role:'publisher',token:room.publisher}));publish();});
function publish(){if(ws.readyState===1)ws.send(JSON.stringify(seal(pair,'state',{version:1,serverID:'browser-qa',sequence,updatedAt:Date.now(),snapshot:{status:'本地验证',question:'加密中转是否完整传递当前问题？',answer,answering:false,position:'1 / 1',sources:'人工构造 QA 样本'}})))}
ws.on('message',raw=>{const frame=JSON.parse(raw);if(frame.kind==='command'){const cmd=open(pair,frame);answer='已确认操作：'+cmd.action+'；同一命令 ID 可跨通道重试。';sequence++;ws.send(JSON.stringify(seal(pair,'ack',{id:cmd.id})));publish();}});
setInterval(publish,2000);
console.log(base+'/#p='+Buffer.from(JSON.stringify(pair)).toString('base64url'));
process.on('SIGINT',async()=>{await fetch(base+'/rooms/'+room.room+'/close',{method:'POST',headers:{Authorization:'Bearer '+room.publisher}});ws.close();process.exit(0)});
