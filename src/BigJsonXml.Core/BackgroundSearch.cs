using System.Diagnostics;

namespace BigJsonXml;

/// <summary>
/// Runs one text search on its own thread over its own reader of the finished document.
/// Hits reach the session only once their path can no longer change, in document order.
/// </summary>
internal sealed class BackgroundSearch
{
    private readonly object _gate = new();
    private readonly IScanSearch _search;
    private readonly List<Hit> _hits;
    private readonly Func<int, int> _stable;
    private readonly IDisposable? _reader;
    private readonly Action<Func<bool>>? _prepare;
    private readonly SearchSkip? _skip;
    private readonly List<Hit> _outbox = [];
    private readonly ManualResetEventSlim _changed = new(false);
    private int _sent;
    private volatile bool _cancel;
    private volatile bool _finished;
    private Thread? _thread;

    public BackgroundSearch(IScanSearch search, List<Hit> hits, Func<int, int> stable, IDisposable? reader, SearchSkip? skip = null, Action<Func<bool>>? prepare = null)
    {
        _prepare = prepare;
        _skip = skip;
        _search = search;
        _hits = hits;
        _stable = stable;
        _reader = reader;
    }

    // The search ended, was cancelled, or failed. Hits not yet taken may still wait in the outbox.
    public bool Finished => _finished;

    // Steps the search on the calling thread first, so a short document needs no thread.
    public void RunInline(long ticks)
    {
        var deadline = Stopwatch.GetTimestamp() + ticks;
        try
        {
            while (!_search.Done && Stopwatch.GetTimestamp() < deadline)
            {
                if (_search.Step() == SearchStep.NeedMore)
                    break;
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            Finish();
            return;
        }

        Hand();
        if (_search.Done)
            Finish();
    }

    public void Start()
    {
        if (_finished)
            return;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "BigJsonXml search",
            Priority = ThreadPriority.BelowNormal,
        };
        _thread.Start();
    }

    // Moves hits that can no longer change into the session's list. True when any moved.
    public bool Drain(List<Hit> into)
    {
        lock (_gate)
        {
            if (_outbox.Count == 0)
                return false;
            into.AddRange(_outbox);
            _outbox.Clear();
            _changed.Reset();
            return true;
        }
    }

    public bool HasWaiting
    {
        get
        {
            lock (_gate)
                return _outbox.Count > 0;
        }
    }

    public void Wait(int milliseconds) => _changed.Wait(milliseconds);

    public void Cancel(bool join)
    {
        _cancel = true;
        if (join && _thread is { } thread && thread != Thread.CurrentThread)
            thread.Join();
        if (_thread is null)
            Finish();
    }

    private void Run()
    {
        try
        {
            // Work that may take a while, such as finding the entries to jump through, stays off the caller.
            _prepare?.Invoke(() => _cancel);
            if (_skip is not null)
                _skip.Enabled = true;
            var batch = Stopwatch.Frequency / 250;
            var stuck = false;
            while (!_cancel && !_search.Done && !stuck)
            {
                var deadline = Stopwatch.GetTimestamp() + batch;
                do
                {
                    // A finished document never waits for bytes. Waiting would only spin.
                    stuck = _search.Step() == SearchStep.NeedMore;
                }
                while (!stuck && !_search.Done && !_cancel && Stopwatch.GetTimestamp() < deadline);
                if (!_cancel)
                    Hand();
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            // The file changed or went away. Hits already handed over stay.
        }
        finally
        {
            Finish();
        }
    }

    private void Hand()
    {
        var stable = _stable(_sent);
        if (stable <= _sent)
            return;
        lock (_gate)
        {
            for (var i = _sent; i < stable; i++)
                _outbox.Add(_hits[i]);
            _changed.Set();
        }

        _sent = stable;
    }

    private void Finish()
    {
        if (_finished)
            return;
        _finished = true;
        _reader?.Dispose();
        lock (_gate)
            _changed.Set();
    }
}
