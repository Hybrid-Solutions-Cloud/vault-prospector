using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.App;

public sealed class AvaloniaClipboardService : IClipboardService
{
    public async Task CopyWithAutoClearAsync(SensitiveValue value, TimeSpan clearAfter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var window = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
            ?? throw new InvalidOperationException("The desktop window is unavailable.");
        var clipboard = TopLevel.GetTopLevel(window)?.Clipboard ?? throw new InvalidOperationException("The system clipboard is unavailable.");
        var copiedText = value.Reveal();
        await clipboard.SetTextAsync(copiedText);
        _ = ClearLaterAsync(clipboard, copiedText, clearAfter);
    }

    private static async Task ClearLaterAsync(Avalonia.Input.Platform.IClipboard clipboard, string copiedText, TimeSpan clearAfter)
    {
        await Task.Delay(clearAfter);
        var current = await clipboard.TryGetTextAsync();
        if (string.Equals(current, copiedText, StringComparison.Ordinal)) await clipboard.ClearAsync();
    }
}
