// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using SkiaSharp.Views.Maui.Controls.Hosting;

namespace SamElla.Mobile;

public static class MauiProgram
{
  public static MauiApp CreateMauiApp() => MauiApp.CreateBuilder()
    .UseMauiApp<App>()
    .UseSkiaSharp()
    .Build();
}
