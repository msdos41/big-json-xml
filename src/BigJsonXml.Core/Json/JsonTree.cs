using BigJsonXml.Xml;

namespace BigJsonXml.Json;

internal readonly struct JsonNote
{
    public bool Stop { get; init; }

    public string Message { get; init; }

    public long Byte { get; init; }

    public int Line { get; init; }

    public int Column { get; init; }

    public bool HasEntry { get; init; }

    public DocumentPath? Path { get; init; }

    public long EntryStart { get; init; }

    public Slot? Slot { get; init; }

    public static JsonNote Halt(string message, JsonEvent ev) => new()
    {
        Stop = true,
        Message = message,
        Byte = ev.ByteStart,
        Line = ev.Line,
        Column = ev.Column,
    };

    public static JsonNote Entry(DocumentPath path, long start, Slot slot) => new()
    {
        HasEntry = true,
        Path = path,
        EntryStart = start,
        Slot = slot,
    };
}

/// <summary>
/// Builds record and list slots for one JSON value. An object that is a list entry
/// contributes columns and is not kept as a record table.
/// </summary>
internal sealed class JsonTree
{
    private readonly DocumentTree? _host;
    private readonly Stack<Box> _boxes = new();
    private readonly Stack<Box> _spare = new();
    private readonly Stack<HashSet<string>> _sets = new();
    private readonly Stack<List<string>> _names = new();

    public JsonTree(DocumentPath path, DocumentTree? host)
    {
        Path = path;
        _host = host;
    }

    public DocumentPath Path { get; }

    public Frame? Root { get; private set; }

    public JsonNote Apply(JsonEvent ev) => ev.Kind switch
    {
        JsonEventKind.ObjectStart => OnObject(ev),
        JsonEventKind.ObjectEnd => OnObjectEnd(ev),
        JsonEventKind.ArrayStart => OnArray(ev),
        JsonEventKind.ArrayEnd => OnArrayEnd(ev),
        JsonEventKind.Key => OnKey(ev),
        JsonEventKind.Scalar => OnScalar(ev),
        _ => default,
    };

    private JsonNote OnObject(JsonEvent ev)
    {
        if (_boxes.Count == 0)
        {
            var root = NewFrame(ev.ByteStart, Path);
            Adopt(root);
            _boxes.Push(new Box { Frame = root, Start = ev.ByteStart, OwnsFrame = true });
            return default;
        }

        var parent = _boxes.Peek();
        if (parent.Array && !parent.Light)
        {
            _boxes.Push(LightObject(parent, ev.ByteStart, capture: true));
            return default;
        }

        if (parent.Light)
        {
            _boxes.Push(LightObject(parent, ev.ByteStart, capture: false));
            return default;
        }

        var slot = AddRecord(parent.Frame, parent.PendingKey!, ev.ByteStart);
        var child = NewFrame(ev.ByteStart, slot.Path!);
        Push(child);
        _boxes.Push(new Box { Frame = child, Record = slot, Start = ev.ByteStart, OwnsFrame = true });
        return default;
    }

    private JsonNote OnObjectEnd(JsonEvent ev)
    {
        var box = _boxes.Pop();
        if (box.Light)
        {
            JsonNote note = default;
            if (box.Capture)
            {
                var list = box.List!;
                foreach (var name in box.Direct!)
                    AddColumn(list, FieldKey.Elem("", name), name);
                note = Note(_boxes.Peek(), box.Start);
            }

            Recycle(box);
            return note;
        }

        box.Frame.EndByte = ev.ByteEnd;
        box.Frame.ContentEnd = ev.ByteStart;
        Pop(box.Frame);
        if (box.Record is not null)
        {
            box.Record.EndByte = ev.ByteEnd;
            box.Record.Detail = box.Frame;
        }

        if (!box.Entry)
            return default;
        var parent = _boxes.Peek();
        foreach (var slot in box.Frame.Slots)
            AddColumn(parent.List!, slot.Key, slot.Name);
        return Note(parent, box.Start);
    }

    private JsonNote OnArray(JsonEvent ev)
    {
        if (_boxes.Count == 0)
        {
            var root = NewFrame(ev.ByteStart, Path);
            var list = AddList(root, "#value", ev.ByteStart);
            Adopt(root);
            _boxes.Push(new Box { Array = true, Frame = root, List = list, Start = ev.ByteStart, OwnsFrame = true });
            return default;
        }

        var parent = _boxes.Peek();
        if (parent.Array && !parent.Light)
        {
            _boxes.Push(LightArray(parent, ev.ByteStart, entry: true));
            return default;
        }

        if (parent.Light)
        {
            _boxes.Push(LightArray(parent, ev.ByteStart, entry: false));
            return default;
        }

        var slot = AddList(parent.Frame, parent.PendingKey!, ev.ByteStart);
        _boxes.Push(new Box { Array = true, Frame = parent.Frame, List = slot, Start = ev.ByteStart });
        return default;
    }

    private JsonNote OnArrayEnd(JsonEvent ev)
    {
        var box = _boxes.Pop();
        if (box.Light)
        {
            JsonNote note = default;
            if (box.Entry)
            {
                var list = _boxes.Peek();
                AddColumn(list.List!, ValueKey, "#value");
                note = Note(list, box.Start);
            }

            Recycle(box);
            return note;
        }

        box.List!.EndByte = ev.ByteEnd;
        if (box.OwnsFrame)
            Pop(box.Frame);
        if (!box.Entry)
            return default;

        var parent = _boxes.Peek();
        AddColumn(parent.List!, ValueKey, "#value");
        return Note(parent, box.Start);
    }

    private JsonNote OnKey(JsonEvent ev)
    {
        var box = _boxes.Peek();
        if (box.Light)
        {
            if (box.Keys is null || !box.Keys.Add(ev.Name))
                return JsonNote.Halt("A repeated key stops the scan.", ev);
            if (box.Capture)
                box.Direct!.Add(ev.Name);
            box.PendingKey = ev.Name;
            return default;
        }

        if (box.Frame.ByName.ContainsKey(ev.Name))
            return JsonNote.Halt("A repeated key stops the scan.", ev);
        box.PendingKey = ev.Name;
        return default;
    }

    private JsonNote OnScalar(JsonEvent ev)
    {
        if (_boxes.Count == 0)
        {
            var root = NewFrame(ev.ByteStart, Path);
            AddScalar(root, "#value", ev.ByteStart, ev.ByteEnd);
            Adopt(root);
            return default;
        }

        var box = _boxes.Peek();
        if (box.Light)
            return default;
        if (box.Array)
        {
            AddColumn(box.List!, ValueKey, "#value");
            return Note(box, ev.ByteStart);
        }

        AddScalar(box.Frame, box.PendingKey!, ev.ByteStart, ev.ByteEnd);
        return default;
    }

    private static JsonNote Note(Box array, long start)
    {
        var list = array.List!;
        list.Count++;
        list.EntryStarts!.Add(start);
        return JsonNote.Entry(list.Path!, start, list);
    }

    public void AddDrained(Slot list, long start, IReadOnlyList<string> keys, bool valueColumn)
    {
        if (valueColumn)
            AddColumn(list, ValueKey, "#value");
        else
        {
            foreach (var name in keys)
                AddColumn(list, FieldKey.Elem("", name), name);
        }

        list.Count++;
        list.EntryStarts!.Add(start);
    }

    public void AddSame(Slot list, long start)
    {
        list.Count++;
        list.EntryStarts!.Add(start);
    }

    private static void AddColumn(Slot list, FieldKey key, string name)
    {
        foreach (var column in list.Columns)
        {
            if (column.Key == key)
                return;
        }

        list.Columns.Add(new Column { Key = key, Name = name });
    }

    private Slot AddRecord(Frame frame, string name, long start)
    {
        var slot = new Slot
        {
            Key = FieldKey.Elem("", name),
            Name = name,
            Kind = SlotKind.Record,
            StartByte = start,
            EndByte = -1,
            Closed = "{}",
            HasFields = true,
        };
        Add(frame, slot);
        return slot;
    }

    private Slot AddList(Frame frame, string name, long start)
    {
        var slot = new Slot
        {
            Key = FieldKey.Elem("", name),
            Name = name,
            Kind = SlotKind.List,
            Count = 0,
            StartByte = start,
            EndByte = -1,
            EntryStarts = new EntryOffsets(),
            HasFields = true,
        };
        Add(frame, slot);
        return slot;
    }

    private static void AddScalar(Frame frame, string name, long start, long end)
    {
        var slot = new Slot
        {
            Key = FieldKey.Elem("", name),
            Name = name,
            Kind = SlotKind.Scalar,
            StartByte = start,
            EndByte = end,
            Text = [TextPiece.JsonLiteral(start, end)],
        };
        Add(frame, slot);
    }

    private static void Add(Frame frame, Slot slot)
    {
        slot.PathParent = frame.Path;
        slot.Path = frame.Path.Field(slot.Name);
        frame.Slots.Add(slot);
        frame.ByKey[slot.Key] = slot;
        frame.ByName[slot.Name] = slot;
    }

    private static Frame NewFrame(long start, DocumentPath path) => new()
    {
        StartByte = start,
        ContentByte = start,
        StableByte = start,
        EndByte = -1,
        ContentEnd = -1,
        Path = path,
        AlwaysRecord = true,
    };

    private void Adopt(Frame frame)
    {
        Root = frame;
        if (_host is null)
            return;
        _host.SetRoot(frame);
        Push(frame);
    }

    private void Push(Frame frame) => _host?.Stack.Push(frame);

    private void Pop(Frame frame)
    {
        if (_host is null || _host.Stack.Count == 0)
            return;
        if (ReferenceEquals(_host.Stack.Peek(), frame))
            _host.Stack.Pop();
    }

    private static FieldKey ValueKey => FieldKey.Elem("", "#value");

    private Box LightObject(Box parent, long start, bool capture)
    {
        var box = Rent();
        box.Light = true;
        box.Entry = capture;
        box.Capture = capture;
        box.Frame = parent.Frame;
        box.List = parent.List;
        box.Start = start;
        box.Keys = RentSet();
        if (capture)
            box.Direct = RentNames();
        return box;
    }

    private Box LightArray(Box parent, long start, bool entry)
    {
        var box = Rent();
        box.Light = true;
        box.Array = true;
        box.Entry = entry;
        box.Frame = parent.Frame;
        box.List = parent.List;
        box.Start = start;
        return box;
    }

    private Box Rent()
    {
        if (_spare.Count == 0)
            return new Box();
        var box = _spare.Pop();
        box.Array = false;
        box.Entry = false;
        box.Light = false;
        box.Capture = false;
        box.Record = null;
        box.PendingKey = null;
        box.OwnsFrame = false;
        box.Keys = null;
        box.Direct = null;
        return box;
    }

    private void Recycle(Box box)
    {
        if (box.Keys is not null)
        {
            box.Keys.Clear();
            _sets.Push(box.Keys);
            box.Keys = null;
        }

        if (box.Direct is not null)
        {
            box.Direct.Clear();
            _names.Push(box.Direct);
            box.Direct = null;
        }

        if (_spare.Count < 64)
            _spare.Push(box);
    }

    private HashSet<string> RentSet()
    {
        if (_sets.Count == 0)
            return new HashSet<string>(StringComparer.Ordinal);
        return _sets.Pop();
    }

    private List<string> RentNames() => _names.Count == 0 ? [] : _names.Pop();

    private sealed class Box
    {
        public bool Array;
        public bool Entry;
        public bool Light;
        public bool Capture;
        public Frame Frame = null!;
        public Slot? List;
        public Slot? Record;
        public long Start;
        public string? PendingKey;
        public bool OwnsFrame;
        public HashSet<string>? Keys;
        public List<string>? Direct;
    }
}

internal static class JsonLoader
{
    public static Frame Load(XmlDocument document, long offset, DocumentPath path)
    {
        var parser = JsonParser.Fragment(document, offset);
        var tree = new JsonTree(path, host: null);
        while (true)
        {
            var ev = parser.Read();
            if (ev.Kind is JsonEventKind.Done or JsonEventKind.NeedMore or JsonEventKind.Error or JsonEventKind.Refuse)
                break;
            if (ev.Kind == JsonEventKind.Continue)
                continue;
            if (tree.Apply(ev).Stop)
                break;
        }

        return tree.Root ?? throw new InvalidOperationException("The value could not be read.");
    }
}
