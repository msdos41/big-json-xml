using IOPath = System.IO.Path;

namespace BigJsonXml.Desktop.Tests;

public class DocumentTabTests
{
    [Fact]
    public void Open_keeps_each_document_and_the_same_path_activates_the_tab()
    {
        var first = Write("<root><item>alpha</item><item>beta</item></root>", "a.xml");
        var secondDir = IOPath.Combine(IOPath.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(secondDir);
        var second = IOPath.Combine(secondDir, "a.xml");
        File.WriteAllText(second, "<root><item>gamma</item></root>");
        using var model = new ViewerModel();
        model.OpenPath(first.Path);

        Assert.Single(model.Tabs);
        Assert.Equal("a.xml", model.Tabs[0].Label);
        Assert.Equal(IOPath.GetFullPath(first.Path), model.Tabs[0].Path);
        Assert.True(model.Tabs[0].IsActive);
        Assert.Contains(model.Rows, row => row.Name == "item" && row.Value == "item[2]");
        model.SetViewportSize(800, 72);
        var list = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "item");
        model.Toggle(list);
        Assert.Contains(model.Rows, row => row.Kind == SurfaceKind.Entry && row.Value == "alpha");
        model.ScrollY = 48;
        var scrolled = model.ScrollY;
        Assert.True(scrolled > 0);

        model.OpenPath(second);
        Assert.Equal(2, model.Tabs.Count);
        Assert.Equal(1, model.ActiveIndex);
        Assert.Equal("a.xml", model.Tabs[1].Label);
        Assert.Equal(model.Tabs[0].Label, model.Tabs[1].Label);
        Assert.Contains(model.Rows, row => row.Value == "gamma");
        Assert.DoesNotContain(model.Rows, row => row.Value == "alpha");

        var dotted = IOPath.Combine(IOPath.GetDirectoryName(first.Path)!, ".", "a.xml");
        model.OpenPath(dotted);
        Assert.Equal(2, model.Tabs.Count);
        Assert.Equal(0, model.ActiveIndex);
        Assert.Contains(model.Rows, row => row.Kind == SurfaceKind.Field && row.Name == "item" && row.IsExpanded);
        Assert.Equal(scrolled, model.ScrollY);

        model.OpenPath(first.Path.ToUpperInvariant());
        Assert.Equal(2, model.Tabs.Count);
        Assert.Equal(0, model.ActiveIndex);
        Assert.Contains(model.Rows, row => row.Kind == SurfaceKind.Field && row.Name == "item" && row.IsExpanded);

        var loose = new MemoryByteSource();
        loose.Append("<root><n>1</n></root>");
        loose.Complete();
        var looseAgain = new MemoryByteSource();
        looseAgain.Append("<root><n>2</n></root>");
        looseAgain.Complete();
        model.Open(loose);
        model.Open(looseAgain);
        Assert.Equal(4, model.Tabs.Count);
        Assert.Equal("", model.Tabs[2].Path);
        Assert.Equal("", model.Tabs[3].Path);
        Assert.Contains(model.Rows, row => row.Value == "2");
    }

    [Fact]
    public void Switching_back_keeps_the_place_widths_selection_and_full_value()
    {
        var body = new string('m', 80);
        var rows = string.Concat(Enumerable.Range(0, 30).Select(i => $"<row><name>n{i}</name></row>"));
        var path = Write($"<root><note>{body}</note>{rows}</root>").Path;
        using var model = new ViewerModel();
        model.OpenPath(path);
        model.SetViewportSize(420, ViewerSession.RowHeight * 8);
        var note = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "note");
        model.Toggle(note);
        Assert.Contains(model.Rows, row => row.Kind == SurfaceKind.ValueLine && row.Path.Equals(note.Path));
        model.SetSelection(new CellSelection(note.Path, 2, 6, true, null, SurfaceKind.Field));
        Assert.Equal("mmmm", model.SelectedText());

        var list = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "row");
        model.Toggle(list);
        var header = model.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.Name == "name");
        var widened = header.Width + 36;
        model.DragWidth(header, widened);
        var dragged = model.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.Name == "name").Width;
        model.ScrollY = model.ScrollMaxY;
        var y = model.ScrollY;
        var x = 24d;
        model.ScrollX = x;
        Assert.True(y > 40);
        Assert.Equal(x, model.ScrollX);

        var other = new MemoryByteSource();
        other.Append("<root><a>z</a></root>");
        other.Complete();
        model.Open(other);
        model.SetViewportSize(420, ViewerSession.RowHeight * 8 + 80);
        Assert.Contains(model.Rows, row => row.Value == "z");
        Assert.Equal(0, model.ScrollY);

        model.Activate(model.Tabs[0]);
        Assert.Equal("mmmm", model.SelectedText());
        Assert.True(model.ScrollY > 0);
        Assert.True(model.ScrollY <= model.ScrollMaxY + 0.5);
        Assert.NotEqual(0, model.ScrollY);
        Assert.Equal(x, model.ScrollX);
        model.ScrollY = 0;
        Assert.Contains(model.Rows, row => row.Kind == SurfaceKind.ValueLine && row.Path.Equals(note.Path));
        Assert.Contains(model.Rows, row => row.Kind == SurfaceKind.Field && row.Name == "row" && row.IsExpanded);
        Assert.Equal(dragged, model.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.Name == "name").Width);
        model.SelectAll();
        Assert.Equal(body, model.SelectedText());
    }

    [Fact]
    public void Close_keeps_order_picks_a_neighbor_and_the_last_tab_leaves_no_document()
    {
        using var model = new ViewerModel();
        var texts = new[] { "one", "two", "three" };
        foreach (var text in texts)
        {
            var source = new MemoryByteSource();
            source.Append($"<root><a>{text}</a></root>");
            source.Complete();
            model.Open(source);
        }

        model.Activate(model.Tabs[0]);
        model.Close(model.Tabs[1]);
        Assert.Equal(2, model.Tabs.Count);
        Assert.Equal(0, model.ActiveIndex);
        Assert.Contains(model.Rows, row => row.Value == "one");

        model.Close(model.Tabs[0]);
        Assert.Equal(0, model.ActiveIndex);
        Assert.Contains(model.Rows, row => row.Value == "three");

        model.Close(model.ActiveTab!);
        Assert.Empty(model.Tabs);
        Assert.Equal(-1, model.ActiveIndex);
        Assert.Equal("No document", model.Status);
        Assert.Equal("", model.Error);
        Assert.False(model.ScanFractionShown);
        Assert.Equal("", model.RemainingTime);
        Assert.Empty(model.Rows);
        Assert.Empty(model.Results);

        var again = new MemoryByteSource();
        again.Append("<root><a>back</a></root>");
        again.Complete();
        model.Open(again);
        Assert.Contains(model.Rows, row => row.Value == "back");
        Assert.Equal("Complete", model.Status);
    }

    [Fact]
    public void A_failed_open_adds_no_tab_and_the_message_follows_the_active_tab()
    {
        var good = Write("<root><a>1</a></root>").Path;
        var missing = IOPath.Combine(IOPath.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");
        using var model = new ViewerModel();
        model.OpenPath(missing);
        Assert.Empty(model.Tabs);
        Assert.Equal("No document", model.Status);
        Assert.NotEqual("", model.Error);
        var alone = model.Error;
        model.Tick();
        Assert.Equal(alone, model.Error);

        model.OpenPath(good);
        Assert.Single(model.Tabs);
        Assert.Equal("", model.Error);
        Assert.Contains(model.Rows, row => row.Value == "1");

        model.OpenPath(missing);
        Assert.Single(model.Tabs);
        Assert.NotEqual("", model.Error);
        var failure = model.Error;
        model.Tick();
        Assert.Equal(failure, model.Error);
        model.Activate(model.Tabs[0]);
        Assert.Equal(failure, model.Error);

        var other = Write("<root><a>2</a></root>").Path;
        model.OpenPath(other);
        Assert.Equal(2, model.Tabs.Count);
        Assert.Equal("", model.Error);
        model.Activate(model.Tabs[0]);
        Assert.Equal("", model.Error);
        Assert.Contains(model.Rows, row => row.Value == "1");
    }

    [Fact]
    public void A_stopped_scan_still_opens_a_tab_and_keeps_the_outline()
    {
        var path = Write("<root><child>1</child><<").Path;
        using var model = new ViewerModel();
        model.OpenPath(path);
        Assert.Single(model.Tabs);
        Assert.Equal(0, model.ActiveIndex);
        Assert.Equal("Stopped", model.Status);
        Assert.Equal(ScanProgress.Stopped, model.Tabs[0].Progress);
        Assert.Contains(model.Rows, row => row.Name == "child" && row.Value == "1");
        Assert.Contains("line", model.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_hidden_scan_and_search_keep_moving_while_the_toolbar_follows_the_active_tab()
    {
        var slow = new MemoryByteSource();
        slow.Append("<root><item>findme</item><item>later</item>");
        using var model = new ViewerModel();
        model.Open(slow);
        Assert.Equal(ScanProgress.Scanning, model.Tabs[0].Progress);
        model.FindWhat = "findme";
        model.ParentElementName = "item";
        model.MatchCase = false;
        model.FindAll();
        Assert.Equal(1, model.ResultCount);
        Assert.Equal("/root/item[1]/#text", model.Results[0].Path);
        var first = model.Results[0];

        var quick = new MemoryByteSource();
        quick.Append("<root><b>2</b></root>");
        quick.Complete();
        model.Open(quick);
        Assert.Equal(1, model.ActiveIndex);
        Assert.Equal("Complete", model.Status);
        Assert.False(model.ScanFractionShown);
        Assert.Equal("", model.RemainingTime);
        Assert.Equal("findme", model.FindWhat);
        Assert.Equal("item", model.ParentElementName);
        Assert.False(model.MatchCase);
        Assert.Equal(ScanProgress.Scanning, model.Tabs[0].Progress);
        Assert.Empty(model.Results);

        slow.Append("<item>later</item><item>findme</item></root>");
        slow.Complete();
        for (var i = 0; i < 40 && model.Tabs[0].Progress == ScanProgress.Scanning; i++)
            model.Tick();

        Assert.Equal(ScanProgress.Completed, model.Tabs[0].Progress);
        Assert.Equal(1, model.ActiveIndex);
        Assert.Equal("Complete", model.Status);
        Assert.Contains(model.Rows, row => row.Value == "2");

        model.Activate(model.Tabs[0]);
        Assert.Equal(2, model.ResultCount);
        Assert.Same(first, model.Results[0]);
        Assert.Equal("findme", model.Results[1].Text);
        Assert.Equal("Complete", model.Status);
        Assert.False(model.ScanFractionShown);
    }

    [Fact]
    public void The_tab_row_moves_one_tab_at_a_time_and_keeps_the_active_document()
    {
        using var model = new ViewerModel();
        model.SetTabStripWidth(180);
        var width = DocumentTab.EstimateWidth("");
        for (var i = 0; i < 4; i++)
        {
            var source = new MemoryByteSource();
            source.Append($"<root><a>{i}</a></root>");
            source.Complete();
            model.Open(source);
            Assert.Equal(width, model.Tabs[i].Width);
        }

        Assert.True(model.TabArrowsVisible);
        Assert.True(model.Tabs[3].FullyVisible);
        Assert.False(model.Tabs[0].FullyVisible);
        Assert.Equal(3, model.ActiveIndex);
        Assert.True(model.CanMoveTabsLeft);
        Assert.False(model.CanMoveTabsRight);
        var parked = model.TabStripOffset;
        model.MoveTabsRight();
        Assert.Equal(parked, model.TabStripOffset);
        model.MoveTabsLeft();
        Assert.True(model.TabStripOffset < parked);
        Assert.Equal(3, model.ActiveIndex);
        Assert.Contains(model.Rows, row => row.Value == "3");
        var shown = model.Tabs.Single(tab => tab.FullyVisible && tab.Left <= model.TabStripOffset + 0.5);
        Assert.NotEqual(0, shown.Left);

        model.Activate(model.Tabs[0]);
        Assert.Equal(0, model.ActiveIndex);
        Assert.True(model.Tabs[0].FullyVisible);
        Assert.Contains(model.Rows, row => row.Value == "0");

        model.RevealActiveTab();
        model.Activate(model.Tabs[3]);
        Assert.True(model.Tabs[3].FullyVisible);
        model.Close(model.Tabs[3]);
        Assert.Equal(2, model.ActiveIndex);
        Assert.True(model.ActiveTab!.FullyVisible);
        Assert.Contains(model.Rows, row => row.Value == "2");

        var offset = model.TabStripOffset;
        model.Close(model.Tabs[0]);
        Assert.Equal(1, model.ActiveIndex);
        Assert.Contains(model.Rows, row => row.Value == "2");
        Assert.False(model.TabArrowsVisible);
        Assert.Equal(0, model.TabStripOffset);
        Assert.True(offset >= 0);
        Assert.All(model.Tabs, tab => Assert.True(tab.FullyVisible));
    }

    [Fact]
    public void Closing_a_tab_pulls_the_row_back_only_past_the_last_tab()
    {
        using var model = new ViewerModel();
        model.SetTabStripWidth(220);
        for (var i = 0; i < 4; i++)
        {
            var source = new MemoryByteSource();
            source.Append($"<root><a>{i}</a></root>");
            source.Complete();
            model.Open(source);
            model.SetTabWidth(model.Tabs[i], 100);
        }

        model.RevealActiveTab();
        Assert.True(model.Tabs[3].FullyVisible);
        Assert.False(model.Tabs[0].FullyVisible);
        model.MoveTabsLeft();
        var before = model.TabStripOffset;
        Assert.True(before > 0);
        model.Close(model.Tabs[0]);
        Assert.Equal(2, model.ActiveIndex);
        Assert.Contains(model.Rows, row => row.Value == "3");
        var view = 220 - DocumentTab.ArrowSpan;
        var last = model.Tabs[^1];
        Assert.Equal(last.Left + last.Width, model.TabStripOffset + view, 0);
        Assert.True(model.TabArrowsVisible);
    }

    [Fact]
    public void Search_reads_only_the_active_tab_and_hits_stay_there()
    {
        var alpha = Write("<root><item>alpha</item><item>beta</item></root>").Path;
        var gamma = Write("<root><item>gamma</item></root>").Path;
        using var model = new ViewerModel();
        model.ShowFind();
        model.FindWhat = "alpha";
        model.FindAll();
        Assert.Empty(model.FindHistory);
        Assert.False(model.ResultsWindowOpen);
        Assert.True(model.FindWindowOpen);

        model.OpenPath(alpha);
        model.FindWhat = "alpha";
        model.StartsWith = true;
        model.FindAll();
        model.FindNext();
        var current = model.CurrentResult;
        Assert.NotNull(current);
        Assert.Contains(model.Rows, row => row.IsMarked);
        model.SetFoundColumnWidth(180);
        Assert.Equal(["alpha"], model.FindHistory);

        model.OpenPath(gamma);
        Assert.Equal("alpha", model.FindWhat);
        Assert.True(model.StartsWith);
        Assert.Equal(180, model.FoundColumnWidth);
        Assert.True(model.FindWindowOpen);
        Assert.True(model.ResultsWindowOpen);
        Assert.Empty(model.ResultDocuments);
        Assert.Empty(model.VisibleResults);
        model.FindNext();
        Assert.Empty(model.Results);
        Assert.DoesNotContain(model.Rows, row => row.IsMarked);

        model.FindAll();
        Assert.Empty(model.Results);
        var empty = Assert.Single(model.ResultDocuments);
        Assert.Equal("0 matches", empty.MatchLabel);
        Assert.IsType<ResultDocumentLine>(Assert.Single(model.VisibleResults));
        Assert.Equal(["alpha"], model.FindHistory);

        model.FindWhat = "gamma";
        model.FindAll();
        Assert.Equal(["gamma", "alpha"], model.FindHistory);
        Assert.Equal("gamma", Assert.Single(model.Results).Text);
        model.FindNext();
        Assert.Contains(model.Rows, row => row.IsMarked && row.Value == "gamma");

        model.Activate(model.Tabs[0]);
        Assert.Equal(current, model.CurrentResult);
        Assert.Contains(model.VisibleResults, line => line is ResultHitLine hit && ReferenceEquals(hit.Result, current) && hit.IsCurrent);
        Assert.Contains(model.Rows, row => row.IsMarked);
        Assert.Equal("alpha", model.Results[0].Text);
        Assert.Equal(180, model.FoundColumnWidth);

        model.FindWhat = "beta";
        model.FindAll();
        model.ChooseResult(Assert.Single(model.Results));
        Assert.Contains(model.Rows, row => row.IsMarked);
        model.Activate(model.Tabs[1]);
        Assert.Equal("gamma", Assert.Single(model.Results).Text);
        Assert.Contains(model.Rows, row => row.IsMarked);

        model.CloseFind();
        Assert.False(model.ResultsWindowOpen);
        Assert.DoesNotContain(model.Rows, row => row.IsMarked);
        model.Activate(model.Tabs[0]);
        Assert.Contains(model.Rows, row => row.IsMarked);
        model.ShowFind();
        Assert.False(model.ResultsWindowOpen);
        Assert.Contains(model.Rows, row => row.IsMarked);

        model.Close(model.Tabs[1]);
        model.Close(model.Tabs[0]);
        Assert.Equal(["beta", "gamma", "alpha"], model.FindHistory);
        Assert.True(model.FindWindowOpen);
        model.FindAll();
        Assert.Equal(["beta", "gamma", "alpha"], model.FindHistory);
    }

    [Fact]
    public void A_hidden_stop_turns_the_circle_red_without_changing_the_active_status()
    {
        var slow = new MemoryByteSource();
        slow.Append("<root><a>1</a>");
        using var model = new ViewerModel();
        model.Open(slow);
        var quick = new MemoryByteSource();
        quick.Append("<root><b>2</b></root>");
        quick.Complete();
        model.Open(quick);
        slow.Append("<<");
        slow.Complete();
        for (var i = 0; i < 40 && model.Tabs[0].Progress == ScanProgress.Scanning; i++)
            model.Tick();

        Assert.Equal(ScanProgress.Stopped, model.Tabs[0].Progress);
        Assert.Equal("Complete", model.Status);
        Assert.Equal("", model.Error);
        Assert.Equal(1, model.ActiveIndex);
    }

    private static Written Write(string xml, string? name = null)
    {
        var directory = IOPath.Combine(IOPath.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = IOPath.Combine(directory, name ?? "doc.xml");
        File.WriteAllText(path, xml);
        return new Written(path);
    }

    private sealed record Written(string Path);
}
