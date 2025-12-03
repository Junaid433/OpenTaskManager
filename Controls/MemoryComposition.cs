using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace OpenTaskManager.Controls;

public class MemoryComposition : Control
{
    public static readonly StyledProperty<long> TotalBytesProperty =
        AvaloniaProperty.Register<MemoryComposition, long>(nameof(TotalBytes));

    public static readonly StyledProperty<long> UsedBytesProperty =
        AvaloniaProperty.Register<MemoryComposition, long>(nameof(UsedBytes));

    public static readonly StyledProperty<long> CompressedBytesProperty =
        AvaloniaProperty.Register<MemoryComposition, long>(nameof(CompressedBytes));

    public static readonly StyledProperty<IBrush> FillBrushProperty =
        AvaloniaProperty.Register<MemoryComposition, IBrush>(nameof(FillBrush), new SolidColorBrush(Color.Parse("#60CDFF")));

    public static readonly StyledProperty<IBrush> ReservedBrushProperty =
        AvaloniaProperty.Register<MemoryComposition, IBrush>(nameof(ReservedBrush), new SolidColorBrush(Color.Parse("#4A90E2")));

    public long TotalBytes
    {
        get => GetValue(TotalBytesProperty);
        set => SetValue(TotalBytesProperty, value);
    }

    public long UsedBytes
    {
        get => GetValue(UsedBytesProperty);
        set => SetValue(UsedBytesProperty, value);
    }

    public long CompressedBytes
    {
        get => GetValue(CompressedBytesProperty);
        set => SetValue(CompressedBytesProperty, value);
    }

    public IBrush FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public IBrush ReservedBrush
    {
        get => GetValue(ReservedBrushProperty);
        set => SetValue(ReservedBrushProperty, value);
    }

    static MemoryComposition()
    {
        AffectsRender<MemoryComposition>(TotalBytesProperty, UsedBytesProperty, CompressedBytesProperty, FillBrushProperty, ReservedBrushProperty);
    }

    public MemoryComposition()
    {
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Dark theme background
        var bg = new SolidColorBrush(Color.Parse("#0B2233"));
        context.DrawRectangle(bg, null, new Rect(0, 0, bounds.Width, bounds.Height));

        if (TotalBytes <= 0) return;

        double usedPct = Math.Max(0, Math.Min(1.0, (double)UsedBytes / TotalBytes));
        double compressedPct = TotalBytes > 0 ? Math.Max(0, Math.Min(usedPct, (double)CompressedBytes / TotalBytes)) : 0;

        // In use area - use FillBrush if provided otherwise fallback to blue
        var usedBrush = FillBrush as SolidColorBrush ?? new SolidColorBrush(Color.Parse("#60CDFF"));
        var usedRect = new Rect(0, 0, bounds.Width * usedPct, bounds.Height);
        context.DrawRectangle(usedBrush, null, usedRect);

        // "In use" label bubble
        if (usedRect.Width > 50)
        {
            var bubbleBrush = new SolidColorBrush(Color.Parse("#1A6F9D"));
            var bubbleWidth = 44;
            var bubbleHeight = 18;
            var bubbleX = Math.Min(usedRect.Width - bubbleWidth - 8, usedRect.Width * 0.5);
            if (bubbleX < 4) bubbleX = 4;
            var bubbleY = (bounds.Height - bubbleHeight) / 2;
            var bubbleRect = new Rect(bubbleX, bubbleY, bubbleWidth, bubbleHeight);
            context.DrawRectangle(bubbleBrush, null, bubbleRect);
        }
    }
}
