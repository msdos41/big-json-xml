using System.Collections;
using System.Collections.Specialized;

namespace BigJsonXml.Desktop;

/// <summary>
/// Results window rows: one group line plus one slot per hit. Count tracks Hits immediately;
/// hit text is described only when the indexer (virtualized ListBox) asks for a row.
/// </summary>
internal sealed class VirtualResultLines : IList, INotifyCollectionChanged
{
    public const int LineCacheLimit = 256;

    private readonly DocumentTab _tab;
    private ResultDocumentLine? _group;
    private int _hitCount;

    public int PublishedHitCount => _hitCount;
    private readonly Dictionary<int, ResultHitLine> _lines = new();
    private readonly Queue<int> _order = new();

    public VirtualResultLines(DocumentTab tab) => _tab = tab;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Count
    {
        get
        {
            if (!_tab.Searched || _group is null)
                return 0;
            return _group.Document.IsExpanded ? 1 + _hitCount : 1;
        }
    }

    public object? this[int index]
    {
        get
        {
            if (_group is null || (uint)index >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (index == 0)
                return _group;
            return HitLine(index - 1);
        }
        set => throw new NotSupportedException();
    }

    public bool IsFixedSize => false;
    public bool IsReadOnly => true;
    public bool IsSynchronized => false;
    public object SyncRoot => this;

    public void EnsureGroup(ResultDocument document)
    {
        if (_group is not null && ReferenceEquals(_group.Document, document))
            return;
        _group = new ResultDocumentLine(document);
        Reset();
    }

    public void ClearRows()
    {
        _hitCount = 0;
        _group = null;
        _lines.Clear();
        _order.Clear();
        Reset();
    }

    /// <summary>Publish how many hits the session holds. Scroll extent updates without describing rows.</summary>
    public void PublishHits(int hitCount)
    {
        if (hitCount < 0)
            hitCount = 0;
        if (_hitCount == hitCount)
            return;
        if (hitCount < _hitCount)
            DropFrom(hitCount);
        _hitCount = hitCount;
        Reset();
    }

    public void NotifyExpanded()
    {
        DropLines();
        Reset();
    }

    public void MarkCurrent(FindResult? current)
    {
        foreach (var line in _lines.Values)
            line.SetCurrent(ReferenceEquals(line.Result, current));
    }

    public void InvalidateDescribed()
    {
        DropLines();
        _tab.Results.Clear();
        Reset();
    }

    public int Add(object? value) => throw new NotSupportedException();
    public void Clear() => ClearRows();
    public bool Contains(object? value)
    {
        if (value is null || _group is null)
            return false;
        if (ReferenceEquals(value, _group))
            return true;
        if (value is ResultHitLine hit)
        {
            foreach (var line in _lines.Values)
            {
                if (ReferenceEquals(line, hit))
                    return true;
            }
        }

        return false;
    }

    public int IndexOf(object? value)
    {
        if (value is null || _group is null)
            return -1;
        if (ReferenceEquals(value, _group))
            return 0;
        if (value is ResultHitLine hit)
        {
            foreach (var (index, line) in _lines)
            {
                if (ReferenceEquals(line, hit))
                    return index + 1;
            }
        }

        return -1;
    }

    public void Insert(int index, object? value) => throw new NotSupportedException();
    public void Remove(object? value) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();
    public void CopyTo(Array array, int index)
    {
        for (var i = 0; i < Count; i++)
            array.SetValue(this[i], index + i);
    }

    public IEnumerator GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
            yield return this[i];
    }

    private ResultHitLine HitLine(int hitIndex)
    {
        if (_lines.TryGetValue(hitIndex, out var line))
        {
            line.SetCurrent(ReferenceEquals(line.Result, _tab.Current));
            return line;
        }

        line = new ResultHitLine(_tab.Results[hitIndex]);
        line.SetCurrent(ReferenceEquals(line.Result, _tab.Current));
        while (_lines.Count >= LineCacheLimit && _order.Count > 0)
        {
            var drop = _order.Dequeue();
            _lines.Remove(drop);
        }

        _lines[hitIndex] = line;
        _order.Enqueue(hitIndex);
        return line;
    }

    private void DropFrom(int hitCount)
    {
        if (_lines.Count == 0)
            return;
        var remove = new List<int>();
        foreach (var key in _lines.Keys)
        {
            if (key >= hitCount)
                remove.Add(key);
        }

        foreach (var key in remove)
            _lines.Remove(key);
    }

    private void DropLines()
    {
        _lines.Clear();
        _order.Clear();
    }

    private void Reset() =>
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
}
