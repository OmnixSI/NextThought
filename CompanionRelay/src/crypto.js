import {gcm} from '@noble/ciphers/aes.js';
import {hkdf} from '@noble/hashes/hkdf.js';
import {sha256} from '@noble/hashes/sha2.js';
const utf8 = new TextEncoder(), text = new TextDecoder();
export const b64 = bytes => { let s=''; for(let i=0;i<bytes.length;i+=8192)s+=String.fromCharCode(...bytes.subarray(i,i+8192));return btoa(s); };
export const unb64 = value => Uint8Array.from(atob(value.replace(/-/g,'+').replace(/_/g,'/')), x => x.charCodeAt(0));
function key(pair, kind) {
 const master=unb64(pair.key); if(master.length!==32)throw Error('invalid pairing key');
 return hkdf(sha256,master,utf8.encode(pair.room),utf8.encode('ih-companion-v1/'+kind),32);
}
export function seal(pair,kind,value,nonce=crypto.getRandomValues(new Uint8Array(12))) {
 const cipher=gcm(key(pair,kind),nonce,utf8.encode(pair.room+'/'+kind)).encrypt(utf8.encode(JSON.stringify(value)));
 return {v:1,kind,n:b64(nonce),body:b64(cipher)};
}
export function open(pair,packet) {
 if(packet.v!==1||!['state','command','ack'].includes(packet.kind)||packet.body.length>350000)throw Error('invalid frame');
 return JSON.parse(text.decode(gcm(key(pair,packet.kind),unb64(packet.n),utf8.encode(pair.room+'/'+packet.kind)).decrypt(unb64(packet.body))));
}
