using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for cloud STT provider config, transcript assembly and resampling.</summary>
public sealed class CloudTranscriptionTests
{
    [Fact]
    public void ProvidersExposeDefaultsAndRates()
    {
        Assert.Equal("stt.soniox", CloudSTTProvider.Soniox.KeychainAccount());
        Assert.Equal("stt-rt-v5", CloudSTTProvider.Soniox.DefaultModel());
        Assert.Equal(24000, CloudSTTProvider.OpenAI.SampleRate());
        Assert.Equal(16000, CloudSTTProvider.Deepgram.SampleRate());
        Assert.True(CloudSTTProvider.OpenAI.IsRemovedWhisperModelIdentifier("gpt-realtime-whisper"));
        Assert.True(CloudSTTProvider.OpenAI.IsRemovedWhisperModelIdentifier("Whisper-1"));
        Assert.False(CloudSTTProvider.Deepgram.IsRemovedWhisperModelIdentifier("my-whisper-model"));
        Assert.False(CloudSTTProvider.OpenAI.SendsBinaryAudio());
        Assert.True(CloudSTTProvider.Soniox.SendsBinaryAudio());
    }

    [Fact]
    public void EndpointsCarryModelLanguageAndKeyterms()
    {
        Assert.Equal("wss://stt-rt.soniox.com/transcribe-websocket", CloudSTTProvider.Soniox.Endpoint("", "auto", []));
        Assert.Equal("wss://api.openai.com/v1/realtime?intent=transcription", CloudSTTProvider.OpenAI.Endpoint("m", "en", []));
        var deepgram = CloudSTTProvider.Deepgram.Endpoint("", "en", ["alpha", "beta gamma"])!;
        Assert.StartsWith("wss://api.deepgram.com/v1/listen?", deepgram);
        Assert.Contains("model=nova-3", deepgram);
        Assert.Contains("language=en", deepgram);
        Assert.Contains("keyterm=alpha", deepgram);
        Assert.Contains("keyterm=beta%20gamma", deepgram);
        Assert.Contains("language=zh-CN", CloudSTTProvider.Deepgram.Endpoint("", "auto", [])!);
    }

    [Fact]
    public void AuthorizationAndMessageShapesMatchTheProviders()
    {
        Assert.Equal("Bearer key", CloudSTTProvider.Soniox.AuthorizationHeader("key"));
        Assert.Equal("Token key", CloudSTTProvider.Deepgram.AuthorizationHeader("key"));

        Assert.Equal("{\"type\":\"keepalive\"}", CloudSTTProvider.Soniox.KeepAliveMessage());
        Assert.Null(CloudSTTProvider.OpenAI.KeepAliveMessage());
        Assert.Equal("", CloudSTTProvider.Soniox.CloseMessage());
        Assert.Equal("{\"type\":\"CloseStream\"}", CloudSTTProvider.Deepgram.CloseMessage());
        Assert.Equal("{\"type\":\"input_audio_buffer.commit\"}", CloudSTTProvider.OpenAI.CloseMessage());

        var append = CloudSTTProviders.OpenAiAppendMessage([1, 2, 3]);
        Assert.Contains("\"audio\":\"AQID\"", append);

        var soniox = CloudSTTProvider.Soniox.ConfigurationMessage("", "auto", ["term1"]);
        Assert.Contains("\"model\":\"stt-rt-v5\"", soniox);
        Assert.Contains("\"language_hints\":[\"zh\",\"en\"]", soniox);
        Assert.Contains("\"terms\":[\"term1\"]", soniox);
        var openai = CloudSTTProvider.OpenAI.ConfigurationMessage("", "en", ["bad<term>", "ok"]);
        Assert.Contains("session.update", openai);
        Assert.Contains("\"rate\":24000", openai);
        Assert.Contains("\"keywords\":[\"ok\"]", openai);
        Assert.Null(CloudSTTProvider.Deepgram.ConfigurationMessage("", "en", []));
    }

    [Fact]
    public void SonioxAssemblerEmitsLiveAndFinalUpdates()
    {
        var assembler = new CloudTranscriptAssembler(CloudSTTProvider.Soniox);
        var live = assembler.Consume("""
            {"tokens":[{"text":"你","is_final":true,"start_ms":0,"end_ms":100},{"text":"好","is_final":false,"end_ms":200}]}
            """);
        var update = Assert.Single(live);
        Assert.Equal("你好", update.Text);
        Assert.False(update.IsFinal);
        Assert.Equal(0, update.StartMs);
        Assert.Equal(200, update.EndMs);

        var final = assembler.Consume("""
            {"tokens":[{"text":"<end>","is_final":true}]}
            """);
        // The non-final tail is dropped when the utterance ends, like the Mac assembler.
        Assert.Equal("你", Assert.Single(final).Text);
        Assert.True(final[0].IsFinal);

        var finished = assembler.Consume("""
            {"tokens":[{"text":"再","is_final":true}],"finished":true}
            """);
        Assert.Contains(finished, item => item.Text == "再" && item.IsFinal);
    }

    [Fact]
    public void DeepgramAssemblerAccumulatesUntilSpeechFinal()
    {
        var assembler = new CloudTranscriptAssembler(CloudSTTProvider.Deepgram);
        var interim = assembler.Consume("""
            {"type":"Results","start":1.5,"duration":0.5,"is_final":true,"speech_final":false,
             "channel":{"alternatives":[{"transcript":"hello"}]}}
            """);
        Assert.Equal("hello", Assert.Single(interim).Text);
        Assert.False(interim[0].IsFinal);

        var final = assembler.Consume("""
            {"type":"Results","start":1.5,"duration":0.5,"is_final":true,"speech_final":true,
             "channel":{"alternatives":[{"transcript":"world"}]}}
            """);
        Assert.Equal("hello world", Assert.Single(final).Text);
        Assert.True(final[0].IsFinal);

        assembler.Consume("""
            {"type":"Results","is_final":true,"speech_final":false,"channel":{"alternatives":[{"transcript":"next"}]}}
            """);
        var ended = assembler.Consume("""{"type":"UtteranceEnd"}""");
        Assert.Equal("next", Assert.Single(ended).Text);
        Assert.True(ended[0].IsFinal);
    }

    [Fact]
    public void OpenAI_AssemblerUsesPerItemDeltasAndCompletion()
    {
        var assembler = new CloudTranscriptAssembler(CloudSTTProvider.OpenAI);
        assembler.Consume("""
            {"type":"conversation.item.input_audio_transcription.delta","item_id":"i1","delta":"Hel"}
            """);
        var live = assembler.Consume("""
            {"type":"conversation.item.input_audio_transcription.delta","item_id":"i1","delta":"lo"}
            """);
        Assert.Equal("Hello", Assert.Single(live).Text);
        Assert.False(live[0].IsFinal);

        var done = assembler.Consume("""
            {"type":"conversation.item.input_audio_transcription.completed","item_id":"i1","transcript":"Hello there"}
            """);
        Assert.Equal("Hello there", Assert.Single(done).Text);
        Assert.True(done[0].IsFinal);
        Assert.Empty(assembler.Consume("""
            {"type":"conversation.item.input_audio_transcription.completed","item_id":"i1"}
            """));
    }

    [Fact]
    public void ProviderErrorsBecomeExceptions()
    {
        Assert.Throws<CloudTranscriptException>(() =>
            new CloudTranscriptAssembler(CloudSTTProvider.Soniox).Consume("""{"error_code":401,"error_message":"bad key"}"""));
        Assert.Throws<CloudTranscriptException>(() =>
            new CloudTranscriptAssembler(CloudSTTProvider.Deepgram).Consume("""{"err_msg":"boom"}"""));
        Assert.Throws<CloudTranscriptException>(() =>
            new CloudTranscriptAssembler(CloudSTTProvider.OpenAI).Consume("""{"type":"error","error":{"message":"nope"}}"""));
        Assert.Throws<CloudTranscriptException>(() =>
            new CloudTranscriptAssembler(CloudSTTProvider.OpenAI).Consume("""{"type":"conversation.item.input_audio_transcription.failed","item_id":"i1","error":{"message":"bad"}}"""));
    }

    [Fact]
    public void InvalidMessagesAreIgnored()
    {
        var assembler = new CloudTranscriptAssembler(CloudSTTProvider.Soniox);
        Assert.Empty(assembler.Consume("not json"));
        Assert.Empty(assembler.Consume("[]"));
        Assert.Empty(assembler.Consume("""{"tokens":[]}"""));
    }

    [Fact]
    public void ResamplerUpsamplesAndWritesLittleEndian()
    {
        Assert.Equal<short[]>([1, 1, 2], PCMResampler.Upsample16kTo24k([1, 2]));
        var output = PCMResampler.Upsample16kTo24k([0, 30, 60]);
        Assert.Equal(4, output.Length);
        Assert.Equal(0, output[0]);
        Assert.Equal(20, output[1]);
        Assert.Equal(40, output[2]);
        Assert.Equal(60, output[3]);

        Assert.Equal([0x01, 0x00, 0xFF, 0xFF], PCMResampler.LittleEndianData([1, -1]));
    }
}
