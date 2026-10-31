using System.Collections;
using System.Collections.Specialized;

namespace BigJsonXml.Desktop;

/// <summary>Empty visible results when no document is open.</summary>
internal sealed class EmptyResultLines : IList, INotifyCollectionChanged
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add { }
        remove { }
    }

    public int Count => 0;
    public bool IsFixedSize => true;
    public bool IsReadOnly => true;
    public bool IsSynchronized => false;
    public object SyncRoot => this;
    public object? this[int index]
    {
        get => throw new ArgumentOutOfRangeException(nameof(index));
        set => throw new NotSupportedException();
    }

    public int Add(object? value) => throw new NotSupportedException();
    public void Clear() { }
    public bool Contains(object? value) => false;
    public int IndexOf(object? value) => -1;
    public void Insert(int index, object? value) => throw new NotSupportedException();
    public void Remove(object? value) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();
    public void CopyTo(Array array, int index) { }
    public IEnumerator GetEnumerator()
    {
        yield break;
    }
}
