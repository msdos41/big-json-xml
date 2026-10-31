using System.Collections;

namespace BigJsonXml.Desktop;

/// <summary>
/// Lazy Find All rows keyed by hit index. Count follows session Hits; ReadHitText runs only on indexer access.
/// </summary>
internal sealed class FindResultList : IReadOnlyList<FindResult>
{
    public const int CacheLimit = 512;

    private readonly DocumentTab _tab;
    private readonly Dictionary<int, Entry> _cache = new();
    private readonly Queue<int> _order = new();
    private int _width = 40;

    public FindResultList(DocumentTab tab) => _tab = tab;

    public int Count => _tab.Session.Hits.Count;

    /// <summary>How many hits have been described into the cache (tests / diagnostics).</summary>
    public int DescribedCount => _cache.Count;

    public FindResult this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (_cache.TryGetValue(index, out var entry) && entry.Width == _width)
                return entry.Result;

            var hit = _tab.Session.Hits[index];
            try
            {
                var text = _tab.Session.ReadHitText(hit, _width);
                var result = new FindResult(hit, text.Text, text.MarkStart, text.MarkLength, _tab.Session.FormatPath(hit.Path));
                Set(index, result, _width);
                return result;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
            {
                // Hit not readable yet (scan/search race). Placeholder until the next ask.
                var placeholder = new FindResult(hit, "", 0, 0, _tab.Session.FormatPath(hit.Path));
                return placeholder;
            }
        }
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
    }

    public void ReviseCachedPaths()
    {
        var hits = _tab.Session.Hits;
        foreach (var (index, entry) in _cache)
        {
            if ((uint)index >= (uint)hits.Count)
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
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void Set(int index, FindResult result, int width)
    {
        if (_cache.ContainsKey(index))
        {
            _cache[index] = new Entry(result, width);
            return;
        }

        while (_cache.Count >= CacheLimit && _order.Count > 0)
        {
            var drop = _order.Dequeue();
            _cache.Remove(drop);
        }

        _cache[index] = new Entry(result, width);
        _order.Enqueue(index);
    }

    private readonly struct Entry(FindResult result, int width)
    {
        public FindResult Result { get; } = result;
        public int Width { get; } = width;
    }
}
