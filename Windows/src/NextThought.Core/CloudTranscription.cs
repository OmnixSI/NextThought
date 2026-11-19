using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NextThought.Core;

/// <summary>
/// Cloud streaming speech-to-text providers (port of CloudSTTProvider). Audio leaves the machine
/// only after the user enables a provider and enters their own key.
/// </summary>
public enum CloudSTTProvider
{
    Soniox,
    OpenAI,
    Deepgram
}

public static class CloudSTTProviders
{
    public static string Title(this CloudSTTProvider provider) => provider switch
    {
        CloudSTTProvider.Soniox => "Soniox stt-rt-v5 · 中英混说最佳",
        CloudSTTProvider.OpenAI => "OpenAI Realtime 转写",
        _ => "Deepgram Nova-3 · 单语"
    };

    public static string KeychainAccount(this CloudSTTProvider provider) => "stt." + provider.Id();

    public static string Id(this CloudSTTProvider provider) => provider switch
    {
        CloudSTTProvider.Soniox => "soniox",
        CloudSTTProvider.OpenAI => "openai",
        _ => "deepgram"
    };

    public static string DefaultModel(this CloudSTTProvider provider) => provider switch
    {
        CloudSTTProvider.Soniox => "stt-rt-v5",
        CloudSTTProvider.OpenAI => "gpt-live-transcribe",
        _ => "nova-3"
    };

    /// <summary>Whisper was a model choice only for the OpenAI connection.</summary>
    public static bool IsRemovedWhisperModelIdentifier(this CloudSTTProvider provider, string identifier)
    {
        var normalized = identifier.Trim().ToLowerInvariant();
        return normalized == "gpt-realtime-whisper" || (provider == CloudSTTProvider.OpenAI && normalized.Contains("whisper"));
    }

    /// <summary>OpenAI Realtime only accepts 24 kHz PCM.</summary>
    public static int SampleRate(this CloudSTTProvider provider) => provider == CloudSTTProvider.OpenAI ? 24000 : 16000;

    public static string? Endpoint(this CloudSTTProvider provider, string model, string language, IReadOnlyList<string> vocabulary)
    {
        if (provider == CloudSTTProvider.Soniox) return "wss://stt-rt.soniox.com/transcribe-websocket";
        if (provider == CloudSTTProvider.OpenAI) return "wss://api.openai.com/v1/realtime?intent=transcription";
        var items = new List<string>
        {
            "model=" + Uri.EscapeDataString(model.Length == 0 ? provider.DefaultModel() : model),
            "encoding=linear16", "sample_rate=16000", "channels=1",
            "language=" + (language == "en" ? "en" : "zh-CN"),
            "interim_results=true", "endpointing=300", "utterance_end_ms=1000",
            "smart_format=true", "punctuate=true"
        };
        items.AddRange(vocabulary.Take(50).Select(term => "keyterm=" + Uri.EscapeDataString(term)));
        return "wss://api.deepgram.com/v1/listen?" + string.Join("&", items);
    }

    public static string AuthorizationHeader(this CloudSTTProvider provider, string apiKey) =>
        provider == CloudSTTProvider.Deepgram ? "Token " + apiKey : "Bearer " + apiKey;

    /// <summary>First text message after the socket opens (null when configuration is in the URL).</summary>
    public static string? ConfigurationMessage(this CloudSTTProvider provider, string model, string language, IReadOnlyList<string> vocabulary)
    {
        var languages = language == "auto" ? new[] { "zh", "en" } : [language];
        JsonObject? message = null;
        if (provider == CloudSTTProvider.Soniox)
        {
            var config = new JsonObject
            {
                ["model"] = model.Length == 0 ? provider.DefaultModel() : model,
                ["audio_format"] = "pcm_s16le",
                ["sample_rate"] = 16000,
                ["num_channels"] = 1,
                ["language_hints"] = new JsonArray(languages.Select(hint => (JsonNode)hint).ToArray()),
                ["enable_endpoint_detection"] = true,
                ["enable_language_identification"] = language == "auto"
            };
            if (vocabulary.Count > 0) config["context"] = new JsonObject { ["terms"] = new JsonArray(vocabulary.Take(100).Select(term => (JsonNode)term).ToArray()) };
            message = config;
        }
        else if (provider == CloudSTTProvider.OpenAI)
        {
            var transcription = new JsonObject
            {
                ["model"] = model.Length == 0 ? provider.DefaultModel() : model,
                ["languages"] = new JsonArray(languages.Select(hint => (JsonNode)hint).ToArray()),
                ["delay"] = "low"
            };
            var keywords = vocabulary.Take(50).Where(term => !term.Contains('<') && !term.Contains('>') && !term.Contains('\n') && !term.Contains('\r')).ToList();
            if (keywords.Count > 0) transcription["keywords"] = new JsonArray(keywords.Select(term => (JsonNode)term).ToArray());
            message = new JsonObject
            {
                ["type"] = "session.update",
                ["session"] = new JsonObject
                {
                    ["type"] = "transcription",
                    ["audio"] = new JsonObject
                    {
                        ["input"] = new JsonObject
                        {
                            ["format"] = new JsonObject { ["type"] = "audio/pcm", ["rate"] = 24000 },
                            ["transcription"] = transcription,
                            ["turn_detection"] = null,
                            ["noise_reduction"] = new JsonObject { ["type"] = "near_field" }
                        }
                    }
                }
            };
        }
        if (message is null) return null;
        return message.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    public static string? KeepAliveMessage(this CloudSTTProvider provider) => provider switch
    {
        CloudSTTProvider.Soniox => "{\"type\":\"keepalive\"}",
        CloudSTTProvider.Deepgram => "{\"type\":\"KeepAlive\"}",
        _ => null
    };

    public static string CloseMessage(this CloudSTTProvider provider) => provider switch
    {
        CloudSTTProvider.Soniox => "",
        CloudSTTProvider.Deepgram => "{\"type\":\"CloseStream\"}",
        _ => "{\"type\":\"input_audio_buffer.commit\"}"
    };

    /// <summary>OpenAI expects base64 audio inside JSON; others take binary frames.</summary>
    public static bool SendsBinaryAudio(this CloudSTTProvider provider) => provider != CloudSTTProvider.OpenAI;

    public static string OpenAiAppendMessage(byte[] pcm24k) =>
        "{\"type\":\"input_audio_buffer.append\",\"audio\":\"" + Convert.ToBase64String(pcm24k) + "\"}";
}

public sealed record CloudTranscriptUpdate(string Text, bool IsFinal, int? StartMs = null, int? EndMs = null);

public sealed class CloudTranscriptException(string message) : Exception(message);

/// <summary>
/// Turns provider messages into utterance-level updates (port of CloudTranscriptAssembler).
/// Soniox sends final tokens once and re-sends the non-final tail; Deepgram sends finalized
/// segments until speech_final; OpenAI sends deltas per item plus a completed transcript.
/// </summary>
public sealed class CloudTranscriptAssembler(CloudSTTProvider provider)
{
    private string finalText = "";
    private int? startMs;
    private int? lastEndMs;
    private readonly Dictionary<string, string> openAiItems = new(StringComparer.Ordinal);

    public CloudSTTProvider Provider { get; } = provider;

    public IReadOnlyList<CloudTranscriptUpdate> Consume(string message)
    {
        JsonObject? json;
        try
        {
            json = JsonNode.Parse(message) as JsonObject;
        }
        catch (JsonException)
        {
            return [];
        }
        if (json is null) return [];
        return Provider switch
        {
            CloudSTTProvider.Soniox => ConsumeSoniox(json),
            CloudSTTProvider.Deepgram => ConsumeDeepgram(json),
            _ => ConsumeOpenAi(json)
        };
    }

    private List<CloudTranscriptUpdate> ConsumeSoniox(JsonObject json)
    {
        if (Int(json["error_code"]) is { } code)
            throw new CloudTranscriptException("Soniox 错误 " + code + "：" + (String(json["error_message"]) ?? ""));
        var updates = new List<CloudTranscriptUpdate>();
        var tail = "";
        foreach (var tokenNode in Array(json["tokens"]))
        {
            if (tokenNode is not JsonObject token) continue;
            var text = String(token["text"]) ?? "";
            var isFinal = Bool(token["is_final"]) ?? false;
            if (text is "<end>" or "<fin>")
            {
                if (finalText.Trim().Length > 0)
                    updates.Add(new CloudTranscriptUpdate(Clean(finalText), true, startMs, lastEndMs));
                finalText = "";
                startMs = null;
                tail = "";
                continue;
            }
            startMs ??= Int(token["start_ms"]);
            if (Int(token["end_ms"]) is { } end) lastEndMs = end;
            if (isFinal) finalText += text;
            else tail += text;
        }
        var live = Clean(finalText + tail);
        if (live.Length > 0) updates.Add(new CloudTranscriptUpdate(live, false, startMs, lastEndMs));
        if (Bool(json["finished"]) == true && Clean(finalText).Length > 0)
        {
            updates.Add(new CloudTranscriptUpdate(Clean(finalText), true, startMs, lastEndMs));
            finalText = "";
            startMs = null;
        }
        return updates;
    }

    private List<CloudTranscriptUpdate> ConsumeDeepgram(JsonObject json)
    {
        var error = String(json["err_msg"]) ?? (String(json["type"]) == "Error" ? String(json["description"]) : null);
        if (error is not null) throw new CloudTranscriptException("Deepgram 错误：" + error);
        var type = String(json["type"]) ?? "Results";
        if (type == "UtteranceEnd")
        {
            if (Clean(finalText).Length == 0) return [];
            var final = new CloudTranscriptUpdate(Clean(finalText), true, startMs, lastEndMs);
            finalText = "";
            startMs = null;
            return [final];
        }
        if (type != "Results") return [];
        var alternative = Array((json["channel"] as JsonObject)?["alternatives"]).FirstOrDefault();
        if (alternative is not JsonObject alternativeObject) return [];
        var transcript = String(alternativeObject["transcript"]) ?? "";
        var start = Double(json["start"]) is { } seconds ? (int?)(seconds * 1000) : null;
        var duration = Double(json["duration"]) is { } d ? (int)(d * 1000) : 0;
        if (startMs is null && transcript.Length > 0) startMs = start;
        if (start is not null) lastEndMs = start + duration;
        var isFinal = Bool(json["is_final"]) ?? false;
        var speechFinal = Bool(json["speech_final"]) ?? false;
        if (isFinal)
        {
            if (transcript.Length > 0) finalText += (finalText.Length == 0 ? "" : " ") + transcript;
            if (speechFinal && Clean(finalText).Length > 0)
            {
                var update = new CloudTranscriptUpdate(Clean(finalText), true, startMs, lastEndMs);
                finalText = "";
                startMs = null;
                return [update];
            }
            return Clean(finalText).Length == 0 ? [] : [new CloudTranscriptUpdate(Clean(finalText), false, startMs, lastEndMs)];
        }
        var live = Clean(finalText + (finalText.Length == 0 || transcript.Length == 0 ? "" : " ") + transcript);
        return live.Length == 0 ? [] : [new CloudTranscriptUpdate(live, false, startMs, lastEndMs)];
    }

    private List<CloudTranscriptUpdate> ConsumeOpenAi(JsonObject json)
    {
        var type = String(json["type"]) ?? "";
        switch (type)
        {
            case "error":
                var message = String((json["error"] as JsonObject)?["message"]) ?? "未知错误";
                throw new CloudTranscriptException("OpenAI 转写错误：" + message);
            case "conversation.item.input_audio_transcription.delta":
                var item = String(json["item_id"]);
                var delta = String(json["delta"]);
                if (item is null || delta is null) return [];
                openAiItems[item] = openAiItems.GetValueOrDefault(item) + delta;
                return [new CloudTranscriptUpdate(Clean(openAiItems[item]), false)];
            case "conversation.item.input_audio_transcription.completed":
                var completedItem = String(json["item_id"]);
                if (completedItem is null) return [];
                var transcript = Clean(String(json["transcript"]) ?? openAiItems.GetValueOrDefault(completedItem) ?? "");
                openAiItems.Remove(completedItem);
                return transcript.Length == 0 ? [] : [new CloudTranscriptUpdate(transcript, true)];
            case "conversation.item.input_audio_transcription.failed":
                var failure = String((json["error"] as JsonObject)?["message"]) ?? "片段转写失败";
                if (String(json["item_id"]) is { } failedItem) openAiItems.Remove(failedItem);
                throw new CloudTranscriptException("OpenAI 转写：" + failure);
            default:
                return [];
        }
    }

    private static string Clean(string text) => text.Trim();

    private static string? String(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static int? Int(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    private static double? Double(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<double>(out var number) ? number : null;

    private static bool? Bool(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

    private static JsonArray Array(JsonNode? node) => node as JsonArray ?? [];
}

/// <summary>16 kHz → 24 kHz linear-interpolation resampler for OpenAI Realtime (2:3).</summary>
public static class PCMResampler
{
    public static short[] Upsample16kTo24k(short[] samples)
    {
        if (samples.Length <= 1) return samples;
        var outputCount = samples.Length * 3 / 2;
        var output = new short[outputCount];
        for (var index = 0; index < outputCount; index++)
        {
            var position = index * 2.0 / 3.0;
            var left = (int)position;
            var right = Math.Min(left + 1, samples.Length - 1);
            var fraction = position - left;
            output[index] = (short)(samples[left] * (1 - fraction) + samples[right] * fraction);
        }
        return output;
    }

    public static byte[] LittleEndianData(short[] samples)
    {
        var data = new byte[samples.Length * 2];
        for (var index = 0; index < samples.Length; index++)
        {
            var value = (ushort)samples[index];
            data[index * 2] = (byte)(value & 0xFF);
            data[index * 2 + 1] = (byte)(value >> 8);
        }
        return data;
    }
}
