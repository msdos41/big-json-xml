namespace BigJsonXml;

/// <summary>
/// Steps from the document element to a field or a list entry.
/// A list step carries the 1-based position among siblings of that name.
/// </summary>
public sealed class DocumentPath : IEquatable<DocumentPath>
{
    public static DocumentPath Root { get; } = new([]);

    private readonly Step[] _steps;

    private DocumentPath(Step[] steps) => _steps = steps;

    public IReadOnlyList<Step> Steps => _steps;

    public DocumentPath Field(string name, int? oneBasedIndex = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (oneBasedIndex is <= 0)
            throw new ArgumentOutOfRangeException(nameof(oneBasedIndex));
        var next = new Step[_steps.Length + 1];
        Array.Copy(_steps, next, _steps.Length);
        next[^1] = new Step(name, oneBasedIndex);
        return new DocumentPath(next);
    }

    public DocumentPath WithIndex(int oneBasedIndex)
    {
        if (_steps.Length == 0)
            throw new InvalidOperationException("The document element has no list index.");
        if (oneBasedIndex <= 0)
            throw new ArgumentOutOfRangeException(nameof(oneBasedIndex));
        var next = (Step[])_steps.Clone();
        var last = next[^1];
        next[^1] = last with { OneBasedIndex = oneBasedIndex };
        return new DocumentPath(next);
    }

    internal static DocumentPath From(IReadOnlyList<Step> steps)
    {
        var copy = new Step[steps.Count];
        for (var i = 0; i < steps.Count; i++)
            copy[i] = steps[i];
        return new DocumentPath(copy);
    }

    public override string ToString()
    {
        if (_steps.Length == 0)
            return "/";
        var parts = new string[_steps.Length];
        for (var i = 0; i < _steps.Length; i++)
        {
            var step = _steps[i];
            parts[i] = step.OneBasedIndex is int index ? $"{step.Name}[{index}]" : step.Name;
        }

        return string.Join(" / ", parts);
    }

    public bool Equals(DocumentPath? other)
    {
        if (other is null || other._steps.Length != _steps.Length)
            return false;
        for (var i = 0; i < _steps.Length; i++)
        {
            if (_steps[i] != other._steps[i])
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is DocumentPath path && Equals(path);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var step in _steps)
            hash.Add(step);
        return hash.ToHashCode();
    }

    public readonly record struct Step(string Name, int? OneBasedIndex);
}
