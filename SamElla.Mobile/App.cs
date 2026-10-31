namespace SamElla.Mobile;

public sealed class App : Application
{
  protected override Window CreateWindow(IActivationState? activationState)
  {
    var page = new GamePage();
    var window = new Window(page);
    window.Stopped += (_, _) => page.Suspend();
    window.Deactivated += (_, _) => page.Suspend();
    window.Activated += (_, _) => page.Resume();
    window.Resumed += (_, _) => page.Resume();
    window.Destroying += (_, _) => page.Dispose();
    return window;
  }
}
