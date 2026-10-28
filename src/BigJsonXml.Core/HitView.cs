using System.Collections;

namespace BigJsonXml;

// The published prefix of the hits the search has found, in document order.
internal sealed class HitView(List<Hit> found) : IReadOnlyList<Hit>
{
    public int Count { get; set; }

    public Hit this[int index] =>
        (uint)index < (uint)Count ? found[index] : throw new ArgumentOutOfRangeException(nameof(index));

    public IEnumerator<Hit> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
            yield return found[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
