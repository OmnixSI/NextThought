# P4 公开验证音频

`public-sample.wav` 是已校验的官方 X-ASR 480 ms 模型包内首段公开测试音频，约 10 秒；不含用户实采声音。

- 来源：与 `src/NextThought.Platform/Resources/models.json` 中 xasr480 对应的 sherpa-onnx 官方发布模型包。Windows 准备脚本 `scripts/prepare_test_models.py` 保存为 `.cache/test-audio/xasr480-0.wav`。
- SHA-256：`7d93384ca14702cc584a7a33fe2fed92e89e708549161cb12ea38c916882103b`。
- X-ASR 版权与 Apache-2.0 许可保留于发布包 `licenses/X-ASR-LICENSE.txt` 和第三方说明。
- 仅用于独立机器公开 WAV 推理检查。基础自检和 WAV 推理不会采集硬件声音、读取真实会议或请求远程回答服务。
