namespace NextThought.Core;

public enum RecordingPermission
{
    Undetermined,
    Granted,
    Denied
}

public enum RecordingIssue
{
    Recognition,
    SystemAudioPermission,
    AudioSource,
    MicrophonePermission
}

/// <summary>
/// Only the capabilities needed to capture a meeting (port of RecordingReadiness in
/// RealtimeSetupReadiness.swift). AI services are deliberately independent of this decision.
/// </summary>
public sealed record RecordingReadiness(
    bool Speech,
    bool Capture,
    bool AudioSource,
    bool SystemAudioRequired,
    bool MicrophoneRequired,
    RecordingPermission Microphone)
{
    public IReadOnlyList<RecordingIssue> Issues
    {
        get
        {
            var issues = new List<RecordingIssue>();
            if (!Speech) issues.Add(RecordingIssue.Recognition);
            if (SystemAudioRequired)
            {
                if (!Capture) issues.Add(RecordingIssue.SystemAudioPermission);
                else if (!AudioSource) issues.Add(RecordingIssue.AudioSource);
            }
            if (MicrophoneRequired && Microphone != RecordingPermission.Granted) issues.Add(RecordingIssue.MicrophonePermission);
            return issues;
        }
    }

    public bool CanStartRecording => Issues.Count == 0;

    /// <summary>An unasked permission is requested by the start flow; an explicit refusal needs repair.</summary>
    public bool CanRequestStart =>
        Speech
        && (!SystemAudioRequired || !Capture || AudioSource)
        && (!MicrophoneRequired || Microphone != RecordingPermission.Denied);
}

/// <summary>The setup checklist shown before recording (port of RealtimeSetupReadiness).</summary>
public sealed record RealtimeSetupReadiness(
    bool Speech,
    bool Capture,
    bool MicrophoneRequired,
    bool Microphone,
    bool Answers,
    bool AudioSource,
    bool SystemAudioRequired = true)
{
    public static bool MicrophonePermissionAllowsStart(bool isGranted, bool isUndetermined) => isGranted || isUndetermined;

    public static bool RequiresMicrophone(bool microphoneEnabled, bool isOfflineMeeting) => microphoneEnabled || isOfflineMeeting;

    public bool SystemAudioReady => !SystemAudioRequired || (Capture && AudioSource);

    public bool VoiceReady => Speech && (!SystemAudioRequired || Capture) && (!MicrophoneRequired || Microphone);

    /// <summary>Retained for existing callers; recording never depends on an AI service.</summary>
    public bool CoreReady => VoiceReady;

    public bool CanRecord => CoreReady && (!SystemAudioRequired || AudioSource);

    public bool CanAnswer => Answers;

    public bool CanStart => CanRecord;

    public bool CanUseAI => CanAnswer;
}
