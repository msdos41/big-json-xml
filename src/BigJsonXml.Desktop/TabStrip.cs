using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace BigJsonXml.Desktop;

public sealed class TabStrip : Grid
{
    public const double RowHeight = 32;

    private readonly Border _clip;
    private readonly Canvas _row;
    private readonly StackPanel _arrows;
    private readonly Button _left;
    private readonly Button _right;
    private readonly List<Chip> _chips = [];
    private ViewerModel? _model;
    private bool _reveal;

    public TabStrip()
    {
        Height = RowHeight;
        MinHeight = RowHeight;
        ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _row = new Canvas { Height = RowHeight, Name = "TabRow" };
        _clip = new Border
        {
            Name = "TabClip",
            ClipToBounds = true,
            Height = RowHeight,
            Child = _row,
        };
        _left = Arrow("TabLeft", "\u25C0");
        _right = Arrow("TabRight", "\u25B6");
        _arrows = new StackPanel
        {
            Name = "TabArrows",
            Orientation = Orientation.Horizontal,
            IsVisible = false,
        };
        _arrows.Children.Add(_left);
        _arrows.Children.Add(_right);
        Children.Add(_clip);
        Children.Add(_arrows);
        SetColumn(_arrows, 1);
        _left.Click += (_, _) => _model?.MoveTabsLeft();
        _right.Click += (_, _) => _model?.MoveTabsRight();
        PointerWheelChanged += (_, e) => e.Handled = true;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
            _model.Tabs.CollectionChanged -= OnTabsChanged;
        }

        _model = DataContext as ViewerModel;
        if (_model is null)
            return;
        _model.PropertyChanged += OnModelChanged;
        _model.Tabs.CollectionChanged += OnTabsChanged;
        Rebuild(false);
    }

    protected override Size MeasureOverride(Size available)
    {
        if (_model is not null)
        {
            foreach (var chip in _chips)
            {
                var held = chip.Root.Width;
                chip.Root.Width = double.NaN;
                chip.Root.Measure(new Size(double.PositiveInfinity, RowHeight));
                var desired = chip.Root.DesiredSize.Width;
                chip.Root.Width = held;
                if (desired > 1)
                    _model.SetTabWidth(chip.Tab, desired);
            }
        }

        return base.MeasureOverride(available);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_model is not null)
        {
            _model.SetTabStripWidth(finalSize.Width);
            if (_reveal)
            {
                _reveal = false;
                _model.RevealActiveTab();
            }

            SyncArrows();
        }

        var arranged = base.ArrangeOverride(finalSize);
        if (_model is not null)
        {
            var offset = _model.TabStripOffset;
            foreach (var chip in _chips)
            {
                var width = Math.Max(1, chip.Tab.Width);
                chip.Root.Width = width;
                var left = chip.Tab.Left - offset;
                Canvas.SetLeft(chip.Root, left);
                chip.Root.Arrange(new Rect(left, 0, width, RowHeight));
            }
        }

        return arranged;
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ViewerModel.TabStripOffset)
            or nameof(ViewerModel.TabArrowsVisible)
            or nameof(ViewerModel.CanMoveTabsLeft)
            or nameof(ViewerModel.CanMoveTabsRight))
        {
            SyncArrows();
            InvalidateArrange();
        }
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Rebuild(e.Action != NotifyCollectionChangedAction.Remove);

    private void Rebuild(bool reveal)
    {
        _row.Children.Clear();
        _chips.Clear();
        if (_model is null)
            return;
        foreach (var tab in _model.Tabs)
        {
            var chip = Build(tab);
            _chips.Add(chip);
            _row.Children.Add(chip.Root);
        }

        if (reveal)
            _reveal = true;
        InvalidateMeasure();
    }

    private void SyncArrows()
    {
        if (_model is null)
            return;
        _arrows.IsVisible = _model.TabArrowsVisible;
        _left.IsEnabled = _model.CanMoveTabsLeft;
        _right.IsEnabled = _model.CanMoveTabsRight;
    }

    private Chip Build(DocumentTab tab)
    {
        var label = new TextBlock
        {
            Name = "TabLabel",
            Text = tab.Label,
            TextWrapping = TextWrapping.NoWrap,
            Foreground = Brushes.Black,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(label, tab.Path);
        var circle = new Ellipse
        {
            Name = "TabCircle",
            Width = DocumentTab.CircleSize,
            Height = DocumentTab.CircleSize,
            VerticalAlignment = VerticalAlignment.Center,
        };
        circle.Bind(Shape.FillProperty, new Binding(nameof(DocumentTab.CircleBrush)));
        ToolTip.SetTip(circle, tab.Path);
        var close = new Button
        {
            Name = "TabClose",
            Content = CloseGlyph(),
            Width = DocumentTab.CloseSize,
            Height = DocumentTab.CloseSize,
            MinWidth = DocumentTab.CloseSize,
            MinHeight = DocumentTab.CloseSize,
            MaxWidth = DocumentTab.CloseSize,
            MaxHeight = DocumentTab.CloseSize,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        close.Click += (_, e) =>
        {
            if (close.DataContext is DocumentTab current && _model is not null)
                _model.Close(current);
            e.Handled = true;
        };
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = DocumentTab.Gap,
            Margin = new Thickness(DocumentTab.Pad, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        stack.Children.Add(circle);
        stack.Children.Add(label);
        stack.Children.Add(close);
        var border = new Border
        {
            Name = "TabChrome",
            DataContext = tab,
            Height = RowHeight,
            BorderThickness = new Thickness(0, 0, 0, 3),
            Child = stack,
        };
        border.Bind(Border.BackgroundProperty, new Binding(nameof(DocumentTab.Background)));
        border.Bind(Border.BorderBrushProperty, new Binding(nameof(DocumentTab.BottomEdge)));
        border.PointerPressed += (_, e) =>
        {
            if (InsideClose(e.Source))
                return;
            if (border.DataContext is DocumentTab current && _model is not null)
                _model.Activate(current);
            e.Handled = true;
        };
        return new Chip(tab, border);
    }

    private static Panel CloseGlyph()
    {
        var glyph = new Panel
        {
            Name = "TabCloseGlyph",
            Width = 12,
            Height = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        glyph.Children.Add(Bar(1, 1, 11, 11));
        glyph.Children.Add(Bar(11, 1, 1, 11));
        return glyph;
    }

    private static Line Bar(double x1, double y1, double x2, double y2) => new()
    {
        StartPoint = new Point(x1, y1),
        EndPoint = new Point(x2, y2),
        Stroke = Brushes.Black,
        StrokeThickness = 1.5,
        StrokeLineCap = PenLineCap.Flat,
    };

    private static bool InsideClose(object? source)
    {
        for (var node = source as Visual; node is not null; node = node.GetVisualParent())
        {
            if (node is Button { Name: "TabClose" })
                return true;
            if (node is Border { Name: "TabChrome" })
                return false;
        }

        return false;
    }

    private static Button Arrow(string name, string glyph) => new()
    {
        Name = name,
        Content = glyph,
        Width = DocumentTab.ArrowButtonWidth,
        Height = RowHeight,
        MinWidth = DocumentTab.ArrowButtonWidth,
        Padding = new Thickness(0),
        FontSize = 14,
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Foreground = Brushes.Black,
    };

    private sealed record Chip(DocumentTab Tab, Border Root);
}
