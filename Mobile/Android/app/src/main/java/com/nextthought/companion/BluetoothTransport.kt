@file:Suppress("MissingPermission", "DEPRECATION")
package com.nextthought.companion

import android.bluetooth.*
import android.bluetooth.le.*
import android.content.Context
import android.os.Handler
import android.os.Looper
import android.os.ParcelUuid
import org.json.JSONObject
import java.util.ArrayDeque
import kotlin.random.Random

class BluetoothTransport(private val context: Context, val onFrame:(JSONObject)->Unit, val onReady:()->Unit, val onStatus:(String)->Unit) {
    private val handler=Handler(Looper.getMainLooper())
    private val adapter=(context.getSystemService(Context.BLUETOOTH_SERVICE) as BluetoothManager).adapter
    private var pair:JSONObject?=null;private var gatt:BluetoothGatt?=null;private var write:BluetoothGattCharacteristic?=null
    private var assembler=Protocol.Assembler();private var mtu=20;private var nextId=Random.nextInt();private val queue=ArrayDeque<ByteArray>();private var writing=false
    var ready=false;private set
    private var stopped=true
    fun start(pairing:JSONObject){stop();pair=pairing;stopped=false;scan()}
    fun stop(){stopped=true;handler.removeCallbacksAndMessages(null);runCatching{adapter?.bluetoothLeScanner?.stopScan(scanCallback);gatt?.disconnect();gatt?.close()};gatt=null;pair=null;ready=false;write=null;queue.clear();writing=false;assembler=Protocol.Assembler()}
    private fun scan(){if(stopped)return;runCatching{
        if(adapter?.isEnabled!=true){onStatus("蓝牙未开启");handler.postDelayed({scan()},3000);return}
        adapter.bluetoothLeScanner.startScan(listOf(ScanFilter.Builder().setServiceUuid(ParcelUuid(Protocol.service)).build()),ScanSettings.Builder().setScanMode(ScanSettings.SCAN_MODE_LOW_LATENCY).build(),scanCallback)
        onStatus("正在寻找 Mac 蓝牙备用")
    }.onFailure{onStatus("蓝牙未授权或不可用")}}
    private val scanCallback=object:ScanCallback(){
        override fun onScanResult(callbackType:Int,result:ScanResult){handler.post{
            val ble=pair?.optString("ble")?:return@post
            if(gatt!=null || result.scanRecord?.deviceName!="IH-"+ble.take(8))return@post
            runCatching{adapter.bluetoothLeScanner.stopScan(this);gatt=result.device.connectGatt(context,false,callback,BluetoothDevice.TRANSPORT_LE)
                handler.postDelayed({if(!ready)reconnect()},10000)}
        }}
        override fun onScanFailed(errorCode:Int){handler.post{onStatus("蓝牙扫描暂不可用");handler.postDelayed({scan()},2000)}}
    }
    private fun reconnect(){
        if(stopped)return
        runCatching{gatt?.disconnect();gatt?.close()};gatt=null;write=null;ready=false;queue.clear();writing=false;assembler=Protocol.Assembler()
        handler.removeCallbacksAndMessages(null);onStatus("蓝牙正在恢复");handler.postDelayed({scan()},1000)
    }
    private val callback=object:BluetoothGattCallback(){
        override fun onConnectionStateChange(g:BluetoothGatt,status:Int,newState:Int){handler.post{
            if(g!==gatt)return@post
            if(status==BluetoothGatt.GATT_SUCCESS&&newState==BluetoothProfile.STATE_CONNECTED){if(!g.requestMtu(247))g.discoverServices()}
            else if(newState==BluetoothProfile.STATE_DISCONNECTED||status!=BluetoothGatt.GATT_SUCCESS)reconnect()
        }}
        override fun onMtuChanged(g:BluetoothGatt,mtuValue:Int,status:Int){handler.post{if(g===gatt){mtu=if(status==0)mtuValue-3 else 20;g.discoverServices()}}}
        override fun onServicesDiscovered(g:BluetoothGatt,status:Int){handler.post{
            if(g!==gatt)return@post
            val service=g.getService(Protocol.service);val state=service?.getCharacteristic(Protocol.state);write=service?.getCharacteristic(Protocol.command)
            if(status!=0||state==null||write==null){reconnect();return@post}
            if(!g.setCharacteristicNotification(state,true)){reconnect();return@post}
            val descriptor=state.getDescriptor(Protocol.cccd)?:run{reconnect();return@post}
            descriptor.value=BluetoothGattDescriptor.ENABLE_NOTIFICATION_VALUE;if(!g.writeDescriptor(descriptor))reconnect()
        }}
        override fun onDescriptorWrite(g:BluetoothGatt,descriptor:BluetoothGattDescriptor,status:Int){handler.post{if(g===gatt){if(status==0){ready=true;handler.removeCallbacksAndMessages(null);onStatus("蓝牙备用已连接");onReady();flush()}else reconnect()}}}
        override fun onCharacteristicChanged(g:BluetoothGatt,c:BluetoothGattCharacteristic){val value=c.value?.clone()?:return;handler.post{receive(g,value)}}
        override fun onCharacteristicChanged(g:BluetoothGatt,c:BluetoothGattCharacteristic,value:ByteArray){handler.post{receive(g,value)}}
        override fun onCharacteristicWrite(g:BluetoothGatt,c:BluetoothGattCharacteristic,status:Int){handler.post{if(g===gatt){if(status!=0){reconnect();return@post};if(queue.isNotEmpty())queue.removeFirst();writing=false;flush()}}}
    }
    fun send(frame:JSONObject){if(!ready||queue.size>32768)return;queue.addAll(Protocol.fragments(frame.toString().toByteArray(Charsets.UTF_8),++nextId,mtu));flush()}
    private fun flush(){val g=gatt?:return;val c=write?:return;if(!ready||writing||queue.isEmpty())return
        c.writeType=BluetoothGattCharacteristic.WRITE_TYPE_DEFAULT;c.value=queue.first();writing=g.writeCharacteristic(c)
        if(!writing)handler.postDelayed({flush()},100)
    }
    private fun receive(g:BluetoothGatt,value:ByteArray){
        if(g!==gatt)return;val result=assembler.receive(value)?:return;val p=pair?:return
        runCatching{
            val frame=JSONObject(String(result.second,Charsets.UTF_8));Protocol.open(p,frame)
            send(Protocol.seal(p,"ack",JSONObject().put("id","ble:${result.first.toLong() and 0xffffffffL}")))
            onFrame(frame)
        }.onFailure{onStatus("蓝牙配对校验失败")}
    }
}
