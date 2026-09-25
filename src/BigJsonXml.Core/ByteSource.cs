namespace BigJsonXml;

/// <summary>
/// Random-access bytes of one document. Length grows when the source is still being fed.
/// </summary>
public interface IByteSource
{
    long Length { get; }

    bool IsComplete { get; }

    int Read(long offset, Span<byte> buffer);
}

/// <summary>
/// In-memory source tests use to feed a document in chunks.
/// </summary>
public sealed class MemoryByteSource : IByteSource
{
    private byte[] _data = new byte[256];
    private long _length;

    public long Length => _length;

    public bool IsComplete { get; private set; }

    public void Append(ReadOnlySpan<byte> chunk)
    {
        if (IsComplete)
            throw new InvalidOperationException("The document is already complete.");
        if (chunk.IsEmpty)
            return;
        var needed = _length + chunk.Length;
        if (needed > int.MaxValue)
            throw new InvalidOperationException("The in-memory document is larger than this source can hold.");
        if (needed > _data.Length)
        {
            var size = _data.Length;
            while (size < needed)
                size = size <= int.MaxValue / 2 ? size * 2 : int.MaxValue;
            Array.Resize(ref _data, size);
        }

        chunk.CopyTo(_data.AsSpan((int)_length));
        _length = needed;
    }

    public void Append(string text) => Append(System.Text.Encoding.UTF8.GetBytes(text));

    public void Complete() => IsComplete = true;

    public int Read(long offset, Span<byte> buffer)
    {
        if ((ulong)offset > (ulong)_length || buffer.IsEmpty)
            return 0;
        var count = (int)Math.Min(buffer.Length, _length - offset);
        _data.AsSpan((int)offset, count).CopyTo(buffer);
        return count;
    }
}

/// <summary>
/// Seekable stream, typically a file opened read-only. The session does not write it.
/// </summary>
public sealed class StreamByteSource : IByteSource, IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;

    public StreamByteSource(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek || !stream.CanRead)
            throw new ArgumentException("The document stream must be readable and seekable.", nameof(stream));
        _stream = stream;
        _leaveOpen = leaveOpen;
        Length = stream.Length;
    }

    public long Length { get; }

    public bool IsComplete => true;

    public int Read(long offset, Span<byte> buffer)
    {
        if ((ulong)offset > (ulong)Length || buffer.IsEmpty)
            return 0;
        _stream.Position = offset;
        return _stream.Read(buffer);
    }

    public void Dispose()
    {
        if (!_leaveOpen)
            _stream.Dispose();
    }
}
