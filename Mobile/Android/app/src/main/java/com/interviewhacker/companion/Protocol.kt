package com.interviewhacker.companion

import java.nio.ByteBuffer
import java.security.SecureRandom
import java.util.Base64
import java.util.UUID
import javax.crypto.Cipher
import javax.crypto.Mac
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec
import org.json.JSONObject

object Protocol {
    val service: UUID = UUID.fromString("9A8F0001-1C28-4E55-BE7D-C4E9A13277B0")
    val state: UUID = UUID.fromString("9A8F0002-1C28-4E55-BE7D-C4E9A13277B0")
    val command: UUID = UUID.fromString("9A8F0003-1C28-4E55-BE7D-C4E9A13277B0")
    val cccd: UUID = UUID.fromString("00002902-0000-1000-8000-00805f9b34fb")
    private val rng = SecureRandom()
    private fun hmac(key: ByteArray, data: ByteArray): ByteArray = Mac.getInstance("HmacSHA256").run { init(SecretKeySpec(key,"HmacSHA256")); doFinal(data) }
    private fun key(pair: JSONObject, kind: String): ByteArray {
        val master = Base64.getDecoder().decode(pair.getString("key")); require(master.size == 32)
        val prk = hmac(pair.getString("room").toByteArray(Charsets.UTF_8), master)
        return hmac(prk, "ih-companion-v1/$kind".toByteArray(Charsets.UTF_8) + byteArrayOf(1))
    }
    fun seal(pair: JSONObject, kind: String, value: JSONObject): JSONObject = sealRaw(pair, kind, value.toString().toByteArray(Charsets.UTF_8))
    fun sealRaw(pair: JSONObject, kind: String, value: ByteArray, nonce: ByteArray = ByteArray(12).also(rng::nextBytes)): JSONObject {
        require(kind in listOf("state","command","ack") && value.size <= 256*1024 && nonce.size == 12)
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, SecretKeySpec(key(pair,kind),"AES"), GCMParameterSpec(128,nonce))
        cipher.updateAAD((pair.getString("room")+"/"+kind).toByteArray(Charsets.UTF_8))
        return JSONObject().put("v",1).put("kind",kind).put("n",Base64.getEncoder().encodeToString(nonce)).put("body",Base64.getEncoder().encodeToString(cipher.doFinal(value)))
    }
    fun openRaw(pair: JSONObject, frame: JSONObject): ByteArray {
        val kind = frame.getString("kind"); require(frame.getInt("v")==1 && kind in listOf("state","command","ack"))
        val encoded = frame.getString("body"); require(encoded.length <= 350000)
        val nonce = Base64.getDecoder().decode(frame.getString("n")); require(nonce.size == 12)
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE,SecretKeySpec(key(pair,kind),"AES"),GCMParameterSpec(128,nonce))
        cipher.updateAAD((pair.getString("room")+"/"+kind).toByteArray(Charsets.UTF_8))
        return cipher.doFinal(Base64.getDecoder().decode(encoded))
    }
    fun open(pair: JSONObject, frame: JSONObject): JSONObject = JSONObject(String(openRaw(pair,frame),Charsets.UTF_8))
    fun fragments(data: ByteArray, id: Int, mtu: Int): List<ByteArray> {
        if(data.isEmpty() || data.size > 384*1024 || mtu < 20) return emptyList()
        val payload = minOf(512,mtu)-8; val count = (data.size+payload-1)/payload
        if(count>32768)return emptyList()
        return (0 until count).map { i -> ByteBuffer.allocate(8+minOf(payload,data.size-i*payload)).putInt(id).putShort(i.toShort()).putShort(count.toShort()).put(data,i*payload,minOf(payload,data.size-i*payload)).array() }
    }
    class Assembler {
        private var id: Int? = null; private var total = 0; private var started = 0L; private val chunks = mutableMapOf<Int,ByteArray>(); private var bytes = 0
        fun receive(packet: ByteArray, now: Long = System.currentTimeMillis()): Pair<Int,ByteArray>? {
            if(packet.size !in 9..512)return null
            val buffer=ByteBuffer.wrap(packet);val message=buffer.int;val index=buffer.short.toInt() and 65535;val count=buffer.short.toInt() and 65535
            if(count !in 1..32768 || index >= count)return null
            if(id!=message || now-started>15000){id=message;total=count;started=now;chunks.clear();bytes=0}
            if(total!=count)return null
            if(!chunks.containsKey(index)){chunks[index]=packet.copyOfRange(8,packet.size);bytes+=packet.size-8;started=now}
            if(bytes>384*1024){id=null;chunks.clear();return null}
            if(chunks.size!=total)return null
            val result=ByteArray(bytes);var position=0
            for(i in 0 until total){val part=chunks[i]?:return null;part.copyInto(result,position);position+=part.size}
            id=null;chunks.clear();bytes=0;return Pair(message,result)
        }
    }
}
