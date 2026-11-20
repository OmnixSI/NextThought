using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>E5: the assistant module layout (AssistantLayout.swift).</summary>
public sealed class AssistantLayoutTests
{
    [Fact]
    public void StandardIsTwoColumnsWithTheMacSlots()
    {
        var layout = AssistantLayout.Standard;
        Assert.True(layout.UsesTwoColumns);
        Assert.Equal(0.44, layout.Split);
        Assert.Equal([AssistantModule.Conversation, AssistantModule.History], layout.SlotsIn(0).Select(slot => slot.Module));
        Assert.Equal([AssistantModule.Question, AssistantModule.Answer], layout.SlotsIn(1).Select(slot => slot.Module));
        Assert.All(layout.Slots, slot => Assert.True(slot.Weight > 0));
        Assert.True(layout.IsDocked(AssistantModule.Answer));
        Assert.False(layout.IsDetached(AssistantModule.Answer));
        Assert.True(layout.IsHidden(AssistantModule.Insights));
    }

    [Fact]
    public void EveryPresetMatchesTheMacSlots()
    {
        var answerFirst = AssistantLayout.For(AssistantLayout.Preset.AnswerFirst);
        Assert.Equal(0.58, answerFirst.Split);
        Assert.Equal([AssistantModule.Question, AssistantModule.Answer], answerFirst.SlotsIn(0).Select(slot => slot.Module));
        Assert.Equal([AssistantModule.Conversation, AssistantModule.History], answerFirst.SlotsIn(1).Select(slot => slot.Module));

        var presenting = AssistantLayout.For(AssistantLayout.Preset.Presenting);
        Assert.Equal(0.5, presenting.Split);
        Assert.Equal([AssistantModule.Insights, AssistantModule.Conversation], presenting.SlotsIn(0).Select(slot => slot.Module));
        Assert.Equal([AssistantModule.Question, AssistantModule.Answer], presenting.SlotsIn(1).Select(slot => slot.Module));

        var stacked = AssistantLayout.For(AssistantLayout.Preset.Stacked);
        Assert.False(stacked.UsesTwoColumns);
        Assert.Equal(
            [AssistantModule.Question, AssistantModule.Answer, AssistantModule.Conversation, AssistantModule.History],
            stacked.SlotsIn(0).Select(slot => slot.Module));

        var answerOnly = AssistantLayout.For(AssistantLayout.Preset.AnswerOnly);
        Assert.Equal([AssistantModule.Question, AssistantModule.Answer], answerOnly.Slots.Select(slot => slot.Module));
    }

    [Fact]
    public void DetachAndDockFollowMacSemantics()
    {
        var layout = AssistantLayout.Standard.Detach(AssistantModule.Answer);
        Assert.True(layout.IsDetached(AssistantModule.Answer));
        Assert.False(layout.IsDocked(AssistantModule.Answer));
        Assert.DoesNotContain(layout.Slots, slot => slot.Module == AssistantModule.Answer);

        var docked = layout.Dock(AssistantModule.Answer);
        Assert.False(docked.IsDetached(AssistantModule.Answer));
        Assert.True(docked.IsHidden(AssistantModule.Answer)); // docked but not yet placed (Mac: window close calls dockModule)

        var shown = docked.Show(AssistantModule.Answer);
        Assert.True(shown.IsDocked(AssistantModule.Answer));
        Assert.Contains(shown.SlotsIn(1), slot => slot.Module == AssistantModule.Answer);
    }

    [Fact]
    public void HideShowPlaceAndMoveKeepOneSlotPerModule()
    {
        var layout = AssistantLayout.Standard
            .Hide(AssistantModule.History)
            .Place(AssistantModule.Conversation, 1, 0);
        Assert.True(layout.IsHidden(AssistantModule.History));
        Assert.DoesNotContain(layout.Slots, slot => slot.Module == AssistantModule.History);
        var conversationSlots = layout.Slots.Where(slot => slot.Module == AssistantModule.Conversation).ToList();
        Assert.Single(conversationSlots);
        Assert.Equal(1, conversationSlots[0].Column);
        Assert.Equal(0, layout.Slots.ToList().IndexOf(conversationSlots[0]));

        var shown = layout.Show(AssistantModule.History);
        Assert.True(shown.IsDocked(AssistantModule.History));

        var standard = AssistantLayout.Standard;
        var moved = standard.Move(AssistantModule.Answer, -1);
        Assert.Equal(AssistantModule.Answer, moved.SlotsIn(1)[0].Module);
        // Moving beyond the column edge keeps the layout unchanged (Move returns the same instance).
        Assert.Same(standard, standard.Move(AssistantModule.Answer, 1));
    }

    [Fact]
    public void WeightsAndSplitAreClamped()
    {
        var layout = AssistantLayout.Standard
            .WithWeight(AssistantModule.Answer, 99)
            .WithSplit(0.9);
        Assert.Equal(6, layout.Slots.First(slot => slot.Module == AssistantModule.Answer).Weight);
        Assert.Equal(0.75, layout.Split);
        Assert.Equal(0.25, AssistantLayout.Standard.WithSplit(0.1).Split);
        Assert.Equal(0.15, AssistantLayout.Standard.WithWeight(AssistantModule.Answer, 0.01).Slots
            .First(slot => slot.Module == AssistantModule.Answer).Weight);
    }

    [Fact]
    public void JsonRoundTripsAndRejectsDuplicateSlots()
    {
        var json = AssistantLayout.For(AssistantLayout.Preset.Presenting).ToJson();
        var loaded = AssistantLayout.FromJson(json);
        Assert.Equal(json, loaded.ToJson());

        var duplicate = AssistantLayout.FromJson("""
            {"Slots":[{"Module":2,"Column":1,"Weight":1},{"Module":2,"Column":0,"Weight":1}],"Detached":[2],"Split":0.3}
            """);
        Assert.Single(duplicate.Slots.Where(slot => slot.Module == AssistantModule.Answer));
        Assert.Empty(duplicate.Detached); // already docked once, the duplicate is dropped

        Assert.Equal(AssistantLayout.Standard.ToJson(), AssistantLayout.FromJson(null).ToJson());
        Assert.Equal(AssistantLayout.Standard.ToJson(), AssistantLayout.FromJson("{not json").ToJson());
    }

    [Fact]
    public void EveryModuleHasAStableKey()
    {
        foreach (var module in Enum.GetValues<AssistantModule>())
            Assert.False(string.IsNullOrWhiteSpace(AssistantLayout.ModuleKey(module)));
        Assert.Equal("conversation", AssistantLayout.ModuleKey(AssistantModule.Conversation));
        Assert.Equal("insights", AssistantLayout.ModuleKey(AssistantModule.Insights));
    }
}
