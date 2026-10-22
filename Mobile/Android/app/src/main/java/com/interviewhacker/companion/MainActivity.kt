package com.interviewhacker.companion

import android.Manifest
import android.content.ClipDescription
import android.content.ClipboardManager
import android.content.Intent
import android.os.Build
import android.os.Bundle
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.viewModels
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.journeyapps.barcodescanner.ScanContract
import com.journeyapps.barcodescanner.ScanOptions

class MainActivity:ComponentActivity(){
    private val model:CompanionModel by viewModels()
    private var pairLink by mutableStateOf("");private var enableBluetooth by mutableStateOf(true)
    private var pendingLink:String?=null
    private val permissions=registerForActivityResult(ActivityResultContracts.RequestMultiplePermissions()){result->
        pendingLink?.let{model.connect(it,enableBluetooth&&result.values.all{allowed->allowed})};pendingLink=null;pairLink=""
    }
    private val scanner=registerForActivityResult(ScanContract()){result->result.contents?.let{connect(it)}}
    override fun onCreate(savedInstanceState:Bundle?){super.onCreate(savedInstanceState);window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        setContent{MaterialTheme{Surface{Content()}}};intent?.dataString?.let{connect(it)}
    }
    override fun onNewIntent(intent:Intent){super.onNewIntent(intent);intent.dataString?.let{connect(it)}}
    override fun onResume(){super.onResume();model.resume()}
    private fun connect(link:String){
        if(!enableBluetooth){model.connect(link,false);pairLink="";return}
        pendingLink=link
        val names=if(Build.VERSION.SDK_INT>=31)arrayOf(Manifest.permission.BLUETOOTH_SCAN,Manifest.permission.BLUETOOTH_CONNECT)else arrayOf(Manifest.permission.ACCESS_FINE_LOCATION)
        permissions.launch(names)
    }
    @Composable private fun Content(){
        val state by model.screen.collectAsState();var fontSize by remember{mutableFloatStateOf(22f)}
        Column(Modifier.fillMaxSize().statusBarsPadding().navigationBarsPadding().padding(16.dp),verticalArrangement=Arrangement.spacedBy(12.dp)){
            Text("InterviewHacker",style=MaterialTheme.typography.titleLarge);Text(state.connection,style=MaterialTheme.typography.labelMedium)
            if(!state.paired){
                Text("在 Mac 设置 → 手机伴侣中开启服务，再扫描 App 配对码。")
                OutlinedTextField(value=pairLink,onValueChange={pairLink=it},label={Text("粘贴配对链接")},maxLines=3,modifier=Modifier.fillMaxWidth())
                Row{Switch(checked=enableBluetooth,onCheckedChange={enableBluetooth=it});Text("启用蓝牙备用",Modifier.padding(12.dp))}
                Row(horizontalArrangement=Arrangement.spacedBy(8.dp)){
                    Button(onClick={scanner.launch(ScanOptions().setDesiredBarcodeFormats(ScanOptions.QR_CODE).setPrompt("扫描 Mac 的 App 配对码").setBeepEnabled(false))}){Text("扫描")}
                    Button(onClick={val clipboard=getSystemService(CLIPBOARD_SERVICE)as ClipboardManager;if(clipboard.primaryClipDescription?.hasMimeType(ClipDescription.MIMETYPE_TEXT_PLAIN)==true)pairLink=clipboard.primaryClip?.getItemAt(0)?.coerceToText(this@MainActivity)?.toString()?:""}){Text("粘贴")}
                    Button(onClick={connect(pairLink)}){Text("连接")}
                }
                Text("密钥仅保留在当前 App 进程。局域网优先，蓝牙及已启用的加密中转作为备用。当前版本面向前台使用。",style=MaterialTheme.typography.bodySmall)
            }else{
                Row(horizontalArrangement=Arrangement.spacedBy(6.dp)){
                    TextButton(onClick={fontSize=maxOf(14f,fontSize-2)}){Text("A−")};TextButton(onClick={fontSize=minOf(40f,fontSize+2)}){Text("A+")};TextButton(onClick={model.stop()}){Text("断开")}
                }
                Text(state.bluetooth,style=MaterialTheme.typography.bodySmall)
                state.snapshot?.let{s->
                    Text(s.optString("status")+(if(s.optBoolean("answering"))" · 生成中" else ""),style=MaterialTheme.typography.bodySmall)
                    Text(s.optString("question"),style=MaterialTheme.typography.titleMedium)
                    Text(s.optString("answer").ifEmpty{if(s.optBoolean("answering"))"正在生成…"else"等待回答…"},fontSize=fontSize.sp,modifier=Modifier.weight(1f).fillMaxWidth().verticalScroll(rememberScrollState()))
                    Text(listOf(s.optString("slide"),s.optString("sources")).filter{it.isNotEmpty()&&it!="null"}.joinToString(" · "),style=MaterialTheme.typography.labelSmall)
                    if(s.optBoolean("updateAvailable"))Button(onClick={model.action("applyWeb")}){Text("显示核查更新")}
                }?:Text("等待 Mac 当前状态…",Modifier.weight(1f))
                Row(Modifier.horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(8.dp)){
                    listOf("回答" to "answer","简短" to "shorter","举例" to "example","英文" to "english","上一条" to "previous","下一条" to "next","取消" to "cancel").forEach{(label,action)->Button(onClick={model.action(action)}){Text(label)}}
                }
            }
            if(state.error.isNotEmpty())Text(state.error,color=MaterialTheme.colorScheme.error,style=MaterialTheme.typography.bodySmall)
        }
    }
}
