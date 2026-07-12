# Audio Relay — PC system audio → phone

把电脑正在播放的系统声音，通过 WebRTC 实时推到手机播放。Windows 服务端 + Flutter 手机端。

## 架构

```
Windows 服务端 (C# .NET 10 + Avalonia)              Flutter 客户端 (手机)
  WASAPI loopback (NAudio) → 48k float               扫码 (mobile_scanner)
   → 攒 20ms 帧 → Concentus Opus 编码                  → POST /offer (+PIN)
   → libdatachannel 音频轨 (Opus packetizer +SR)       → WebRTC 接收 → 自动播放
信令: 本地 HTTP /offer (+PIN 校验)                      (NetEq 自适应,延迟不漂移)
```

延迟稳定性靠接收端 WebRTC NetEq 自适应吸收时钟漂移；发送端零缓冲、按采集时钟实时推。

## 目录

```
server/                  .NET 解决方案 (AudioRelay.slnx)
  src/AudioRelay.Codec/      Opus 编码 (Concentus) + 48k 浮点分帧 (纯逻辑,可测)
  src/AudioRelay.Audio/      采集抽象 + 管线编排 + 线性重采样 (纯逻辑,可测)
  src/AudioRelay.Signaling/  信令端点: JSON 契约 + PIN 校验 + 状态码 (纯逻辑,可测)
  src/AudioRelay.WebRTC/     libdatachannel 封装: Opus 轨配置 (可测) + 发布器 (集成)
  src/AudioRelay.App/        Avalonia UI + WASAPI 采集器 + Kestrel 信令宿主 + 装配
client/                  Flutter 客户端
  lib/  main / scan_page / player_page / webrtc_client / signaling_client
```

## 服务端构建与运行

```bash
cd server
dotnet build AudioRelay.slnx
dotnet run --project src/AudioRelay.App            # 启动托盘窗口,显示含 PIN 的二维码
```

启动后窗口显示二维码 (`http://<LAN-IP>:8080?pin=<PIN>`)，监听 8080。音频管线在手机连接后自动开始（空闲时不编码，省 CPU）。

## 客户端构建与运行（需要 Flutter SDK）

```bash
cd client
flutter pub get
flutter run                                       # 连真机/模拟器
```

> 本环境未安装 Flutter SDK，客户端代码已写好但**未编译/未运行验证**。装好 SDK 后 `flutter pub get` 可能需要按实际包版本微调 `pubspec.yaml`。

## 单元测试与覆盖率

```bash
cd server
dotnet test AudioRelay.slnx --collect:"XPlat Code Coverage"
```

61 个测试，各生产程序集行覆盖率（[ExcludeFromCodeCoverage] 排除了 WASAPI/libdatachannel/UI 等集成胶水）：

| 程序集 | 行覆盖率 |
|---|---|
| AudioRelay.Codec | 98.4% |
| AudioRelay.Audio | 100% |
| AudioRelay.Signaling | 100% |
| AudioRelay.WebRTC (可测部分) | 100% |
| AudioRelay.App (可测部分) | 90.9% |

均 > 85%。

## 端到端验证（需真机/真声，本环境无法执行）

1. 服务端起在 Windows PC，放音乐（如 YouTube）。
2. 手机扫码（或手输 IP+PIN）。
3. 手机应听到 PC 的声音，单向延迟目标 < 150ms（LAN）。
4. **长跑**：连播 2–4 小时，延迟恒定不累积（靠 NetEq）。
5. **生命周期**：播放中切换系统默认输出设备、手机锁屏恢复、断开重连、服务端启停 —— 无爆音、无静默失败。
6. **弱网**：限速/丢包下不爆音（NetEq + PLC）。

## 已验证 / 待验证

- ✅ 服务端全部库 + App 编译干净（0 错误 0 警告）。
- ✅ 61 个单元测试通过，覆盖率达标。
- ⚠️ **libdatachannel 媒体路径**（`RtcMediaPublisher`）写了但**未运行验证** —— 需真机 E2E（浏览器当客户端先用测试音打通，再接真实采集）。这是最大风险点。
- ⚠️ **Flutter 客户端**未编译/未运行（无 SDK）。
- ⚠️ 托盘图标、设置持久化、开机自启、trickle ICE 等 v1 润色项待补。
