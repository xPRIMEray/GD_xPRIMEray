namespace XPrimeRay.ObservationLayer;

public readonly record struct FormalQuerySegmentV1(int ProbeStep, float TraveledB, float ChordLength);

public readonly record struct FormalQueryCoverageSummaryV1(uint QueriedChordBits, uint MaxUnqueriedGapBits);

public static class FormalQueryCoverageV1
{
    public static FormalQueryCoverageSummaryV1 Measure(float traveled, IEnumerable<FormalQuerySegmentV1> segments)
    {
        if (!float.IsFinite(traveled) || traveled < 0f) throw new ArgumentOutOfRangeException(nameof(traveled));
        var intervals = new List<(float Start, float End)>();
        foreach (FormalQuerySegmentV1 segment in segments ?? throw new ArgumentNullException(nameof(segments)))
        {
            if (segment.ProbeStep < 0 || !float.IsFinite(segment.TraveledB) || !float.IsFinite(segment.ChordLength) || segment.ChordLength < 0f)
                throw new ArgumentException("Query coverage inputs must be finite and nonnegative.", nameof(segments));
            float end = Math.Clamp(segment.TraveledB, 0f, traveled);
            float start = Math.Clamp(segment.TraveledB - segment.ChordLength, 0f, traveled);
            if (end < start) (start, end) = (end, start);
            intervals.Add((start, end));
        }
        intervals.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.End.CompareTo(b.End));
        float covered = 0f, maxGap = 0f, cursor = 0f;
        foreach ((float start, float end) in intervals)
        {
            if (start > cursor) maxGap = Math.Max(maxGap, start - cursor);
            if (end > cursor)
            {
                covered += end - Math.Max(cursor, start);
                cursor = end;
            }
        }
        maxGap = Math.Max(maxGap, traveled - cursor);
        return new FormalQueryCoverageSummaryV1(
            BitConverter.SingleToUInt32Bits(Math.Min(traveled, covered)),
            BitConverter.SingleToUInt32Bits(Math.Min(traveled, maxGap)));
    }
}
