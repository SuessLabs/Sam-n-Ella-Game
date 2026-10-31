using Android.App;
using Android.Runtime;

namespace SamElla.Mobile;

[Application]
public sealed class MainApplication(nint handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
  protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
