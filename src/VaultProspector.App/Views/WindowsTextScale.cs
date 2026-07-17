using System.Globalization;
using System.Security;
using Microsoft.Win32;

namespace VaultProspector.App.Views;

public static class WindowsTextScale
{
    public const int DefaultPercent = 100;
    public const int MaximumPercent = 225;

    private const string AccessibilityKey = @"HKEY_CURRENT_USER\Software\Microsoft\Accessibility";
    private const string TextScaleValue = "TextScaleFactor";

    public static double ReadFactor()
    {
        try
        {
            return FactorFrom(Registry.GetValue(AccessibilityKey, TextScaleValue, DefaultPercent));
        }
        catch (Exception exception) when (exception is IOException or SecurityException or UnauthorizedAccessException)
        {
            return 1;
        }
    }

    public static double FactorFrom(object? rawValue)
    {
        var parsed = rawValue switch
        {
            int value => value,
            long value when value is >= int.MinValue and <= int.MaxValue => (int)value,
            string value when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent) => percent,
            _ => DefaultPercent,
        };
        return Math.Clamp(parsed, DefaultPercent, MaximumPercent) / 100d;
    }
}
