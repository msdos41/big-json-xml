using Microsoft.Win32.SafeHandles;

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
/// A source that can hand a second, independent reader to another thread once it is complete.
/// </summary>
internal interface IReaderSource
{
    // Null when the source cannot be read from another thread.
    IByteSource? OpenReader();
}

/// <summary>
/// A source whose large read-ahead window would cost more than the read itself when
/// the reads jump far apart, as a sampled column fit does.
/// </summary>
internal interface ISparseReadSource
{
    // Reads only what the buffer holds. The read-ahead window is used when it already covers the offset.
    int ReadSparse(long offset, Span<byte> buffer);
}

/// <summary>
/// In-memory source tests use to feed a document in chunks.
/// </summary>
public sealed class MemoryByteSource : IByteSource, IReaderSource
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

    // A complete buffer is never written again, so another thread may read it directly.
    IByteSource? IReaderSource.OpenReader() => IsComplete ? this : null;

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
public sealed class StreamByteSource : IByteSource, IDisposable, IReaderSource, ISparseReadSource
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly byte[] _window = new byte[1024 * 1024];
    private long _origin = -1;
    private int _filled;
    private SafeFileHandle? _sparse;
    private bool _sparseRefused;

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
        if (buffer.Length >= _window.Length)
        {
            _origin = -1;
            _filled = 0;
            _stream.Position = offset;
            return _stream.Read(buffer);
        }

        var copied = 0;
        while (copied < buffer.Length)
        {
            var pos = offset + copied;
            if (pos >= Length)
                break;
            if ((ulong)(pos - _origin) >= (ulong)_filled)
                Fill(pos);
            var local = (int)(pos - _origin);
            var count = Math.Min(buffer.Length - copied, _filled - local);
            _window.AsSpan(local, count).CopyTo(buffer[copied..]);
            copied += count;
        }

        return copied;
    }

    // A file stream reads ahead by its own buffer on every seek. A sampled read goes
    // through a second handle that reads only the bytes asked for.
    int ISparseReadSource.ReadSparse(long offset, Span<byte> buffer)
    {
        if ((ulong)offset > (ulong)Length || buffer.IsEmpty)
            return 0;
        if ((ulong)(offset - _origin) < (ulong)_filled && offset + buffer.Length <= _origin + _filled)
            return Read(offset, buffer);
        var handle = SparseHandle();
        if (handle is null)
            return Read(offset, buffer);
        var count = (int)Math.Min(buffer.Length, Length - offset);
        var read = 0;
        while (read < count)
        {
            var n = RandomAccess.Read(handle, buffer[read..count], offset + read);
            if (n == 0)
                break;
            read += n;
        }

        return read;
    }

    private SafeFileHandle? SparseHandle()
    {
        if (_sparse is not null || _sparseRefused)
            return _sparse;
        if (_stream is FileStream file)
        {
            try
            {
                _sparse = File.OpenHandle(file.Name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
            }
        }

        _sparseRefused = _sparse is null;
        return _sparse;
    }

    // A file is opened again, so the second reader has its own position and window.
    IByteSource? IReaderSource.OpenReader()
    {
        if (_stream is not FileStream file)
            return null;
        try
        {
            var again = new FileStream(file.Name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.SequentialScan);
            if (again.Length != Length)
            {
                again.Dispose();
                return null;
            }

            return new StreamByteSource(again);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private void Fill(long offset)
    {
        var count = (int)Math.Min(_window.Length, Length - offset);
        _stream.Position = offset;
        var read = 0;
        while (read < count)
        {
            var n = _stream.Read(_window.AsSpan(read, count - read));
            if (n == 0)
                break;
            read += n;
        }

        _origin = offset;
        _filled = read;
    }

    public void Dispose()
    {
        _sparse?.Dispose();
        if (!_leaveOpen)
            _stream.Dispose();
    }
}
