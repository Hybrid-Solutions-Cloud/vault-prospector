using System.Reflection;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace VaultProspector.App.Views;

/// <summary>
/// Preserves screen-reader focus notifications after selected tab content changes.
/// </summary>
/// <remarks>
/// Avalonia 12.1.0 can retain a queryable focused peer while omitting the Windows
/// automation focus event for controls in newly selected tab content. The platform
/// automation node is already subscribed to the peer event; this bridge re-raises that
/// existing event after Avalonia has synchronized its focused peer.
/// </remarks>
internal sealed class ReliableWindowAutomationPeer : WindowAutomationPeer
{
    private static readonly FieldInfo? FocusChangedHandlers = typeof(WindowBaseAutomationPeer).GetField(
        "FocusChanged",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? FocusedControl = typeof(WindowBaseAutomationPeer).GetField(
        "_focus",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly MainWindow _owner;
    private readonly IDisposable _focusSubscription;

    internal static bool HasExpectedAvaloniaFocusInternals =>
        FocusChangedHandlers?.FieldType == typeof(EventHandler) &&
        FocusedControl?.FieldType == typeof(Control);

    public ReliableWindowAutomationPeer(MainWindow owner)
        : base(owner)
    {
        _owner = owner;
        _focusSubscription = InputElement.GotFocusEvent.AddClassHandler<Control>(
            Control_OnGotFocus,
            RoutingStrategies.Direct | RoutingStrategies.Bubble,
            true);
        _owner.Closed += Owner_OnClosed;
    }

    private void Control_OnGotFocus(Control control, FocusChangedEventArgs e)
    {
        if (TopLevel.GetTopLevel(control) != _owner ||
            !_owner.IsSecondaryTabSelected ||
            control is TabItem)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            FocusedControl?.SetValue(this, control);
            if (FocusChangedHandlers?.GetValue(this) is EventHandler handlers)
                handlers.Invoke(this, EventArgs.Empty);
        }, DispatcherPriority.Input);
    }

    private void Owner_OnClosed(object? sender, EventArgs e)
    {
        _focusSubscription.Dispose();
        _owner.Closed -= Owner_OnClosed;
    }
}
