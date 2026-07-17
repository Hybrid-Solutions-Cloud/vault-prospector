namespace VaultProspector.App.Views;

public readonly record struct WindowLayoutMetrics(double Width, double Height, bool UseNarrowLayout)
{
    public static WindowLayoutMetrics Fit(
        double desiredWidth,
        double desiredHeight,
        double physicalWorkingWidth,
        double physicalWorkingHeight,
        double displayScale,
        double textScale,
        double narrowLayoutThreshold)
    {
        var scale = Math.Max(1, displayScale);
        var width = Math.Min(desiredWidth, physicalWorkingWidth / scale);
        var height = Math.Min(desiredHeight, physicalWorkingHeight / scale);
        return new WindowLayoutMetrics(
            width,
            height,
            RequiresNarrow(width, textScale, narrowLayoutThreshold));
    }

    public static bool RequiresNarrow(double logicalWidth, double textScale, double narrowLayoutThreshold) =>
        logicalWidth / Math.Max(1, textScale) < narrowLayoutThreshold;
}
