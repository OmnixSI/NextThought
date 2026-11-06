package com.nextthought.companion

import android.Manifest
import android.content.Intent
import android.net.Uri
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createEmptyComposeRule
import androidx.test.core.app.ActivityScenario
import androidx.test.platform.app.InstrumentationRegistry
import androidx.test.rule.GrantPermissionRule
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import java.net.ServerSocket
import java.net.Socket
import java.util.Base64
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.ConcurrentHashMap
import kotlin.concurrent.thread

class LANIntegrationTest {
    @get:Rule val compose=createEmptyComposeRule()
    @get:Rule val permissions:GrantPermissionRule=GrantPermissionRule.grant(Manifest.permission.BLUETOOTH_SCAN,Manifest.permission.BLUETOOTH_CONNECT)
    @Test fun realTcpStreamControlAndSilentConnectionRecovery(){
        val fixture=Fixture();fixture.start()
        val p=JSONObject().put("v",1).put("lan","http://127.0.0.1:${fixture.port}").put("token","authored-test").put("room","native-fixture").put("key",Base64.getEncoder().encodeToString(ByteArray(32){7}))
        val uri="nextthought-companion://pair#p="+Base64.getUrlEncoder().withoutPadding().encodeToString(p.toString().toByteArray())
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val intent=Intent(context,MainActivity::class.java).setAction(Intent.ACTION_VIEW).setData(Uri.parse(uri)).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        try{ActivityScenario.launch<MainActivity>(intent).use{
            fun waitFor(text:String,timeout:Long=5000){compose.waitUntil(timeout){compose.onAllNodesWithText(text).fetchSemanticsNodes().isNotEmpty()}}
            waitFor("手机网络验证问题");compose.onNodeWithText("第一条答案").assertIsDisplayed()
            fixture.update(2,"第二条问题","",true);waitFor("第二条问题");compose.onNodeWithText("第一条答案").assertDoesNotExist();compose.onNodeWithText("正在生成…").assertIsDisplayed()
            compose.onNodeWithText("停止生成").assertIsDisplayed()
            compose.onNodeWithText("下一条",useUnmergedTree=true).performClick();compose.waitUntil(5000){fixture.commands.size==1}
            waitFor("Mac 已确认操作")
            fixture.update(3,"静默连接已补齐","最终答案",false);waitFor("静默连接已补齐",10000);compose.onNodeWithText("最终答案").assertIsDisplayed()
            assertEquals(1,fixture.commands.size)
            val shot=InstrumentationRegistry.getInstrumentation().uiAutomation.takeScreenshot()
            java.io.FileOutputStream(java.io.File(context.getExternalFilesDir(null),"design-review.png")).use{shot.compress(android.graphics.Bitmap.CompressFormat.PNG,100,it)}
        }}finally{fixture.close()}
    }
    private class Fixture {
        private val listener=ServerSocket(0);val port=listener.localPort
        @Volatile private var active=true
        @Volatile private var latest=frame(1,"手机网络验证问题","第一条答案")
        private val connections=CopyOnWriteArrayList<Socket>();private val streams=CopyOnWriteArrayList<Socket>()
        val commands=ConcurrentHashMap.newKeySet<String>()
        fun start(){thread(isDaemon=true){while(active)runCatching{val socket=listener.accept();connections.add(socket);thread(isDaemon=true){serve(socket)}}}}
        fun update(seq:Long,q:String,a:String,broadcast:Boolean){latest=frame(seq,q,a);if(broadcast)for(s in streams)runCatching{s.getOutputStream().apply{write("data: $latest\n\n".toByteArray());flush()}}}
        private fun serve(socket:Socket){runCatching{
            val input=socket.getInputStream();val head=StringBuilder();while(!head.endsWith("\r\n\r\n")&&head.length<16384){val c=input.read();if(c<0)return;head.append(c.toChar())}
            val path=head.toString().lineSequence().first().split(" ")[1];val output=socket.getOutputStream()
            when{
                path.startsWith("/events")-> {streams.add(socket);output.write("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nConnection: keep-alive\r\n\r\ndata: $latest\n\n".toByteArray());output.flush()}
                path.startsWith("/snapshot")-> {val bytes=latest.toString().toByteArray();output.write("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: ${bytes.size}\r\nConnection: close\r\n\r\n".toByteArray());output.write(bytes);output.flush();socket.close()}
                path.startsWith("/action")-> {Uri.parse("http://local$path").getQueryParameter("id")?.let{commands.add(it)};output.write("HTTP/1.1 204 No Content\r\nContent-Length: 0\r\nConnection: close\r\n\r\n".toByteArray());output.flush();socket.close()}
                else->socket.close()
            }
        }}
        fun close(){active=false;listener.close();for(s in connections)runCatching{s.close()}}
        private fun frame(seq:Long,q:String,a:String)=JSONObject().put("version",1).put("serverID","android-native-qa").put("sequence",seq).put("updatedAt",System.currentTimeMillis()).put("snapshot",JSONObject().put("status","fixture").put("question",q).put("answer",a).put("answering",a.isEmpty()).put("candidate","").put("position","1 / 1"))
    }
}
