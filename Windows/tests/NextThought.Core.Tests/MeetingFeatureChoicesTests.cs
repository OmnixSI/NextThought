using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// J2 (§7): 这场用什么 — the per-series feature choices. Ported from
/// Sources/NextThought/LiveNotesEngine.swift:151-178, where the Mac keeps them in
/// <c>UserDefaults</c> under <c>"meetingFeatures." + seriesID.uuidString</c>, never in the
/// Workspace/series file and never in a backup, and from Sources/NextThought/PrepareView.swift
/// FeaturePicker: 实时转写 is an always-on row (<c>isOn: nil</c>), the four switches the Mac stores
/// are answers / liveNotes / translation / speakers.
///
/// The Windows sidecar (<see cref="MeetingFeatureChoices.FileName"/>) uses exactly the Mac key
/// names at the root of its JSON, so a name never drifts between the two apps, but it stays out of
/// the series file and out of a backup (Mac UserDefaults are not part of one either).
/// </summary>
public sealed class MeetingFeatureChoicesTests
{
    private const string SeriesId = "7F4E9A2C-1B3D-4E5F-8A9B-0C1D2E3F4A5B";

    [Fact]
    public void StandardFollowsTheSceneAndKeepsInterpretationOn()
    {
        // LiveNotes.swift:188-191 standard(prompting:speakers:).
        Assert.Equal(new MeetingFeatures(true, false, true, false), MeetingFeatures.Standard(prompting: true, speakers: false));
        Assert.Equal(new MeetingFeatures(false, true, true, false), MeetingFeatures.Standard(prompting: false, speakers: false));
        // Answers where the scene prompts, notes where it only records; interpretation on by default.
        var prompting = MeetingFeatures.Standard(prompting: true, speakers: true);
        Assert.True(prompting.Answers);
        Assert.False(prompting.LiveNotes);
        Assert.True(prompting.Translation);
        Assert.True(prompting.Speakers);
        var recording = MeetingFeatures.Standard(prompting: false, speakers: true);
        Assert.False(recording.Answers);
        Assert.True(recording.LiveNotes);
    }

    [Fact]
    public void TheKeyIsTheMacUserDefaultsKey()
    {
        // LiveNotesEngine.swift:151 "meetingFeatures." + (draft series id ?? active workspace id).
        Assert.Equal("meetingFeatures." + SeriesId, MeetingFeatureChoices.KeyFor(SeriesId));
        // Mac UUID.uuidString is upper-case; ids may reach Core in either case (A6).
        Assert.Equal("meetingFeatures." + SeriesId, MeetingFeatureChoices.KeyFor(SeriesId.ToLowerInvariant()));
        Assert.Equal($"meetingFeatures.{SeriesId}", MeetingFeatureChoices.KeyFor(" " + SeriesId.ToLowerInvariant() + " "));
    }

    [Fact]
    public void UnsetSeriesUsesTheSceneDefaultAndASavedChoiceIsKeptForTheNextMeeting()
    {
        var store = new MeetingFeatureChoices();
        // Mac meetingFeatures getter: saved by series key, else standardMeetingFeatures.
        Assert.Equal(MeetingFeatures.Standard(prompting: true, speakers: false),
            store.ForSeries(SeriesId, prompting: true, speakers: false));
        Assert.Equal(MeetingFeatures.Standard(prompting: false, speakers: true),
            store.ForSeries(SeriesId, prompting: false, speakers: true));

        var chosen = new MeetingFeatures(Answers: false, LiveNotes: true, Translation: false, Speakers: true);
        store.Set(SeriesId.ToLowerInvariant(), chosen);
        // A later call with a different scene default still reads the series' choice (下一场沿用).
        Assert.Equal(chosen, store.ForSeries(SeriesId, prompting: true, speakers: false));
        Assert.Equal(chosen, store.ForSeries(SeriesId.ToLowerInvariant(), prompting: false, speakers: true));
        // Another series is untouched.
        Assert.Equal(MeetingFeatures.Standard(prompting: true, speakers: true),
            store.ForSeries("00000000-0000-0000-0000-0000000000AA", prompting: true, speakers: true));
    }

    [Fact]
    public void ChoicesRoundTripPerSeriesAndAreForgottenOneAtATime()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-j2-features-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, MeetingFeatureChoices.FileName);
        try
        {
            var store = MeetingFeatureChoices.Load(path);
            Assert.Null(store.Get(SeriesId));
            var interview = new MeetingFeatures(Answers: true, LiveNotes: false, Translation: true, Speakers: true);
            var meeting = new MeetingFeatures(Answers: false, LiveNotes: true, Translation: false, Speakers: false);
            store.Set(SeriesId, interview);
            store.Set("00000000-0000-0000-0000-0000000000BB", meeting);
            store.Save(path);
            Assert.True(File.Exists(path));

            var reloaded = MeetingFeatureChoices.Load(path);
            Assert.Equal(interview, reloaded.Get(SeriesId.ToLowerInvariant()));
            Assert.Equal(meeting, reloaded.Get("00000000-0000-0000-0000-0000000000BB"));
            Assert.Equal(interview, reloaded.ForSeries(SeriesId, prompting: false, speakers: false));
            // Mac resetMeetingFeatures removes exactly one series' key; next time that series falls
            // back to the scene default, and the other series is untouched.
            Assert.True(reloaded.Remove(SeriesId));
            reloaded.Save(path);
            var again = MeetingFeatureChoices.Load(path);
            Assert.Null(again.Get(SeriesId));
            Assert.Equal(MeetingFeatures.Standard(prompting: false, speakers: true),
                again.ForSeries(SeriesId, prompting: false, speakers: true));
            Assert.Equal(meeting, again.Get("00000000-0000-0000-0000-0000000000BB"));
        }
        finally { try { Directory.Delete(root, true); } catch { /* best effort */ } }
    }

    [Fact]
    public void TheStoredJsonUsesTheMacKeysAndOnlyTheFourPersistedFields()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-j2-json-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, MeetingFeatureChoices.FileName);
        try
        {
            var store = new MeetingFeatureChoices();
            store.Set(SeriesId, new MeetingFeatures(true, false, true, true));
            store.Save(path);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            // The sidecar's root keys are the Mac UserDefaults keys themselves.
            var entry = document.RootElement.GetProperty(MeetingFeatureChoices.KeyFor(SeriesId));
            var names = entry.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray();
            // 实时转写 is always on in the Mac UI and has no persisted field: never invent one.
            Assert.Equal(new[] { "answers", "liveNotes", "speakers", "translation" }, names);
            Assert.True(entry.GetProperty("answers").GetBoolean());
            Assert.False(entry.GetProperty("liveNotes").GetBoolean());
            Assert.True(entry.GetProperty("translation").GetBoolean());
            Assert.True(entry.GetProperty("speakers").GetBoolean());
        }
        finally { try { Directory.Delete(root, true); } catch { /* best effort */ } }
    }

    [Fact]
    public void AMissingOrDamagedFileIsAnEmptyStoreRatherThanAnError()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-j2-damaged-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var missing = MeetingFeatureChoices.Load(Path.Combine(root, "nope.json"));
            Assert.Empty(missing.Keys);
            var damagedPath = Path.Combine(root, "damaged.json");
            File.WriteAllText(damagedPath, "{ this is not json");
            Assert.Empty(MeetingFeatureChoices.Load(damagedPath).Keys);
            File.WriteAllText(damagedPath, "");
            Assert.Empty(MeetingFeatureChoices.Load(damagedPath).Keys);
        }
        finally { try { Directory.Delete(root, true); } catch { /* best effort */ } }
    }

    [Fact]
    public void SavingTheSidecarDoesNotTouchTheSeriesFileOrAddKeysToIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-j2-sidecar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            // A Mac series file as the app stores it: the J2 choices are NOT written into it.
            var fixture = MacFixtures.Available ? MacFixtures.FixturePath("workspace.json") : null;
            var seriesJson = fixture is not null
                ? File.ReadAllText(fixture)
                : JsonSerializer.Serialize(WorkspaceSeries.Create("系列") with { Mode = "meeting" },
                    JsonDefaults.ArchiveOptions);
            var before = JsonDocument.Parse(seriesJson).RootElement
                .EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray();
            Assert.DoesNotContain(before, name => name.Contains("meetingFeature", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(before, name => name.Contains("features", StringComparison.OrdinalIgnoreCase));

            // Saving a choice goes to the Windows sidecar, not the series file.
            var path = Path.Combine(root, MeetingFeatureChoices.FileName);
            var store = new MeetingFeatureChoices();
            store.Set(SeriesId, new MeetingFeatures(true, false, true, false));
            store.Save(path);
            Assert.True(File.Exists(path));
            Assert.Equal(seriesJson, fixture is not null ? File.ReadAllText(fixture) : seriesJson);

            // A Mac series file round-trips through the Core type without gaining keys.
            var series = JsonSerializer.Deserialize<WorkspaceSeries>(seriesJson, JsonDefaults.ArchiveOptions)!;
            var encoded = JsonSerializer.Serialize(series, JsonDefaults.ArchiveOptions);
            var after = JsonDocument.Parse(encoded).RootElement
                .EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray();
            Assert.Equal(before, after);
            Assert.DoesNotContain(after, name => name.Contains("meetingFeature", StringComparison.OrdinalIgnoreCase));
        }
        finally { try { Directory.Delete(root, true); } catch { /* best effort */ } }
    }
}
