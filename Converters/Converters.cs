using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OpenTaskManager.Converters;

public class BoolToWidthConverter : IValueConverter
{
    public static readonly BoolToWidthConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
        {
            return isExpanded ? 220.0 : 48.0;
        }
        return 220.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PercentToWidthConverter : IMultiValueConverter
{
    public static readonly PercentToWidthConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && values[0] != null && values[1] != null)
        {
            double percent = 0;
            double totalWidth = 0;

            // Try to parse percent value
            if (values[0] is double d)
                percent = d;
            else if (values[0] is int i)
                percent = i;
            else if (double.TryParse(values[0]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
                percent = p;

            // Try to parse total width
            if (values[1] is double w)
                totalWidth = w;
            else if (double.TryParse(values[1]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var tw))
                totalWidth = tw;

            // Clamp percent to 0-100
            percent = Math.Max(0, Math.Min(100, percent));
            
            return totalWidth * percent / 100.0;
        }
        return 0.0;
    }
}

public class IntEqualsConverter : IValueConverter
{
    public static readonly IntEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter is string paramStr && int.TryParse(paramStr, out int paramInt))
        {
            return intValue == paramInt;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToStringConverter : IValueConverter
{
    public static readonly BoolToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? "Enabled" : "Disabled";
        return "Disabled";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class LongGreaterThanZeroConverter : IValueConverter
{
    public static readonly LongGreaterThanZeroConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return false;
        if (value is long l) return l > 0;
        if (long.TryParse(value.ToString(), out var lv)) return lv > 0;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StringNotNullOrEmptyConverter : IValueConverter
{
    public static readonly StringNotNullOrEmptyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool result = value is string s && !string.IsNullOrWhiteSpace(s);
        if (parameter is string p && p.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            result = !result;
        return result;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
