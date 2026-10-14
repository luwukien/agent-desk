using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using EasyWindowsTerminalControl;
using EasyWindowsTerminalControl.Internals;
using Microsoft.Terminal.Wpf;

namespace AgentDesk.Spike;

// Adapter: the renderer talks ITerminalConnection; this class owns the process lifetime.
// All public methods except the output callback are called on the WPF UI thread.
public sealed class TerminalSession : ITerminalConnection
{
    private readonly SessionPty _pty = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _outputLock = new();
    private readonly StringBuilder _recentOutput = new();
    private Task? _run;
    private bool _closing;
    private bool _closed;
    private readonly string _command;

    public TerminalControl View { get; } = new() { AutoResize = true, Focusable = true };
    public string Name { get; }
    public string DirectoryPath { get; }
    public string Status { get; private set; } = "Chưa khởi động";
    public int? ProcessId { get; private set; }
    public bool IsRunning => _ready.Task.IsCompletedSuccessfully && _run is { IsCompleted: false } && !_closing;
    public Task Ready => _ready.Task;
    public event EventHandler? StateChanged;
    public event EventHandler<TerminalOutputEventArgs>? TerminalOutput;

    public TerminalSession(string name, string directory, string? command = null)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        Name = name;
        DirectoryPath = Path.GetFullPath(directory);
        // Windows PowerShell is present without installing PowerShell 7.
        var shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
        _command = command ?? $"\"{shell}\" -NoLogo -NoProfile -NoExit -Command " +
            "\"[Environment]::SetEnvironmentVariable('TERM','xterm-256color','Process')\"";
        KeyboardNavigation.SetTabNavigation(View, KeyboardNavigationMode.Contained);
        KeyboardNavigation.SetDirectionalNavigation(View, KeyboardNavigationMode.Contained);
        View.Loaded += OnLoaded;
        _pty.TermReady += OnReady;
        _pty.TerminalOutput += OnOutput;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_run != null || _closing) return; // Loaded can fire more than once.
        SetStatus("Đang khởi động");
        View.SetTheme(new TerminalTheme
        {
            DefaultBackground = 0x171717, DefaultForeground = 0xEEEEEE,
            DefaultSelectionBackground = 0x777777, CursorStyle = CursorStyle.BlinkingBar,
            ColorTable = new uint[] { 0x0C0C0C, 0x1F0FC5, 0x0EA113, 0x009CC1,
                0xDA3700, 0x981788, 0xDD963A, 0xCCCCCC, 0x767676, 0x5648E7,
                0x0CC616, 0xA5F1F9, 0xFF783B, 0x9E00B4, 0xD6D661, 0xF2F2F2 }
        }, "Consolas", 14);
        int columns = Math.Max(2, View.Columns), rows = Math.Max(2, View.Rows);
        // TermPTY.Start blocks until the session exits. Never execute it on the UI thread.
        _run = Task.Run(() => _pty.Start(_command, columns, rows, workingDirectory: DirectoryPath));
        try
        {
            await _run;
            await _pty.ReaderCompletion;
            _ready.TrySetException(new InvalidOperationException("Shell thoát trước khi sẵn sàng."));
            SetStatus("Đã thoát");
        }
        catch (Exception ex)
        {
            _ready.TrySetException(ex);
            _ = _ready.Task.Exception; // Observe failure even when no caller awaits Ready.
            SetStatus("Lỗi: " + ex.Message);
        }
    }

    private void OnReady(object? sender, EventArgs e)
    {
        View.Dispatcher.Invoke(() =>
        {
            ProcessId = (_pty.Process as ProcessFactory.WrappedProcess)?.Pid;
            View.Connection = this;
            _pty.Win32DirectInputMode(true);
            _pty.Resize(Math.Max(2, View.Columns), Math.Max(2, View.Rows));
            _ready.TrySetResult();
            SetStatus($"Đang chạy · PID {ProcessId}");
        });
    }

    private void OnOutput(object? sender, TerminalOutputEventArgs e)
    {
        // Bounded diagnostics for the smoke test, not a saved transcript or agent status parser.
        lock (_outputLock)
        {
            _recentOutput.Append(e.Data);
            if (_recentOutput.Length > 128_000) _recentOutput.Remove(0, _recentOutput.Length - 128_000);
        }
        TerminalOutput?.Invoke(this, e);
    }

    internal string RecentOutput { get { lock (_outputLock) return _recentOutput.ToString(); } }

    public void SendText(string text)
    {
        if (!IsRunning) throw new InvalidOperationException("Terminal chưa sẵn sàng hoặc đã đóng.");
        _pty.WriteToTerm(text);
    }

    public void CopySelection()
    {
        var text = View.GetSelectedText();
        if (text.Length > 0) Clipboard.SetText(text);
    }

    public void Paste()
    {
        if (!Clipboard.ContainsText()) return;
        var text = Clipboard.GetText();
        // Raw newlines may submit a command. The spike intentionally supports one line only.
        if (text.IndexOfAny(new[] { '\r', '\n', '\x1b', '\0' }) >= 0)
            throw new InvalidOperationException("Bản thử chỉ dán một dòng, không chứa ký tự điều khiển.");
        SendText(text); // Does not append Enter.
    }

    public async Task CloseAsync()
    {
        if (_closed) return;
        _closing = true;
        View.Loaded -= OnLoaded;
        SetStatus("Đang đóng");
        try
        {
            Trace("close begin");
            if (_run != null)
            {
                // Covers closing a tab while its process is still starting.
                await Task.WhenAny(_ready.Task, _run).WaitAsync(TimeSpan.FromSeconds(10));
                Trace("startup resolved");
                if (_ready.Task.IsCompletedSuccessfully)
                {
                    await Task.Run(() =>
                    {
                        if (_pty.Process.HasExited == false)
                        {
                            Trace("kill tree begin");
                            try { _pty.Process.Kill(EntireProcessTree: true); }
                            catch (InvalidOperationException) when (_pty.Process.HasExited) { }
                            Trace("kill tree done");
                        }
                        // The library's Start task owns and disposes the ConPTY after process exit.
                    }).WaitAsync(TimeSpan.FromSeconds(10));
                }
                try { await _run.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (Exception) when (_run.IsFaulted) { /* Failure is already shown by OnLoaded. */ }
                await _pty.ReaderCompletion.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Trace("run finished");
            View.Connection = null;
            _pty.TermReady -= OnReady;
            _pty.TerminalOutput -= OnOutput;
            // Start's using blocks may already have closed the underlying pipe.
            try { _pty.CloseStdinToApp(); }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
            if (_pty.Process is ProcessFactory.WrappedProcess wrapped) wrapped.Process.Dispose();
            _closed = true;
            SetStatus("Đã đóng");
        }
        catch (Exception ex)
        {
            SetStatus("Đóng thất bại: " + ex.Message);
            throw; // Keep the tab so the user sees the error and can retry.
        }
    }

    private void Trace(string message)
    {
        if (Environment.GetCommandLineArgs().Contains("--smoke"))
            lock (_outputLock) File.AppendAllText(Path.Combine(Environment.CurrentDirectory, "lifecycle.txt"),
                $"{DateTimeOffset.Now:O} {Name} {message}\n");
    }

    private void SetStatus(string text)
    {
        Status = text;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // The adapter deliberately does not start a second output reader or own teardown.
    void ITerminalConnection.Start() { }
    void ITerminalConnection.Close() { }
    void ITerminalConnection.WriteInput(string data) { if (IsRunning) _pty.WriteToTerm(data); }
    void ITerminalConnection.Resize(uint rows, uint columns)
    {
        if (IsRunning && rows > 0 && columns > 0) _pty.Resize((int)columns, (int)rows);
    }
}
