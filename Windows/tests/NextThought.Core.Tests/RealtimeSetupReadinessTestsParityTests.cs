using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from RealtimeSetupReadinessTests.swift: recording readiness needs voice and audio but
/// never depends on the answer/document services, and an unasked microphone permission may be
/// requested while an explicit refusal may not be bypassed.
/// </summary>
public sealed class RealtimeSetupReadinessTestsParityTests
{
    [Fact]
    public void RecordingRequiresVoiceAndAudioButNotAnswerService()
    {
        for (var mask = 0; mask < 64; mask++)
        {
            var speech = (mask & 1) != 0;
            var capture = (mask & 2) != 0;
            var microphoneRequired = (mask & 4) != 0;
            var microphone = (mask & 8) != 0;
            var answers = (mask & 16) != 0;
            var audioSource = (mask & 32) != 0;
            var setup = new RealtimeSetupReadiness(speech, capture, microphoneRequired, microphone, answers, audioSource);
            Assert.Equal(speech && capture && (!microphoneRequired || microphone), setup.VoiceReady);
            Assert.Equal(speech && capture && (!microphoneRequired || microphone) && audioSource, setup.CanRecord);
            Assert.Equal(setup.CanRecord, setup.CanStart);
            Assert.Equal(answers, setup.CanAnswer);
        }
    }

    [Fact]
    public void OptionalDocumentModelsAreNotNeededToStartRealtimeAssistance()
    {
        var setup = new RealtimeSetupReadiness(true, true, true, true, true, true);
        var documents = new DocumentFeatureAvailability(false, false, false, false, false, false);
        Assert.False(documents.Ready(DocumentFeatureAvailability.Feature.Paddle));
        Assert.False(documents.Ready(DocumentFeatureAvailability.Feature.Semantic));
        Assert.True(setup.CanStart);
    }

    [Fact]
    public void StartCanRequestPendingMicrophonePermissionButOfflineCannotSkipDenial()
    {
        var permissionCanBeRequested = RealtimeSetupReadiness.MicrophonePermissionAllowsStart(
            isGranted: false, isUndetermined: true);
        var deniedPermissionBlocks = RealtimeSetupReadiness.MicrophonePermissionAllowsStart(
            isGranted: false, isUndetermined: false);
        var deniedRequiredMicrophone = new RealtimeSetupReadiness(
            true, true, true, deniedPermissionBlocks, true, true);
        var optionalOnlineMicrophoneDisabled = new RealtimeSetupReadiness(
            true, true, false, false, false, true);

        Assert.True(permissionCanBeRequested);
        Assert.True(new RealtimeSetupReadiness(
            true, true, true, permissionCanBeRequested, false, true).CanRecord);
        Assert.False(deniedRequiredMicrophone.CanRecord);
        Assert.True(optionalOnlineMicrophoneDisabled.CanRecord);
        Assert.True(RealtimeSetupReadiness.RequiresMicrophone(microphoneEnabled: false, isOfflineMeeting: true));
        Assert.False(RealtimeSetupReadiness.RequiresMicrophone(microphoneEnabled: false, isOfflineMeeting: false));
    }
}

/// <summary>
/// Translated from RecordingReadinessTests in RealtimeSetupReadinessTests.swift: the core audio
/// capture requirements cannot be bypassed, AI stays optional, and the online system-audio path
/// still needs a selected source.
/// </summary>
public sealed class RecordingReadinessTestsParityTests
{
    [Fact]
    public void CoreAudioCannotBeBypassedAndAIIsOptional()
    {
        for (var mask = 0; mask < 128; mask++)
        {
            var speech = (mask & 1) != 0;
            var capture = (mask & 2) != 0;
            var microphoneRequired = (mask & 4) != 0;
            var microphone = (mask & 8) != 0;
            var answers = (mask & 16) != 0;
            var audioSource = (mask & 32) != 0;
            var systemAudioRequired = (mask & 64) != 0;
            var setup = new RealtimeSetupReadiness(speech, capture, microphoneRequired, microphone,
                answers, audioSource, systemAudioRequired);
            Assert.Equal(
                speech && (!systemAudioRequired || capture) && (!microphoneRequired || microphone),
                setup.VoiceReady);
            Assert.Equal(
                speech && (!systemAudioRequired || capture) && (!microphoneRequired || microphone)
                    && (!systemAudioRequired || audioSource),
                setup.CanStart);
            Assert.Equal(answers, setup.CanUseAI);
        }
    }

    [Fact]
    public void FirstMicrophonePermissionCanBeRequestedButRefusalCannotBeBypassed()
    {
        foreach (var permission in Enum.GetValues<RecordingPermission>())
        {
            var setup = new RecordingReadiness(true, false, false, false, true, permission);
            Assert.Equal(permission != RecordingPermission.Denied, setup.CanRequestStart);
            Assert.Equal(permission == RecordingPermission.Granted, setup.CanStartRecording);
            RecordingIssue[] expectedIssues = permission == RecordingPermission.Granted
                ? []
                : [RecordingIssue.MicrophonePermission];
            Assert.Equal<RecordingIssue>(expectedIssues, setup.Issues);
        }
    }

    [Fact]
    public void OnlineSourceMustStillExistAfterPermissionWasGranted()
    {
        var setup = new RecordingReadiness(true, true, false, true, false, RecordingPermission.Denied);
        Assert.False(setup.CanRequestStart);
        Assert.False(setup.CanStartRecording);
        Assert.Equal<RecordingIssue>([RecordingIssue.AudioSource], setup.Issues);

        var unasked = new RecordingReadiness(true, false, false, true, false, RecordingPermission.Undetermined);
        Assert.True(unasked.CanRequestStart);
        Assert.False(unasked.CanStartRecording);
        Assert.Equal<RecordingIssue>([RecordingIssue.SystemAudioPermission], unasked.Issues);
    }

    [Fact]
    public void InPersonMeetingNeedsMicrophoneButNotSystemAudio()
    {
        var offline = new RealtimeSetupReadiness(true, false, true, true, true, false, false);
        var online = new RealtimeSetupReadiness(true, false, true, true, true, false);

        Assert.True(offline.CanStart);
        Assert.False(online.CanStart);
        Assert.False(new RealtimeSetupReadiness(true, false, true, false, true, false, false).CanStart);
    }

    [Fact]
    public void OptionalDocumentModelsAreNotNeededToStartRealtimeAssistance()
    {
        var setup = new RealtimeSetupReadiness(true, true, true, true, true, true);
        var documents = new DocumentFeatureAvailability(false, false, false, false, false, false);
        Assert.False(documents.Ready(DocumentFeatureAvailability.Feature.Paddle));
        Assert.False(documents.Ready(DocumentFeatureAvailability.Feature.Semantic));
        Assert.True(setup.CanStart);
    }
}
