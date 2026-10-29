namespace BigJsonXml;

/// <summary>
/// A list the finished scan already indexed, which a background search may jump through.
/// Entries before the next candidate byte hold no hit, so search counts them from the offsets.
/// </summary>
internal sealed class SkipList(long listStart, string name, EntryOffsets? offsets)
{
    private volatile EntryOffsets? _offsets = offsets;

    // JSON: the opening bracket. XML: unused.
    public long ListStart { get; } = listStart;

    // XML: the entry element's local name. JSON: unused.
    public string Name { get; } = name;

    // XML: the root element's content, where the entries are counted.
    public long ContentStart { get; init; }

    public long ContentEnd { get; init; }

    // XML: how many entries the scan counted. Counting the entry tags must find exactly this many.
    public int Expected { get; init; }

    // Null until the entries are counted. XML counts them on the search thread before the first jump.
    public EntryOffsets? Offsets
    {
        get => _offsets;
        set => _offsets = value;
    }
}

internal sealed class SearchSkip(CandidateFinder finder, IReadOnlyList<SkipList> lists)
{
    public CandidateFinder Finder { get; } = finder;

    public IReadOnlyList<SkipList> Lists { get; } = lists;

    // How far one look for a candidate reads, so one step stays short and cancel is noticed soon.
    public const long Window = 32L << 20;

    private volatile bool _enabled;

    // Entries jumped over, for tests and measurements.
    public long Skipped { get; set; }

    // Off while the search runs on the caller's thread. Only the search thread may read ahead.
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>
    /// The entry to read next when <paramref name="done"/> entries are finished and the reader is at
    /// <paramref name="at"/>, or -1 when no entry can be jumped over.
    /// </summary>
    public int Target(EntryOffsets? offsets, int done, long at, long length)
    {
        if (offsets is null || !_enabled)
            return -1;
        if (done >= offsets.Count || offsets[done] < at)
            return -1;
        // No candidate before the window's end means none in the entries that end before it.
        var candidate = Finder.Next(at, at + Window);
        var target = candidate >= length ? offsets.Count - 1 : offsets.LastAtOrBefore(candidate);
        return target > done ? target : -1;
    }
}
