// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;

namespace SamElla.Audio;

// Only this thread owns native audio resources. Device operations may block.
internal sealed class AudioWorker : IDisposable
{
  private readonly BlockingCollection<Action> _commands = new();
  private readonly Thread _thread;
  private bool _disposed;

  public AudioWorker(Action update, Action cleanup)
  {
    _thread = new Thread(() => Run(update, cleanup)) { IsBackground = true, Name = "SamElla audio" };
    _thread.Start();
  }

  public void Dispose()
  {
    if (_disposed)
      return;

    _disposed = true;
    _commands.CompleteAdding();
    _thread.Join();
    _commands.Dispose();
  }

  public void Enqueue(Action command)
  {
    if (!_disposed)
      _commands.Add(command);
  }

  private void Run(Action update, Action cleanup)
  {
    try
    {
      while (!_commands.IsCompleted)
      {
        if (_commands.TryTake(out var command, 5))
          command();

        update();
      }
    }
    finally
    {
      cleanup();
    }
  }
}
