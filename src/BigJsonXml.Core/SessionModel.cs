namespace BigJsonXml;

public enum ScanProgress
{
    Scanning,
    Completed,
    Stopped,
}

public enum FieldKind
{
    Scalar,
    Record,
    List,
}

public enum SurfaceKind
{
    Field,
    ColumnHeader,
    Entry,
    ValueLine,
}

/// <summary>
/// The point where a well-formedness error stopped the scan.
/// Tables built before this point stay available.
/// </summary>
public sealed record ScanStop(string Message, long ByteOffset, int Line, int Column);

public sealed record FieldView(
    string Name,
    FieldKind Kind,
    string? Text,
    bool CanExpand,
    bool IsExpanded,
    string? ListLabel,
    int? ListCount,
    bool CanExpandFullValue,
    bool IsFullValueExpanded,
    bool IsTruncated,
    int WindowStart,
    bool IsMarked);

public sealed record ListEntryView(int OneBasedIndex, IReadOnlyList<FieldView> Cells);

public sealed record ListView(
    IReadOnlyList<string> Columns,
    int EntryCount,
    int Start,
    IReadOnlyList<ListEntryView> Entries);

public sealed record RecordView(IReadOnlyList<FieldView> Fields);

/// <summary>
/// One search match. The path is enough to open the cell later.
/// CharacterOffset is the match in the expanded scalar value.
/// </summary>
public sealed class Hit
{
    internal readonly List<DocumentPath.Step> Steps = [];
    internal bool SimpleText;
    internal bool FieldOnSimple;

    public Hit(int characterOffset, string snippet)
    {
        CharacterOffset = characterOffset;
        Snippet = snippet;
    }

    public DocumentPath Path => DocumentPath.From(Steps);

    public int CharacterOffset { get; }

    public string Snippet { get; }

    public override string ToString() => $"{Path} @{CharacterOffset}";
}

public sealed record SurfaceRow(
    double X,
    double Y,
    double Width,
    double Height,
    string Name,
    string? Value,
    bool CanExpand,
    bool IsExpanded,
    bool IsMarked,
    DocumentPath Path,
    SurfaceKind Kind,
    bool CanGrowValue = false,
    DocumentPath? ExpandPath = null,
    DocumentPath? GrowPath = null);

/// <summary>
/// The one scrollable surface. Rows are only those that intersect the viewport.
/// </summary>
public sealed record SurfaceFrame(
    double ContentWidth,
    double ContentHeight,
    double ScrollX,
    double ScrollY,
    IReadOnlyList<SurfaceRow> Rows,
    ScanStop? Error,
    int ScrollSurfaces);
