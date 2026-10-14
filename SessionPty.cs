using System.IO;
using EasyWindowsTerminalControl;

namespace AgentDesk.Spike;

// TermPTY.Start calls ReadOutputLoop before WaitForExit while it still owns pipe handles.
// Run exactly one reader separately, so process exit can reach the library's using/finally
// cleanup and close those handles. This override is specific to version 1.0.38.
internal sealed class SessionPty : TermPTY
{
    private int _readerStarted;
    public Task ReaderCompletion { get; private set; } = Task.CompletedTask;

    protected override void ReadOutputLoop()
    {
        if (Interlocked.Exchange(ref _readerStarted, 1) != 0) return;
        ReaderCompletion = Task.Run(() =>
        {
            try { base.ReadOutputLoop(); }
            catch (IOException) when (Process?.HasExited != false) { }
            catch (ObjectDisposedException) when (Process?.HasExited != false) { }
        });
    }
}
