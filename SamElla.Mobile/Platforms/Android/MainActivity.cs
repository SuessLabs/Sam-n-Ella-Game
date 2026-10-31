using Android.App;
using Android.Content.PM;
using Android.OS;

namespace SamElla.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
  ScreenOrientation = ScreenOrientation.SensorLandscape,
  ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode
    | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity
{
  protected override void OnCreate(Bundle? savedInstanceState)
  {
    base.OnCreate(savedInstanceState);
    Window?.AddFlags(Android.Views.WindowManagerFlags.KeepScreenOn);
  }
}
