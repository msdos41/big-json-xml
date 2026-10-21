using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
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
        _left = Arrow("TabLeft", left: true);
        _right = Arrow("TabRight", left: false);
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

    [DynamicDependency(nameof(DocumentTab.CircleBrush), typeof(DocumentTab))]
    [DynamicDependency(nameof(DocumentTab.Background), typeof(DocumentTab))]
    [DynamicDependency(nameof(DocumentTab.TopEdge), typeof(DocumentTab))]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The three DocumentTab brushes above stay available to these reflection bindings.")]
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
            BorderThickness = new Thickness(0, 3, 0, 0),
            Padding = new Thickness(0, 0, 0, 3),
            Child = stack,
        };
        border.Bind(Border.BackgroundProperty, new Binding(nameof(DocumentTab.Background)));
        border.Bind(Border.BorderBrushProperty, new Binding(nameof(DocumentTab.TopEdge)));
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

    private static readonly IBrush ArrowInk = new SolidColorBrush(Color.Parse("#1F2933"));
    private static readonly IBrush ArrowMuted = new SolidColorBrush(Color.Parse("#A0A7B2"));
    private static readonly IBrush ArrowHover = new SolidColorBrush(Color.Parse("#E7E9ED"));
    private static readonly IBrush ArrowPressed = new SolidColorBrush(Color.Parse("#D9DDE3"));
    private static readonly ControlTheme ArrowButtonTheme = CreateArrowTheme();

    private static ControlTheme CreateArrowTheme()
    {
        var theme = new ControlTheme(typeof(Button));
        theme.Setters.Add(new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent));
        theme.Setters.Add(new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0)));
        theme.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(0)));
        theme.Setters.Add(new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        theme.Setters.Add(new Setter(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        theme.Setters.Add(new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<Button>((button, _) =>
        {
            var presenter = new ContentPresenter
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                [!ContentPresenter.BackgroundProperty] = button[!TemplatedControl.BackgroundProperty],
                [!ContentPresenter.ContentProperty] = button[!ContentControl.ContentProperty],
                [!ContentPresenter.PaddingProperty] = button[!TemplatedControl.PaddingProperty],
            };
            return presenter;
        })));
        return theme;
    }

    private static Button Arrow(string name, bool left)
    {
        var arms = new[] { ChevronArm(left, tip: true), ChevronArm(left, tip: false) };
        var glyph = new Canvas
        {
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        foreach (var arm in arms)
            glyph.Children.Add(arm);
        var button = new Button
        {
            Name = name,
            Theme = ArrowButtonTheme,
            Content = glyph,
            Width = DocumentTab.ArrowButtonWidth,
            Height = RowHeight,
            MinWidth = DocumentTab.ArrowButtonWidth,
            MinHeight = RowHeight,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        void Paint()
        {
            var stroke = button.IsEnabled ? ArrowInk : ArrowMuted;
            foreach (var arm in arms)
                arm.Stroke = stroke;
            if (!button.IsEnabled)
                button.Background = Brushes.Transparent;
        }

        Paint();
        button.PropertyChanged += (_, e) =>
        {
            if (e.Property == InputElement.IsEnabledProperty)
                Paint();
        };
        button.PointerEntered += (_, _) =>
        {
            if (button.IsEnabled)
                button.Background = ArrowHover;
        };
        button.PointerExited += (_, _) => button.Background = Brushes.Transparent;
        button.PointerPressed += (_, e) =>
        {
            if (button.IsEnabled && e.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
                button.Background = ArrowPressed;
        };
        button.PointerReleased += (_, _) =>
            button.Background = button.IsPointerOver && button.IsEnabled ? ArrowHover : Brushes.Transparent;
        return button;
    }

    private static Line ChevronArm(bool left, bool tip)
    {
        var joint = new Point(left ? 6 : 10, 8);
        var end = tip
            ? new Point(left ? 10.5 : 5.5, 3.5)
            : new Point(left ? 10.5 : 5.5, 12.5);
        return new Line
        {
            StartPoint = joint,
            EndPoint = end,
            Stroke = ArrowInk,
            StrokeThickness = 1.5,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };
    }

    private sealed record Chip(DocumentTab Tab, Border Root);
}
