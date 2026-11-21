namespace NextThought.Core;

/// <summary>One of the answer-area modules (port of AssistantModule, AssistantLayout.swift).</summary>
public enum AssistantModule
{
    Conversation,
    Question,
    Answer,
    History,
    Insights
}

/// <summary>
/// E5: the assistant area's docked/detached modules and the two-column split (port of
/// AssistantLayout). Docked modules sit in one of two columns with a relative weight; a module
/// can be hidden or popped out into its own window. The layout serialises to JSON.
/// </summary>
public sealed record AssistantLayout
{
    public sealed record Slot(AssistantModule Module, int Column, double Weight);

    public IReadOnlyList<Slot> Slots { get; init; } = [];
    public IReadOnlyList<AssistantModule> Detached { get; init; } = [];
    /// <summary>Left column share of the width (0.25–0.75).</summary>
    public double Split { get; init; } = 0.44;

    public static AssistantLayout Standard => new()
    {
        Slots =
        [
            new Slot(AssistantModule.Conversation, 0, 1), new Slot(AssistantModule.History, 0, 0.45),
            new Slot(AssistantModule.Question, 1, 1), new Slot(AssistantModule.Answer, 1, 1)
        ]
    };

    public enum Preset { Standard, AnswerFirst, Presenting, Stacked, AnswerOnly }

    public static AssistantLayout For(Preset preset) => preset switch
    {
        Preset.AnswerFirst => new AssistantLayout
        {
            Slots =
            [
                new Slot(AssistantModule.Question, 0, 1), new Slot(AssistantModule.Answer, 0, 1),
                new Slot(AssistantModule.Conversation, 1, 1), new Slot(AssistantModule.History, 1, 0.5)
            ],
            Split = 0.58
        },
        Preset.Presenting => new AssistantLayout
        {
            Slots =
            [
                new Slot(AssistantModule.Insights, 0, 1.4), new Slot(AssistantModule.Conversation, 0, 0.6),
                new Slot(AssistantModule.Question, 1, 1), new Slot(AssistantModule.Answer, 1, 1)
            ],
            Split = 0.5
        },
        Preset.Stacked => new AssistantLayout
        {
            Slots =
            [
                new Slot(AssistantModule.Question, 0, 1), new Slot(AssistantModule.Answer, 0, 1.3),
                new Slot(AssistantModule.Conversation, 0, 1), new Slot(AssistantModule.History, 0, 0.45)
            ]
        },
        Preset.AnswerOnly => new AssistantLayout
        {
            Slots = [new Slot(AssistantModule.Question, 0, 1), new Slot(AssistantModule.Answer, 0, 1)]
        },
        _ => Standard
    };

    /// <summary>The module names the Windows UI can show (used for tests and the tooltips).</summary>
    public static string ModuleKey(AssistantModule module) => module switch
    {
        AssistantModule.Conversation => "conversation",
        AssistantModule.Question => "question",
        AssistantModule.Answer => "answer",
        AssistantModule.History => "history",
        _ => "insights"
    };

    public IReadOnlyList<Slot> SlotsIn(int column) => Slots.Where(slot => slot.Column == column).ToList();

    public bool IsDocked(AssistantModule module) => Slots.Any(slot => slot.Module == module);

    public bool IsDetached(AssistantModule module) => Detached.Contains(module);

    public bool IsHidden(AssistantModule module) => !IsDocked(module) && !IsDetached(module);

    public bool UsesTwoColumns => SlotsIn(0).Count > 0 && SlotsIn(1).Count > 0;

    /// <summary>Moves (or docks) a module into a column before the module at <paramref name="index"/>.</summary>
    public AssistantLayout Place(AssistantModule module, int column, int index)
    {
        var weight = Slots.FirstOrDefault(slot => slot.Module == module)?.Weight ?? 1;
        var slots = Slots.Where(slot => slot.Module != module).ToList();
        var detached = Detached.Where(item => item != module).ToList();
        column = Math.Min(Math.Max(column, 0), 1);
        var inColumn = slots.Select((slot, position) => (slot, position)).Where(pair => pair.slot.Column == column).ToList();
        var position = index < inColumn.Count
            ? inColumn[index].position
            : inColumn.Count > 0 ? inColumn[^1].position + 1 : slots.Count;
        slots.Insert(position, new Slot(module, column, weight));
        return this with { Slots = slots, Detached = detached };
    }

    public AssistantLayout Hide(AssistantModule module) => this with
    {
        Slots = Slots.Where(slot => slot.Module != module).ToList(),
        Detached = Detached.Where(item => item != module).ToList()
    };

    /// <summary>Shows a hidden module at the end of the column that usually holds it.</summary>
    public AssistantLayout Show(AssistantModule module)
    {
        if (!IsHidden(module)) return this;
        var column = module is AssistantModule.Conversation or AssistantModule.History
            ? 0
            : (SlotsIn(1).Count == 0 && SlotsIn(0).Count > 0 ? 0 : 1);
        return Place(module, column, int.MaxValue);
    }

    public AssistantLayout Detach(AssistantModule module) => this with
    {
        Slots = Slots.Where(slot => slot.Module != module).ToList(),
        Detached = Detached.Contains(module) ? Detached : [.. Detached, module]
    };

    public AssistantLayout Dock(AssistantModule module) => this with
    {
        Detached = Detached.Where(item => item != module).ToList()
    };

    public AssistantLayout Move(AssistantModule module, int delta)
    {
        var slot = Slots.FirstOrDefault(item => item.Module == module);
        if (slot is null) return this;
        var column = SlotsIn(slot.Column);
        var index = column.ToList().FindIndex(item => item.Module == module);
        if (index < 0) return this;
        var target = index + delta;
        return target < 0 || target >= column.Count ? this : Place(module, slot.Column, target);
    }

    public AssistantLayout WithWeight(AssistantModule module, double weight)
    {
        var slots = Slots.ToList();
        var index = slots.FindIndex(slot => slot.Module == module);
        if (index < 0) return this;
        slots[index] = slots[index] with { Weight = Math.Clamp(weight, 0.15, 6) };
        return this with { Slots = slots };
    }

    public AssistantLayout WithSplit(double split) => this with { Split = Math.Clamp(split, 0.25, 0.75) };

    /// <summary>Reads a saved layout; a module listed twice keeps its first place (Mac AssistantLayout.load).</summary>
    public static AssistantLayout FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Standard;
        try
        {
            var loaded = System.Text.Json.JsonSerializer.Deserialize<AssistantLayout>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (loaded is null) return Standard;
            var seen = new HashSet<AssistantModule>();
            var slots = loaded.Slots.Where(slot => seen.Add(slot.Module)).ToList();
            var detached = loaded.Detached.Where(module => seen.Add(module)).ToList();
            return loaded with { Slots = slots, Detached = detached, Split = Math.Clamp(loaded.Split, 0.25, 0.75) };
        }
        catch
        {
            return Standard;
        }
    }

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this);
}
