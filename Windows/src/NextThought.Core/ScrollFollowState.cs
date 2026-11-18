namespace NextThought.Core;

/// <summary>
/// Whether a live pane follows incoming content or preserves the position being read
/// (port of ScrollFollowState.swift). Each pane owns its own value.
/// </summary>
public sealed class ScrollFollowState
{
    public enum Modes
    {
        Following,
        ReadingHistory,
        SelectingText
    }

    public Modes Mode { get; private set; } = Modes.Following;

    public int PendingItemCount { get; private set; }

    public bool IsFollowing => Mode == Modes.Following;

    public bool HasNewContent => PendingItemCount > 0;

    /// <summary>A user scroll away from the latest content pauses automatic following.</summary>
    public void UserScrolledAway()
    {
        if (!IsFollowing) return;
        Mode = Modes.ReadingHistory;
    }

    /// <summary>Selecting text also pins the viewport so streaming updates cannot move it.</summary>
    public void UserSelectedText() => Mode = Modes.SelectingText;

    /// <summary>Scrolling history back to the live edge resumes following; a selection stays pinned.</summary>
    public void ReachedLatest()
    {
        if (Mode != Modes.ReadingHistory) return;
        ReturnToLatest();
    }

    /// <summary>Counts completed transcript items or coarse answer updates while paused.</summary>
    public void ReceivedNewContent(int count = 1)
    {
        if (IsFollowing || count <= 0) return;
        PendingItemCount += count;
    }

    /// <summary>Explicitly restore the live edge and clear the unread count.</summary>
    public void ReturnToLatest()
    {
        Mode = Modes.Following;
        PendingItemCount = 0;
    }
}

/// <summary>A display rectangle in the same coordinate space as the input point.</summary>
public readonly record struct ScrollFrame(double X, double Y, double Width, double Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(double x, double y) => !IsEmpty && x >= X && x < X + Width && y >= Y && y < Y + Height;
}

/// <summary>
/// The small subset of input that can move a scroll view without a wheel or drag
/// (port of ScrollFollowInputClassifier.swift).
/// </summary>
public static class ScrollFollowInputClassifier
{
    /// <summary>macOS ANSI key codes for arrows, Home/End, Page Up/Down and Space.</summary>
    public static bool IsScrollNavigationKeyCode(ushort keyCode) =>
        keyCode is (>= 123 and <= 126) or 115 or 116 or 119 or 121 or 49;

    public static bool IsPoint(double x, double y, ScrollFrame? frame) =>
        frame is { } value && value.Contains(x, y);
}
