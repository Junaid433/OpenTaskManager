using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System;

namespace OpenTaskManager.Controls;

public class PerformanceGraph : Control
{
    public static readonly StyledProperty<ObservableCollection<double>> ValuesProperty =
        AvaloniaProperty.Register<PerformanceGraph, ObservableCollection<double>>(nameof(Values));

    public static readonly StyledProperty<IBrush> LineBrushProperty =
        AvaloniaProperty.Register<PerformanceGraph, IBrush>(nameof(LineBrush), new SolidColorBrush(Color.Parse("#60CDFF")));

    public static readonly StyledProperty<IBrush> FillBrushProperty =
        AvaloniaProperty.Register<PerformanceGraph, IBrush>(nameof(FillBrush), new SolidColorBrush(Color.Parse("#3360CDFF")));

    public static readonly StyledProperty<double> MaxValueProperty =
        AvaloniaProperty.Register<PerformanceGraph, double>(nameof(MaxValue), 100);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<PerformanceGraph, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PerformanceGraph, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> CurrentValueTextProperty =
        AvaloniaProperty.Register<PerformanceGraph, string>(nameof(CurrentValueText), string.Empty);

    public ObservableCollection<double> Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IBrush LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public IBrush FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public double MaxValue
    {
        get => GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string CurrentValueText
    {
        get => GetValue(CurrentValueTextProperty);
        set => SetValue(CurrentValueTextProperty, value);
    }

    static PerformanceGraph()
    {
        AffectsRender<PerformanceGraph>(ValuesProperty, LineBrushProperty, FillBrushProperty, MaxValueProperty, ShowGridProperty);
    }

    public PerformanceGraph()
    {
        Values = [];
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValuesProperty)
        {
            if (change.OldValue is ObservableCollection<double> oldCollection)
                oldCollection.CollectionChanged -= OnValuesCollectionChanged;

            if (change.NewValue is ObservableCollection<double> newCollection)
                newCollection.CollectionChanged += OnValuesCollectionChanged;
        }
    }

    private void OnValuesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        var width = bounds.Width;
        var height = bounds.Height;

        if (width <= 0 || height <= 0) return;

        // Dark theme background
        var bgBrush = new SolidColorBrush(Color.Parse("#1A1A1A"));
        context.DrawRectangle(bgBrush, null, new Rect(0, 0, width, height));

        // Grid lines (dark theme)
        if (ShowGrid)
        {
            var gridPen = new Pen(new SolidColorBrush(Color.Parse("#2A2A2A")), 1);

            // Horizontal lines
            for (int i = 1; i < 4; i++)
            {
                var y = height * i / 4;
                context.DrawLine(gridPen, new Point(0, y), new Point(width, y));
            }

            // Vertical lines
            for (int i = 1; i < 10; i++)
            {
                var x = width * i / 10;
                context.DrawLine(gridPen, new Point(x, 0), new Point(x, height));
            }
        }

        // Draw the graph
        var values = Values?.ToList() ?? [];
        if (values.Count < 2) return;

        var points = new List<Point>();
        var step = width / (values.Count - 1);

        for (int i = 0; i < values.Count; i++)
        {
            var x = i * step;
            var normalizedValue = Math.Min(values[i] / MaxValue, 1.0); // Clamp to 1.0
            var y = height - (normalizedValue * height * 0.9) - (height * 0.05); // Leave some margin
            points.Add(new Point(x, y));
        }

        // Fill area with light blue
        if (points.Count > 1)
        {
            var fillGeometry = new StreamGeometry();
            using (var ctx = fillGeometry.Open())
            {
                ctx.BeginFigure(new Point(0, height), true);
                foreach (var point in points)
                    ctx.LineTo(point);
                ctx.LineTo(new Point(width, height));
                ctx.EndFigure(true);
            }
            context.DrawGeometry(FillBrush, null, fillGeometry);
        }

        // Line with better styling
        if (points.Count > 1)
        {
            var linePen = new Pen(LineBrush, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var lineGeometry = new StreamGeometry();
            using (var ctx = lineGeometry.Open())
            {
                ctx.BeginFigure(points[0], false);
                for (int i = 1; i < points.Count; i++)
                    ctx.LineTo(points[i]);
                ctx.EndFigure(false);
            }
            context.DrawGeometry(null, linePen, lineGeometry);
        }

        // Border (dark theme)
        var borderPen = new Pen(new SolidColorBrush(Color.Parse("#333333")), 1);
        context.DrawRectangle(null, borderPen, new Rect(0, 0, width, height));
    }
}
