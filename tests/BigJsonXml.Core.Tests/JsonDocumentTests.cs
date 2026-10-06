using System.Text;

namespace BigJsonXml.Tests;

public class JsonDocumentTests
{
    [Fact]
    public void An_object_is_a_record_of_keys_in_file_order()
    {
        var session = Open("""{"z":1,"a":"x","m":true}""");

        var fields = session.ReadRecord(DocumentPath.Root).Fields;
        Assert.Equal(["z", "a", "m"], fields.Select(field => field.Name).ToArray());
        Assert.Equal("1", fields[0].Text);
        Assert.Equal("\"x\"", fields[1].Text);
        Assert.Equal("true", fields[2].Text);
        Assert.DoesNotContain(fields, field => field.Name.StartsWith('@'));
        Assert.Equal(ScanProgress.Completed, session.Progress);
    }

    [Fact]
    public void Scalars_are_shown_as_json_literals()
    {
        var session = Open("""
            {"q":"a\"b","sl":"a\\b","slash":"a\/b","nl":"A\nB","u":"\u0041","empty":"","n":1.0,"e":1e2,"z":-0,"nil":null,"str":"null","num":"1","t":true,"ts":"true"}
            """);

        Assert.Equal("\"a\\\"b\"", session.Field("q").Text);
        Assert.Equal("\"a\\\\b\"", session.Field("sl").Text);
        Assert.Equal("\"a/b\"", session.Field("slash").Text);
        Assert.Equal("\"A\nB\"", session.Field("nl").Text);
        Assert.Equal("\"A\"", session.Field("u").Text);
        Assert.Equal("\"\"", session.Field("empty").Text);
        Assert.Equal("1.0", session.Field("n").Text);
        Assert.Equal("1e2", session.Field("e").Text);
        Assert.Equal("-0", session.Field("z").Text);
        Assert.Equal("null", session.Field("nil").Text);
        Assert.Equal("\"null\"", session.Field("str").Text);
        Assert.Equal("\"1\"", session.Field("num").Text);
        Assert.Equal("true", session.Field("t").Text);
        Assert.Equal("\"true\"", session.Field("ts").Text);
    }

    [Fact]
    public void An_empty_object_is_an_empty_record_and_a_nested_object_collapses_to_braces()
    {
        var empty = Open("{}");
        Assert.Empty(empty.ReadRecord(DocumentPath.Root).Fields);
        Assert.Equal(ScanProgress.Completed, empty.Progress);

        var session = Open("""{"box":{"a":1},"b":2}""");
        Assert.Equal("{}", session.Field("box").Text);
        Assert.Equal(FieldKind.Record, session.Field("box").Kind);
        Assert.Contains(session.ReadSurface().Rows, row => row.Name == "box" && row.Value == "{}");

        var box = DocumentPath.Root.Field("box");
        session.Expand(box);
        Assert.Equal("1", session.ReadRecord(box).Fields.Single(field => field.Name == "a").Text);
        Assert.Equal(["box", "b"], session.ReadRecord(DocumentPath.Root).Fields.Select(field => field.Name).ToArray());
    }

    [Fact]
    public void Whitespace_and_a_leading_bom_are_not_fields()
    {
        var pretty = Open("""
            {
              "a" : 1,
              "b" : true
            }
            """);
        Assert.Equal(["a", "b"], pretty.ReadRecord(DocumentPath.Root).Fields.Select(field => field.Name).ToArray());

        var source = new MemoryByteSource();
        source.Append([0xEF, 0xBB, 0xBF]);
        source.Append(" \n{\"a\":1}"u8);
        source.Complete();
        var session = ViewerSession.Open(source);
        session.PumpAll();
        Assert.Equal("1", session.Field("a").Text);
        Assert.Equal(ScanProgress.Completed, session.Progress);
    }

    [Fact]
    public void A_partly_scanned_object_shows_the_fields_already_finished()
    {
        var source = new MemoryByteSource();
        source.Append("{\"name\":\"Ada\",\"big\":\"");
        var session = ViewerSession.Open(source);
        session.PumpAll();

        Assert.Equal(ScanProgress.Scanning, session.Progress);
        Assert.Equal("\"Ada\"", session.Field("name").Text);
        Assert.DoesNotContain(session.ReadRecord(DocumentPath.Root).Fields, field => field.Name == "big");
    }

    [Fact]
    public void A_long_string_is_a_window_of_the_same_literal()
    {
        var body = new string('a', 80_000);
        var session = Open("{\"s\":\"" + body + "\"}");
        var path = DocumentPath.Root.Field("s");
        var before = GC.GetAllocatedBytesForCurrentThread();
        var field = session.Field("s");
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.StartsWith("\"", field.Text, StringComparison.Ordinal);
        Assert.InRange(field.Text!.Length, 1, 25);
        Assert.True(field.CanExpandFullValue);
        Assert.True(allocated < 40_000, $"window allocated {allocated} bytes");

        session.ExpandFullValue(path);
        var full = session.ReadFullValueSlice(path, 0, session.ScalarLength(path));
        Assert.Equal("\"" + body + "\"", full);
    }

    [Fact]
    public void A_leading_less_than_is_still_xml()
    {
        var session = SessionFixture.Scan("<root id=\"1\"><a>1</a></root>");
        Assert.Equal("1", session.Field("a").Text);
        Assert.Equal("@id", session.Field("@id").Name);
        Assert.Equal("/root/a", session.FormatPath(DocumentPath.Root.Field("a")));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\n\t")]
    [InlineData("Hello")]
    public void Bytes_that_do_not_start_a_value_do_not_open(string text)
    {
        var source = new MemoryByteSource();
        source.Append(Encoding.UTF8.GetBytes(text));
        source.Complete();
        var session = ViewerSession.Open(source);
        session.PumpAll();

        Assert.Equal(ScanProgress.Stopped, session.Progress);
        Assert.Equal(0, session.BytesReached);
        Assert.Equal(0, session.Error!.ByteOffset);
        Assert.Contains("no root element", session.Error.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => session.ReadRecord(DocumentPath.Root));
    }

    [Fact]
    public void The_scan_reports_a_fraction_before_a_long_array_finishes()
    {
        var source = new MemoryByteSource();
        source.Append("["u8);
        AppendCopies(source, "1,"u8, 4_000);
        source.Append("1]"u8);
        source.Complete();
        var session = ViewerSession.Open(source);

        Assert.True(session.Pump(1));
        Assert.Equal(ScanProgress.Scanning, session.Progress);
        Assert.InRange(session.BytesReached, 1, session.DocumentLength - 1);

        session.PumpAll();
        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Equal("#value[4001]", session.Field("#value").ListLabel);
    }

    [Fact]
    public void An_array_is_a_list_at_length_zero_and_one_and_keeps_growing()
    {
        var source = new MemoryByteSource();
        source.Append("""{"a":[""");
        var session = ViewerSession.Open(source);
        session.PumpAll();

        Assert.Equal(ScanProgress.Scanning, session.Progress);
        Assert.Equal(FieldKind.List, session.Field("a").Kind);
        Assert.Equal(0, session.Field("a").ListCount);
        Assert.Equal("a[0]", session.Field("a").ListLabel);

        source.Append("""{"id":"a1"}""");
        session.PumpAll();
        Assert.Equal(1, session.Field("a").ListCount);
        Assert.Equal(FieldKind.List, session.Field("a").Kind);

        source.Append("""]}""");
        source.Complete();
        session.PumpAll();
        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Equal(1, session.Field("a").ListCount);
        Assert.Equal("a[1]", session.Field("a").ListLabel);

        var list = DocumentPath.Root.Field("a");
        session.Expand(list);
        var view = session.ReadList(list, 0, 1);
        Assert.Equal(["id"], view.Columns);
        Assert.Equal(1, view.Entries[0].OneBasedIndex);
        Assert.Equal(FieldKind.Scalar, Assert.Single(view.Entries[0].Cells).Kind);
        Assert.Equal("\"a1\"", view.Entries[0].Cells[0].Text);
        Assert.Equal("/a[1]/id", session.FormatPath(list.WithIndex(1).Field("id")));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.WithIndex(0));
    }

    [Fact]
    public void An_empty_array_is_a_list_labeled_with_zero()
    {
        var session = Open("""{"a":[]}""");
        Assert.Equal(FieldKind.List, session.Field("a").Kind);
        Assert.Equal("a[0]", session.Field("a").ListLabel);
        Assert.Equal(0, session.Field("a").ListCount);
    }

    [Fact]
    public void Later_object_keys_append_columns_and_a_missing_key_is_blank()
    {
        var session = Open("""{"nums":[{"id":"a1"},{"id":"a2","extra":true},{"extra":false}]}""");
        var list = DocumentPath.Root.Field("nums");
        session.Expand(list);
        var view = session.ReadList(list, 0, 3);

        Assert.Equal(["id", "extra"], view.Columns);
        Assert.Equal("\"a1\"", Cell(view, 0, "id").Text);
        Assert.Equal("", Cell(view, 0, "extra").Text);
        Assert.Equal(FieldKind.Scalar, Cell(view, 0, "extra").Kind);
        Assert.Equal("true", Cell(view, 1, "extra").Text);
        Assert.Equal("", Cell(view, 2, "id").Text);
        Assert.NotEqual("null", Cell(view, 0, "extra").Text);
    }

    [Fact]
    public void Opening_one_list_entry_does_not_reread_the_others()
    {
        const int count = 40;
        var payload = new string('x', 2_000);
        var body = new StringBuilder();
        body.Append("{\"nums\":[");
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
                body.Append(',');
            body.Append("{\"id\":\"").Append(payload).Append("\"}");
        }

        body.Append("]}");
        var session = Open(body.ToString());
        var list = DocumentPath.Root.Field("nums");
        session.Expand(list);
        _ = session.ReadList(list, count - 1, 1);
        session.Collapse(list);
        session.Expand(list);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var view = session.ReadList(list, count - 1, 1);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.StartsWith("\"xxx", view.Entries[0].Cells[0].Text, StringComparison.Ordinal);
        Assert.True(allocated < 50_000, $"tail read allocated {allocated} bytes");
    }

    [Fact]
    public void A_root_scalar_and_a_root_array_use_the_value_field()
    {
        Assert.Equal("1", Open("1").Field("#value").Text);
        Assert.Equal("\"x\"", Open("\"x\"").Field("#value").Text);
        Assert.Equal("true", Open("true").Field("#value").Text);
        Assert.Equal("false", Open("false").Field("#value").Text);
        Assert.Equal("null", Open("null").Field("#value").Text);
        Assert.Equal(FieldKind.List, Open("[1]").Field("#value").Kind);
        Assert.Equal("#value[1]", Open("[1]").Field("#value").ListLabel);
    }

    [Fact]
    public void A_mixed_array_puts_scalars_and_nested_arrays_on_value_and_object_keys_beside_them()
    {
        var session = Open("[1,{\"a\":2},null,[3]]");
        var list = DocumentPath.Root.Field("#value");
        session.Expand(list);
        var view = session.ReadList(list, 0, 4);

        Assert.Equal(["#value", "a"], view.Columns);
        Assert.Equal("1", Cell(view, 0, "#value").Text);
        Assert.Equal("", Cell(view, 0, "a").Text);
        Assert.Equal("", Cell(view, 1, "#value").Text);
        Assert.Equal("2", Cell(view, 1, "a").Text);
        Assert.Equal("null", Cell(view, 2, "#value").Text);
        Assert.Equal(FieldKind.List, Cell(view, 3, "#value").Kind);
        Assert.Equal("#value[1]", Cell(view, 3, "#value").ListLabel);

        var nested = list.WithIndex(4).Field("#value");
        session.Expand(nested);
        var inner = session.ReadList(nested, 0, 1);
        Assert.Equal("3", inner.Entries[0].Cells[0].Text);
        Assert.Equal("/#value[4]/#value[1]/#value", session.FormatPath(nested.WithIndex(1).Field("#value")));
    }

    [Fact]
    public void A_nested_array_path_keeps_each_value_step()
    {
        var session = Open("[[1]]");
        var outer = DocumentPath.Root.Field("#value");
        session.Expand(outer);
        var inner = outer.WithIndex(1).Field("#value");
        session.Expand(inner);
        Assert.Equal("1", session.ReadList(inner, 0, 1).Entries[0].Cells[0].Text);
        Assert.Equal("/#value[1]/#value[1]/#value", session.FormatPath(inner.WithIndex(1).Field("#value")));
    }

    [Fact]
    public void An_empty_object_entry_is_a_blank_row_and_a_value_key_shares_the_scalar_column()
    {
        var blanks = Open("[{},{\"a\":1}]");
        var list = DocumentPath.Root.Field("#value");
        blanks.Expand(list);
        var view = blanks.ReadList(list, 0, 2);
        Assert.Equal("", Cell(view, 0, "a").Text);
        Assert.NotEqual("null", Cell(view, 0, "a").Text);

        var shared = Open("[1,{\"#value\":\"x\"}]");
        var root = DocumentPath.Root.Field("#value");
        shared.Expand(root);
        var rows = shared.ReadList(root, 0, 2);
        Assert.Equal(["#value"], rows.Columns);
        Assert.Equal("1", rows.Entries[0].Cells[0].Text);
        Assert.Equal("\"x\"", rows.Entries[1].Cells[0].Text);
    }

    [Fact]
    public void A_root_object_keeps_its_own_keys_as_the_first_path_step()
    {
        var session = Open("{\"a\":{\"b\":1}}");
        var path = DocumentPath.Root.Field("a").Field("b");
        Assert.Equal("/a/b", session.FormatPath(path));
        Assert.Equal("1", session.ReadScalar(path).Text);
    }

    [Fact]
    public void A_repeated_key_stops_the_scan_and_keeps_the_fields_before_it()
    {
        var json = "{\"a\":1,\"b\":2,\"a\":3,\"c\":4}";
        var session = Open(json);
        var fields = session.ReadRecord(DocumentPath.Root).Fields;

        Assert.Equal(["a", "b"], fields.Select(field => field.Name).ToArray());
        Assert.Equal("1", fields[0].Text);
        Assert.Equal("2", fields[1].Text);
        Assert.Equal(ScanProgress.Stopped, session.Progress);
        Assert.Equal("A repeated key stops the scan.", session.Error!.Message);
        Assert.Equal(json.LastIndexOf("\"a\"", StringComparison.Ordinal), session.Error.ByteOffset);
    }

    [Theory]
    [InlineData("{\"a\":1,}")]
    [InlineData("{\"a\":1}//no")]
    [InlineData("{\"a\":1,\"b\":NaN}")]
    [InlineData("{\"a\":1,\"b\":+1}")]
    [InlineData("{\"a\":1,\"b\":'x'}")]
    [InlineData("{\"a\":1,\"b\":01}")]
    public void Ill_formed_json_keeps_the_finished_prefix(string json)
    {
        var session = Open(json);
        Assert.Equal(ScanProgress.Stopped, session.Progress);
        Assert.Equal("1", session.Field("a").Text);
        Assert.DoesNotContain(session.ReadRecord(DocumentPath.Root).Fields, field => field.Name == "b");
        Assert.False(string.IsNullOrEmpty(session.Error!.Message));
    }

    [Fact]
    public void Invalid_utf8_stops_on_that_byte_and_keeps_the_prefix()
    {
        var source = new MemoryByteSource();
        source.Append("{\"ok\":1,\"bad\":\""u8);
        var offset = source.Length;
        source.Append([0xFF]);
        source.Complete();
        var session = ViewerSession.Open(source);
        session.PumpAll();

        Assert.Equal(ScanProgress.Stopped, session.Progress);
        Assert.Equal("1", session.Field("ok").Text);
        Assert.DoesNotContain(session.ReadRecord(DocumentPath.Root).Fields, field => field.Name == "bad");
        Assert.Equal("The document is not valid UTF-8.", session.Error!.Message);
        Assert.Equal(offset, session.Error.ByteOffset);
    }

    [Fact]
    public void Trailing_junk_keeps_the_object_and_a_second_value_does_not_open()
    {
        var junk = "{} oops";
        var kept = Open(junk);
        Assert.Equal(ScanProgress.Stopped, kept.Progress);
        Assert.Empty(kept.ReadRecord(DocumentPath.Root).Fields);
        Assert.Equal(junk.IndexOf('o'), kept.Error!.ByteOffset);

        foreach (var json in new[] { "{}{}", "{} {}", "{} true" })
        {
            var session = Open(json);
            Assert.Equal(ScanProgress.Stopped, session.Progress);
            Assert.Equal("A JSON document is one value.", session.Error!.Message);
            Assert.Throws<InvalidOperationException>(() => session.ReadRecord(DocumentPath.Root));
        }
    }

    [Fact]
    public void Field_names_are_written_raw_in_the_path()
    {
        var blank = Open("{\"\":1}");
        Assert.Equal("", Assert.Single(blank.ReadRecord(DocumentPath.Root).Fields).Name);
        Assert.Equal("1", blank.Field("").Text);
        Assert.Equal("/", blank.FormatPath(DocumentPath.Root.Field("")));

        var slash = Open("{\"a/b\":1}");
        var slashPath = DocumentPath.Root.Field("a/b");
        Assert.Equal("a/b", Assert.Single(slashPath.Steps).Name);
        Assert.Null(slashPath.Steps[0].OneBasedIndex);
        Assert.Equal("/a/b", slash.FormatPath(slashPath));
        slash.Search(new TextSearch { FindWhat = "a/b", Value = false, AttributeName = false });
        slash.PumpAll();
        slash.Reveal(Assert.Single(slash.Hits));
        Assert.Equal(slashPath, slash.Marked);

        var brackets = Open("{\"a[1]\":1}");
        var bracketPath = DocumentPath.Root.Field("a[1]");
        Assert.Equal(FieldKind.Scalar, brackets.Field("a[1]").Kind);
        Assert.Null(bracketPath.Steps[0].OneBasedIndex);
        Assert.Equal("/a[1]", brackets.FormatPath(bracketPath));
    }

    [Fact]
    public void Value_search_uses_the_literal_and_skips_collapsed_labels()
    {
        var session = Open("""{"nil":null,"str":"null","empty":"","box":{}}""");
        Search(session, new TextSearch { FindWhat = "null", EntireCell = true, ElementName = false, AttributeName = false });
        Assert.Equal(["/nil"], Paths(session));

        Search(session, new TextSearch { FindWhat = "\"null\"", EntireCell = true, ElementName = false, AttributeName = false });
        Assert.Equal(["/str"], Paths(session));

        Search(session, new TextSearch { FindWhat = "null", ElementName = false, AttributeName = false });
        Assert.Equal(["/nil", "/str"], Paths(session));

        Search(session, new TextSearch { FindWhat = "\"\"", EntireCell = true, ElementName = false, AttributeName = false });
        Assert.Equal(["/empty"], Paths(session));

        Search(session, new TextSearch { FindWhat = "{", ElementName = false, AttributeName = false });
        Assert.Empty(session.Hits);

        var list = Open("""{"nums":[{},{}]}""");
        Search(list, new TextSearch { FindWhat = "nums[2]" });
        Assert.Empty(list.Hits);
    }

    [Fact]
    public void Field_names_use_the_element_switch_and_attributes_match_nothing()
    {
        var session = Open("""{"name":1,"@id":1}""");
        Search(session, new TextSearch { FindWhat = "name", Value = false, AttributeName = false });
        Assert.Equal(["/name"], Paths(session));

        Search(session, new TextSearch { FindWhat = "@id", ElementName = false, Value = false });
        Assert.Empty(session.Hits);

        Search(session, new TextSearch { FindWhat = "@id", AttributeName = false, Value = false });
        Assert.Equal(["/@id"], Paths(session));

        Search(session, new TextSearch { FindWhat = "1", ElementName = false, AttributeName = false, FieldName = "" });
        Assert.Equal(2, session.Hits.Count);

        Search(session, new TextSearch { FindWhat = "" });
        Assert.Empty(session.Hits);

        var flags = Open("""{"a":true}""");
        Search(flags, new TextSearch { FindWhat = "TRUE", EntireCell = true, ElementName = false, AttributeName = false });
        Assert.Empty(flags.Hits);
        Search(flags, new TextSearch { FindWhat = "TRUE", EntireCell = true, ElementName = false, AttributeName = false, MatchCase = false });
        Assert.Equal(["/a"], Paths(flags));
    }

    [Fact]
    public void Two_matches_in_one_string_count_into_the_literal()
    {
        var session = Open("""{"s":"ab ab"}""");
        Search(session, new TextSearch { FindWhat = "ab", ElementName = false, AttributeName = false });

        Assert.Equal(2, session.Hits.Count);
        Assert.Equal(1, session.Hits[0].CharacterOffset);
        Assert.Equal(4, session.Hits[1].CharacterOffset);
        Assert.All(session.Hits, hit => Assert.Equal(2, hit.MatchLength));
        Assert.Equal("/s", session.FormatPath(session.Hits[0].Path));
    }

    [Fact]
    public void Revealing_a_hit_expands_the_embedded_tables_and_scrolls_the_cell()
    {
        var session = Open("""{"wrap":{"nums":[{"id":"a1"}]}}""");
        Search(session, new TextSearch { FindWhat = "\"a1\"", EntireCell = true, ElementName = false, AttributeName = false });
        var hit = Assert.Single(session.Hits);
        Assert.Equal("/wrap/nums[1]/id", session.FormatPath(hit.Path));

        session.SetViewport(0, 0, 800, ViewerSession.RowHeight * 4);
        session.Reveal(hit);

        Assert.True(session.Field("wrap").IsExpanded);
        var nums = DocumentPath.Root.Field("wrap").Field("nums");
        Assert.True(session.ReadRecord(DocumentPath.Root.Field("wrap")).Fields.Single(field => field.Name == "nums").IsExpanded);
        Assert.Equal(hit.Path, session.Marked);
        Assert.Contains(session.ReadSurface().Rows, row => row.IsMarked);

        var items = string.Join(",", Enumerable.Range(0, 40).Select(i => i == 30 ? "{\"id\":\"NEEDLE\"}" : "{\"id\":\"x\"}"));
        var deep = Open("{\"nums\":[" + items + "]}");
        deep.SetViewport(0, 0, 800, ViewerSession.RowHeight * 5);
        Search(deep, new TextSearch { FindWhat = "NEEDLE", ElementName = false, AttributeName = false });
        deep.Reveal(Assert.Single(deep.Hits));
        Assert.True(deep.ViewportY > ViewerSession.RowHeight * 10);
        Assert.Contains(deep.ReadSurface().Rows, row => row.IsMarked);
    }

    [Fact]
    public void A_field_name_search_hits_each_entry_that_has_the_field()
    {
        var session = Open("""{"nums":[{"id":"a1"},{"id":"a2"},{"x":"a1"}]}""");
        Search(session, new TextSearch { FindWhat = "id", Value = false, AttributeName = false });
        Assert.Equal(["/nums[1]/id", "/nums[2]/id"], Paths(session));

        Search(session, new TextSearch
        {
            FindWhat = "\"a1\"",
            EntireCell = true,
            ElementName = false,
            AttributeName = false,
            FieldName = "id",
        });
        Assert.Equal(["/nums[1]/id"], Paths(session));

        Search(session, new TextSearch { FindWhat = "nums[3]", ElementName = false, AttributeName = false });
        Assert.Empty(session.Hits);
    }

    [Fact]
    public void List_search_parents_skip_the_entry_object_and_ignore_attributes()
    {
        var session = Open("""{"wrap":{"nums":[{"id":"a1"}]},"id":"a1"}""");
        var value = new TextSearch
        {
            FindWhat = "\"a1\"",
            EntireCell = true,
            ElementName = false,
            AttributeName = false,
            ParentElementName = "nums",
            GrandfatherElementName = "wrap",
        };
        Search(session, value);
        Assert.Equal(["/wrap/nums[1]/id"], Paths(session));

        Search(session, value with { GrandfatherElementName = "" });
        Assert.Equal(["/wrap/nums[1]/id"], Paths(session));

        Search(session, value with { ParentElementName = "wrap", GrandfatherElementName = "" });
        Assert.Empty(session.Hits);

        Search(session, value with { ParentElementName = "nums", GrandfatherElementName = "", ParentAttributeName = "@id" });
        Assert.Empty(session.Hits);

        var root = Open("""{"id":"a1"}""");
        Search(root, new TextSearch
        {
            FindWhat = "\"a1\"",
            EntireCell = true,
            ElementName = false,
            AttributeName = false,
            ParentElementName = "id",
        });
        Assert.Empty(root.Hits);
    }

    [Fact]
    public void A_value_field_limit_stays_on_that_field()
    {
        var session = Open("[1,{\"a\":1}]");
        Search(session, new TextSearch
        {
            FindWhat = "1",
            EntireCell = true,
            ElementName = false,
            AttributeName = false,
            FieldName = "#value",
        });
        Assert.Equal(["/#value[1]/#value"], Paths(session));
    }

    private static ViewerSession Open(string json)
    {
        var source = new MemoryByteSource();
        source.Append(Encoding.UTF8.GetBytes(json));
        source.Complete();
        var session = ViewerSession.Open(source);
        session.PumpAll();
        return session;
    }

    private static void Search(ViewerSession session, TextSearch search)
    {
        session.Search(search);
        session.PumpAll();
    }

    private static string[] Paths(ViewerSession session) =>
        session.Hits.Select(hit => session.FormatPath(hit.Path)).ToArray();

    private static FieldView Cell(ListView view, int row, string name)
    {
        var index = view.Columns.ToList().IndexOf(name);
        Assert.True(index >= 0, name);
        return view.Entries[row].Cells[index];
    }

    private static void AppendCopies(MemoryByteSource source, ReadOnlySpan<byte> item, int count)
    {
        var batchItems = 128;
        var batch = new byte[item.Length * batchItems];
        for (var i = 0; i < batchItems; i++)
            item.CopyTo(batch.AsSpan(i * item.Length));
        var full = count / batchItems;
        var rest = count % batchItems;
        for (var i = 0; i < full; i++)
            source.Append(batch);
        if (rest > 0)
            source.Append(batch.AsSpan(0, rest * item.Length));
    }
}
