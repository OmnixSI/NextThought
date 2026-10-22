# 原生手机伴侣（草稿）

Android 使用 Kotlin/Compose + Bluetooth GATT；iOS 使用 SwiftUI + Core Bluetooth。两端使用同一版本 1 的完整状态、顺序号和命令 UUID；局域网 SSE 优先，静默流失效后补拉快照。蓝牙和用户开启的 WSS 中转作为备用，状态与操作使用相同的 AES-GCM/HKDF 协议。手机仅接收当前问题、回答、状态及简要来源，不接收资料、完整转录或图片。

## 配对与使用

1. Mac 设置 → 手机伴侣，开启局域网服务；需要蓝牙时开启“原生手机 App 的蓝牙备用”。需要互联网备用时先配置自己的 HTTPS 中转并开启，再配对。
2. 在手机 App 扫描“原生手机 App 配对”二维码，或粘贴其链接；也支持 `interviewhacker-companion://pair#p=…` 深链。配对密钥保留在进程内，结束进程后重新配对。
3. 给 App 授予本地网络、蓝牙权限；扫码需要相机权限，也可粘贴链接。Android 11 及更早版本的 BLE 扫描由系统要求位置权限；不采集位置。
4. App 显示实际接收通道。网络失联后显示离线；恢复时补齐完整最新状态，不把旧答案挂在新问题下。可调整字号、显示核查更新、触发回答/简短/举例/语言/前后/取消。
5. 当前面向前台使用。锁屏、进程被系统终止、两种真实手机各 60 分钟及切网行为尚未验收。蓝牙不会承载图片和文件。

更换局域网链接、重启 Mac 服务或重新启用中转后请重新扫描。中转房间最长 8 小时且重启即消失。不要把配对链接发给他人。

## 构建

Android 需要 JDK 17+、SDK 35，使用仓库内 Gradle wrapper：

```sh
cd Mobile/Android
./gradlew assembleDebug testDebugUnitTest
# 连接设备或启动模拟器后：
./gradlew connectedDebugAndroidTest
```

调试 APK 位于 `app/build/outputs/apk/debug/app-debug.apk`。正式发布需用户自己的签名配置；本仓库不提交签名密钥。开发缓存可通过 `GRADLE_USER_HOME`、`ANDROID_HOME`、`ANDROID_USER_HOME` 指向项目内目录。

iOS 需要 Xcode 和 iOS 16+ SDK：打开 `iOS/InterviewHackerCompanion.xcodeproj`，选择自己的开发团队及设备后运行。项目文件可用 `python3 iOS/generate_project.py` 确定性重建，无须 XcodeGen。模拟器构建：

```sh
xcodebuild -project Mobile/iOS/InterviewHackerCompanion.xcodeproj -scheme InterviewHackerCompanion -destination 'generic/platform=iOS Simulator' CODE_SIGNING_ALLOWED=NO build
```

`Shared` 是独立 Swift 包；符号链接复用仓库中的协议/密码/分片源文件，不引入桌面 ASR 依赖。可在 macOS 上运行 `swift test --package-path Mobile/Shared`，覆盖真实 TCP SSE/操作/静默恢复与加密向量。当前机器只有 CLT，没有 iOS SDK，因此没有提供可安装 IPA，也没有宣称 iOS UI 已编译或真机通过。手动 GitHub Actions 工作流可在有 Xcode 的 runner 构建两端；未自动触发付费构建。

## 蓝牙协议

固定服务 UUID 和特征 UUID 见 `CompanionBLEProtocol.swift`。Mac peripheral 以 `IH-<配对 ble ID 前 8 位>` 广播。状态使用 notify，操作及确认使用 write-with-response。每个 ATT 包含 8 字节网络序头：messageID UInt32、partIndex UInt16、partCount UInt16，再接负载。分片上限 512 字节，重组上限 384 KiB，未完成帧有超时；重复片不重复拼接。

完整负载是加密 `CompanionCipherFrame`。手机认证后通过 `resync` 请求当前状态；完整状态及命令回执通过加密 `ack {id:"ble:<messageID>"}` 确认，Mac 超时重发最多三次，并合并等待中的状态为最新一份。命令本身沿用同一个 UUID 重试，Mac 三通道共用去重缓存。没有依据“收到 ATT 包”就标记操作已执行。

回滚：关闭 Mac 蓝牙备用并卸载测试 App；撤销本阶段即可，无用户资料迁移。手机发布前仍须完成两平台实体设备、权限拒绝/恢复、蓝牙射频丢包/距离、局域网/中转切换、锁屏/恢复和长会议验收。
