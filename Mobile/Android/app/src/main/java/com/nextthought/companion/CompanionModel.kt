package com.nextthought.companion

import android.app.Application
import android.net.Uri
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import okhttp3.*
import okhttp3.sse.EventSource
import okhttp3.sse.EventSourceListener
import okhttp3.sse.EventSources
import org.json.JSONObject
import java.util.Base64
import java.util.UUID
import java.util.concurrent.TimeUnit

class CompanionModel(application:Application):AndroidViewModel(application) {
    data class Screen(val connection:String="请扫描或粘贴 Mac 配对链接", val bluetooth:String="蓝牙未开启", val snapshot:JSONObject?=null,val paired:Boolean=false,val error:String="",val operationStatus:String="",val pendingActions:Set<String> = emptySet(),val canRetryAction:Boolean=false)
    private val mutable=MutableStateFlow(Screen());val screen=mutable.asStateFlow()
    private val http=OkHttpClient.Builder().connectTimeout(2,TimeUnit.SECONDS).readTimeout(2500,TimeUnit.MILLISECONDS).callTimeout(3,TimeUnit.SECONDS).build()
    private val streaming=http.newBuilder().readTimeout(0,TimeUnit.MILLISECONDS).callTimeout(0,TimeUnit.MILLISECONDS).pingInterval(2,TimeUnit.SECONDS).build()
    private var pair:JSONObject?=null;private var generation=UUID.randomUUID();private var ticker:Job?=null;private var stream:EventSource?=null;private var socket:WebSocket?=null
    private var reconnectLAN:Job?=null;private var reconnectRelay:Job?=null;private var sequence=-1L;private var server="";private val retired=mutableSetOf<String>()
    private val last=mutableMapOf<String,Long>();private val pending=mutableMapOf<String,JSONObject>();private val nonces=LinkedHashSet<String>()
    private var failedCommand:JSONObject?=null
    private val firstSent=mutableMapOf<String,Long>()
    private val ble=BluetoothTransport(application,{frame->viewModelScope.launch{encrypted(frame,"蓝牙备用")}},{resync()},{text->mutable.value=mutable.value.copy(bluetooth=text)})
    fun connect(link:String,enableBluetooth:Boolean){
        try{
            val fragment=Uri.parse(link.trim()).encodedFragment?:error("缺少配对内容")
            val encoded=Uri.parse("https://pair.invalid/?$fragment").getQueryParameter("p")?:error("配对内容无效")
            require(encoded.length<20000);val p=JSONObject(String(Base64.getUrlDecoder().decode(encoded),Charsets.UTF_8))
            require(p.optInt("v")==1 && Base64.getDecoder().decode(p.getString("key")).size==32 && p.getString("room").isNotEmpty())
            if(p.has("relay"))require(Uri.parse(p.getString("relay")).scheme=="https")
            if(p.has("lan"))require(Uri.parse(p.getString("lan")).scheme in listOf("http","https"))
            stop();pair=p;mutable.value=Screen(connection="正在连接…",paired=true);val g=generation
            if(enableBluetooth&&p.has("ble"))ble.start(p)
            connectLAN(g);connectRelay(g)
            ticker=viewModelScope.launch{
                while(isActive&&generation==g){
                    if(now()-(last["局域网"]?:0)>5000)poll(g)
                    if(generation!=g)return@launch
                    if(now()-(last.values.maxOrNull()?:0)>8000)mutable.value=mutable.value.copy(connection="离线，正在恢复…")
                    for((id,command) in pending.toMap()){
                        if(now()-command.getLong("createdAt")>15000){pending.remove(id);failedCommand?.let{firstSent.remove(it.optString("id"))};failedCommand=command;mutable.value=mutable.value.copy(pendingActions=mutable.value.pendingActions-command.getString("action"),canRetryAction=true,operationStatus="操作未确认，请检查连接后重试")}
                        else send(command,g)
                    }
                    delay(1000)
                }
            }
        }catch(error:Exception){mutable.value=mutable.value.copy(error="配对失败：${error.message}")}
    }
    fun stop(){
        generation=UUID.randomUUID();ticker?.cancel();reconnectLAN?.cancel();reconnectRelay?.cancel();stream?.cancel();stream=null;socket?.close(1000,"disconnect");socket=null;ble.stop();pair=null
        pending.clear();firstSent.clear();failedCommand=null;last.clear();retired.clear();nonces.clear();server="";sequence=-1;mutable.value=Screen(connection="已断开")
    }
    fun resume(){val g=generation;viewModelScope.launch{poll(g);resync();if(stream==null)connectLAN(g);if(socket==null)connectRelay(g)}}
    fun action(action:String){
        if(action !in setOf("answer","cancel","previous","next","shorter","example","english","applyWeb"))return
        if(pair==null||action in mutable.value.pendingActions)return
        if(failedCommand?.optString("action")==action){retryAction();return}
        val command=JSONObject().put("id",UUID.randomUUID().toString()).put("action",action).put("createdAt",now());pending[command.getString("id")]=command;firstSent[command.getString("id")]=now()
        mutable.value=mutable.value.copy(pendingActions=mutable.value.pendingActions+action,operationStatus="正在发送操作，等待 Mac 确认…")
        viewModelScope.launch{send(command,generation)}
    }
    fun retryAction(){
        val command=failedCommand?:return
        if(now()-(firstSent[command.getString("id")]?:0)>=240000){failedCommand=null;firstSent.remove(command.getString("id"));mutable.value=mutable.value.copy(canRetryAction=false,operationStatus="操作已过期，请先核对 Mac 当前状态");return}
        command.put("createdAt",now());failedCommand=null;pending[command.getString("id")]=command
        mutable.value=mutable.value.copy(canRetryAction=false,pendingActions=mutable.value.pendingActions+command.getString("action"),operationStatus="正在重试，等待 Mac 确认…")
        viewModelScope.launch{send(command,generation)}
    }
    private fun confirmed(id:String){
        val command=pending.remove(id)?:failedCommand?.takeIf{it.optString("id")==id}?:return;firstSent.remove(id)
        if(failedCommand?.optString("id")==id){failedCommand=null;mutable.value=mutable.value.copy(canRetryAction=false)}
        mutable.value=mutable.value.copy(pendingActions=mutable.value.pendingActions-command.getString("action"),operationStatus=if(failedCommand!=null)mutable.value.operationStatus else if(pending.isEmpty())"Mac 已确认操作" else "等待其他操作确认…")
    }
    private fun now()=System.currentTimeMillis()
    private fun url(path:String):HttpUrl?{val p=pair?:return null;val base=p.optString("lan").takeIf{it.isNotEmpty()}?:return null
        return runCatching{Request.Builder().url(base).build().url.newBuilder().addPathSegment(path).addQueryParameter("t",p.optString("token")).build()}.getOrNull()}
    private suspend fun request(request:Request):Pair<Int,String>?=withContext(Dispatchers.IO){runCatching{http.newCall(request).execute().use{Pair(it.code,it.body?.string()?:"")}}.getOrNull()}
    private suspend fun poll(g:UUID){val u=url("snapshot")?:return;val response=request(Request.Builder().url(u).build())?:return
        if(generation==g&&response.first==200)runCatching{apply(JSONObject(response.second),"局域网")}}
    private fun connectLAN(g:UUID){val u=url("events")?:return;if(generation!=g)return
        stream?.cancel();stream=EventSources.createFactory(streaming).newEventSource(Request.Builder().url(u).build(),object:EventSourceListener(){
            override fun onEvent(eventSource:EventSource,id:String?,type:String?,data:String){viewModelScope.launch{if(generation==g)runCatching{apply(JSONObject(data),"局域网")}}}
            override fun onFailure(eventSource:EventSource,t:Throwable?,response:Response?){viewModelScope.launch{if(generation==g&&stream===eventSource){stream=null;reconnectLAN?.cancel();reconnectLAN=viewModelScope.launch{delay((800..1600).random().toLong());connectLAN(g)}}}}
            override fun onClosed(eventSource:EventSource){onFailure(eventSource,null,null)}
        })
    }
    private fun connectRelay(g:UUID){val p=pair?:return;val endpoint=p.optString("relay").takeIf{it.startsWith("https://")}?:return;if(generation!=g)return
        val u=endpoint.trimEnd('/')+"/socket";socket=streaming.newWebSocket(Request.Builder().url(u).build(),object:WebSocketListener(){
            override fun onOpen(ws:WebSocket,response:Response){ws.send(JSONObject().put("room",p.getString("room")).put("role","subscriber").put("token",p.getString("subscriber")).toString())}
            override fun onMessage(ws:WebSocket,text:String){viewModelScope.launch{if(generation==g)runCatching{val f=JSONObject(text);if(!f.optBoolean("joined"))encrypted(f,"加密中转")}}}
            override fun onMessage(ws:WebSocket,bytes:okio.ByteString){onMessage(ws,bytes.utf8())}
            override fun onFailure(ws:WebSocket,t:Throwable,response:Response?){viewModelScope.launch{if(generation==g&&socket===ws){socket=null;reconnectRelay?.cancel();reconnectRelay=viewModelScope.launch{delay((800..1800).random().toLong());connectRelay(g)}}}}
            override fun onClosed(ws:WebSocket,code:Int,reason:String){onFailure(ws,IllegalStateException("closed"),null)}
        })
    }
    private fun resync(){val p=pair?:return;if(ble.ready)runCatching{ble.send(Protocol.seal(p,"command",JSONObject().put("id",UUID.randomUUID().toString()).put("action","resync").put("createdAt",now())))}}
    private fun encrypted(frame:JSONObject,transport:String){val p=pair?:return
        runCatching{
            val value=Protocol.open(p,frame)
            if(frame.getString("kind")=="ack"){confirmed(value.optString("id"));return}
            if(frame.getString("kind")!="state")return
            val nonce=frame.getString("kind")+frame.getString("n");if(!nonces.add(nonce))return
            if(nonces.size>4096)nonces.remove(nonces.first())
            apply(value,transport)
        }.onFailure{mutable.value=mutable.value.copy(error="配对校验失败，请重新扫描")}
    }
    internal fun apply(envelope:JSONObject,transport:String){
        val incoming=envelope.optString("serverID");val n=envelope.optLong("sequence",-1)
        if(envelope.optInt("version")!=1||incoming in retired||(incoming==server&&n<sequence)||n<0)return
        last[transport]=now();if(transport!="局域网"&&now()-(last["局域网"]?:0)<4000)return
        if(server.isNotEmpty()&&server!=incoming)retired.add(server)
        server=incoming;sequence=n;mutable.value=mutable.value.copy(connection="$transport · 已连接",snapshot=envelope.getJSONObject("snapshot"))
    }
    private suspend fun send(command:JSONObject,g:UUID){
        val u=url("action")?.newBuilder()?.addQueryParameter("a",command.getString("action"))?.addQueryParameter("id",command.getString("id"))?.build()
        if(u!=null){val response=request(Request.Builder().url(u).post(RequestBody.create(null,ByteArray(0))).build());if(response?.first==204&&generation==g){confirmed(command.getString("id"));return}}
        if(generation!=g)return;val p=pair?:return
        runCatching{val frame=Protocol.seal(p,"command",command);if(ble.ready)ble.send(frame);socket?.send(frame.toString())}
    }
    override fun onCleared(){stop();http.dispatcher.executorService.shutdown();streaming.dispatcher.executorService.shutdown();super.onCleared()}
}
