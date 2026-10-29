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

    // XML: the entry element's local name. JSON: the field name.
    public string Name { get; } = name;

    // Only the search thread reads and writes these: how often looking ahead found nothing to jump.
    public int Misses;
    public int Pause;
    public int PauseUntil;

    // The path of the record that holds the list.
    public DocumentPath Parent { get; init; } = DocumentPath.Root;

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

    private readonly Dictionary<(DocumentPath, string), SkipList> _byParent = Index(lists, list => (list.Parent, list.Name));
    private readonly Dictionary<long, SkipList> _byStart = Index(lists, list => list.ListStart);

    // XML: the list named <paramref name="name"/> inside the record at <paramref name="parent"/>.
    public SkipList? Find(DocumentPath parent, string name) => _byParent.GetValueOrDefault((parent, name));

    // JSON: the list whose opening bracket is at <paramref name="start"/>.
    public SkipList? Find(long start) => _byStart.GetValueOrDefault(start);

    private static Dictionary<TKey, SkipList> Index<TKey>(IReadOnlyList<SkipList> lists, Func<SkipList, TKey> key)
        where TKey : notnull
    {
        var index = new Dictionary<TKey, SkipList>();
        foreach (var list in lists)
            index.TryAdd(key(list), list);
        return index;
    }

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
    public int Target(SkipList list, int done, long at, long length)
    {
        var offsets = list.Offsets;
        if (offsets is null || !_enabled || done < list.PauseUntil)
            return -1;
        if (done >= offsets.Count || offsets[done] < at)
            return -1;
        // No candidate before the window's end means none in the entries that end before it.
        var candidate = Finder.Next(at, at + Window);
        var target = candidate >= length ? offsets.Count - 1 : offsets.LastAtOrBefore(candidate);
        if (target > done)
        {
            list.Misses = 0;
            list.Pause = 0;
            return target;
        }

        // Where nearly every entry holds a candidate, looking costs more than it saves. After a few
        // misses in a row the next entries are read without looking, for twice as many each time.
        if (++list.Misses >= 4)
        {
            list.Misses = 0;
            list.Pause = Math.Min(Math.Max(list.Pause * 2, 16), 4096);
            list.PauseUntil = done + list.Pause;
        }

        return -1;
    }
}
