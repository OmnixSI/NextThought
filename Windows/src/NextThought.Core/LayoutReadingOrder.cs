namespace NextThought.Core;

/// <summary>Conservative two-column whitespace correction for page-local layout boxes.</summary>
public static class LayoutReadingOrder
{
    public const string Version = "two-column-whitespace-v1";
    public static IReadOnlyList<DocumentBlock> Order(IReadOnlyList<DocumentBlock> blocks)
    {
        if (blocks.Count < 4 || blocks.Any(b => b.Box?.Count != 4)) return blocks;
        var extent = blocks.Max(b => b.Box![2]); var starts = blocks.Select(b => b.Box![0]).Distinct().Order().ToArray();
        if (starts.Length < 2) return blocks;
        var gap = starts.Zip(starts.Skip(1)).OrderByDescending(p => p.Second - p.First).First();
        if (gap.Second - gap.First < extent * .2) return blocks;
        var cut = (gap.First + gap.Second) / 2.0;
        var left = blocks.Where(b => b.Box![2] < cut).ToArray(); var right = blocks.Where(b => b.Box![0] > cut).ToArray();
        if (left.Length < 2 || right.Length < 2) return blocks;
        var leftEdge = left.Max(b => b.Box![2]); var rightEdge = right.Min(b => b.Box![0]);
        if (rightEdge - leftEdge < Math.Max(40, extent * .08)) return blocks;
        var remaining = left.Concat(right).ToList(); var result = new List<DocumentBlock>();
        void Band(IEnumerable<DocumentBlock> band)
        { result.AddRange(band.OrderBy(b => b.Box![0] < cut ? 0 : 1).ThenBy(b => b.Box![1]).ThenBy(b => b.Box![0])); }
        foreach (var span in blocks.Except(left).Except(right).OrderBy(b => b.Box![1]))
        {
            var before = remaining.Where(b => (b.Box![1] + b.Box![3]) / 2.0 < span.Box![1]).ToArray();
            Band(before); foreach (var block in before) remaining.Remove(block); result.Add(span);
        }
        Band(remaining); return result;
    }
}
