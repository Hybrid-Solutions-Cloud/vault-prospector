using Android.App;
using Android.OS;
using Android.Service.Autofill;

namespace VaultProspector.Mobile.Android;

[Service(
    Name =
        "cloud.hybridsolutions.vaultprospector.autofill.VaultProspectorAutofillService",
    Permission = "android.permission.BIND_AUTOFILL_SERVICE",
    Exported = true,
    Enabled = false)]
public sealed class VaultProspectorAutofillService : AutofillService
{
    public override void OnFillRequest(
        FillRequest request,
        CancellationSignal cancellationSignal,
        FillCallback callback)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(cancellationSignal);
        ArgumentNullException.ThrowIfNull(callback);

        if (cancellationSignal.IsCanceled)
        {
            callback.OnSuccess(null);
            return;
        }

        var contexts = request.FillContexts;
        var latest = contexts.Count == 0
            ? null
            : contexts[contexts.Count - 1];
        if (latest?.Structure is null ||
            !AndroidAutofillRequestParser.TryAnalyze(
                latest.Structure,
                out _))
        {
            callback.OnSuccess(null);
            return;
        }

        // The framework request is eligible for an explicit mapping lookup, but the prototype
        // deliberately returns no dataset. Shipping a value requires the app-owned encrypted
        // mapping, a fresh foreground verification activity, and an authenticated FillResponse.
        callback.OnSuccess(null);
    }

    public override void OnSaveRequest(
        SaveRequest request,
        SaveCallback callback)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(callback);

        // Vault Prospector never imports or persists values observed in another application.
        callback.OnSuccess();
    }
}
