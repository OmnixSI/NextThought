namespace NextThought.Core;

/// <summary>
/// Bounding region of meaningful changed pixels in two small RGBA previews
/// (port of ScreenDelta.swift). Coordinates are normalized to the preview.
/// </summary>
public static class ScreenDelta
{
    public static double[]? Region(byte[]? previous, byte[] current, int width, int height)
    {
        if (width <= 0 || height <= 0 || current.Length != width * height * 4) return null;
        if (previous is null || previous.Length != current.Length) return [0, 0, 1, 1];

        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;
        var changed = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4;
                if (Math.Abs(current[offset] - previous[offset]) >= 12
                    || Math.Abs(current[offset + 1] - previous[offset + 1]) >= 12
                    || Math.Abs(current[offset + 2] - previous[offset + 2]) >= 12)
                {
                    changed++;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        }
        if (changed < Math.Max(1, width * height / 2000)) return null;
        var margin = Math.Max(2, Math.Min(width, height) / 30);
        minX = Math.Max(0, minX - margin);
        minY = Math.Max(0, minY - margin);
        maxX = Math.Min(width - 1, maxX + margin);
        maxY = Math.Min(height - 1, maxY + margin);
        var w = (maxX - minX + 1) / (double)width;
        var h = (maxY - minY + 1) / (double)height;
        if (w * h > 0.75) return [0, 0, 1, 1];
        return [minX / (double)width, minY / (double)height, w, h];
    }
}
