package com.interviewhacker.companion

import android.Manifest
import android.content.ClipDescription
import android.content.ClipboardManager
import android.content.Intent
import android.os.Build
import android.os.Bundle
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.enableEdgeToEdge
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.viewModels
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
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
    override fun onCreate(savedInstanceState:Bundle?){super.onCreate(savedInstanceState);enableEdgeToEdge();window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        setContent{
            val colors=if(isSystemInDarkTheme())darkColorScheme(primary=Color(0xFFBDB3FF),onPrimary=Color(0xFF2A1B63))else lightColorScheme(primary=Color(0xFF5146C8),onPrimary=Color.White)
            MaterialTheme(colorScheme=colors){Surface{Content()}}
        };intent?.dataString?.let{connect(it)}
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
        val state by model.screen.collectAsState()
        var fontSize by remember{mutableFloatStateOf(22f)}
        var showsLink by remember{mutableStateOf(false)}
        var showsSources by remember{mutableStateOf(false)}
        Column(Modifier.fillMaxSize().statusBarsPadding().navigationBarsPadding().padding(horizontal=16.dp),verticalArrangement=Arrangement.spacedBy(10.dp)){
            Text("InterviewHacker",style=MaterialTheme.typography.titleLarge,modifier=Modifier.padding(top=12.dp))
            Text(state.connection,style=MaterialTheme.typography.bodyMedium)
            if(!state.paired){
                Column(Modifier.weight(1f).verticalScroll(rememberScrollState()),verticalArrangement=Arrangement.spacedBy(16.dp)){
                    Text("把当前回答放到手机上",style=MaterialTheme.typography.headlineSmall)
                    Text("在 Mac 的高级设置 → 更多 → 手机伴侣中开启服务，选择“已安装手机 App”。",color=MaterialTheme.colorScheme.onSurfaceVariant)
                    Button(onClick={scanner.launch(ScanOptions().setDesiredBarcodeFormats(ScanOptions.QR_CODE).setPrompt("扫描 Mac 的 App 配对码").setBeepEnabled(false))},modifier=Modifier.fillMaxWidth().heightIn(min=48.dp)){Text("扫描二维码")}
                    TextButton(onClick={showsLink=!showsLink}){Text(if(showsLink)"收起配对链接"else"使用配对链接")}
                    if(showsLink){
                        OutlinedTextField(value=pairLink,onValueChange={pairLink=it},label={Text("Mac 的 App 配对链接")},maxLines=4,modifier=Modifier.fillMaxWidth())
                        Row(horizontalArrangement=Arrangement.spacedBy(8.dp)){
                            OutlinedButton(onClick={val clipboard=getSystemService(CLIPBOARD_SERVICE)as ClipboardManager;if(clipboard.primaryClipDescription?.hasMimeType(ClipDescription.MIMETYPE_TEXT_PLAIN)==true)pairLink=clipboard.primaryClip?.getItemAt(0)?.coerceToText(this@MainActivity)?.toString()?:""}){Text("粘贴")}
                            OutlinedButton(onClick={connect(pairLink)},enabled=pairLink.isNotBlank()){Text("连接")}
                        }
                    }
                    Row(verticalAlignment=androidx.compose.ui.Alignment.CenterVertically){
                        Switch(checked=enableBluetooth,onCheckedChange={enableBluetooth=it})
                        Text("启用蓝牙备用",Modifier.padding(start=12.dp))
                    }
                    Text("局域网优先；蓝牙和已启用的加密中转作为备用。配对密钥仅保留在当前 App 进程，当前版本需保持前台使用。",style=MaterialTheme.typography.bodySmall,color=MaterialTheme.colorScheme.onSurfaceVariant)
                    if(state.error.isNotEmpty())Text(state.error,color=MaterialTheme.colorScheme.error,style=MaterialTheme.typography.bodyMedium)
                }
            }else{
                Row(horizontalArrangement=Arrangement.spacedBy(4.dp)){
                    TextButton(onClick={fontSize=maxOf(14f,fontSize-2)}){Text("字号 −")}
                    TextButton(onClick={fontSize=minOf(40f,fontSize+2)}){Text("字号 +")}
                    Spacer(Modifier.weight(1f))
                    TextButton(onClick={model.stop()}){Text("断开")}
                }
                Text(state.bluetooth,style=MaterialTheme.typography.bodySmall,color=MaterialTheme.colorScheme.onSurfaceVariant)
                val snapshot=state.snapshot
                if(snapshot!=null){
                    Row(Modifier.fillMaxWidth(),verticalAlignment=androidx.compose.ui.Alignment.CenterVertically){
                        TextButton(onClick={model.action("previous")},enabled=!state.pendingActions.contains("previous")){Text("上一条")}
                        Text(snapshot.optString("position").ifEmpty{"当前"},modifier=Modifier.weight(1f),textAlign=androidx.compose.ui.text.style.TextAlign.Center,style=MaterialTheme.typography.bodySmall)
                        TextButton(onClick={model.action("next")},enabled=!state.pendingActions.contains("next")){Text("下一条")}
                    }
                    Column(Modifier.weight(1f).fillMaxWidth().verticalScroll(rememberScrollState()),verticalArrangement=Arrangement.spacedBy(12.dp)){
                        Text(snapshot.optString("status")+(if(snapshot.optBoolean("answering"))" · 生成中" else ""),style=MaterialTheme.typography.bodySmall,color=MaterialTheme.colorScheme.onSurfaceVariant)
                        if(snapshot.optString("question").isNotEmpty())Text(snapshot.optString("question"),style=MaterialTheme.typography.titleMedium)
                        Surface(color=MaterialTheme.colorScheme.surfaceContainerLow,shape=androidx.compose.foundation.shape.RoundedCornerShape(16.dp),modifier=Modifier.fillMaxWidth()){
                            androidx.compose.foundation.text.selection.SelectionContainer{
                                Text(snapshot.optString("answer").ifEmpty{if(snapshot.optBoolean("answering"))"正在生成…"else"等待 Mac 的下一条回答…"},fontSize=fontSize.sp,lineHeight=(fontSize*1.5).sp,modifier=Modifier.padding(16.dp))
                            }
                        }
                        val sources=listOf(snapshot.optString("slide"),snapshot.optString("sources")).filter{it.isNotEmpty()&&it!="null"}.joinToString(" · ")
                        if(sources.isNotEmpty()){
                            TextButton(onClick={showsSources=!showsSources}){Text(if(showsSources)"收起本次来源"else"本次来源")}
                            if(showsSources)Text(sources,style=MaterialTheme.typography.bodySmall,color=MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        if(snapshot.optBoolean("updateAvailable"))OutlinedButton(onClick={model.action("applyWeb")},enabled=!state.pendingActions.contains("applyWeb")){Text("显示核查更新")}
                    }
                }else Text("等待 Mac 当前状态…",Modifier.weight(1f),color=MaterialTheme.colorScheme.onSurfaceVariant)
                HorizontalDivider()
                if(state.error.isNotEmpty())Text(state.error,color=MaterialTheme.colorScheme.error,style=MaterialTheme.typography.bodySmall)
                if(state.operationStatus.isNotEmpty())Text(state.operationStatus,style=MaterialTheme.typography.bodySmall,color=MaterialTheme.colorScheme.onSurfaceVariant)
                if(state.canRetryAction)OutlinedButton(onClick={model.retryAction()}){Text("重试操作")}
                val answering=snapshot?.optBoolean("answering")==true
                Button(onClick={model.action(if(answering)"cancel"else"answer")},enabled=answering||!state.pendingActions.contains("answer"),modifier=Modifier.fillMaxWidth().heightIn(min=48.dp)){
                    Text(if(answering)"停止生成"else"回答")
                }
                Row(Modifier.fillMaxWidth().padding(bottom=10.dp),horizontalArrangement=Arrangement.spacedBy(8.dp)){
                    listOf("更简短" to "shorter","举例" to "example","英文版" to "english").forEach{(label,action)->
                        OutlinedButton(onClick={model.action(action)},enabled=!state.pendingActions.contains(action),modifier=Modifier.weight(1f).heightIn(min=48.dp),contentPadding=PaddingValues(horizontal=8.dp,vertical=10.dp)){Text(label)}
                    }
                }
            }
        }
    }
}
