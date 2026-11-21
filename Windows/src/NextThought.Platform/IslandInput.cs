using System.Runtime.InteropServices;

namespace NextThought.Platform;

/// <summary>
/// C2: the little the cue island needs from the mouse beyond the window messages. A pinned deck is
/// put away by a click outside it; the island is click-through most of the time, so the button has
/// to be read from the system instead of from a window message.
/// </summary>
public static class IslandInput
{
    private const int VkLeftButton = 0x01;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    /// <summary>True while the primary mouse button is held down, anywhere on the desktop.</summary>
    public static bool LeftButtonDown => (GetAsyncKeyState(VkLeftButton) & 0x8000) != 0;
}
