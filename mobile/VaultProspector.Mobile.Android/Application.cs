using Android.App;
using Android.Runtime;

namespace VaultProspector.Mobile.Android;

[Application]
public sealed class MainApplication(nint handle, JniHandleOwnership ownership)
    : global::Android.App.Application(handle, ownership);
