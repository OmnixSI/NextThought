using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from CloudTranscriptionTests.swift: Soniox/Deepgram/OpenAI transcript assembly,
/// provider configuration messages, whisper migration and the 16 kHz → 24 kHz resampler.
/// </summary>
public sealed class CloudTranscriptionTestsParityTests
{
    [Fact]
    public void SonioxFinalTokensAccumulateAndEndTokenCommits()
    {
        var assembler = new CloudTranscriptAssembler(CloudSTTProvider.Soniox);
        var updates = assembler.Consume("""
            {"tokens":[{"text":"你好","is_final":true,"start_ms":100,"end_ms":400},{"text":" wor","is_final":false,"end_ms":500}]}
            """);
        Assert.Equal(new CloudTranscriptUpdate("你好 wor", false, 100, 500), updates[^1]);

        updates = assembler.Consume("""
            {"tokens":[{"text":" world","is_final":true,"end_ms":700},{"text":"<end>","is_final":true}]}
            """);
        Assert.Equal(new CloudTranscriptUpdate("你好 world", true, 100, 700), updates[0]);

        // Next utterance starts fresh.
        updates = assembler.Consume("""
            {"tokens":[{"text":"Next","is_final":false,"start_ms":900,"end_ms":1000}]}
            """);
        Assert.Equal("Next", updates[^1].Text);
        Assert.Throws<CloudTranscriptException>(() => assembler.Consume("""
            {"error_code":401,"error_message":"bad key"}
            """));
    }

    [Fact]
    public void DeepgramInterimThenSpeechFinal()
    {
        var assembler = new CloudTranscriptAssembler(CloudSTTProvider.Deepgram);
        var updates = assembler.Consume("""
            {"type":"Results","is_final":false,"speech_final":false,"start":1.0,"duration":0.5,"channel":{"alternatives":[{"transcript":"你好"}]}}
            """);
        Assert.Equal<CloudTranscriptUpdate>(
            [new CloudTranscriptUpdate("你好", false, 1_000, 1_500)], updates);

        updates = assembler.Consume("""
            {"type":"Results","is_final":true,"speech_final":true,"start":1.0,"duration":0.8,"channel":{"alternatives":[{"transcript":"你好吗"}]}}
            """);
        Assert.Equal("你好吗", updates[0].Text);
        Assert.True(updates[0].IsFinal);
    }

    [Fact]
    public void OpenAIDeltasAndCompletion()
    {
        var assembler = new CloudTranscriptAssembler(CloudSTTProvider.OpenAI);
        _ = assembler.Consume("""
            {"type":"conversation.item.input_audio_transcription.delta","item_id":"a","delta":"How "}
            """);
        var partial = assembler.Consume("""
            {"type":"conversation.item.input_audio_transcription.delta","item_id":"a","delta":"are you"}
            """);
        Assert.Equal("How are you", partial[0].Text);
        var final = assembler.Consume("""
            {"type":"conversation.item.input_audio_transcription.completed","item_id":"a","transcript":"How are you?"}
            """);
        Assert.Equal<CloudTranscriptUpdate>(
            [new CloudTranscriptUpdate("How are you?", true)], final);
    }

    [Fact]
    public void ConfigurationMessagesUseExpectedFormats()
    {
        var soniox = CloudSTTProvider.Soniox.ConfigurationMessage("", "auto", ["Kubernetes"]);
        Assert.NotNull(soniox);
        Assert.Contains("pcm_s16le", soniox);
        Assert.Contains("stt-rt-v5", soniox);
        Assert.Contains("Kubernetes", soniox);

        var openai = CloudSTTProvider.OpenAI.ConfigurationMessage("", "auto", ["Kubernetes"]);
        Assert.NotNull(openai);
        using var document = JsonDocument.Parse(openai!);
        var session = document.RootElement.GetProperty("session");
        var audio = session.GetProperty("audio");
        var input = audio.GetProperty("input");
        var transcription = input.GetProperty("transcription");
        Assert.Equal("gpt-live-transcribe", transcription.GetProperty("model").GetString());
        Assert.Equal<string>(
            ["zh", "en"],
            transcription.GetProperty("languages").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal<string>(
            ["Kubernetes"],
            transcription.GetProperty("keywords").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal(JsonValueKind.Null, input.GetProperty("turn_detection").ValueKind);
        Assert.Null(CloudSTTProvider.Deepgram.ConfigurationMessage("", "zh", Array.Empty<string>()));

        var url = CloudSTTProvider.Deepgram.Endpoint("", "zh", ["gRPC"]);
        Assert.NotNull(url);
        Assert.Contains("language=zh-CN", url);
        Assert.Contains("keyterm=gRPC", url);
    }

    [Fact]
    public void WhisperMigrationIsScopedToTheOpenAITranscriptionProvider()
    {
        Assert.True(CloudSTTProvider.OpenAI.IsRemovedWhisperModelIdentifier("gpt-realtime-whisper"));
        Assert.True(CloudSTTProvider.OpenAI.IsRemovedWhisperModelIdentifier("whisper-1"));
        Assert.False(CloudSTTProvider.Soniox.IsRemovedWhisperModelIdentifier("soniox-whisper-compatible"));
        Assert.False(CloudSTTProvider.Deepgram.IsRemovedWhisperModelIdentifier("custom-whisper-v2"));
    }

    [Fact]
    public void ResamplerProducesThreeHalvesSamples()
    {
        var input = new short[] { 0, 300, 600, 900 };
        var output = PCMResampler.Upsample16kTo24k(input);
        Assert.Equal(6, output.Length);
        Assert.Equal(0, output[0]);
        Assert.Equal(600, output[3]);
        Assert.Equal<byte>([1, 0, 255, 255], PCMResampler.LittleEndianData([1, -1]));
    }
}

/// <summary>
/// Translated from the CompanionTests struct in CloudTranscriptionTests.swift. The C# core has no
/// CompanionHTTP request parser, constant-time token comparison or SSE frame writer, so each of
/// those Swift tests is present but skipped until the API exists.
/// </summary>
public sealed class CompanionTestsParityTests
{
    [Fact]
    public void ParsesRequestLineAndQuery()
    {
        var request = CompanionHTTP.Parse("POST /action?t=abc&a=shorter HTTP/1.1\r\nHost: x\r\n\r\n");
        Assert.Equal("POST", request?.Method);
        Assert.Equal("/action", request?.Path);
        Assert.Equal("abc", request?.Query["t"]);
        Assert.Equal("shorter", request?.Query["a"]);
        Assert.Null(CompanionHTTP.Parse("garbage"));
        Assert.Null(CompanionHTTP.Parse("GET noslash HTTP/1.1\r\n"));
    }

    [Fact]
    public void TokenComparison()
    {
        Assert.True(CompanionHTTP.TokenMatches("abcd", "abcd"));
        Assert.False(CompanionHTTP.TokenMatches("abce", "abcd"));
        Assert.False(CompanionHTTP.TokenMatches(null, "abcd"));
        Assert.False(CompanionHTTP.TokenMatches("", ""));
    }

    [Fact]
    public void SseFraming()
    {
        var snapshot = new CompanionSnapshot("s", "q", "a\nb", false, "", "");
        var text = System.Text.Encoding.UTF8.GetString(CompanionHTTP.SseEvent(snapshot));
        Assert.StartsWith("data: {", text);
        Assert.EndsWith("\n\n", text);
        Assert.DoesNotContain("\n", text[..^2]);
    }
}
