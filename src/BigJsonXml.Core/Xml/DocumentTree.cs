namespace BigJsonXml.Xml;

internal enum SlotKind
{
    Scalar,
    Record,
    List,
}

internal enum NameKind
{
    Attribute,
    Element,
    Text,
}

internal readonly record struct FieldKey(NameKind Kind, string Ns, string Local)
{
    public static FieldKey Text { get; } = new(NameKind.Text, "", "#text");

    public static FieldKey Attr(string ns, string local) => new(NameKind.Attribute, ns, local);

    public static FieldKey Elem(string ns, string local) => new(NameKind.Element, ns, local);
}

internal sealed class Column
{
    public FieldKey Key;
    public string Name = "";
}

internal sealed class Slot
{
    public FieldKey Key;
    public string Name = "";
    public SlotKind Kind;
    public int Count = 1;
    public long StartByte;
    public long EndByte = -1;
    public long PendingStart = -1;
    public NsScope ParentScope = NsScope.Empty;
    public List<TextPiece> Text = [];
    public List<Column> Columns = [];
    public List<Column> Inner = [];
    public bool HasFields;

    // Closed record body. A refresh reads this instead of parsing the children again.
    public Frame? Detail;
}

internal sealed class Frame
{
    public string Name = "";
    public string Local = "";
    public string Ns = "";
    public long StartByte;
    public long ContentByte;
    public long ContentEnd = -1;
    public long EndByte = -1;
    public long StableByte;
    public NsScope Scope = NsScope.Empty;
    public DocumentPath Path = DocumentPath.Root;
    public bool AlwaysRecord;
    public bool SawAttribute;
    public bool SawElement;
    public List<Slot> Slots = [];
    public Dictionary<string, Slot> ByName = new(StringComparer.Ordinal);
    public Dictionary<FieldKey, Slot> ByKey = [];
    public List<TextPiece> Text = [];
    public int TextAt = -1;

    public bool IsSimple => !SawAttribute && !SawElement;
}

internal readonly struct Folded(Frame parent, Slot slot, Frame child, bool promoted)
{
    public Frame Parent { get; } = parent;
    public Slot Slot { get; } = slot;
    public Frame Child { get; } = child;
    public bool Promoted { get; } = promoted;
}

internal sealed class OffsetIndex
{
    public List<long> Offsets { get; } = [];
    public long Watermark { get; set; }
}

internal sealed class DocumentTree
{
    private readonly bool _documentElement;

    public DocumentTree(XmlDocument document, bool documentElement)
    {
        Document = document;
        _documentElement = documentElement;
    }

    public XmlDocument Document { get; }

    public Frame? Root { get; private set; }

    public Stack<Frame> Stack { get; } = new();

    public bool RootClosed { get; private set; }

    private Folded _announced;
    private bool _hasAnnounced;

    public bool Apply(XmlEvent ev, out Folded folded)
    {
        folded = default;
        switch (ev.Kind)
        {
            case XmlEventKind.Start:
                _hasAnnounced = false;
                var frame = Start(ev);
                if (ev.Empty)
                {
                    var promoted = _hasAnnounced && _announced.Promoted;
                    if (!Close(frame, ev.ByteEnd, ev.ByteEnd, out folded))
                        return false;
                    if (promoted)
                        folded = new Folded(folded.Parent, folded.Slot, folded.Child, true);
                    return true;
                }

                if (!_hasAnnounced)
                    return false;
                folded = _announced;
                return true;
            case XmlEventKind.End:
                if (Stack.Count == 0)
                    return false;
                return Close(Stack.Peek(), ev.ByteEnd, ev.ByteStart, out folded);
            case XmlEventKind.Text when Stack.Count > 0:
                AddText(Stack.Peek(), ev.Text);
                return false;
            default:
                return false;
        }
    }

    public Frame Load(XmlParser parser, DocumentPath path)
    {
        var depth = 0;
        while (true)
        {
            var ev = parser.Read();
            if (ev.Kind is XmlEventKind.Done or XmlEventKind.NeedMore or XmlEventKind.Error)
                break;
            Apply(ev, out _);
            if (ev.Kind == XmlEventKind.Start)
            {
                if (Root is not null && Root.Path == DocumentPath.Root && path != DocumentPath.Root)
                    Root.Path = path;
                if (!ev.Empty)
                    depth++;
                else if (depth == 0)
                    break;
            }
            else if (ev.Kind == XmlEventKind.End)
            {
                depth--;
                if (depth <= 0)
                    break;
            }
        }

        return Root ?? throw new InvalidOperationException("The element could not be read.");
    }

    private Frame Start(XmlEvent ev)
    {
        var parent = Stack.Count == 0 ? null : Stack.Peek();
        var frame = new Frame
        {
            Name = ev.Name,
            Local = ev.Local,
            Ns = ev.Ns,
            StartByte = ev.ByteStart,
            ContentByte = ev.ByteEnd,
            StableByte = ev.ByteEnd,
            Scope = ev.Scope,
            AlwaysRecord = _documentElement && parent is null,
            Path = parent is null ? DocumentPath.Root : parent.Path.Field(ev.Name),
        };
        foreach (var attr in ev.Attributes)
        {
            var slot = new Slot
            {
                Key = FieldKey.Attr(attr.Ns, attr.Local),
                Name = "@" + attr.Name,
                Kind = SlotKind.Scalar,
                Text = [attr.Value],
                ParentScope = ev.Scope,
            };
            AddSlot(frame, slot);
            frame.SawAttribute = true;
        }

        if (parent is null)
            Root = frame;
        else
            NoteElement(parent);
        Stack.Push(frame);
        Announce(frame);
        return frame;
    }

    // A non-simple element is a field before its end tag, so a long parent shows up while the scan is still inside it.
    private void Announce(Frame frame)
    {
        if (!frame.IsSimple)
        {
            var parent = ParentOf(frame);
            if (parent is not null && Ensure(parent, frame, out var folded))
            {
                _announced = folded;
                _hasAnnounced = true;
            }
        }

        var ancestor = ParentOf(frame);
        if (ancestor is not null)
            Announce(ancestor);
    }

    private Frame? ParentOf(Frame frame)
    {
        var seen = false;
        foreach (var item in Stack)
        {
            if (seen)
                return item;
            if (ReferenceEquals(item, frame))
                seen = true;
        }

        return null;
    }

    private bool Ensure(Frame parent, Frame child, out Folded folded)
    {
        folded = default;
        var key = FieldKey.Elem(child.Ns, child.Local);
        if (!parent.ByKey.TryGetValue(key, out var slot))
        {
            slot = new Slot
            {
                Key = key,
                Name = child.Name,
                StartByte = child.StartByte,
                EndByte = -1,
                PendingStart = child.StartByte,
                ParentScope = parent.Scope,
                Kind = SlotKind.Record,
                HasFields = true,
                Inner = ColumnsOf(child),
            };
            AddSlot(parent, slot);
            folded = new Folded(parent, slot, child, false);
            return true;
        }

        if (slot.PendingStart == child.StartByte)
        {
            if (slot.Kind == SlotKind.Record)
                slot.Inner = ColumnsOf(child);
            return false;
        }

        var promoted = false;
        if (slot.Kind != SlotKind.List)
        {
            promoted = true;
            var columns = slot.Kind == SlotKind.Scalar ? TextColumns(slot) : CopyColumns(slot.Inner);
            Merge(columns, child);
            slot.Kind = SlotKind.List;
            slot.Count = 2;
            slot.HasFields = true;
            slot.Columns = columns;
            slot.Text.Clear();
            slot.Inner.Clear();
            slot.Detail = null;
        }
        else
        {
            slot.Count++;
            Merge(slot.Columns, child);
        }

        slot.PendingStart = child.StartByte;
        folded = new Folded(parent, slot, child, promoted);
        return true;
    }

    private bool Close(Frame frame, long endByte, long contentEnd, out Folded folded)
    {
        folded = default;
        if (Stack.Count > 0 && ReferenceEquals(Stack.Peek(), frame))
            Stack.Pop();
        frame.EndByte = endByte;
        frame.ContentEnd = contentEnd;
        if (Stack.Count == 0)
        {
            RootClosed = true;
            return false;
        }

        var parent = Stack.Peek();
        parent.StableByte = endByte;
        folded = Fold(parent, frame);
        return true;
    }

    private Folded Fold(Frame parent, Frame child)
    {
        NoteElement(parent);
        var key = FieldKey.Elem(child.Ns, child.Local);
        if (parent.ByKey.TryGetValue(key, out var pending) && pending.PendingStart == child.StartByte)
        {
            pending.PendingStart = -1;
            pending.EndByte = child.EndByte;
            if (pending.Kind == SlotKind.List)
            {
                Merge(pending.Columns, child);
                pending.Detail = null;
            }
            else if (child.IsSimple)
            {
                pending.Kind = SlotKind.Scalar;
                pending.HasFields = false;
                pending.Text = CopyPieces(child.Text);
                pending.Inner.Clear();
                pending.Columns.Clear();
                pending.Detail = null;
            }
            else
            {
                pending.Kind = SlotKind.Record;
                pending.HasFields = true;
                pending.Inner = ColumnsOf(child);
                pending.Detail = child;
            }

            return new Folded(parent, pending, child, false);
        }

        var promoted = false;
        if (!parent.ByKey.TryGetValue(key, out var slot))
        {
            slot = new Slot
            {
                Key = key,
                Name = child.Name,
                StartByte = child.StartByte,
                EndByte = child.EndByte,
                ParentScope = parent.Scope,
            };
            if (child.IsSimple)
            {
                slot.Kind = SlotKind.Scalar;
                slot.Text = CopyPieces(child.Text);
            }
            else
            {
                slot.Kind = SlotKind.Record;
                slot.HasFields = true;
                slot.Inner = ColumnsOf(child);
                slot.Detail = child;
            }

            AddSlot(parent, slot);
        }
        else if (slot.Kind != SlotKind.List)
        {
            promoted = true;
            var columns = slot.Kind == SlotKind.Scalar ? TextColumns(slot) : CopyColumns(slot.Inner);
            Merge(columns, child);
            slot.Kind = SlotKind.List;
            slot.Count = 2;
            slot.HasFields = true;
            slot.Columns = columns;
            slot.Text.Clear();
            slot.Inner.Clear();
            slot.Detail = null;
        }
        else
        {
            slot.Count++;
            Merge(slot.Columns, child);
            slot.Detail = null;
        }

        return new Folded(parent, slot, child, promoted);
    }

    private void NoteElement(Frame frame)
    {
        if (!frame.SawElement && !frame.SawAttribute && frame.Text.Count > 0)
        {
            var slot = new Slot
            {
                Key = FieldKey.Text,
                Name = "#text",
                Kind = SlotKind.Scalar,
                Text = CopyPieces(frame.Text),
            };
            frame.TextAt = frame.Slots.Count;
            AddSlot(frame, slot);
            frame.Text.Clear();
        }

        frame.SawElement = true;
    }

    private void AddText(Frame frame, TextPiece piece)
    {
        if (ExpandedText.IsWhitespaceOnly(Document, [piece]))
            return;
        if (frame.AlwaysRecord || frame.SawAttribute || frame.SawElement)
            AppendHash(frame, piece);
        else
            frame.Text.Add(piece);
    }

    private void AppendHash(Frame frame, TextPiece piece)
    {
        if (frame.TextAt < 0)
        {
            var slot = new Slot
            {
                Key = FieldKey.Text,
                Name = "#text",
                Kind = SlotKind.Scalar,
            };
            frame.TextAt = frame.Slots.Count;
            AddSlot(frame, slot);
        }

        frame.Slots[frame.TextAt].Text.Add(piece);
    }

    private static void AddSlot(Frame frame, Slot slot)
    {
        frame.Slots.Add(slot);
        frame.ByKey[slot.Key] = slot;
        frame.ByName.TryAdd(slot.Name, slot);
    }

    private static List<Column> TextColumns(Slot slot)
    {
        var columns = new List<Column>();
        if (slot.Text.Count > 0)
            columns.Add(new Column { Key = FieldKey.Text, Name = "#text" });
        return columns;
    }

    private static List<Column> ColumnsOf(Frame frame)
    {
        var columns = new List<Column>(frame.Slots.Count);
        foreach (var slot in frame.Slots)
            columns.Add(new Column { Key = slot.Key, Name = slot.Name });
        return columns;
    }

    private static List<Column> CopyColumns(List<Column> source)
    {
        var columns = new List<Column>(source.Count);
        foreach (var column in source)
            columns.Add(new Column { Key = column.Key, Name = column.Name });
        return columns;
    }

    private void Merge(List<Column> columns, Frame child)
    {
        if (child.IsSimple)
        {
            if (child.Text.Count > 0)
                AddColumn(columns, FieldKey.Text, "#text");
            return;
        }

        foreach (var slot in child.Slots)
            AddColumn(columns, slot.Key, slot.Name);
    }

    private static void AddColumn(List<Column> columns, FieldKey key, string name)
    {
        foreach (var column in columns)
        {
            if (column.Key == key)
                return;
        }

        columns.Add(new Column { Key = key, Name = name });
    }

    private static List<TextPiece> CopyPieces(List<TextPiece> source)
    {
        var copy = new List<TextPiece>(source.Count);
        copy.AddRange(source);
        return copy;
    }
}
