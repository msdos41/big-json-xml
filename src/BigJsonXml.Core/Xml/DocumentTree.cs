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
    public DocumentPath? Path;
    public DocumentPath? PathParent;

    // Closed record body. A refresh reads this instead of parsing the children again.
    public Frame? Detail;

    // JSON object shown while collapsed. XML leaves this empty and shows the element name.
    public string? Closed;

    // Direct fields of a record, remembered so a collapsed cell does not open it again.
    // -1 means the outline was not recorded.
    public int OutlineCount = -1;
    public bool OutlineSoleList;

    // JSON list entry offsets. XML leaves this empty and scans children when the list opens.
    public List<long>? EntryStarts;
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
    public Frame? Parent;
    public bool Announced;
    public bool ColumnsDirty;

    public bool IsSimple => !SawAttribute && !SawElement;

    public Slot? FindKey(FieldKey key)
    {
        if (ByKey.Count > 0)
            return ByKey.GetValueOrDefault(key);
        foreach (var slot in Slots)
        {
            if (slot.Key == key)
                return slot;
        }

        return null;
    }

    public Slot? FindName(string name)
    {
        if (ByName.Count > 0)
        {
            return ByName.TryGetValue(name, out var slot) ? slot : null;
        }

        foreach (var slot in Slots)
        {
            if (slot.Name == name)
                return slot;
        }

        return null;
    }
}

internal readonly struct Folded(Frame parent, Slot slot, long childStart, long childEnd, bool promoted)
{
    public Frame Parent { get; } = parent;
    public Slot Slot { get; } = slot;
    public long ChildStart { get; } = childStart;
    public long ChildEnd { get; } = childEnd;
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

    internal void SetRoot(Frame frame) => Root = frame;

    internal void ClearRoot()
    {
        Root = null;
        Stack.Clear();
        RootClosed = false;
    }

    public bool RootClosed { get; private set; }

    private Folded _announced;
    private bool _hasAnnounced;
    private readonly Stack<Frame> _spareFrames = new();
    private readonly Stack<Slot> _spareSlots = new();
    private readonly Dictionary<(DocumentPath Parent, string Name), DocumentPath> _childPaths = new();
    private readonly Dictionary<string, string> _atNames = new(StringComparer.Ordinal);
    private readonly TextPiece[] _onePiece = new TextPiece[1];
    private bool _retainRecordBody = true;

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
                        folded = new Folded(folded.Parent, folded.Slot, folded.ChildStart, folded.ChildEnd, true);
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
                AddText(Stack.Peek(), ev.Text, ev.TrustedText);
                return false;
            case XmlEventKind.Flat when Stack.Count > 0:
                return NoteFlat(ev, out folded);
            default:
                return false;
        }
    }

    public Frame Load(XmlParser parser, DocumentPath path)
    {
        // The caller keeps this element. Nested records stay reachable from their start bytes,
        // so this read does not retain a second copy of each of them.
        _retainRecordBody = false;
        var depth = 0;
        while (true)
        {
            var ev = parser.Read();
            if (ev.Kind is XmlEventKind.Done or XmlEventKind.NeedMore or XmlEventKind.Error)
                break;
            if (Apply(ev, out var folded) && folded.Promoted)
                parser.PreferFlat(folded.Slot.Name);
            if (ev.Kind == XmlEventKind.Start)
            {
                if (Root is not null && Root.Path == DocumentPath.Root && path != DocumentPath.Root)
                    Root.Path = path;
                if (!ev.Empty)
                {
                    depth++;
                    // Names at this level are the outline. Their children are read when that field is opened.
                    if (depth >= 3)
                        parser.SkipNestedContent();
                }
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
        var frame = RentFrame();
        frame.Parent = parent;
        frame.Name = ev.Name;
        frame.Local = ev.Local;
        frame.Ns = ev.Ns;
        frame.StartByte = ev.ByteStart;
        frame.ContentByte = ev.ByteEnd;
        frame.StableByte = ev.ByteEnd;
        frame.Scope = ev.Scope;
        frame.AlwaysRecord = _documentElement && parent is null;
        frame.Path = parent is null ? DocumentPath.Root : ChildPath(parent, ev.Name);
        foreach (var attr in ev.Attributes)
        {
            var slot = RentSlot();
            slot.Key = FieldKey.Attr(attr.Ns, attr.Local);
            slot.Name = At(attr.Name);
            slot.Kind = SlotKind.Scalar;
            slot.Text.Clear();
            slot.Text.Add(attr.Value);
            slot.ParentScope = ev.Scope;
            AddSlot(frame, slot);
            frame.SawAttribute = true;
        }

        if (parent is null)
        {
            Root = frame;
            frame.Announced = true;
        }
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
            var parent = frame.Parent;
            if (parent is not null && Ensure(parent, frame, out var folded))
            {
                _announced = folded;
                _hasAnnounced = true;
            }
        }

        var ancestor = frame.Parent;
        if (ancestor is not null)
        {
            if (ancestor.Announced && !ancestor.ColumnsDirty)
                return;
            Announce(ancestor);
        }
    }

    private static Frame? ParentOf(Frame frame) => frame.Parent;

    private bool Ensure(Frame parent, Frame child, out Folded folded)
    {
        folded = default;
        var key = FieldKey.Elem(child.Ns, child.Local);
        if (parent.FindKey(key) is not Slot slot)
        {
            slot = RentSlot();
            slot.Key = key;
            slot.Name = child.Name;
            slot.StartByte = child.StartByte;
            slot.EndByte = -1;
            slot.PendingStart = child.StartByte;
            slot.ParentScope = parent.Scope;
            slot.Kind = SlotKind.Record;
            slot.HasFields = true;
            slot.Inner = ColumnsOf(child);
            child.ColumnsDirty = false;
            child.Announced = true;
            AddSlot(parent, slot);
            folded = new Folded(parent, slot, child.StartByte, child.EndByte, false);
            return true;
        }

        if (slot.PendingStart == child.StartByte)
        {
            child.Announced = true;
            if (slot.Kind == SlotKind.Record && child.ColumnsDirty)
            {
                slot.Inner = ColumnsOf(child);
                child.ColumnsDirty = false;
            }
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
            ReleaseDetail(slot);
        }
        else
        {
            slot.Count++;
            Merge(slot.Columns, child);
        }

        child.Announced = true;
        slot.PendingStart = child.StartByte;
        folded = new Folded(parent, slot, child.StartByte, child.EndByte, promoted);
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
        if (!ReferenceEquals(folded.Slot.Detail, frame))
            Recycle(frame);
        return true;
    }

    private Folded Fold(Frame parent, Frame child)
    {
        NoteElement(parent);
        var key = FieldKey.Elem(child.Ns, child.Local);
        if (parent.FindKey(key) is Slot pending && pending.PendingStart == child.StartByte)
        {
            pending.PendingStart = -1;
            pending.EndByte = child.EndByte;
            if (pending.Kind == SlotKind.List)
            {
                Merge(pending.Columns, child);
                ReleaseDetail(pending);
            }
            else if (child.IsSimple)
            {
                pending.Kind = SlotKind.Scalar;
                pending.HasFields = false;
                GivePieces(pending.Text, child.Text);
                pending.Inner.Clear();
                pending.Columns.Clear();
                ReleaseDetail(pending);
            }
            else
            {
                pending.Kind = SlotKind.Record;
                pending.HasFields = true;
                if (child.ColumnsDirty || pending.Inner.Count == 0)
                {
                    pending.Inner = ColumnsOf(child);
                    child.ColumnsDirty = false;
                }
                NoteOutline(pending, child);
                if (_retainRecordBody)
                    pending.Detail = child;
            }

            return new Folded(parent, pending, child.StartByte, child.EndByte, false);
        }

        var promoted = false;
        if (parent.FindKey(key) is not Slot slot)
        {
            slot = RentSlot();
            slot.Key = key;
            slot.Name = child.Name;
            slot.StartByte = child.StartByte;
            slot.EndByte = child.EndByte;
            slot.ParentScope = parent.Scope;
            if (child.IsSimple)
            {
                slot.Kind = SlotKind.Scalar;
                GivePieces(slot.Text, child.Text);
            }
            else
            {
                slot.Kind = SlotKind.Record;
                slot.HasFields = true;
                slot.Inner = ColumnsOf(child);
                child.ColumnsDirty = false;
                NoteOutline(slot, child);
                if (_retainRecordBody)
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
            ReleaseDetail(slot);
        }
        else
        {
            slot.Count++;
            Merge(slot.Columns, child);
            ReleaseDetail(slot);
        }

        return new Folded(parent, slot, child.StartByte, child.EndByte, promoted);
    }

    private void NoteElement(Frame frame)
    {
        if (!frame.SawElement && !frame.SawAttribute && frame.Text.Count > 0)
        {
            var slot = RentSlot();
            slot.Key = FieldKey.Text;
            slot.Name = "#text";
            slot.Kind = SlotKind.Scalar;
            GivePieces(slot.Text, frame.Text);
            frame.TextAt = frame.Slots.Count;
            AddSlot(frame, slot);
            frame.Text.Clear();
        }

        frame.SawElement = true;
    }

    internal bool SameFlat(Slot slot, List<FlatField> fields)
    {
        var columns = slot.Columns;
        if (columns.Count != fields.Count)
            return false;
        for (var i = 0; i < fields.Count; i++)
        {
            if (columns[i].Name != fields[i].Name)
                return false;
        }

        return true;
    }

    internal void MergeFlat(Slot slot, List<FlatField> fields)
    {
        foreach (var field in fields)
            AddColumn(slot.Columns, field.Key, field.Name);
    }

    private bool NoteFlat(XmlEvent ev, out Folded folded)
    {
        folded = default;
        if (Stack.Count == 0)
            return false;
        var parent = Stack.Peek();
        var key = FieldKey.Elem(ev.Ns, ev.Local);
        var slot = parent.FindKey(key);
        if (slot is null || slot.Kind != SlotKind.List)
            return false;
        slot.Count++;
        slot.EndByte = ev.ByteEnd;
        parent.StableByte = ev.ByteEnd;
        if (ev.SameShape)
        {
            folded = new Folded(parent, slot, ev.ByteStart, ev.ByteEnd, false);
            return true;
        }

        var columns = slot.Columns;
        var fields = ev.FlatFields;
        var same = fields.Count == columns.Count;
        if (same)
        {
            for (var i = 0; i < fields.Count; i++)
            {
                if (!ReferenceEquals(fields[i].Name, columns[i].Name))
                {
                    same = false;
                    break;
                }
            }
        }

        if (!same)
        {
            foreach (var field in fields)
                AddColumn(columns, field.Key, field.Name);
        }

        folded = new Folded(parent, slot, ev.ByteStart, ev.ByteEnd, false);
        return true;
    }

    private void AddText(Frame frame, TextPiece piece, bool trusted)
    {
        if (!trusted)
        {
            _onePiece[0] = piece;
            if (ExpandedText.IsWhitespaceOnly(Document, _onePiece))
                return;
        }
        if (frame.AlwaysRecord || frame.SawAttribute || frame.SawElement)
            AppendHash(frame, piece);
        else
            frame.Text.Add(piece);
    }

    private void AppendHash(Frame frame, TextPiece piece)
    {
        if (frame.TextAt < 0)
        {
            var slot = RentSlot();
            slot.Key = FieldKey.Text;
            slot.Name = "#text";
            slot.Kind = SlotKind.Scalar;
            frame.TextAt = frame.Slots.Count;
            AddSlot(frame, slot);
        }

        frame.Slots[frame.TextAt].Text.Add(piece);
    }

    private void AddSlot(Frame frame, Slot slot)
    {
        if (slot.Path is null || !ReferenceEquals(slot.PathParent, frame.Path) || slot.Path.Steps[^1].Name != slot.Name)
        {
            slot.PathParent = frame.Path;
            slot.Path = ChildPath(frame, slot.Name);
        }

        frame.Slots.Add(slot);
        frame.ColumnsDirty = true;
        if (frame.AlwaysRecord || frame.Slots.Count > 8)
        {
            if (frame.ByKey.Count == 0)
            {
                foreach (var existing in frame.Slots)
                {
                    frame.ByKey[existing.Key] = existing;
                    frame.ByName[existing.Name] = existing;
                }
            }
            else
            {
                frame.ByKey[slot.Key] = slot;
                frame.ByName[slot.Name] = slot;
            }
        }
    }

    private static void NoteOutline(Slot slot, Frame child)
    {
        slot.OutlineCount = child.Slots.Count;
        slot.OutlineSoleList = child.Slots.Count == 1 && child.Slots[0].Kind == SlotKind.List;
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

    private static void GivePieces(List<TextPiece> target, List<TextPiece> source)
    {
        target.Clear();
        target.AddRange(source);
    }

    private string At(string name)
    {
        if (_atNames.TryGetValue(name, out var existing))
            return existing;
        var text = "@" + name;
        _atNames[name] = text;
        return text;
    }

    private DocumentPath ChildPath(Frame parent, string name)
    {
        if (_childPaths.TryGetValue((parent.Path, name), out var found))
            return found;
        var path = parent.Path.Field(name);
        _childPaths[(parent.Path, name)] = path;
        return path;
    }

    private Frame RentFrame()
    {
        if (_spareFrames.Count == 0)
        {
            return new Frame();
        }
        var frame = _spareFrames.Pop();
        frame.Name = "";
        frame.Local = "";
        frame.Ns = "";
        frame.StartByte = 0;
        frame.ContentByte = 0;
        frame.ContentEnd = -1;
        frame.EndByte = -1;
        frame.StableByte = 0;
        frame.Scope = NsScope.Empty;
        frame.Path = DocumentPath.Root;
        frame.AlwaysRecord = false;
        frame.SawAttribute = false;
        frame.SawElement = false;
        frame.TextAt = -1;
        frame.Parent = null;
        frame.Announced = false;
        frame.ColumnsDirty = false;
        return frame;
    }

    private Slot RentSlot()
    {
        if (_spareSlots.Count == 0)
        {
            return new Slot();
        }
        var slot = _spareSlots.Pop();
        slot.Name = "";
        slot.Kind = SlotKind.Scalar;
        slot.Count = 1;
        slot.StartByte = 0;
        slot.EndByte = -1;
        slot.PendingStart = -1;
        slot.ParentScope = NsScope.Empty;
        slot.Text.Clear();
        slot.Columns.Clear();
        slot.Inner.Clear();
        slot.HasFields = false;
        slot.Detail = null;
        slot.Closed = null;
        slot.EntryStarts = null;
        slot.OutlineCount = -1;
        slot.OutlineSoleList = false;
        return slot;
    }

    private void ReleaseDetail(Slot slot)
    {
        if (slot.Detail is null)
            return;
        var detail = slot.Detail;
        slot.Detail = null;
        Recycle(detail);
    }

    private void Recycle(Frame frame)
    {
        foreach (var slot in frame.Slots)
        {
            if (slot.Detail is not null)
            {
                var detail = slot.Detail;
                slot.Detail = null;
                Recycle(detail);
            }

            slot.Text.Clear();
            slot.Columns.Clear();
            slot.Inner.Clear();
            slot.Detail = null;
            if (_spareSlots.Count < 64)
                _spareSlots.Push(slot);
        }

        frame.Slots.Clear();
        frame.ByName.Clear();
        frame.ByKey.Clear();
        frame.Text.Clear();
        frame.TextAt = -1;
        frame.SawAttribute = false;
        frame.SawElement = false;
        frame.AlwaysRecord = false;
        frame.EndByte = -1;
        frame.ContentEnd = -1;
        frame.Path = DocumentPath.Root;
        frame.Parent = null;
        frame.Announced = false;
        frame.ColumnsDirty = false;
        if (_spareFrames.Count < 32)
            _spareFrames.Push(frame);
    }
}
