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
    FieldName,
    ColumnHeader,
    Entry,
    EntryIndex,
    ValueLine,
}

public enum WidthAxis
{
    None,
    RecordName,
    RecordValue,
    ListColumn,
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
/// One text search. Find what is a character sequence. Empty name limits add nothing.
/// </summary>
public sealed record TextSearch
{
    public string FindWhat { get; init; } = "";

    public bool MatchCase { get; init; } = true;

    public bool ElementName { get; init; } = true;

    public bool AttributeName { get; init; } = true;

    public bool Value { get; init; } = true;

    public bool StartsWith { get; init; }

    public bool EntireCell { get; init; }

    public string FieldName { get; init; } = "";

    public string ParentElementName { get; init; } = "";

    public string GrandfatherElementName { get; init; } = "";

    public string ParentAttributeName { get; init; } = "";
}

/// <summary>
/// Field text for one result row. The mark is the matched stretch inside <see cref="Text"/>.
/// </summary>
public readonly record struct HitText(string Text, int MarkStart, int MarkLength);

/// <summary>
/// One search match. The path is enough to open the cell later.
/// CharacterOffset is the match in the field text. MatchLength is that stretch.
/// </summary>
public sealed class Hit
{
    internal readonly List<DocumentPath.Step> Steps = [];
    internal bool SimpleText;
    internal bool FieldOnSimple;
    internal bool OnName;
    internal string NameText = "";

    public Hit(int characterOffset, int matchLength)
    {
        CharacterOffset = characterOffset;
        MatchLength = matchLength;
    }

    public DocumentPath Path => DocumentPath.From(Steps);

    public int CharacterOffset { get; }

    public int MatchLength { get; }

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
    DocumentPath? GrowPath = null,
    bool IsCutOff = false,
    WidthAxis Axis = WidthAxis.None,
    DocumentPath? WidthTable = null,
    string? WidthColumn = null,
    int TextStart = 0,
    bool ScalarText = false);

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
