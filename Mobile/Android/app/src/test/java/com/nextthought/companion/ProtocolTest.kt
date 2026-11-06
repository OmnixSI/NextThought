package com.nextthought.companion

import java.util.Base64
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class ProtocolTest {
    @Test fun swiftAndNobleKnownVectorAuthenticates() {
        val p=JSONObject().put("room","room-one").put("key",Base64.getEncoder().encodeToString(ByteArray(32){7}))
        val raw="\"secret 可靠 -3.7 dBm\"".toByteArray(Charsets.UTF_8)
        val frame=Protocol.sealRaw(p,"state",raw,ByteArray(12){4})
        assertEquals("4p2rT4wvtodvAZxobxrsTNxx8zyhDXC7TyxE1esTaHBCIhkoLHmlIw==",frame.getString("body"))
        assertArrayEquals(raw,Protocol.openRaw(p,frame))
        assertThrows(Exception::class.java){Protocol.openRaw(p,JSONObject(frame.toString()).put("kind","command"))}
        assertThrows(Exception::class.java){Protocol.openRaw(JSONObject(p.toString()).put("room","wrong"),frame)}
    }
    @Test fun shuffledFragmentsDuplicatesAndNewMessage() {
        val raw=ByteArray(3000){(it%251).toByte()};val packets=Protocol.fragments(raw,0x12345678,185)
        assertEquals(17,packets.size);assertArrayEquals(byteArrayOf(0x12,0x34,0x56,0x78,0,0,0,17),packets[0].take(8).toByteArray())
        val assembler=Protocol.Assembler();assertNull(assembler.receive(packets[0]));assertNull(assembler.receive(packets[0]))
        var result:Pair<Int,ByteArray>?=null
        for(packet in packets.drop(1).reversed()){assembler.receive(packet)?.let{result=it}}
        assertEquals(0x12345678,result?.first);assertArrayEquals(raw,result?.second)
        assertNull(assembler.receive(packets[0]));val next=Protocol.fragments("new".toByteArray(),9,20)
        assertArrayEquals("new".toByteArray(),assembler.receive(next[0])?.second)
    }
    @Test fun expiredIncompleteAndBounds() {
        val packets=Protocol.fragments(ByteArray(50),9,20);val assembler=Protocol.Assembler()
        assertNull(assembler.receive(packets.first(),1000))
        for(packet in packets.drop(1))assertNull(assembler.receive(packet,17000))
        assertTrue(Protocol.fragments(ByteArray(400000),1,20).isEmpty())
        assertTrue(Protocol.fragments(ByteArray(20),1,19).isEmpty())
    }
}
