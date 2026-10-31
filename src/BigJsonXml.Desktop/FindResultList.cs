using System.Collections;

namespace BigJsonXml.Desktop;

/// <summary>
/// Lazy Find All rows keyed by hit index. The indexer never calls ReadHitText — it returns a
/// placeholder and queues work. <see cref="PumpDescribe"/> fills the cache on the UI tick.
/// </summary>
internal sealed class FindResultList : IReadOnlyList<FindResult>
{
    public const int CacheLimit = 768;
    public const int PrefetchRadius = 80;
    public const int DescribeBudget = 48;

    private readonly DocumentTab _tab;
    private readonly Dictionary<int, Entry> _cache = new();
    private readonly Queue<int> _order = new();
    private readonly HashSet<int> _pending = [];
    private readonly List<int> _scratch = [];
    private int _width = 40;
    private int _focus = -1;
    private int _ready;

    public FindResultList(DocumentTab tab) => _tab = tab;

    public int Count => _tab.Session.Hits.Count;

    /// <summary>How many hits have a finished ReadHitText body.</summary>
    public int DescribedCount => _ready;

    public bool HasPending => _pending.Count > 0;

    public FindResult this[int index] => EnsureDescribed(index);

    /// <summary>Scroll-path access: never calls ReadHitText.</summary>
    public FindResult Placeholder(int index)
    {
        if ((uint)index >= (uint)Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        RequestAround(index);
        if (_cache.TryGetValue(index, out var entry) && entry.Width == _width)
            return entry.Result;

        var hit = _tab.Session.Hits[index];
        var placeholder = new FindResult(hit, "…", 0, 0, "", ready: false);
        Set(index, placeholder, _width, ready: false);
        _pending.Add(index);
        return placeholder;
    }

    public void SetWidth(int width)
    {
        if (width < 1)
            width = 1;
        if (_width == width)
            return;
        _width = width;
        Clear();
    }

    public void Clear()
    {
        _cache.Clear();
        _order.Clear();
        _pending.Clear();
        _ready = 0;
        _focus = -1;
    }

    public void RequestAround(int center)
    {
        if ((uint)center >= (uint)Count)
            return;
        _focus = center;
        var lo = Math.Max(0, center - PrefetchRadius);
        var hi = Math.Min(Count - 1, center + PrefetchRadius);
        for (var i = lo; i <= hi; i++)
        {
            if (_cache.TryGetValue(i, out var entry) && entry.Width == _width && entry.Result.IsReady)
                continue;
            _pending.Add(i);
        }
    }

    public void RequestRange(int firstHit, int lastHit)
    {
        if (Count == 0)
            return;
        firstHit = Math.Clamp(firstHit, 0, Count - 1);
        lastHit = Math.Clamp(lastHit, 0, Count - 1);
        if (lastHit < firstHit)
            (firstHit, lastHit) = (lastHit, firstHit);
        var center = firstHit + (lastHit - firstHit) / 2;
        RequestAround(center);
        for (var i = firstHit; i <= lastHit; i++)
            _pending.Add(i);
    }

    /// <summary>Describe pending hits near the focus. Never call from the ListBox indexer.</summary>
    public int PumpDescribe(int budget, long deadlineTick)
    {
        if (_pending.Count == 0 || budget < 1)
            return 0;

        _scratch.Clear();
        foreach (var index in _pending)
            _scratch.Add(index);
        if (_focus >= 0)
            _scratch.Sort((a, b) => Math.Abs(a - _focus).CompareTo(Math.Abs(b - _focus)));

        var done = 0;
        foreach (var index in _scratch)
        {
            if (done >= budget || Environment.TickCount64 >= deadlineTick)
                break;
            if ((uint)index >= (uint)Count)
            {
                _pending.Remove(index);
                continue;
            }

            if (_cache.TryGetValue(index, out var existing) && existing.Width == _width && existing.Result.IsReady)
            {
                _pending.Remove(index);
                continue;
            }

            if (!TryDescribe(index))
                continue;
            _pending.Remove(index);
            done++;
        }

        return done;
    }

    /// <summary>Synchronous describe for Find Next / choose — not used while scrolling.</summary>
    public FindResult EnsureDescribed(int index)
    {
        if ((uint)index >= (uint)Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (_cache.TryGetValue(index, out var entry) && entry.Width == _width && entry.Result.IsReady)
            return entry.Result;
        RequestAround(index);
        TryDescribe(index);
        _pending.Remove(index);
        return _cache[index].Result;
    }

    public void ReviseCachedPaths()
    {
        var hits = _tab.Session.Hits;
        foreach (var (index, entry) in _cache)
        {
            if ((uint)index >= (uint)hits.Count || !entry.Result.IsReady)
                continue;
            entry.Result.RevisePath(_tab.Session.FormatPath(hits[index].Path));
        }
    }

    public bool TryGet(int index, out FindResult result)
    {
        if (_cache.TryGetValue(index, out var entry) && entry.Width == _width)
        {
            result = entry.Result;
            return true;
        }

        result = null!;
        return false;
    }

    public IEnumerator<FindResult> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
            yield return EnsureDescribed(i);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private bool TryDescribe(int index)
    {
        var hit = _tab.Session.Hits[index];
        try
        {
            var text = _tab.Session.ReadHitText(hit, _width);
            var path = _tab.Session.FormatPath(hit.Path);
            if (_cache.TryGetValue(index, out var entry) && entry.Width == _width)
            {
                var wasReady = entry.Result.IsReady;
                entry.Result.ReviseBody(text.Text, text.MarkStart, text.MarkLength, path);
                if (!wasReady)
                    _ready++;
                return true;
            }

            var result = new FindResult(hit, text.Text, text.MarkStart, text.MarkLength, path, ready: true);
            Set(index, result, _width, ready: true);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private void Set(int index, FindResult result, int width, bool ready)
    {
        if (_cache.TryGetValue(index, out var prior))
        {
            if (prior.Result.IsReady && !ready)
                _ready--;
            else if (!prior.Result.IsReady && ready)
                _ready++;
            _cache[index] = new Entry(result, width);
            return;
        }

        while (_cache.Count >= CacheLimit && _order.Count > 0)
        {
            var drop = _order.Dequeue();
            if (!_cache.TryGetValue(drop, out var dropped))
                continue;
            if (dropped.Result.IsReady)
                _ready--;
            _cache.Remove(drop);
            _pending.Remove(drop);
        }

        _cache[index] = new Entry(result, width);
        _order.Enqueue(index);
        if (ready)
            _ready++;
    }

    private readonly struct Entry(FindResult result, int width)
    {
        public FindResult Result { get; } = result;
        public int Width { get; } = width;
    }
}
