// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Android.App;
using Android.Runtime;

namespace SamElla.Mobile;

[Application]
public sealed class MainApplication(nint handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
  protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
